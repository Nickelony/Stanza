using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SeparateSameTypeDeclarationGroupsAnalyzer() : StanzaRuleAnalyzer(StanzaRules.SeparateSameTypeDeclarationGroups)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		ImmutableArray<SyntaxNode> siblings = StanzaSyntax.ReadSiblings(context.Node);

		if (siblings.Length < 2)
			return;

		var options = StanzaOptions.From(context.Options, context.Node.SyntaxTree);
		GapOwners gapOwners = BlankLineOwnership.ReadGapOwners(context, options, Rule);

		ImmutableArray<int> boundaries = BlankLineGrouping.ReadSameTypeDeclarationBoundaries(siblings, gapOwners, context.CancellationToken);

		if (boundaries.IsEmpty)
			return;

		// Declaration and invocation runs, member groups and modifier groups own their boundaries;
		// an owner that is off at a boundary, by its severity or a pragma, hands it over.
		ImmutableHashSet<int> ownedBoundaries = BlankLineOwnership.ReadOwnedGroupingBoundaries(context, options, siblings, rule: Rule, gapOwners);

		BlankLineAnalysis.ReportGroupingBoundaries(context, options, siblings, boundaries, rule: Rule, excludedBoundaries: ownedBoundaries);
	}
}
