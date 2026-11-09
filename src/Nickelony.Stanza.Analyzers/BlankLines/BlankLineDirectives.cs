using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Reads the directives that keep a gap blank or that can change the count of units.</summary>
internal static class BlankLineDirectives
{
	/// <summary>Whether ZA0401 keeps the gap blank, so it ends a run even before its blank line is written.</summary>
	/// <remarks>
	/// ZA0401 separates a marker only from a unit beside it, so the first and the last directive of the gap decide.
	/// </remarks>
	public static bool IsRegionMarkerGap(SyntaxNode next, string? ownerId, CancellationToken cancellationToken) =>
		IsDirectiveGapKept(next, ownerId, isKeptFirst: IsRegionMarker, isKeptLast: IsRegionMarker, cancellationToken);

	private static bool IsRegionMarker(DirectiveTriviaSyntax directive) => directive is RegionDirectiveTriviaSyntax or EndRegionDirectiveTriviaSyntax;

	/// <summary>Whether ZA0402 keeps the gap blank, so it ends a run even before its blank line is written.</summary>
	/// <remarks>
	/// ZA0402 separates an <c>#if</c> from the unit above it and an <c>#endif</c> from the unit below it.
	/// </remarks>
	public static bool IsConditionalBlockGap(SyntaxNode next, string? ownerId, CancellationToken cancellationToken)
	{
		return IsDirectiveGapKept(
			next,
			ownerId,
			isKeptFirst: static first => first is IfDirectiveTriviaSyntax && ConditionalBlocks.IsBetweenUnits(first),
			isKeptLast: static last => last is EndIfDirectiveTriviaSyntax && ConditionalBlocks.IsBetweenUnits(last),
			cancellationToken
		);
	}

	private static bool IsDirectiveGapKept(
		SyntaxNode next,
		string? ownerId,
		Func<DirectiveTriviaSyntax, bool> isKeptFirst,
		Func<DirectiveTriviaSyntax, bool> isKeptLast,
		CancellationToken cancellationToken
	)
	{
		if (ownerId is null)
			return false;

		DirectiveTriviaSyntax? first = null;
		DirectiveTriviaSyntax? last = null;

		// A directive is leading trivia of the token after it, so the next unit holds every directive of the gap.
		foreach (SyntaxTrivia trivia in next.GetLeadingTrivia())
		{
			if (trivia.GetStructure() is not DirectiveTriviaSyntax directive)
				continue;

			first ??= directive;
			last = directive;
		}

		return IsKeptDirective(directive: first, isKept: isKeptFirst, ownerId, cancellationToken)
			|| IsKeptDirective(directive: last, isKept: isKeptLast, ownerId, cancellationToken);
	}

	/// <summary>Whether the directive is one the rule keeps blank beside, unless a pragma silences it.</summary>
	private static bool IsKeptDirective(
		DirectiveTriviaSyntax? directive,
		Func<DirectiveTriviaSyntax, bool> isKept,
		string ownerId,
		CancellationToken cancellationToken
	)
	{
		if (directive is null || !isKept(directive))
			return false;

		int position = directive.SpanStart;
		return !StanzaPragmas.IsSilencedByPragma(syntaxTree: directive.SyntaxTree, ruleId: ownerId, position, cancellationToken);
	}

	/// <summary>Whether a conditional directive stands among the units or splits them from the container.</summary>
	/// <remarks>
	/// A block with no branch around every unit changes no count, as its units exist only in the views that take it.
	/// </remarks>
	public static bool HoldsConditionalDirective(ImmutableArray<SyntaxNode> siblings)
	{
		SyntaxNode first = siblings[0];

		// A directive is leading trivia of the token after it, so the tokens from the first unit to the end of the container carry every one.
		// A directive inside a unit can change what it holds, but that unit spans lines, which the two-unit rule skips.
		// One outside the container's braces moves no unit in or out.
		for (int boundary = 1; boundary < siblings.Length; boundary++)
		{
			if (HasConditionalDirectiveBetween(siblings[boundary - 1], siblings[boundary]))
				return true;
		}

		// The token after the last unit is the closing brace, the next switch section or the end of the file,
		// except in an enum, where a comma can come first and the closing brace holds the directive.
		SyntaxToken after = siblings[siblings.Length - 1].GetLastToken().GetNextToken(includeZeroWidth: true);

		List<DirectiveTriviaSyntax> atEnd = ReadConditionalDirectives(after.LeadingTrivia);

		if (after.IsKind(SyntaxKind.CommaToken))
			atEnd.AddRange(ReadConditionalDirectives(after.GetNextToken(includeZeroWidth: true).LeadingTrivia));

		List<DirectiveTriviaSyntax> atStart = ReadConditionalDirectives(first.GetLeadingTrivia());

		return atStart.Any(directive => !IsHarmlessAtStart(directive, first, atEnd))
			|| atEnd.Any(directive => !IsHarmlessAtEnd(directive, first, atStart, atEnd));
	}

	private static List<DirectiveTriviaSyntax> ReadConditionalDirectives(SyntaxTriviaList trivia) =>
		[.. trivia.Where(StanzaSyntax.IsConditionalDirective).Select(static directive => (DirectiveTriviaSyntax)directive.GetStructure()!)];

	private static bool IsAmong(List<DirectiveTriviaSyntax> directives, DirectiveTriviaSyntax directive) =>
		directives.Any(other => other.SpanStart == directive.SpanStart);

	/// <summary>Whether a directive on the first unit moves no unit, in any view.</summary>
	/// <remarks>
	/// An <c>#if</c> is harmless when its <c>#endif</c> follows the last unit, and so is a block of case labels.
	/// </remarks>
	private static bool IsHarmlessAtStart(DirectiveTriviaSyntax directive, SyntaxNode first, List<DirectiveTriviaSyntax> atEnd)
	{
		List<DirectiveTriviaSyntax> related = directive.GetRelatedDirectives();

		if (related.Count != 2 || related[0] is not IfDirectiveTriviaSyntax || related[1] is not EndIfDirectiveTriviaSyntax)
			return false;

		// A block of labels is read the same whether the view takes it, which puts both its ends on one token, or drops it.
		if (first.Parent is SwitchSectionSyntax section && IsBlockOfLabelsBehindALabel(section, related))
			return true;

		return directive is IfDirectiveTriviaSyntax && IsAmong(atEnd, related[1]);
	}

	/// <summary>Whether a directive on the token after the last unit moves no unit, in any view.</summary>
	/// <remarks>
	/// An <c>#endif</c> pairs with its <c>#if</c>, and an <c>#if</c> may open whole switch sections.
	/// </remarks>
	private static bool IsHarmlessAtEnd(
		DirectiveTriviaSyntax directive,
		SyntaxNode first,
		List<DirectiveTriviaSyntax> atStart,
		List<DirectiveTriviaSyntax> atEnd
	)
	{
		List<DirectiveTriviaSyntax> related = directive.GetRelatedDirectives();

		if (related.Count != 2 || related[0] is not IfDirectiveTriviaSyntax || related[1] is not EndIfDirectiveTriviaSyntax)
			return false;

		if (directive is EndIfDirectiveTriviaSyntax)
			return IsAmong(atStart, related[0]) || IsAmong(atEnd, related[0]);

		return first.Parent is SwitchSectionSyntax && IsBlockOfWholeSections(related);
	}

	/// <summary>Whether the block holds only case labels behind a label that stays.</summary>
	private static bool IsBlockOfLabelsBehindALabel(SwitchSectionSyntax section, List<DirectiveTriviaSyntax> block)
	{
		// Without a label above the block, a view that drops it would join the statements to the section above.
		if (!section.Labels.Any(label => !label.IsMissing && label.Span.End <= block[0].SpanStart))
			return false;

		// The text between the directives stays whether or not a view takes it, so every view reads one answer.
		string text = ReadTextWithin(block);

		return !HasDirectiveLine(text) && HoldsOnlyLabels(text);
	}

	/// <summary>Whether the block holds whole switch sections and ends at a section.</summary>
	private static bool IsBlockOfWholeSections(List<DirectiveTriviaSyntax> block)
	{
		SyntaxToken next = block[1].ParentTrivia.Token;

		bool endsAtASection =
			(next.Parent is SwitchLabelSyntax label && label.GetFirstToken() == next)
			|| (next.IsKind(SyntaxKind.CloseBraceToken) && next.Parent is SwitchStatementSyntax);

		// A directive of another block on that token could hide the section it starts, and the units would then run on into it.
		if (!endsAtASection || ReadConditionalDirectives(next.LeadingTrivia).Any(directive => !IsAmong(block, directive)))
			return false;

		string text = ReadTextWithin(block);

		return !HasDirectiveLine(text) && HoldsOnlyWholeSections(text);
	}

	private static string ReadTextWithin(List<DirectiveTriviaSyntax> block)
	{
		SourceText sourceText = block[0].SyntaxTree.GetText();
		return sourceText.ToString(TextSpan.FromBounds(block[0].FullSpan.End, block[1].SpanStart));
	}

	private static bool HasDirectiveLine(string text) =>
		text.Split('\n').Any(static line => line.TrimStart().StartsWith("#", StringComparison.Ordinal));

	/// <summary>Whether the text is case labels alone, which join the labels above the block.</summary>
	private static bool HoldsOnlyLabels(string text)
	{
		return SyntaxFactory.ParseStatement($"switch (0) {{\ncase 0:\n{text}\nbreak;\n}}")
				is SwitchStatementSyntax { ContainsDiagnostics: false } parsed
			&& parsed.Sections.Count == 1
			&& parsed.Sections[0].Statements.Count == 1;
	}

	/// <summary>Whether the text is switch sections from the first label to the last statement, or nothing.</summary>
	private static bool HoldsOnlyWholeSections(string text)
	{
		SyntaxToken first = SyntaxFactory.ParseTokens(text).FirstOrDefault(static token => !token.IsKind(SyntaxKind.EndOfFileToken));

		if (!first.IsKind(SyntaxKind.None) && !first.IsKind(SyntaxKind.CaseKeyword) && !first.IsKind(SyntaxKind.DefaultKeyword))
			return false;

		return SyntaxFactory.ParseStatement($"switch (0) {{\n{text}\n}}") is SwitchStatementSyntax { ContainsDiagnostics: false };
	}

	private static bool HasConditionalDirectiveBetween(SyntaxNode previous, SyntaxNode next)
	{
		SyntaxToken nextStart = next.GetFirstToken();

		// Units that touch have no token between them, and finding the next token of a long list takes time linear in the list.
		if (previous.FullSpan.End >= nextStart.FullSpan.Start)
			return StanzaSyntax.HasConditionalDirective(nextStart.LeadingTrivia);

		// The walk finds each token by its position, because the next token of a list's unit is found by a pass over the list.
		SyntaxNode root = previous.SyntaxTree.GetRoot();
		int position = previous.FullSpan.End;

		while (position < nextStart.FullSpan.Start)
		{
			SyntaxToken between = root.FindToken(position);

			// A unit of broken code can hold no token, and the walk then has no token to stand on.
			if (between.IsKind(SyntaxKind.None) || between.FullSpan.End <= position)
				break;

			if (StanzaSyntax.HasConditionalDirective(between.LeadingTrivia))
				return true;

			position = between.FullSpan.End;
		}

		return StanzaSyntax.HasConditionalDirective(nextStart.LeadingTrivia);
	}
}
