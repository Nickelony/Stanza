using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Measures the gap between two units and creates the changes that add or remove its blank lines.</summary>
internal static class BlankLineGaps
{
	public static bool HasBlankLineBetween(SyntaxNodeOrToken previous, SyntaxNodeOrToken next) =>
		HasBlankLineBetween(previous, leadingUnit: StanzaSyntax.GetLeadingUnit(next), gapStartLineIndex: StanzaSyntax.GetEndLineIndex(previous));

	/// <summary>Whether the gap has no blank line and a fix can write one.</summary>
	public static bool IsGapMissingBlankLine(SyntaxNode previous, SyntaxNode next)
	{
		StanzaSyntax.LeadingUnit leadingUnit = StanzaSyntax.GetLeadingUnit(next);
		int gapStartLineIndex = SiblingGaps.GetGapStartLineIndex(previous, next);

		return !HasBlankLineBetween(previous, leadingUnit, gapStartLineIndex) && SiblingGaps.CanInsertBlankLine(leadingUnit, gapStartLineIndex);
	}

	public static bool HasBlankLineBetween(SyntaxNodeOrToken previous, StanzaSyntax.LeadingUnit leadingUnit, int gapStartLineIndex)
	{
		if (leadingUnit.StartLineIndex - gapStartLineIndex < 2)
			return false;

		if (previous.SyntaxTree is not { } syntaxTree)
			return false;

		SourceText sourceText = syntaxTree.GetText();

		for (int lineIndex = gapStartLineIndex + 1; lineIndex < leadingUnit.StartLineIndex; lineIndex++)
		{
			if (!StanzaText.IsBlankLine(sourceText, lineIndex))
				continue;

			// A blank line inside an inactive block is no line of the code a build compiles, so it separates nothing.
			if (!leadingUnit.HasDirective || !IsInInactiveBlock(syntaxTree, sourceText.Lines[lineIndex].Start))
				return true;
		}

		return false;
	}

	private static bool IsInInactiveBlock(SyntaxTree syntaxTree, int position) =>
		syntaxTree.GetRoot().FindTrivia(position).IsKind(SyntaxKind.DisabledTextTrivia);

	/// <summary>Whether a comment ends on the line directly above the unit, with no blank line between them.</summary>
	/// <remarks>
	/// The commented sibling rule reads this test, and the rules that defer to it read the gap it reports.
	/// </remarks>
	public static bool HasCommentDirectlyAbove(SyntaxNode unit) => HasCommentAbove(unit, documentationReachesAcrossBlankLines: false);

	/// <summary>Whether a comment belongs to the unit: the one directly above it, or a documentation comment.</summary>
	/// <remarks>
	/// A documentation comment belongs to its unit across any blank lines, as <c>GetLeadingUnit</c> reads it.
	/// </remarks>
	public static bool HasCommentAttached(SyntaxNode unit) => HasCommentAbove(unit, documentationReachesAcrossBlankLines: true);

	private static bool HasCommentAbove(SyntaxNode unit, bool documentationReachesAcrossBlankLines)
	{
		SyntaxTriviaList leadingTrivia = unit.GetLeadingTrivia();

		for (int index = leadingTrivia.Count - 1; index >= 0; index--)
		{
			SyntaxTrivia trivia = leadingTrivia[index];

			if (!StanzaTrivia.IsComment(trivia))
				continue;

			if (documentationReachesAcrossBlankLines && StanzaTrivia.IsDocumentationComment(trivia))
				return true;

			// A line comment keeps its break outside its span, and a documentation comment takes it in, so its span ends at column 0.
			LinePosition end = StanzaSyntax.GetLineSpan(unit, trivia.Span).EndLinePosition;
			int endLineIndex = end.Character == 0 ? end.Line - 1 : end.Line;

			return endLineIndex == StanzaSyntax.GetLineSpan(unit).StartLinePosition.Line - 1;
		}

		return false;
	}

	/// <summary>Whether the gap above a single-line unit lacks the blank line its comment asks for.</summary>
	/// <remarks>
	/// The commented sibling rule reports this gap, and the rules that write the same blank line defer to it.
	/// </remarks>
	public static bool IsMissingBlankLineAboveComment(SyntaxNode previous, SyntaxNode next)
	{
		return HasCommentDirectlyAbove(next)
			&& IsSingleLineGapWithoutDirective(previous, next)
			&& StanzaSyntax.GetLeadingUnit(next).StartLineIndex - SiblingGaps.GetGapStartLineIndex(previous, next) == 1;
	}

	/// <summary>Whether ZA0012 keeps a blank line above a single-line unit, written or not.</summary>
	/// <remarks>
	/// The grouping rules end a run here in every fix order, though that rule reports only a comment directly above.
	/// </remarks>
	public static bool IsKeptBlankAboveComment(SyntaxNode previous, SyntaxNode next) =>
		HasCommentAttached(next) && IsSingleLineGapWithoutDirective(previous, next);

	private static bool IsSingleLineGapWithoutDirective(SyntaxNode previous, SyntaxNode next) =>
		!HasDirectiveBefore(next) && !StanzaSyntax.IsMultiLine(previous) && !StanzaSyntax.IsMultiLine(next);

	/// <summary>Whether a directive stands in the gap above the unit, in the unit's leading trivia.</summary>
	public static bool HasDirectiveBefore(SyntaxNodeOrToken next) => StanzaSyntax.HasDirective(next.GetLeadingTrivia());

	public static (SyntaxToken Previous, SyntaxToken Next) OrderGapTokens(SyntaxToken anchor, SyntaxToken neighbor) =>
		anchor.SpanStart < neighbor.SpanStart ? (anchor, neighbor) : (neighbor, anchor);

	public static TextChange? CreateTokenInsertion(SourceText sourceText, SyntaxNodeOrToken previous, SyntaxNodeOrToken next)
	{
		StanzaSyntax.LeadingUnit leadingUnit = StanzaSyntax.GetLeadingUnit(next);

		bool insertsBlankLine = !leadingUnit.HasDirective && leadingUnit.StartLineIndex - StanzaSyntax.GetEndLineIndex(previous) == 1;

		return insertsBlankLine ? StanzaText.CreateBlankLineAt(sourceText, leadingUnit.StartLineIndex) : null;
	}

	public static TextChange? CreateAdjacentRemoval(SourceText sourceText, SyntaxToken previous, SyntaxToken next, string ruleId)
	{
		bool anchoredOnPrevious = StanzaRules.Find(ruleId)?.AnchoredOnPrevious == true;
		StanzaSyntax.LeadingUnit leadingUnit = StanzaSyntax.GetLeadingUnit(next);

		if (leadingUnit.HasDirective)
			return null;

		int gapStartLineIndex = SiblingGaps.GetGapStartLineIndex(previous, next);

		// When the gap is anchored on the previous token, it ends at the first leading comment,
		// or at the unit when there is none,
		// so a blank line between that comment and the unit is not removed.
		int contentLine = anchoredOnPrevious ? GetLeadingContentLineIndex(next, leadingUnit.StartLineIndex) : leadingUnit.StartLineIndex;

		TextChange? above = CreateLineGapRemoval(sourceText, gapStartLineIndex, contentLine);

		// A closing brace is padded below its last comment too, and one change takes both blank gaps out around the comments.
		if (!anchoredOnPrevious || !next.IsKind(SyntaxKind.CloseBraceToken) || FindLastCommentEndLineIndex(next) is not { } commentEndLineIndex)
			return above;

		int braceLineIndex = StanzaSyntax.GetLineSpan(next).StartLinePosition.Line;

		if (CreateLineGapRemoval(sourceText, commentEndLineIndex, braceLineIndex) is not { } below)
			return above;

		if (above is not { } aboveRemoval)
			return below;

		string comments = sourceText.ToString(TextSpan.FromBounds(sourceText.Lines[contentLine].Start, sourceText.Lines[commentEndLineIndex].End));

		return new TextChange(TextSpan.FromBounds(aboveRemoval.Span.Start, below.Span.End), aboveRemoval.NewText + comments + below.NewText);
	}

	/// <summary>Creates the change that removes the blank lines between two lines.</summary>
	private static TextChange? CreateLineGapRemoval(SourceText sourceText, int upperLineIndex, int lowerLineIndex)
	{
		if (lowerLineIndex - upperLineIndex < 2)
			return null;

		var gap = TextSpan.FromBounds(sourceText.Lines[upperLineIndex].End, sourceText.Lines[lowerLineIndex].Start);

		return StanzaText.IsWhitespaceOnly(sourceText, gap)
			? new TextChange(gap, StanzaText.ReadPreferredLineEnding(sourceText, upperLineIndex))
			: null;
	}

	private static int? FindLastCommentEndLineIndex(SyntaxToken token)
	{
		int? endLineIndex = null;

		foreach (SyntaxTrivia trivia in token.LeadingTrivia)
		{
			if (StanzaTrivia.IsComment(trivia))
				endLineIndex = StanzaSyntax.GetLineSpan(token, trivia.Span).EndLinePosition.Line;
		}

		return endLineIndex;
	}

	/// <summary>Creates a change only when exactly one side of the anchor holds a blank line.</summary>
	public static TextChange? CreateAnchorRemoval(SourceText sourceText, SyntaxToken anchor, string ruleId)
	{
		SyntaxToken previous = anchor.GetPreviousToken();
		SyntaxToken next = anchor.GetNextToken();

		TextChange? before = previous.IsKind(SyntaxKind.None) ? null : CreateAdjacentRemoval(sourceText, previous, next: anchor, ruleId);
		TextChange? after = next.IsKind(SyntaxKind.None) ? null : CreateAdjacentRemoval(sourceText, previous: anchor, next, ruleId);

		return before is not null ^ after is not null ? before ?? after : null;
	}

	private static int GetLeadingContentLineIndex(SyntaxToken token, int unitStartLineIndex)
	{
		foreach (SyntaxTrivia trivia in token.LeadingTrivia)
		{
			if (StanzaTrivia.IsComment(trivia))
				return StanzaSyntax.GetLineSpan(token, trivia.Span).StartLinePosition.Line;
		}

		return unitStartLineIndex;
	}
}
