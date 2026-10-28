using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Measures the gap between two units and creates the changes that add or remove its blank lines.</summary>
internal static class BlankLineGaps
{
	public static bool CanInsertBlankLine(SyntaxNode previous, SyntaxNode next) =>
		CanInsertBlankLine(leadingUnit: StanzaSyntax.GetLeadingUnit(next), gapStartLineIndex: GetEndLineIndex(previous));

	/// <summary>
	/// A blank line fits when the unit starts at most one line after the previous node and no directive leads it.
	/// </summary>
	public static bool CanInsertBlankLine(StanzaSyntax.LeadingUnit leadingUnit, int gapStartLineIndex) =>
		!leadingUnit.HasDirective && leadingUnit.StartLineIndex - gapStartLineIndex is 0 or 1;

	public static bool HasBlankLineBetween(SyntaxNodeOrToken previous, SyntaxNodeOrToken next) =>
		HasBlankLineBetween(previous, leadingUnit: StanzaSyntax.GetLeadingUnit(next), gapStartLineIndex: GetEndLineIndex(previous));

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
	/// The commented sibling rule and the tight rules that defer to it read this test, so they never disagree.
	/// </remarks>
	public static bool HasCommentDirectlyAbove(SyntaxNode unit)
	{
		SyntaxTriviaList leadingTrivia = unit.GetLeadingTrivia();

		for (int index = leadingTrivia.Count - 1; index >= 0; index--)
		{
			SyntaxTrivia trivia = leadingTrivia[index];

			if (!StanzaTrivia.IsComment(trivia))
				continue;

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
			&& !HasDirectiveBefore(next)
			&& !StanzaSyntax.IsMultiLine(previous)
			&& !StanzaSyntax.IsMultiLine(next)
			&& StanzaSyntax.GetLeadingUnit(next).StartLineIndex - GetGapStartLineIndex(previous, next) == 1;
	}

	public static bool HasDirectiveBefore(SyntaxNodeOrToken next)
	{
		// Roslyn attaches a directive to the leading trivia of the token after it, so only the next unit can hold one.
		foreach (SyntaxTrivia trivia in next.GetLeadingTrivia())
		{
			if (trivia.IsDirective)
				return true;
		}

		return false;
	}

	/// <summary>The line the unit ends on, which a multi-line trailing comment pushes down.</summary>
	public static int GetEndLineIndex(SyntaxNodeOrToken unit)
	{
		SyntaxToken last = unit.IsToken ? unit.AsToken() : unit.AsNode()!.GetLastToken();
		int end = unit.Span.End;

		foreach (SyntaxTrivia trivia in last.TrailingTrivia)
		{
			if (StanzaTrivia.IsComment(trivia))
				end = trivia.Span.End;
		}

		return StanzaSyntax.GetLineSpan(unit, new TextSpan(end, 0)).StartLinePosition.Line;
	}

	/// <summary>The line a gap starts below, past a comment that trails a list unit's comma.</summary>
	public static int GetGapStartLineIndex(SyntaxNodeOrToken previous, SyntaxNodeOrToken next) => GetEndLineIndex(GetGapStartToken(previous, next));

	/// <summary>The token a gap starts after: the comma or semicolon that ends a unit, or its last token.</summary>
	public static SyntaxToken GetGapStartToken(SyntaxNodeOrToken previous, SyntaxNodeOrToken next)
	{
		SyntaxToken last = previous.IsToken ? previous.AsToken() : previous.AsNode()!.GetLastToken();

		// Full spans that touch leave no token between the units, so no separator stands there and the walk is skipped.
		if (previous.FullSpan.End == next.FullSpan.Start)
			return last;

		SyntaxToken separator = last.GetNextToken();

		return separator.Kind() is SyntaxKind.CommaToken or SyntaxKind.SemicolonToken && separator.Span.End <= next.SpanStart ? separator : last;
	}

	public static (SyntaxToken Previous, SyntaxToken Next) OrderBoundary(SyntaxToken anchor, SyntaxToken neighbor) =>
		anchor.SpanStart < neighbor.SpanStart ? (anchor, neighbor) : (neighbor, anchor);

	public static TextChange? CreateSiblingInsertion(SourceText sourceText, SyntaxNode previous, SyntaxNode next, int indentStepColumns)
	{
		StanzaSyntax.LeadingUnit leadingUnit = StanzaSyntax.GetLeadingUnit(next);
		int gapStartLineIndex = GetGapStartLineIndex(previous, next);

		if (!CanInsertBlankLine(leadingUnit, gapStartLineIndex))
			return null;

		if (leadingUnit.StartLineIndex == gapStartLineIndex + 1)
			return StanzaText.CreateBlankLineAt(sourceText, leadingUnit.StartLineIndex);

		// Both units share a line, separated by a separator token plus trivia.
		// Only the whitespace after the last comment of that gap is replaced,
		// so a comment stays on the line above the inserted one.
		int separatorEnd = next.GetFirstToken().GetPreviousToken().Span.End;

		var gap = TextSpan.FromBounds(separatorEnd, leadingUnit.StartPosition);
		int whitespaceStart = GetTrailingWhitespaceStart(sourceText, gap);

		string lineEnding = StanzaText.ReadPreferredLineEnding(sourceText, leadingUnit.StartLineIndex);

		// The pushed-down unit belongs on the level of the line the neighboring units share,
		// one step in when that line opens with a label, a brace or a header rather than with a unit.
		string indentation = StanzaText.GetIndentation(sourceText, previous.SpanStart);

		if (OpensWithAHeader(previous))
			indentation = StanzaText.WithOneIndentStep(sourceText, position: previous.SpanStart, memberIndentation: indentation, indentStepColumns);

		return new TextChange(TextSpan.FromBounds(whitespaceStart, leadingUnit.StartPosition), $"{lineEnding}{lineEnding}{indentation}");
	}

	/// <summary>Whether the unit's line opens with a label, a brace or a header rather than with a unit.</summary>
	/// <remarks>
	/// Only a statement or a member can, since arguments and elements keep the indentation of the line they share.
	/// </remarks>
	private static bool OpensWithAHeader(SyntaxNode unit)
	{
		if (unit is not (StatementSyntax or MemberDeclarationSyntax) || unit.Parent is not { } container)
			return false;

		SyntaxToken first = unit.GetFirstToken();

		while (
			first.GetPreviousToken() is { RawKind: not 0 } before
			&& StanzaSyntax.GetLineSpan(before).EndLinePosition.Line == StanzaSyntax.GetLineSpan(first).StartLinePosition.Line
		)
		{
			first = before;
		}

		for (SyntaxNode? node = first.Parent; node is not null; node = node.Parent)
		{
			if (node.Parent == container)
				return node is not (StatementSyntax or MemberDeclarationSyntax);
		}

		return true;
	}

	public static TextChange? CreateTokenInsertion(SourceText sourceText, SyntaxNodeOrToken previous, SyntaxNodeOrToken next)
	{
		StanzaSyntax.LeadingUnit leadingUnit = StanzaSyntax.GetLeadingUnit(next);

		bool insertsBlankLine = !leadingUnit.HasDirective && leadingUnit.StartLineIndex - GetEndLineIndex(previous) == 1;

		return insertsBlankLine ? StanzaText.CreateBlankLineAt(sourceText, leadingUnit.StartLineIndex) : null;
	}

	public static TextChange? CreateAdjacentRemoval(SourceText sourceText, SyntaxToken previous, SyntaxToken next, string ruleId)
	{
		bool anchoredOnPrevious = StanzaRules.Find(ruleId)?.AnchoredOnPrevious == true;
		StanzaSyntax.LeadingUnit leadingUnit = StanzaSyntax.GetLeadingUnit(next);

		if (leadingUnit.HasDirective)
			return null;

		int gapStartLineIndex = GetGapStartLineIndex(previous, next);

		// When the gap is anchored on the previous token, it ends at the first leading comment,
		// or at the unit when there is none,
		// so a blank line between that comment and the unit is not removed.
		int contentLine = anchoredOnPrevious ? GetLeadingContentLineIndex(next, leadingUnit.StartLineIndex) : leadingUnit.StartLineIndex;

		if (contentLine - gapStartLineIndex < 2)
			return null;

		var gap = TextSpan.FromBounds(sourceText.Lines[gapStartLineIndex].End, sourceText.Lines[contentLine].Start);

		return StanzaText.IsWhitespaceOnly(sourceText, gap)
			? new TextChange(gap, StanzaText.ReadPreferredLineEnding(sourceText, gapStartLineIndex))
			: null;
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

	private static int GetTrailingWhitespaceStart(SourceText sourceText, TextSpan span)
	{
		int position = span.End;

		while (position > span.Start && char.IsWhiteSpace(sourceText[position - 1]))
			position--;

		return position;
	}
}
