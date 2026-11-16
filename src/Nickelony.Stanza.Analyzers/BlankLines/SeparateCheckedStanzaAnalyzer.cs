using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SeparateCheckedStanzaAnalyzer() : StanzaRuleAnalyzer(StanzaRules.SeparateCheckedStanza)
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

		ImmutableHashSet<int> owned =
		[
			.. BlankLineOwnership.ReadGatheredGroupingBorders(container, rule: Rule, gapOwners, gathered: null).Select(border => border.Boundary),
		];

		owned = owned.Union(BlankLineKeptPairs.ReadRunBoundaries(container, Rule));
		int opening = BlankLineGuards.FindAskedGuardBoundary(container, Rule);

		if (opening >= 0)
			owned = owned.Add(opening);

		ImmutableArray<int> boundaries =
		[
			.. BlankLineCheckedStanzas
				.ReadBoundaries(container)
				.Where(boundary =>
					!BlankLineGuards.IsAskedByJumpRule(container, boundary, gapOwners, standing: owned.Add(boundary), guardRule: Rule)
				),
		];

		RequiredBlankLineAnalysis.ReportGroupingBoundaries(container, boundaries, rule: Rule, excludedBoundaries: owned);
	}
}
