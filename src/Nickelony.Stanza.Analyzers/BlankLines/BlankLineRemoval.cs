using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Reports the blank line a tight rule wants gone from a gap.</summary>
internal static class BlankLineRemoval
{
	/// <summary>Reports a blank line between two single-line siblings.</summary>
	public static void ReportSingleLineBoundaries(SyntaxNodeAnalysisContext context, ImmutableArray<SyntaxNode> siblings, StanzaRule rule)
	{
		for (int index = 1; index < siblings.Length; index++)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			if (!IsMultiLineBoundary(siblings, index))
				ReportRemovableBoundary(context, previous: siblings[index - 1], next: siblings[index], rule);
		}
	}

	/// <summary>Reports a blank line beside a multi-line sibling, where this half owns the gap.</summary>
	public static void ReportMultiLineBoundaries(SyntaxNodeAnalysisContext context, ImmutableArray<SyntaxNode> siblings, StanzaRule rule)
	{
		if (siblings.Length < 2)
			return;

		StanzaOptions? options = null;

		for (int index = 1; index < siblings.Length; index++)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			if (!IsMultiLineBoundary(siblings, index))
				continue;

			SyntaxNode next = siblings[index];

			// Options are read once per container, at its first multi-line boundary.
			options ??= StanzaOptions.From(context.Options, context.Node.SyntaxTree);

			if (!StanzaRuleState.OwnsTheSharedGap(scope: context, options, rule, position: next.SpanStart))
				continue;

			ReportRemovableBoundary(context, previous: siblings[index - 1], next, rule);
		}
	}

	/// <summary>Reports a gap between single-line siblings that holds nothing but blank lines and whitespace.</summary>
	public static void ReportWholeGapBoundaries(SyntaxNodeAnalysisContext context, ImmutableArray<SyntaxNode> siblings, StanzaRule rule)
	{
		SourceText sourceText = context.Node.SyntaxTree.GetText(context.CancellationToken);

		for (int index = 1; index < siblings.Length; index++)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			SyntaxNode previous = siblings[index - 1];
			SyntaxNode next = siblings[index];

			if (IsMultiLineBoundary(siblings, index))
				continue;

			if (
				!BlankLineGaps.HasBlankLineBetween(previous, next)
				|| !StanzaText.IsWhitespaceOnly(
					sourceText,
					TextSpan.FromBounds(BlankLineGaps.GetGapStartToken(previous, next).Span.End, next.GetFirstToken().SpanStart)
				)
			)
			{
				continue;
			}

			ReportRemovableBoundary(context, previous, next, rule);
		}
	}

	/// <summary>Reports the blank line between two tokens when nothing but whitespace fills it.</summary>
	public static void ReportRemovableBlankLine(SyntaxNodeAnalysisContext context, SyntaxToken anchor, SyntaxToken neighbor, StanzaRule rule)
	{
		(SyntaxToken previous, SyntaxToken next) = BlankLineGaps.OrderBoundary(anchor, neighbor);

		if (HoldsARemovableBlankLine(context, previous, next, rule))
			context.ReportDiagnostic(Diagnostic.Create(rule.Descriptor, anchor.GetLocation(), additionalLocations: [neighbor.GetLocation()]));
	}

	private static bool IsMultiLineBoundary(ImmutableArray<SyntaxNode> siblings, int boundaryIndex) =>
		StanzaSyntax.IsMultiLine(siblings[boundaryIndex - 1]) || StanzaSyntax.IsMultiLine(siblings[boundaryIndex]);

	private static void ReportRemovableBoundary(SyntaxNodeAnalysisContext context, SyntaxNode previous, SyntaxNode next, StanzaRule rule)
	{
		if (HoldsARemovableBlankLine(context, previous: previous.GetLastToken(), next: next.GetFirstToken(), rule))
		{
			context.ReportDiagnostic(
				Diagnostic.Create(rule.Descriptor, next.GetFirstToken().GetLocation(), additionalLocations: [previous.GetLocation()])
			);
		}
	}

	/// <summary>
	/// Whether the gap holds a blank line that a fix can remove: whitespace alone, with no directive.
	/// </summary>
	private static bool HoldsARemovableBlankLine(SyntaxNodeAnalysisContext context, SyntaxToken previous, SyntaxToken next, StanzaRule rule)
	{
		SourceText sourceText = context.Node.SyntaxTree.GetText(context.CancellationToken);

		// A gap with no blank line skips the whitespace scan and the second trivia walk.
		return BlankLineGaps.HasBlankLineBetween(previous, next)
			&& BlankLineGaps.CreateAdjacentRemoval(sourceText, previous, next, ruleId: rule.Descriptor.Id) is not null;
	}
}
