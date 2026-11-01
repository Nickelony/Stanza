using System;
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

	/// <summary>The run rules from the earliest, which wins a gap that two of them ask for.</summary>
	private static readonly ImmutableArray<(BlankLineGrouping.RunFamily Family, DiagnosticDescriptor Rule)> s_runRules =
	[
		(BlankLineGrouping.RunFamily.Declaration, DiagnosticDescriptors.SeparateDeclarationRuns),
		(BlankLineGrouping.RunFamily.Invocation, DiagnosticDescriptors.SeparateInvocationRuns),
		(BlankLineGrouping.RunFamily.Assertion, DiagnosticDescriptors.SeparateAssertionRuns),
		(BlankLineGrouping.RunFamily.Await, DiagnosticDescriptors.SeparateAwaitRuns),
		(BlankLineGrouping.RunFamily.Subscription, DiagnosticDescriptors.SeparateEventSubscriptionRuns),
		(BlankLineGrouping.RunFamily.Assignment, DiagnosticDescriptors.SeparateAssignmentRuns),
	];

	/// <summary>Gets the rule that owns the runs of a family.</summary>
	public static DiagnosticDescriptor GetRunRule(BlankLineGrouping.RunFamily family)
	{
		foreach ((BlankLineGrouping.RunFamily runFamily, DiagnosticDescriptor rule) in s_runRules)
		{
			if (runFamily == family)
				return rule;
		}

		throw new ArgumentOutOfRangeException(nameof(family), family, message: null);
	}

	/// <summary>Whether a rule other than the one asking owns the gap above a unit, so the asker defers.</summary>
	public static bool IsGapAboveOwnedByAnother(ContainerContext container, int boundary, StanzaRule rule, bool defersMultiLineGaps)
	{
		SyntaxNode previous = container.Siblings[boundary - 1];
		SyntaxNode next = container.Siblings[boundary];

		if (IsGapAboveOwnedByCommentedSibling(context: container.Context, options: container.Options, previous, next, rule))
			return true;

		// The import section rule owns the gap after the last import, and the multi-line rule a gap a multi-line unit borders.
		// Each keeps the gap only where it is on, so an owner that is off hands the gap back.
		if (IsImportSectionGap(context: container.Context, options: container.Options, previous, next, rule))
			return true;

		// The base call rule owns the gap beside a base call ahead of the rules that defer to it.
		if (BlankLineBaseCalls.IsAskedGap(container, boundary, askingRule: rule))
			return true;

		return defersMultiLineGaps
			&& (StanzaSyntax.IsMultiLine(previous) || StanzaSyntax.IsMultiLine(next))
			&& StanzaRuleState.IsRequiredDeferralOnAt(
				scope: container,
				options: container.Options,
				rule,
				target: DiagnosticDescriptors.SeparateMultiLineConstructs,
				position: next.SpanStart
			);
	}

	/// <summary>Whether the import section rule is on and owns the gap below the last import.</summary>
	/// <remarks>
	/// A rule that does not defer to the import rule sees no import, so only a rule that does is asked.
	/// </remarks>
	public static bool IsImportSectionGap(
		SyntaxNodeAnalysisContext context,
		StanzaOptions options,
		SyntaxNode previous,
		SyntaxNode next,
		StanzaRule rule
	)
	{
		return BlankLineImports.IsImportTerminator(previous, next)
			&& StanzaRuleState.IsDeferralOnAt(
				scope: context,
				options,
				rule,
				target: DiagnosticDescriptors.SeparateImportSection,
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
		return BlankLineGaps.IsMissingBlankLineAboveComment(previous, next)
			&& StanzaRuleState.IsDeferralOnAt(
				scope: context,
				options,
				rule,
				target: DiagnosticDescriptors.SeparateCommentedSibling,
				position: next.SpanStart
			);
	}

	/// <summary>
	/// Whether a tight rule that ZA0012 defers to is on above a unit, unless a pragma silences it there.
	/// </summary>
	public static bool IsGapAboveOwnedByTightRule(ContainerContext container, int boundary, StanzaRule commentedSibling)
	{
		SyntaxKind containerKind = container.Kind;

		foreach (DiagnosticDescriptor target in commentedSibling.DefersTo)
		{
			StanzaRule tight = commentedSibling.GetDeferral(target);

			if (!tight.Kinds.Contains(containerKind))
				continue;

			if (target == DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether && !HoldsTwoKeptUnits(container.Siblings))
				continue;

			if (!StanzaRuleState.IsOffAt(scope: container, options: container.Options, rule: tight, position: container.Siblings[boundary].SpanStart))
				return true;
		}

		return false;
	}

	/// <summary>Whether the two-unit rule owns the gap of a container of two units, so the asker defers.</summary>
	public static bool IsGapOwnedByTwoUnitRule(ContainerContext container, StanzaRule rule)
	{
		return HoldsTwoKeptUnits(container.Siblings)
			&& !StanzaRuleState.IsOffAt(
				scope: container,
				options: container.Options,
				rule: rule.GetDeferral(DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether),
				position: container.Siblings[1].SpanStart
			);
	}

	/// <summary>Whether the container holds two units that the two-unit rule judges.</summary>
	/// <remarks>
	/// The rule skips a local function, an import and a container where a conditional directive can change the count.
	/// </remarks>
	public static bool HoldsTwoKeptUnits(ImmutableArray<SyntaxNode> siblings)
	{
		return siblings.Length == 2
			&& IsKeptTogetherUnit(siblings[0])
			&& IsKeptTogetherUnit(siblings[1])
			&& !BlankLineDirectives.HoldsConditionalDirective(siblings);
	}

	private static bool IsKeptTogetherUnit(SyntaxNode unit) =>
		unit is not (ExternAliasDirectiveSyntax or UsingDirectiveSyntax or LocalFunctionStatementSyntax);

	/// <summary>The borders the grouping rules would own once the gathered gaps are tight.</summary>
	/// <remarks>
	/// The rules read their runs from the blank lines that stand, so a gathered run can start a run of theirs.
	/// </remarks>
	public static ImmutableArray<BlankLineGrouping.RunBorder> ReadGatheredGroupingBorders(
		ContainerContext container,
		StanzaRule rule,
		GapOwners gapOwners,
		BlankLineGrouping.GatheredStretch? gathered
	)
	{
		ImmutableArray<SyntaxNode> siblings = container.Siblings;
		CancellationToken cancellationToken = container.CancellationToken;

		ImmutableArray<BlankLineGrouping.RunBorder> borders = CollectOwnedRunBorders(
			container,
			rule,
			reader: new FamilyReader(container, rule, gapOwners, gathered)
		);

		borders = borders.AddRange(
			ReadGroupBorders(
				container,
				owner: rule.GetDeferral(DiagnosticDescriptors.SeparateSameTypeDeclarationRuns),
				readBorders: () => BlankLineGrouping.ReadSameTypeDeclarationBorders(siblings, gapOwners, gathered, cancellationToken)
			)
		);

		if (rule.Defers(DiagnosticDescriptors.SeparateMemberGroups))
		{
			borders = borders.AddRange(
				ReadGroupBorders(
					container,
					owner: rule.GetDeferral(DiagnosticDescriptors.SeparateMemberGroups),
					readBorders: () => BlankLineGrouping.ReadMemberGroupBorders(siblings, gapOwners, gathered, cancellationToken)
				)
			);
		}

		if (rule.Defers(DiagnosticDescriptors.SeparateModifierGroups))
		{
			borders = borders.AddRange(
				ReadGroupBorders(
					container,
					owner: rule.GetDeferral(DiagnosticDescriptors.SeparateModifierGroups),
					readBorders: () => BlankLineGrouping.ReadModifierGroupBorders(siblings, gapOwners, gathered, cancellationToken)
				)
			);
		}

		return borders;
	}

	/// <summary>The borders a grouping rule owns, none while it is off, and none that a pragma silences.</summary>
	private static ImmutableArray<BlankLineGrouping.RunBorder> ReadGroupBorders(
		ContainerContext container,
		StanzaRule owner,
		Func<ImmutableArray<BlankLineGrouping.RunBorder>> readBorders
	)
	{
		return StanzaRuleState.IsRuleDisabled(container, container.Options, owner)
			? []
			: ReadBordersNotSilencedByPragma(container, borders: readBorders(), ownerRuleId: owner.Descriptor.Id);
	}

	public static GapOwners ReadGapOwners(ContainerContext container, StanzaRule rule)
	{
		StanzaRule multiLine = rule.GetDeferral(DiagnosticDescriptors.SeparateMultiLineConstructs);
		StanzaRule regionMarker = rule.GetDeferral(DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors);
		StanzaRule conditionalBlock = rule.GetDeferral(DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors);
		StanzaRule commentedSibling = rule.GetDeferral(DiagnosticDescriptors.SeparateCommentedSibling);

		// The rules that read runs all read the await rule's state, so they agree on the kind of an await statement.
		StanzaRule awaitRule =
			rule.Descriptor == DiagnosticDescriptors.SeparateAwaitRuns ? rule : rule.GetDeferral(DiagnosticDescriptors.SeparateAwaitRuns);

		return new GapOwners(
			MultiLineId: StanzaRuleState.IsRuleDisabled(container, container.Options, multiLine) ? null : multiLine.Descriptor.Id,
			RegionMarkerId: StanzaRuleState.IsRuleDisabled(container, container.Options, regionMarker) ? null : regionMarker.Descriptor.Id,

			ConditionalBlockId: StanzaRuleState.IsRuleDisabled(container, container.Options, conditionalBlock)
				? null
				: conditionalBlock.Descriptor.Id,

			CommentedSiblingId: StanzaRuleState.IsRuleDisabled(container, container.Options, commentedSibling)
				? null
				: commentedSibling.Descriptor.Id,

			AwaitRuleEnabled: !StanzaRuleState.IsRuleDisabled(container, container.Options, awaitRule),
			BaseCallBoundaries: BlankLineBaseCalls.ReadAskedBaseCallBoundaries(container, askingRule: rule)
		);
	}

	/// <summary>The boundaries the run rules ahead of the asking rule own in the container.</summary>
	public static ImmutableHashSet<int> ReadOwnedRunBoundaries(ContainerContext container, StanzaRule rule, FamilyReader reader) =>
		[.. CollectOwnedRunBorders(container, rule, reader).Select(static border => border.Boundary)];

	/// <summary>The boundaries the grouping rules ahead of the asking rule own in the container.</summary>
	public static ImmutableHashSet<int> ReadOwnedGroupingBoundaries(ContainerContext container, StanzaRule rule, GapOwners gapOwners)
	{
		ImmutableArray<SyntaxNode> siblings = container.Siblings;
		CancellationToken cancellationToken = container.CancellationToken;
		HashSet<int> owned = [.. ReadOwnedRunBoundaries(container, rule, reader: new FamilyReader(container, rule, gapOwners, gathered: null))];

		if (!s_memberContainerKinds.Contains(container.Kind))
			return [.. owned];

		StanzaRule memberGroups = rule.GetDeferral(DiagnosticDescriptors.SeparateMemberGroups);

		// Each rule's boundaries are filtered on their own before they join the shared set:
		// the pragma of a later rule must not take away a boundary an earlier one owns.
		if (!StanzaRuleState.IsRuleDisabled(container, container.Options, memberGroups))
		{
			AddOwnedBoundaries(
				container,
				owned,
				ownerRuleId: memberGroups.Descriptor.Id,
				borders: BlankLineGrouping.ReadMemberGroupBorders(siblings, gapOwners, gathered: null, cancellationToken)
			);
		}

		if (StanzaRuleState.IsDeferralOn(scope: container, options: container.Options, rule, target: DiagnosticDescriptors.SeparateModifierGroups))
		{
			AddOwnedBoundaries(
				container,
				owned,
				ownerRuleId: DiagnosticDescriptors.SeparateModifierGroups.Id,
				borders: BlankLineGrouping.ReadModifierGroupBorders(siblings, gapOwners, gathered: null, cancellationToken)
			);
		}

		return [.. owned];
	}

	/// <summary>The borders every grouping rule that is on asks for in the container.</summary>
	/// <remarks>
	/// It takes no asking rule, as A6 of the conventions says, since a deferral would hand over the gap.
	/// </remarks>
	public static ImmutableArray<BlankLineGrouping.RunBorder> ReadAskedGroupingBorders(ContainerContext container)
	{
		// The same-type rule defers to every other grouping rule, so it can name each of them.
		StanzaRule sameType = StanzaRules.SeparateSameTypeDeclarationRuns;
		ImmutableArray<SyntaxNode> siblings = container.Siblings;
		CancellationToken cancellationToken = container.CancellationToken;

		GapOwners gapOwners = ReadGapOwners(container, sameType);

		ImmutableArray<BlankLineGrouping.RunBorder> borders = CollectOwnedRunBorders(
			container,
			rule: sameType,
			reader: new FamilyReader(container, rule: sameType, gapOwners, gathered: null)
		);

		if (s_memberContainerKinds.Contains(container.Kind))
		{
			borders = borders.AddRange(
				ReadGroupBorders(
					container,
					owner: sameType.GetDeferral(DiagnosticDescriptors.SeparateMemberGroups),
					readBorders: () => BlankLineGrouping.ReadMemberGroupBorders(siblings, gapOwners, gathered: null, cancellationToken)
				)
			);

			borders = borders.AddRange(
				ReadGroupBorders(
					container,
					owner: sameType.GetDeferral(DiagnosticDescriptors.SeparateModifierGroups),
					readBorders: () => BlankLineGrouping.ReadModifierGroupBorders(siblings, gapOwners, gathered: null, cancellationToken)
				)
			);
		}

		GapOwners sameTypeOwners = gapOwners with { OwnedBoundaries = ReadOwnedGroupingBoundaries(container, rule: sameType, gapOwners) };

		return borders.AddRange(
			ReadGroupBorders(
				container,
				owner: sameType,

				readBorders: () =>
					BlankLineGrouping.ReadSameTypeDeclarationBorders(siblings, gapOwners: sameTypeOwners, gathered: null, cancellationToken)
			)
		);
	}

	private static void AddOwnedBoundaries(
		ContainerContext container,
		HashSet<int> owned,
		string ownerRuleId,
		ImmutableArray<BlankLineGrouping.RunBorder> borders
	)
	{
		HashSet<int> ownerBoundaries = [.. BlankLineGrouping.ReadBorderBoundaries(borders)];

		RemovePragmaSilencedBoundaries(container, boundaries: ownerBoundaries, ownerRuleId);
		owned.UnionWith(ownerBoundaries);
	}

	/// <summary>Reads the borders of each run family of a container, making a shared read once.</summary>
	/// <remarks>
	/// The borders keep what an owner that is off or a pragma would drop, which the caller that owns them applies.
	/// </remarks>
	public sealed class FamilyReader(ContainerContext container, StanzaRule rule, GapOwners gapOwners, BlankLineGrouping.GatheredStretch? gathered)
	{
		private ImmutableArray<BlankLineGrouping.RunBorder> _statements;
		private ImmutableArray<BlankLineGrouping.RunBorder> _subscriptions;

		/// <summary>Reads the borders the runs of one family ask for.</summary>
		/// <remarks>
		/// A run of assignments ends where the subscription rule keeps its gap, and splits by its targets on request.
		/// </remarks>
		public ImmutableArray<BlankLineGrouping.RunBorder> ReadFamilyBorders(BlankLineGrouping.RunFamily family)
		{
			// Only the subscription family needs a semantic model, so it has a walk of its own.
			if (family == BlankLineGrouping.RunFamily.Subscription)
				return ReadSubscriptionBorders();

			if (family != BlankLineGrouping.RunFamily.Assignment)
				return [.. ReadStatementBorders().Where(border => border.Family == family)];

			Func<SyntaxNode, BlankLineGrouping.AssignmentTarget>? readTarget = BlankLineAssignmentTargets.FindReader(container);
			ImmutableHashSet<int> subscriptions = ReadSubscriptionBoundaries();

			// The walk of the other families binds no symbol and ends no run at a subscription, so it serves the assignments
			// only where nothing splits their runs.
			if (subscriptions.IsEmpty && readTarget is null)
				return [.. ReadStatementBorders().Where(border => border.Family == family)];

			GapOwners walkOwners = subscriptions.IsEmpty ? gapOwners : gapOwners with { OwnedBoundaries = subscriptions };

			return
			[
				.. BlankLineGrouping
					.ReadRunBorders(
						siblings: container.Siblings,
						options: container.Options,
						gapOwners: walkOwners,
						gathered,
						readTarget,
						cancellationToken: container.CancellationToken
					)
					.Where(border => border.Family == family),
			];
		}

		private ImmutableArray<BlankLineGrouping.RunBorder> ReadSubscriptionBorders()
		{
			if (_subscriptions.IsDefault)
				_subscriptions = BlankLineSubscriptions.ReadBorders(container, gapOwners, gathered);

			return _subscriptions;
		}

		private ImmutableArray<BlankLineGrouping.RunBorder> ReadStatementBorders()
		{
			if (_statements.IsDefault)
			{
				_statements = BlankLineGrouping.ReadRunBorders(
					siblings: container.Siblings,
					options: container.Options,
					gapOwners,
					gathered,
					readTarget: null,
					cancellationToken: container.CancellationToken
				);
			}

			return _statements;
		}

		/// <summary>Reads the boundaries the subscription rule owns while on, where assignments end a run.</summary>
		/// <remarks>
		/// A rule that does not defer to the subscription rule reads no such boundary.
		/// </remarks>
		private ImmutableHashSet<int> ReadSubscriptionBoundaries()
		{
			if (
				!StanzaRuleState.IsDeferralOn(
					scope: container,
					options: container.Options,
					rule,
					target: DiagnosticDescriptors.SeparateEventSubscriptionRuns
				)
			)
			{
				return [];
			}

			ImmutableArray<BlankLineGrouping.RunBorder> borders = ReadBordersNotSilencedByPragma(
				container,
				borders: ReadSubscriptionBorders(),
				ownerRuleId: DiagnosticDescriptors.SeparateEventSubscriptionRuns.Id
			);

			return [.. borders.Select(static border => border.Boundary)];
		}
	}

	/// <summary>The borders of the run rules the asking rule defers to, minus a rule that is off.</summary>
	/// <remarks>
	/// Each rule's pragmas apply to its own borders alone.
	/// </remarks>
	private static ImmutableArray<BlankLineGrouping.RunBorder> CollectOwnedRunBorders(
		ContainerContext container,
		StanzaRule rule,
		FamilyReader reader
	)
	{
		ImmutableArray<BlankLineGrouping.RunBorder>.Builder owned = ImmutableArray.CreateBuilder<BlankLineGrouping.RunBorder>();

		// The reader runs the walk of the families once, and only for a rule that has an owner to ask.
		foreach ((BlankLineGrouping.RunFamily family, DiagnosticDescriptor descriptor) in s_runRules)
		{
			// A run rule lists a later one only to read its state, so no rule from its own entry on takes a gap from it.
			if (descriptor == rule.Descriptor)
				break;

			if (!rule.Defers(descriptor))
				continue;

			StanzaRule runRule = rule.GetDeferral(descriptor);

			if (StanzaRuleState.IsRuleDisabled(container, container.Options, runRule))
				continue;

			owned.AddRange(ReadBordersNotSilencedByPragma(container, borders: reader.ReadFamilyBorders(family), ownerRuleId: runRule.Descriptor.Id));
		}

		return owned.ToImmutable();
	}

	private static ImmutableArray<BlankLineGrouping.RunBorder> ReadBordersNotSilencedByPragma(
		ContainerContext container,
		ImmutableArray<BlankLineGrouping.RunBorder> borders,
		string ownerRuleId
	)
	{
		HashSet<int> boundaries = [.. borders.Select(static border => border.Boundary)];

		RemovePragmaSilencedBoundaries(container, boundaries, ownerRuleId);
		return [.. borders.Where(border => boundaries.Contains(border.Boundary))];
	}

	private static void RemovePragmaSilencedBoundaries(ContainerContext container, HashSet<int> boundaries, string ownerRuleId)
	{
		ImmutableArray<SyntaxNode> siblings = container.Siblings;
		SyntaxTree syntaxTree = container.Node.SyntaxTree;
		CancellationToken cancellationToken = container.CancellationToken;

		if (boundaries.Count == 0 || !StanzaPragmas.HasPragmaWarningDirectives(syntaxTree, cancellationToken))
			return;

		// The covered boundaries are collected first, because a set cannot change while it is enumerated.
		// A loop rather than a predicate lets each check poll the cancellation token.
		List<int> covered = [];

		foreach (int boundary in boundaries)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (StanzaPragmas.IsSilencedByPragma(syntaxTree, ruleId: ownerRuleId, position: siblings[boundary].SpanStart, cancellationToken))
				covered.Add(boundary);
		}

		foreach (int boundary in covered)
			boundaries.Remove(boundary);
	}
}
