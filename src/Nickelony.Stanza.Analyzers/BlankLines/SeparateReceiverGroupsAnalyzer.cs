using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SeparateReceiverGroupsAnalyzer() : StanzaRuleAnalyzer(StanzaRules.SeparateReceiverGroups)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		if (
			!RequiredBlankLineAnalysis.TryReadContainer(context, rule: Rule, out ContainerContext container, out GapOwners gapOwners)
			|| BlankLineOwnership.IsGapOwnedByTwoUnitRule(container, Rule)
		)
		{
			return;
		}

		ImmutableHashSet<int> owned = BlankLineOwnership
			.ReadAskedSeparatingBoundaries(container, Rule)
			.Union(BlankLineCheckedStanzas.ReadAskedBoundaries(container, Rule));

		int opening = BlankLineGuards.FindAskedGuardBoundary(container, Rule);

		if (opening >= 0)
			owned = owned.Add(opening);

		ImmutableArray<int> boundaries =
		[
			.. BlankLineReceiverGroups
				.GetGroups(container)
				.Outer.Where(boundary =>
					!BlankLineGuards.IsAskedByJumpRule(container, boundary, gapOwners, standing: owned.Add(boundary), guardRule: Rule)
				),
		];

		RequiredBlankLineAnalysis.ReportGroupingBoundaries(container, boundaries, rule: Rule, excludedBoundaries: owned);
	}
}
