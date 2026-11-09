using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SeparateModifierGroupsAnalyzer() : StanzaRuleAnalyzer(StanzaRules.SeparateModifierGroups)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		if (!RequiredBlankLineAnalysis.TryReadContainer(context, rule: Rule, out ContainerContext container, out GapOwners gapOwners))
			return;

		ImmutableArray<int> boundaries = BlankLineGrouping.ReadBorderBoundaries(
			borders: BlankLineGrouping.ReadModifierGroupBorders(
				siblings: container.Siblings,
				gapOwners,
				gathered: null,
				cancellationToken: context.CancellationToken
			)
		);

		if (boundaries.IsEmpty)
			return;

		// Declaration and invocation runs and member groups own their boundaries;
		// an owner that is off at a boundary, by its severity or a pragma, hands it over.
		ImmutableHashSet<int> ownedBoundaries = BlankLineOwnership.ReadOwnedGroupingBoundaries(container, rule: Rule, gapOwners);

		RequiredBlankLineAnalysis.ReportGroupingBoundaries(container, boundaries, rule: Rule, excludedBoundaries: ownedBoundaries);
	}
}
