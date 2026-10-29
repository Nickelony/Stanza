using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Reads which rule owns a gap, so a rule defers where another keeps the gap.</summary>
internal static class BlankLineOwnership
{
	private static readonly ImmutableHashSet<SyntaxKind> s_memberContainerKinds = [.. StanzaKinds.MemberContainerKinds];

	/// <summary>Whether a rule other than the one asking owns the gap above a unit, so the asker defers.</summary>
	public static bool IsGapAboveOwnedByAnother(
		SyntaxNodeAnalysisContext context,
		StanzaOptions options,
		ImmutableArray<SyntaxNode> siblings,
		int boundaryIndex,
		StanzaRule rule,
		bool defersMultiLineGaps
	)
	{
		SyntaxNode previous = siblings[boundaryIndex - 1];
		SyntaxNode next = siblings[boundaryIndex];

		if (IsGapAboveOwnedByCommentedSibling(context, options, previous, next, rule))
			return true;

		// The import section rule owns the gap after the last import, and the multi-line rule a gap a multi-line unit borders.
		// Each keeps the gap only where it is on, so an owner that is off hands the gap back.
		// A rule that does not defer to the import rule sees no import, so only a rule that does is asked.
		if (
			rule.Defers(DiagnosticDescriptors.SeparateImportSection)
			&& BlankLineImports.IsImportTerminator(previous, next)
			&& !StanzaRuleState.IsOffAt(
				scope: context,
				options,
				rule: rule.GetDeferral(DiagnosticDescriptors.SeparateImportSection),
				position: next.SpanStart
			)
		)
		{
			return true;
		}

		return defersMultiLineGaps
			&& (StanzaSyntax.IsMultiLine(previous) || StanzaSyntax.IsMultiLine(next))
			&& !StanzaRuleState.IsOffAt(
				scope: context,
				options,
				rule: rule.GetDeferral(DiagnosticDescriptors.SeparateMultiLineConstructs),
				position: next.SpanStart
			);
	}

	/// <summary>Whether the commented sibling rule owns the gap above a unit, so the asker defers to it.</summary>
	public static bool IsGapAboveOwnedByCommentedSibling(
		SyntaxNodeAnalysisContext context,
		StanzaOptions options,
		SyntaxNode previous,
		SyntaxNode next,
		StanzaRule rule
	)
	{
		return rule.Defers(DiagnosticDescriptors.SeparateCommentedSibling)
			&& BlankLineGaps.IsMissingBlankLineAboveComment(previous, next)
			&& IsCommentedSiblingRuleOn(context, options, next, rule);
	}

	/// <summary>Whether the commented sibling rule is on above a unit, for a rule that defers to it.</summary>
	public static bool IsCommentedSiblingRuleOn(SyntaxNodeAnalysisContext context, StanzaOptions options, SyntaxNode next, StanzaRule rule)
	{
		return !StanzaRuleState.IsOffAt(
			scope: context,
			options,
			rule: rule.GetDeferral(DiagnosticDescriptors.SeparateCommentedSibling),
			position: next.SpanStart
		);
	}

	/// <summary>Whether the two-unit rule owns the gap of a container of two units, so the asker defers.</summary>
	public static bool IsGapOwnedByTwoUnitRule(
		SyntaxNodeAnalysisContext context,
		StanzaOptions options,
		ImmutableArray<SyntaxNode> siblings,
		StanzaRule rule
	)
	{
		return HoldsTwoKeptUnits(siblings)
			&& !StanzaRuleState.IsOffAt(
				scope: context,
				options,
				rule: rule.GetDeferral(DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether),
				position: siblings[1].SpanStart
			);
	}

	/// <summary>Whether the container holds two units that the two-unit rule judges.</summary>
	/// <remarks>
	/// The rule skips a local function and an import.
	/// </remarks>
	public static bool HoldsTwoKeptUnits(ImmutableArray<SyntaxNode> siblings) =>
		siblings.Length == 2 && IsKeptTogetherUnit(siblings[0]) && IsKeptTogetherUnit(siblings[1]);

	private static bool IsKeptTogetherUnit(SyntaxNode unit) =>
		unit is not (ExternAliasDirectiveSyntax or UsingDirectiveSyntax or LocalFunctionStatementSyntax);

	/// <summary>The borders the grouping rules would own once the gathered gaps are tight.</summary>
	/// <remarks>
	/// Both rules read their runs from the blank lines that stand, so a gathered run can start a run of theirs.
	/// </remarks>
	public static ImmutableArray<BlankLineGrouping.RunBorder> ReadGatheredGroupingBorders(
		SyntaxNodeAnalysisContext context,
		StanzaOptions options,
		ImmutableArray<SyntaxNode> siblings,
		StanzaRule rule,
		GapOwners gapOwners,
		ImmutableHashSet<int> gathered
	)
	{
		ImmutableArray<BlankLineGrouping.RunBorder> declarationBorders = CollectOwnedDeclarationRunBorders(
			context,
			options,
			siblings,
			rule,
			gapOwners,
			gathered
		);

		StanzaRule sameTypeGroups = rule.GetDeferral(DiagnosticDescriptors.SeparateSameTypeDeclarationGroups);

		if (StanzaRuleState.IsRuleOff(context, options, sameTypeGroups))
			return declarationBorders;

		ImmutableArray<BlankLineGrouping.RunBorder> sameTypeBorders = RemovePragmaSilencedBorders(
			scope: context,
			siblings,
			borders: BlankLineGrouping.ReadSameTypeDeclarationBorders(siblings, gapOwners, gathered, cancellationToken: context.CancellationToken),
			ownerRuleId: sameTypeGroups.Descriptor.Id
		);

		return declarationBorders.AddRange(sameTypeBorders);
	}

	public static GapOwners ReadGapOwners(SyntaxNodeAnalysisContext context, StanzaOptions options, StanzaRule rule)
	{
		StanzaRule multiLine = rule.GetDeferral(DiagnosticDescriptors.SeparateMultiLineConstructs);
		StanzaRule regionMarker = rule.GetDeferral(DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors);
		StanzaRule commentedSibling = rule.GetDeferral(DiagnosticDescriptors.SeparateCommentedSibling);

		return new GapOwners(
			MultiLineId: StanzaRuleState.IsRuleOff(context, options, multiLine) ? null : multiLine.Descriptor.Id,
			RegionMarkerId: StanzaRuleState.IsRuleOff(context, options, regionMarker) ? null : regionMarker.Descriptor.Id,
			CommentedSiblingId: StanzaRuleState.IsRuleOff(context, options, commentedSibling) ? null : commentedSibling.Descriptor.Id
		);
	}

	/// <summary>The boundaries the declaration-run rule owns in the container.</summary>
	public static ImmutableHashSet<int> ReadOwnedDeclarationRunBoundaries(
		SyntaxNodeAnalysisContext context,
		StanzaOptions options,
		ImmutableArray<SyntaxNode> siblings,
		StanzaRule rule,
		GapOwners gapOwners
	) => [.. CollectOwnedDeclarationRunBoundaries(context, options, siblings, rule, gapOwners)];

	/// <summary>The boundaries the grouping rules ahead of the asking rule own in the container.</summary>
	public static ImmutableHashSet<int> ReadOwnedGroupingBoundaries(
		SyntaxNodeAnalysisContext context,
		StanzaOptions options,
		ImmutableArray<SyntaxNode> siblings,
		StanzaRule rule,
		GapOwners gapOwners
	)
	{
		HashSet<int> owned = CollectOwnedDeclarationRunBoundaries(context, options, siblings, rule, gapOwners);

		if (!s_memberContainerKinds.Contains(context.Node.Kind()))
			return [.. owned];

		StanzaRule memberGroups = rule.GetDeferral(DiagnosticDescriptors.SeparateMemberGroups);

		// Each rule's boundaries are filtered on their own before they join the shared set:
		// the pragma of a later rule must not take away a boundary an earlier one owns.
		if (!StanzaRuleState.IsRuleOff(context, options, memberGroups))
		{
			AddOwnedBoundaries(
				scope: context,
				siblings,
				owned,
				ownerRuleId: memberGroups.Descriptor.Id,
				boundaries: BlankLineGrouping.ReadMemberGroupBoundaries(siblings, gapOwners, cancellationToken: context.CancellationToken)
			);
		}

		if (!rule.Defers(DiagnosticDescriptors.SeparateModifierGroups))
			return [.. owned];

		StanzaRule modifierGroups = rule.GetDeferral(DiagnosticDescriptors.SeparateModifierGroups);

		if (!StanzaRuleState.IsRuleOff(context, options, modifierGroups))
		{
			AddOwnedBoundaries(
				scope: context,
				siblings,
				owned,
				ownerRuleId: modifierGroups.Descriptor.Id,
				boundaries: BlankLineGrouping.ReadModifierGroupBoundaries(siblings, gapOwners, cancellationToken: context.CancellationToken)
			);
		}

		return [.. owned];
	}

	private static void AddOwnedBoundaries(
		RuleScope scope,
		ImmutableArray<SyntaxNode> siblings,
		HashSet<int> owned,
		string ownerRuleId,
		ImmutableArray<int> boundaries
	)
	{
		HashSet<int> ownerBoundaries = [.. boundaries];

		RemovePragmaSilencedBoundaries(scope, siblings, boundaries: ownerBoundaries, ownerRuleId);
		owned.UnionWith(ownerBoundaries);
	}

	private static HashSet<int> CollectOwnedDeclarationRunBoundaries(
		SyntaxNodeAnalysisContext context,
		StanzaOptions options,
		ImmutableArray<SyntaxNode> siblings,
		StanzaRule rule,
		GapOwners gapOwners
	) => [.. CollectOwnedDeclarationRunBorders(context, options, siblings, rule, gapOwners, gathered: null).Select(static border => border.Boundary)];

	private static ImmutableArray<BlankLineGrouping.RunBorder> CollectOwnedDeclarationRunBorders(
		SyntaxNodeAnalysisContext context,
		StanzaOptions options,
		ImmutableArray<SyntaxNode> siblings,
		StanzaRule rule,
		GapOwners gapOwners,
		ImmutableHashSet<int>? gathered
	)
	{
		StanzaRule declarationRuns = rule.GetDeferral(DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns);

		if (StanzaRuleState.IsRuleOff(context, options, declarationRuns))
			return [];

		return RemovePragmaSilencedBorders(
			scope: context,
			siblings,

			borders: BlankLineGrouping.ReadDeclarationRunBorders(
				siblings,
				options,
				gapOwners,
				gathered,
				cancellationToken: context.CancellationToken
			),

			ownerRuleId: declarationRuns.Descriptor.Id
		);
	}

	private static ImmutableArray<BlankLineGrouping.RunBorder> RemovePragmaSilencedBorders(
		RuleScope scope,
		ImmutableArray<SyntaxNode> siblings,
		ImmutableArray<BlankLineGrouping.RunBorder> borders,
		string ownerRuleId
	)
	{
		HashSet<int> boundaries = [.. borders.Select(static border => border.Boundary)];

		RemovePragmaSilencedBoundaries(scope, siblings, boundaries, ownerRuleId);
		return [.. borders.Where(border => boundaries.Contains(border.Boundary))];
	}

	private static void RemovePragmaSilencedBoundaries(
		RuleScope scope,
		ImmutableArray<SyntaxNode> siblings,
		HashSet<int> boundaries,
		string ownerRuleId
	)
	{
		SyntaxTree syntaxTree = scope.Tree;
		CancellationToken cancellationToken = scope.CancellationToken;

		if (boundaries.Count == 0 || !StanzaPragmas.HasPragmaWarningDirectives(syntaxTree, cancellationToken))
			return;

		// The covered boundaries are collected first, because a set cannot change while it is enumerated.
		// A loop rather than a predicate lets each check poll the cancellation token.
		List<int> covered = [];

		foreach (int boundaryIndex in boundaries)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (StanzaPragmas.IsSilencedByPragma(syntaxTree, ruleId: ownerRuleId, position: siblings[boundaryIndex].SpanStart, cancellationToken))
				covered.Add(boundaryIndex);
		}

		foreach (int boundaryIndex in covered)
			boundaries.Remove(boundaryIndex);
	}
}
