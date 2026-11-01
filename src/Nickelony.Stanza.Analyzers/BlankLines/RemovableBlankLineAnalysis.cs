using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Reports the blank line a tight rule can remove from a gap.</summary>
internal static class RemovableBlankLineAnalysis
{
	/// <summary>Reports a blank line between two single-line siblings, including the one above a comment.</summary>
	public static void ReportSingleLineBoundaries(SyntaxNodeAnalysisContext context, ImmutableArray<SyntaxNode> siblings, StanzaRule rule)
	{
		for (int boundary = 1; boundary < siblings.Length; boundary++)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			if (!IsMultiLineBoundary(siblings, boundary))
				ReportRemovableBoundary(context, previous: siblings[boundary - 1], next: siblings[boundary], rule);
		}
	}

	/// <summary>Reports a blank line beside a multi-line sibling, where this half owns the gap.</summary>
	public static void ReportMultiLineBoundaries(SyntaxNodeAnalysisContext context, ImmutableArray<SyntaxNode> siblings, StanzaRule rule)
	{
		if (siblings.Length < 2)
			return;

		StanzaOptions? options = null;

		for (int boundary = 1; boundary < siblings.Length; boundary++)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			if (!IsMultiLineBoundary(siblings, boundary))
				continue;

			SyntaxNode next = siblings[boundary];

			// Options are read once per container, at its first multi-line boundary.
			options ??= StanzaOptions.From(context.Options, context.Node.SyntaxTree);

			if (!StanzaRuleState.IsTheOwningHalf(scope: context, options, rule, position: next.SpanStart))
				continue;

			ReportRemovableBoundary(context, previous: siblings[boundary - 1], next, rule);
		}
	}

	/// <summary>Reports each blank line of a run, with the run's bounds so that any fix removes them all.</summary>
	/// <remarks>
	/// They are the gaps the same fix writes a blank line into, for a rule whose runs regroup the units beside them.
	/// </remarks>
	public static void ReportRuns(
		ContainerContext container,
		ImmutableArray<BlankLineLooseRuns.LooseRun> runs,
		StanzaRule rule,
		ImmutableArray<int> boundaries = default
	)
	{
		ImmutableArray<SyntaxNode> siblings = container.Siblings;

		foreach (BlankLineLooseRuns.LooseRun run in runs)
		{
			var fixOptions = new FixOptions(
				IndentStepColumns: container.Options.IndentStepColumns,
				RunStart: run.Start,
				RunEnd: run.End,
				Boundaries: boundaries
			);

			for (int boundary = run.Start + 1; boundary < run.End; boundary++)
			{
				container.CancellationToken.ThrowIfCancellationRequested();
				ReportRemovableBoundary(context: container.Context, previous: siblings[boundary - 1], next: siblings[boundary], rule, fixOptions);
			}
		}
	}

	/// <summary>Reports a gap between single-line siblings that holds nothing but blank lines and whitespace.</summary>
	public static void ReportWholeGapBoundaries(SyntaxNodeAnalysisContext context, ImmutableArray<SyntaxNode> siblings, StanzaRule rule)
	{
		SourceText sourceText = context.Node.SyntaxTree.GetText(context.CancellationToken);

		for (int boundary = 1; boundary < siblings.Length; boundary++)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			SyntaxNode previous = siblings[boundary - 1];
			SyntaxNode next = siblings[boundary];

			if (IsMultiLineBoundary(siblings, boundary))
				continue;

			if (
				!BlankLineGaps.HasBlankLineBetween(previous, next)
				|| !StanzaText.IsWhitespaceOnly(
					sourceText,
					TextSpan.FromBounds(SiblingGaps.GetGapStartToken(previous, next).Span.End, next.GetFirstToken().SpanStart)
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
		(SyntaxToken previous, SyntaxToken next) = BlankLineGaps.OrderGapTokens(anchor, neighbor);

		if (HoldsARemovableBlankLine(context, previous, next, rule))
			context.ReportDiagnostic(Diagnostic.Create(rule.Descriptor, anchor.GetLocation(), additionalLocations: [neighbor.GetLocation()]));
	}

	private static bool IsMultiLineBoundary(ImmutableArray<SyntaxNode> siblings, int boundary) =>
		StanzaSyntax.IsMultiLine(siblings[boundary - 1]) || StanzaSyntax.IsMultiLine(siblings[boundary]);

	private static void ReportRemovableBoundary(
		SyntaxNodeAnalysisContext context,
		SyntaxNode previous,
		SyntaxNode next,
		StanzaRule rule,
		FixOptions? fixOptions = null
	)
	{
		if (HoldsARemovableBlankLine(context, previous: previous.GetLastToken(), next: next.GetFirstToken(), rule))
		{
			context.ReportDiagnostic(
				Diagnostic.Create(
					descriptor: rule.Descriptor,
					location: next.GetFirstToken().GetLocation(),
					additionalLocations: [previous.GetLocation()],
					properties: fixOptions?.ToProperties()
				)
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
