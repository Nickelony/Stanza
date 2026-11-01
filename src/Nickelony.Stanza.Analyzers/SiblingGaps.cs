using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers;

/// <summary>Tells whether the rules for multi-line units ask for blank lines around a unit, and creates them.</summary>
internal static class SiblingGaps
{
	/// <summary>Whether the multi-line rule, if any judges the unit, reports the gaps beside it.</summary>
	/// <remarks>
	/// It goes through the pair, so a rule with a pair reports only where its half owns the gap.
	/// </remarks>
	public static bool IsSeparatingNeighbors(RuleScope scope, StanzaOptions options, StanzaRule? separating, SyntaxNode unit) =>
		separating is not null && StanzaRuleState.IsTheReportingHalf(scope, options, rule: separating, position: unit.SpanStart);

	/// <summary>Orders the changes with the blank lines beside each unit, dropping a change written twice.</summary>
	/// <remarks>
	/// A fix that rewrites such a unit writes them too, so the text does not depend on which rule fixes first.
	/// </remarks>
	public static ImmutableArray<TextChange> WithSeparations(
		SourceText sourceText,
		IEnumerable<SyntaxNode> units,
		IEnumerable<TextChange> changes,
		int indentStepColumns
	)
	{
		TextChange[] rewrites = [.. changes];

		// A rewrite that already moves the token beside the unit owns that gap, so no separation is written into it.
		IEnumerable<TextChange> separations = units
			.SelectMany(unit => CreateSeparations(sourceText, unit, indentStepColumns))
			.Where(separation => !rewrites.Any(rewrite => Overlaps(rewrite.Span, separation.Span)));

		return [.. rewrites.Concat(separations).Distinct().OrderBy(static change => change.Span.Start)];
	}

	/// <summary>Whether the spans share a character, or one is an insertion strictly inside the other.</summary>
	private static bool Overlaps(TextSpan first, TextSpan second) => first.Start < second.End && second.Start < first.End;

	/// <summary>Orders the changes with the removal of the blank lines between the unit and each sibling.</summary>
	/// <remarks>
	/// A fix that makes a multi-line unit shorter removes them, as the rule that keeps such units together would have.
	/// </remarks>
	public static ImmutableArray<TextChange> WithJoins(SourceText sourceText, SyntaxNode unit, IEnumerable<TextChange> changes)
	{
		if (StanzaSyntax.FindContainer(unit) is not { } container)
			return [.. changes];

		ImmutableArray<SyntaxNode> siblings = StanzaSyntax.ReadSiblings(container);
		int index = siblings.IndexOf(unit);

		if (index < 0)
			return [.. changes];

		ImmutableArray<TextChange>.Builder joins = ImmutableArray.CreateBuilder<TextChange>();

		if (index > 0 && CreateJoin(sourceText, previous: siblings[index - 1], next: unit) is { } above)
			joins.Add(above);

		if (index < siblings.Length - 1 && CreateJoin(sourceText, previous: unit, next: siblings[index + 1]) is { } below)
			joins.Add(below);

		return [.. changes.Concat(joins).OrderBy(static change => change.Span.Start)];
	}

	/// <summary>Removes the blank lines between two units, where only blank lines stand between them.</summary>
	private static TextChange? CreateJoin(SourceText sourceText, SyntaxNode previous, SyntaxNode next)
	{
		StanzaSyntax.LeadingUnit leadingUnit = StanzaSyntax.GetLeadingUnit(next);
		int firstGapLineIndex = StanzaSyntax.GetEndLineIndex(previous) + 1;

		if (leadingUnit.HasDirective || firstGapLineIndex >= leadingUnit.StartLineIndex)
			return null;

		for (int lineIndex = firstGapLineIndex; lineIndex < leadingUnit.StartLineIndex; lineIndex++)
		{
			if (!IsBlank(sourceText, lineIndex))
				return null;
		}

		return new TextChange(
			TextSpan.FromBounds(sourceText.Lines[firstGapLineIndex].Start, sourceText.Lines[leadingUnit.StartLineIndex].Start),
			string.Empty
		);
	}

	/// <summary>Creates the blank lines above and below a multi-line unit tight against its siblings.</summary>
	private static ImmutableArray<TextChange> CreateSeparations(SourceText sourceText, SyntaxNode unit, int indentStepColumns)
	{
		if (StanzaSyntax.FindContainer(unit) is not { } container)
			return [];

		ImmutableArray<SyntaxNode> siblings = StanzaSyntax.ReadSiblings(container);
		int index = siblings.IndexOf(unit);

		if (index < 0)
			return [];

		return CreateSeparations(
			sourceText,
			unit,
			previous: index > 0 ? siblings[index - 1] : null,
			next: index < siblings.Length - 1 ? siblings[index + 1] : null,
			indentStepColumns
		);
	}

	/// <summary>Creates the blank lines above and below a multi-line unit tight against the siblings given.</summary>
	/// <remarks>
	/// A directive keeps its gap as written, and a sibling on the line of the unit moves down.
	/// </remarks>
	private static ImmutableArray<TextChange> CreateSeparations(
		SourceText sourceText,
		SyntaxNode unit,
		SyntaxNode? previous,
		SyntaxNode? next,
		int indentStepColumns
	)
	{
		if (!StanzaSyntax.IsMultiLine(sourceText, unit.Span))
			return [];

		ImmutableArray<TextChange>.Builder blankLines = ImmutableArray.CreateBuilder<TextChange>();

		StanzaSyntax.LeadingUnit leadingUnit = StanzaSyntax.GetLeadingUnit(unit);

		int startLineIndex = leadingUnit.StartLineIndex;
		int endLineIndex = StanzaSyntax.GetEndLineIndex(unit);

		if (previous is not null && !leadingUnit.HasDirective)
		{
			if (!IsOnLineOf(sourceText, previous, startLineIndex))
			{
				if (!IsBlank(sourceText, startLineIndex - 1))
					blankLines.Add(CreateBlankLineAfter(sourceText, startLineIndex - 1));
			}
			else if (CreateSiblingInsertion(sourceText, previous, next: unit, indentStepColumns) is { } pushDown)
			{
				blankLines.Add(pushDown);
			}
		}

		if (next is not null && !StanzaSyntax.GetLeadingUnit(next).HasDirective)
		{
			if (!IsOnLineOf(sourceText, next, endLineIndex))
			{
				if (!IsBlank(sourceText, endLineIndex + 1))
					blankLines.Add(CreateBlankLineAfter(sourceText, endLineIndex));
			}
			else if (CreateSiblingInsertion(sourceText, previous: unit, next, indentStepColumns) is { } pushDown)
			{
				blankLines.Add(pushDown);
			}
		}

		return blankLines.ToImmutable();
	}

	/// <summary>Doubles a line break, so two units rewritten together write one blank line between them.</summary>
	private static TextChange CreateBlankLineAfter(SourceText sourceText, int lineIndex)
	{
		TextLine line = sourceText.Lines[lineIndex];
		var lineBreak = TextSpan.FromBounds(line.End, line.EndIncludingLineBreak);
		string text = sourceText.ToString(lineBreak);

		return new TextChange(lineBreak, text + text);
	}

	/// <summary>Whether the sibling shares the line, so no line break stands between it and the unit.</summary>
	private static bool IsOnLineOf(SourceText sourceText, SyntaxNode sibling, int lineIndex)
	{
		return sourceText.Lines.GetLineFromPosition(sibling.SpanStart).LineNumber == lineIndex
			|| sourceText.Lines.GetLineFromPosition(sibling.Span.End).LineNumber == lineIndex;
	}

	private static bool IsBlank(SourceText sourceText, int lineIndex) =>
		lineIndex < 0 || lineIndex >= sourceText.Lines.Count || StanzaText.IsBlankLine(sourceText, lineIndex);

	/// <summary>
	/// A blank line fits when the unit starts at most one line after the previous node and no directive leads it.
	/// </summary>
	public static bool CanInsertBlankLine(StanzaSyntax.LeadingUnit leadingUnit, int gapStartLineIndex) =>
		!leadingUnit.HasDirective && leadingUnit.StartLineIndex - gapStartLineIndex is 0 or 1;

	/// <summary>The line a gap starts below, past a comment that trails a list unit's comma.</summary>
	public static int GetGapStartLineIndex(SyntaxNodeOrToken previous, SyntaxNodeOrToken next) =>
		StanzaSyntax.GetEndLineIndex(GetGapStartToken(previous, next));

	/// <summary>The token a gap starts after: the comma or semicolon that ends a unit, or its last token.</summary>
	/// <remarks>
	/// A comma that opens the line of the next unit does not end the unit before it.
	/// </remarks>
	public static SyntaxToken GetGapStartToken(SyntaxNodeOrToken previous, SyntaxNodeOrToken next)
	{
		SyntaxToken last = previous.IsToken ? previous.AsToken() : previous.AsNode()!.GetLastToken();

		// Full spans that touch leave no token between the units, so no separator stands there and the walk is skipped.
		if (previous.FullSpan.End == next.FullSpan.Start)
			return last;

		// The token is found by its position, because the next token of a list's unit is found by a pass over the list.
		SyntaxToken separator = previous.SyntaxTree!.GetRoot().FindToken(previous.FullSpan.End);

		if (separator.Kind() is not (SyntaxKind.CommaToken or SyntaxKind.SemicolonToken) || separator.Span.End > next.SpanStart)
			return last;

		return OpensItsLineBeforeTheUnit(separator, next) ? last : separator;
	}

	/// <summary>
	/// Whether a separator opens its line and the unit follows it there, as in a list written with its commas first.
	/// </summary>
	private static bool OpensItsLineBeforeTheUnit(SyntaxToken separator, SyntaxNodeOrToken next)
	{
		SourceText sourceText = separator.SyntaxTree!.GetText();
		TextLine line = sourceText.Lines.GetLineFromPosition(separator.SpanStart);

		return next.SpanStart <= line.End && StanzaText.StartsItsLine(sourceText, separator.SpanStart);
	}

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

	private static int GetTrailingWhitespaceStart(SourceText sourceText, TextSpan span)
	{
		int position = span.End;

		while (position > span.Start && char.IsWhiteSpace(sourceText[position - 1]))
			position--;

		return position;
	}
}
