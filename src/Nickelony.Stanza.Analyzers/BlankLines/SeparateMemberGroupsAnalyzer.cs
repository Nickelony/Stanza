using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SeparateMemberGroupsAnalyzer() : StanzaRuleAnalyzer(StanzaRules.SeparateMemberGroups)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		if (!RequiredBlankLineAnalysis.TryReadContainer(context, rule: Rule, out ContainerContext container, out GapOwners gapOwners))
			return;

		ImmutableArray<int> boundaries = BlankLineGrouping.ReadBorderBoundaries(
			BlankLineGrouping.ReadMemberGroupBorders(
				siblings: container.Siblings,
				gapOwners,
				gathered: null,
				cancellationToken: context.CancellationToken
			)
		);

		RequiredBlankLineAnalysis.ReportOwnedRunBoundaries(
			container,
			boundaries,
			rule: Rule,
			reader: new BlankLineOwnership.FamilyReader(container, rule: Rule, gapOwners, gathered: null)
		);
	}
}
