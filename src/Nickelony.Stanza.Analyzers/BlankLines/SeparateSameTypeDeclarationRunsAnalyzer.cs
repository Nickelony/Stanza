using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SeparateSameTypeDeclarationRunsAnalyzer() : StanzaRuleAnalyzer(StanzaRules.SeparateSameTypeDeclarationRuns)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		if (!RequiredBlankLineAnalysis.TryReadContainer(context, rule: Rule, out ContainerContext container, out GapOwners gapOwners))
			return;

		// Declaration and invocation runs, member groups and modifier groups own their boundaries;
		// an owner that is off at a boundary, by its severity or a pragma, hands it over.
		ImmutableHashSet<int> ownedBoundaries = BlankLineOwnership.ReadOwnedGroupingBoundaries(container, rule: Rule, gapOwners);

		// A run ends where an owner writes its blank line, so the fix of an owner cannot split a run this rule reads.
		ImmutableArray<int> boundaries = BlankLineGrouping.ReadBorderBoundaries(
			borders: BlankLineGrouping.ReadSameTypeDeclarationBorders(
				siblings: container.Siblings,

				gapOwners: gapOwners with
				{
					OwnedBoundaries = ownedBoundaries,
				},

				gathered: null,
				cancellationToken: context.CancellationToken
			)
		);

		if (boundaries.IsEmpty)
			return;

		RequiredBlankLineAnalysis.ReportGroupingBoundaries(container, boundaries, rule: Rule, excludedBoundaries: ownedBoundaries);
	}
}
