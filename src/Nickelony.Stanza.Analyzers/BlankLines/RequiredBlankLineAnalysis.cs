using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Reports the blank line a separating rule requires at a gap.</summary>
internal static class RequiredBlankLineAnalysis
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

		for (int boundary = 1; boundary < siblings.Length; boundary++)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			if (!StanzaSyntax.IsMultiLine(siblings[boundary - 1]) && !StanzaSyntax.IsMultiLine(siblings[boundary]))
				continue;

			// Options are read once per container, at its first multi-line boundary.
			options ??= StanzaOptions.From(context.Options, context.Node.SyntaxTree);

			if (!StanzaRuleState.IsTheOwningHalf(scope: context, options, rule, position: siblings[boundary].SpanStart))
				continue;

			ReportBoundary(new ContainerContext(context, options, siblings), boundary, rule);
		}
	}

	/// <summary>
	/// Reports the missing blank line above a commented single-line sibling, unless a tight rule owns the gap.
	/// </summary>
	public static void ReportCommentedSiblingGaps(SyntaxNodeAnalysisContext context, ImmutableArray<SyntaxNode> siblings, StanzaRule rule)
	{
		StanzaOptions? options = null;

		for (int boundary = 1; boundary < siblings.Length; boundary++)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			SyntaxNode previous = siblings[boundary - 1];
			SyntaxNode next = siblings[boundary];

			if (!BlankLineGaps.IsMissingBlankLineAboveComment(previous, next))
				continue;

			// Options are read once per container, at its first commented sibling.
			options ??= StanzaOptions.From(context.Options, context.Node.SyntaxTree);

			if (!BlankLineOwnership.IsGapAboveOwnedByTightRule(new ContainerContext(context, options, siblings), boundary, commentedSibling: rule))
				ReportGap(context, previous, next, rule);
		}
	}

	/// <summary>Reads the siblings, options and gap owners of a container, or declines a small one.</summary>
	public static bool TryReadContainer(SyntaxNodeAnalysisContext context, StanzaRule rule, out ContainerContext container, out GapOwners gapOwners)
	{
		ImmutableArray<SyntaxNode> siblings = StanzaSyntax.ReadSiblings(context.Node);

		container = default;
		gapOwners = default;

		if (siblings.Length < 2)
			return false;

		container = new ContainerContext(context, StanzaOptions.From(context.Options, context.Node.SyntaxTree), siblings);
		gapOwners = BlankLineOwnership.ReadGapOwners(container, rule);

		return true;
	}

	/// <summary>Reports the boundaries a family of runs asks for, but not those an earlier rule owns.</summary>
	public static void ReportRunBoundaries(SyntaxNodeAnalysisContext context, StanzaRule rule, BlankLineGrouping.RunFamily family)
	{
		if (!TryReadContainer(context, rule, out ContainerContext container, out GapOwners gapOwners))
			return;

		var reader = new BlankLineOwnership.FamilyReader(container, rule, gapOwners, gathered: null);
		ImmutableArray<int> boundaries = BlankLineGrouping.ReadBorderBoundaries(borders: reader.ReadFamilyBorders(family));

		ReportOwnedRunBoundaries(container, boundaries, rule, reader);
	}

	/// <summary>Reports the boundaries a run asks for, but not those an earlier run rule owns.</summary>
	public static void ReportOwnedRunBoundaries(
		ContainerContext container,
		ImmutableArray<int> boundaries,
		StanzaRule rule,
		BlankLineOwnership.FamilyReader reader
	)
	{
		if (boundaries.IsEmpty)
			return;

		// A run rule that comes first owns its boundaries, and hands one over where it is off, by its severity or a pragma.
		ImmutableHashSet<int> ownedBoundaries = BlankLineOwnership.ReadOwnedRunBoundaries(container, rule, reader);

		ReportGroupingBoundaries(container, boundaries, rule, excludedBoundaries: ownedBoundaries);
	}

	/// <summary>Reports the missing blank line at each given boundary whose gap no other rule owns.</summary>
	public static void ReportGroupingBoundaries(
		ContainerContext container,
		ImmutableArray<int> boundaries,
		StanzaRule rule,
		ImmutableHashSet<int>? excludedBoundaries = null
	)
	{
		ImmutableArray<SyntaxNode> siblings = container.Siblings;
		HashSet<int> given = [.. boundaries];
		ILookup<int, BlankLineGrouping.RunBorder>? asked = null;
		int lastHeld = -1;

		foreach (int boundary in boundaries)
		{
			container.CancellationToken.ThrowIfCancellationRequested();

			if (excludedBoundaries is not null && excludedBoundaries.Contains(boundary))
				continue;

			bool writesGapAbove = false;

			// The gap above goes first in every fix order, while two gaps of one rule stay reported together for one Fix All.
			if (IsGapAboveMissing(siblings, boundary))
			{
				bool isOwnGapAbove = excludedBoundaries?.Contains(boundary - 1) != true && given.Contains(boundary - 1);

				asked ??= BlankLineOwnership.ReadAskedGroupingBorders(container).ToLookup(border => border.Boundary);

				if (isOwnGapAbove ? lastHeld == boundary - 1 : IsHeldByAnotherRule(asked, siblings, boundary))
				{
					lastHeld = boundary;
					continue;
				}

				writesGapAbove = isOwnGapAbove && IsGapAboveAskedAcrossBoundary(asked, boundary);
			}

			if (!BlankLineOwnership.IsGapAboveOwnedByAnother(container, boundary, rule, defersMultiLineGaps: true))
				ReportBoundary(container, boundary, rule, writesGapAbove);
		}
	}

	/// <summary>Whether a fix can write a blank line both at the boundary and at the gap directly above it.</summary>
	private static bool IsGapAboveMissing(ImmutableArray<SyntaxNode> siblings, int boundary)
	{
		return boundary > 1
			&& BlankLineGaps.IsGapMissingBlankLine(siblings[boundary - 1], siblings[boundary])
			&& BlankLineGaps.IsGapMissingBlankLine(siblings[boundary - 2], siblings[boundary - 1]);
	}

	/// <summary>Whether the boundary waits for a gap above it, or for the gap that one waits for itself.</summary>
	private static bool IsHeldByAnotherRule(ILookup<int, BlankLineGrouping.RunBorder> asked, ImmutableArray<SyntaxNode> siblings, int boundary)
	{
		return IsTiedToAskedGapAbove(asked, boundary)
			|| (asked.Contains(boundary - 1) && IsGapAboveMissing(siblings, boundary - 1) && IsTiedToAskedGapAbove(asked, boundary - 1));
	}

	/// <summary>Whether a rule asks for the gap above and a run crosses one of the two gaps.</summary>
	/// <remarks>
	/// A blank line in the crossed gap leaves the unit alone, which ends the ask for the other gap.
	/// </remarks>
	private static bool IsTiedToAskedGapAbove(ILookup<int, BlankLineGrouping.RunBorder> asked, int boundary)
	{
		int boundaryAbove = boundary - 1;

		if (!asked.Contains(boundaryAbove))
			return false;

		return IsGapAboveAskedAcrossBoundary(asked, boundary)
			|| asked[boundary].Any(border => border.RunEnd == boundary && border.RunStart < boundaryAbove);
	}

	/// <summary>Whether every run that asks for the gap above the boundary crosses the boundary.</summary>
	/// <remarks>
	/// A run that stops at the gap above asks for it whatever stands below, so only these tie the two gaps.
	/// </remarks>
	private static bool IsGapAboveAskedAcrossBoundary(ILookup<int, BlankLineGrouping.RunBorder> asked, int boundary) =>
		asked.Contains(boundary - 1) && asked[boundary - 1].All(border => border.RunEnd > boundary);

	/// <summary>Reports the missing blank line at each import boundary the predicate names.</summary>
	public static void ReportImportBoundaries(
		SyntaxNodeAnalysisContext context,
		ImmutableArray<SyntaxNode> siblings,
		StanzaRule rule,
		Func<SyntaxNode, SyntaxNode, bool> isBoundary
	)
	{
		StanzaOptions? options = null;

		for (int boundary = 1; boundary < siblings.Length; boundary++)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			if (!isBoundary(siblings[boundary - 1], siblings[boundary]))
				continue;

			// Options are read once per container, at its first import boundary.
			options ??= StanzaOptions.From(context.Options, context.Node.SyntaxTree);

			ReportImportBoundary(context, options, previous: siblings[boundary - 1], next: siblings[boundary], rule);
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
		(SyntaxToken previous, SyntaxToken next) = BlankLineGaps.OrderGapTokens(anchor, neighbor);

		if (BlankLineGaps.CreateTokenInsertion(context.Node.SyntaxTree.GetText(context.CancellationToken), previous, next) is null)
			return;

		context.ReportDiagnostic(Diagnostic.Create(rule.Descriptor, anchor.GetLocation(), additionalLocations: [neighbor.GetLocation()]));
	}

	private static void ReportBoundary(ContainerContext container, int boundary, StanzaRule rule, bool writesGapAbove = false)
	{
		SyntaxNode previous = container.Siblings[boundary - 1];
		SyntaxNode next = container.Siblings[boundary];

		// The import rules own the boundary between two neighboring import directives.
		if (
			BlankLineImports.IsImportPair(previous, next)
			|| BlankLineOwnership.IsGapAboveOwnedByAnother(container, boundary, rule, defersMultiLineGaps: false)
			|| BlankLineGaps.HasDirectiveBefore(next)
		)
		{
			return;
		}

		ReportGap(context: container.Context, previous, next, rule, writesGapAbove);
	}

	/// <summary>Reports the gap unless a directive in it keeps the gap as written.</summary>
	/// <remarks>
	/// The import rules own their gaps, so only the commented sibling rule is asked.
	/// </remarks>
	private static void ReportImportBoundary(
		SyntaxNodeAnalysisContext context,
		StanzaOptions options,
		SyntaxNode previous,
		SyntaxNode next,
		StanzaRule rule
	)
	{
		if (!BlankLineGaps.HasDirectiveBefore(next) && !BlankLineOwnership.IsGapAboveOwnedByCommentedSibling(context, options, previous, next, rule))
			ReportGap(context, previous, next, rule);
	}

	private static void ReportGap(
		SyntaxNodeAnalysisContext context,
		SyntaxNode previous,
		SyntaxNode next,
		StanzaRule rule,
		bool writesGapAbove = false
	)
	{
		// The blank-line check and the insertion check both need the leading unit and the previous end line,
		// so each is read once and passed to both.
		StanzaSyntax.LeadingUnit leadingUnit = StanzaSyntax.GetLeadingUnit(next);
		int gapStartLineIndex = SiblingGaps.GetGapStartLineIndex(previous, next);

		if (
			BlankLineGaps.HasBlankLineBetween(previous, leadingUnit, gapStartLineIndex)
			|| !SiblingGaps.CanInsertBlankLine(leadingUnit, gapStartLineIndex)
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
				properties: new FixOptions(IndentStepColumns: indentStepColumns, WritesGapAbove: writesGapAbove).ToProperties()
			)
		);
	}
}
