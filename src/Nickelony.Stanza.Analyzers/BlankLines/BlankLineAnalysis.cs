using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Reports the blank line a separating rule wants at a gap.</summary>
internal static class BlankLineAnalysis
{
	public static void ReportMultiLineSiblingGaps(SyntaxNodeAnalysisContext context, ImmutableArray<SyntaxNode> siblings, StanzaRule rule)
	{
		if (siblings.Length < 2)
			return;

		// A separating rule and its tight rule share the boundary,
		// so the file's settings decide which half owns it: each would undo the other's fix on every run.
		// The gap is judged at each boundary, not once per container,
		// so a pragma silencing the owning half at one boundary hands only that boundary to this half.
		StanzaOptions? options = null;

		for (int index = 1; index < siblings.Length; index++)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			if (!StanzaSyntax.IsMultiLine(siblings[index - 1]) && !StanzaSyntax.IsMultiLine(siblings[index]))
				continue;

			// Options are read once per container, at its first multi-line boundary.
			options ??= StanzaOptions.From(context.Options, context.Node.SyntaxTree);

			if (!StanzaRuleState.OwnsTheSharedGap(scope: context, options, rule, position: siblings[index].SpanStart))
				continue;

			ReportBoundary(context, options, siblings, boundaryIndex: index, rule);
		}
	}

	/// <summary>Reports the missing blank line at each given boundary whose gap no other rule owns.</summary>
	public static void ReportGroupingBoundaries(
		SyntaxNodeAnalysisContext context,
		StanzaOptions options,
		ImmutableArray<SyntaxNode> siblings,
		ImmutableArray<int> boundaries,
		StanzaRule rule,
		ImmutableHashSet<int>? excludedBoundaries = null
	)
	{
		foreach (int boundaryIndex in boundaries)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			if (excludedBoundaries is not null && excludedBoundaries.Contains(boundaryIndex))
				continue;

			if (!BlankLineOwnership.IsGapAboveOwnedByAnother(context, options, siblings, boundaryIndex, rule, defersMultiLineGaps: true))
				ReportBoundary(context, options, siblings, boundaryIndex, rule);
		}
	}

	/// <summary>Reports the missing blank line at each import boundary the predicate names.</summary>
	public static void ReportImportBoundaries(
		SyntaxNodeAnalysisContext context,
		ImmutableArray<SyntaxNode> siblings,
		StanzaRule rule,
		Func<SyntaxNode, SyntaxNode, bool> isBoundary
	)
	{
		for (int index = 1; index < siblings.Length; index++)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			if (isBoundary(siblings[index - 1], siblings[index]))
				ReportImportBoundary(context, previous: siblings[index - 1], next: siblings[index], rule);
		}
	}

	public static void ReportFileScopedNamespaceGap(SyntaxNodeAnalysisContext context, StanzaRule rule)
	{
		var declaration = (FileScopedNamespaceDeclarationSyntax)context.Node;

		if (declaration.Members.Count == 0)
			return;

		// An import section after the header comes before the first member, so the header has no member as its neighbor.
		if (declaration.Usings.Count > 0 || declaration.Externs.Count > 0)
			return;

		SyntaxNode header = declaration.Name;
		SyntaxNode member = declaration.Members[0];

		// A directive at the boundary keeps the gap as written.
		if (BlankLineGaps.HasDirectiveBefore(member))
			return;

		// The gap starts below the semicolon, so a comment that trails it belongs to the header.
		ReportGap(context, previous: header, next: member, rule);
	}

	/// <summary>Reports the missing blank line between two tokens when a fix can add it.</summary>
	public static void ReportRequiredBlankLine(SyntaxNodeAnalysisContext context, SyntaxToken anchor, SyntaxToken neighbor, StanzaRule rule)
	{
		(SyntaxToken previous, SyntaxToken next) = BlankLineGaps.OrderBoundary(anchor, neighbor);

		if (BlankLineGaps.CreateTokenInsertion(context.Node.SyntaxTree.GetText(context.CancellationToken), previous, next) is null)
			return;

		context.ReportDiagnostic(Diagnostic.Create(rule.Descriptor, anchor.GetLocation(), additionalLocations: [neighbor.GetLocation()]));
	}

	private static void ReportBoundary(
		SyntaxNodeAnalysisContext context,
		StanzaOptions options,
		ImmutableArray<SyntaxNode> siblings,
		int boundaryIndex,
		StanzaRule rule
	)
	{
		SyntaxNode previous = siblings[boundaryIndex - 1];
		SyntaxNode next = siblings[boundaryIndex];

		// The import rules own the boundary between two neighboring import directives.
		if (
			BlankLineImports.IsImportPair(previous, next)
			|| BlankLineOwnership.IsGapAboveOwnedByAnother(context, options, siblings, boundaryIndex, rule, defersMultiLineGaps: false)
			|| BlankLineGaps.HasDirectiveBefore(next)
		)
		{
			return;
		}

		ReportGap(context, previous, next, rule);
	}

	/// <summary>Reports the gap unless a directive in it keeps the gap as written.</summary>
	/// <remarks>
	/// The import rules own their gaps, so no other rule is asked.
	/// </remarks>
	private static void ReportImportBoundary(SyntaxNodeAnalysisContext context, SyntaxNode previous, SyntaxNode next, StanzaRule rule)
	{
		if (!BlankLineGaps.HasDirectiveBefore(next))
			ReportGap(context, previous, next, rule);
	}

	private static void ReportGap(SyntaxNodeAnalysisContext context, SyntaxNode previous, SyntaxNode next, StanzaRule rule)
	{
		// The blank-line check and the insertion check both need the leading unit and the previous end line,
		// so each is read once and passed to both.
		StanzaSyntax.LeadingUnit leadingUnit = StanzaSyntax.GetLeadingUnit(next);
		int gapStartLineIndex = BlankLineGaps.GetGapStartLineIndex(previous, next);

		if (
			BlankLineGaps.HasBlankLineBetween(previous, leadingUnit, gapStartLineIndex)
			|| !BlankLineGaps.CanInsertBlankLine(leadingUnit, gapStartLineIndex)
		)
		{
			return;
		}

		// Anchor on the first token: the boundary is one line of gap, not the whole unit.
		// The previous unit travels as the additional location,
		// so the code fix reads both neighboring units off the diagnostic.
		// The step travels with the diagnostic, so a fix that pushes a unit down indents it the way the file is configured.
		int indentStepColumns = StanzaOptions.From(context.Options, context.Node.SyntaxTree).IndentStepColumns;

		context.ReportDiagnostic(
			Diagnostic.Create(
				descriptor: rule.Descriptor,
				location: next.GetFirstToken().GetLocation(),
				additionalLocations: [previous.GetLocation()],
				properties: new FixOptions(IndentStepColumns: indentStepColumns).ToProperties()
			)
		);
	}
}
