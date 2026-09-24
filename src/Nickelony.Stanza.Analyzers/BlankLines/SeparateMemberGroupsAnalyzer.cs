using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SeparateMemberGroupsAnalyzer() : StanzaRuleAnalyzer(StanzaRules.SeparateMemberGroups)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		ImmutableArray<SyntaxNode> siblings = StanzaSyntax.ReadSiblings(context.Node);

		if (siblings.Length < 2)
			return;

		var options = StanzaOptions.From(context.Options, context.Node.SyntaxTree);
		GapOwners gapOwners = BlankLineOwnership.ReadGapOwners(context, options, Rule);

		ImmutableHashSet<int> ownedBoundaries = BlankLineOwnership.ReadOwnedDeclarationRunBoundaries(
			context,
			options,
			siblings,
			rule: Rule,
			gapOwners
		);

		BlankLineAnalysis.ReportGroupingBoundaries(
			context,
			options,
			siblings,
			boundaries: BlankLineGrouping.ReadMemberGroupBoundaries(siblings, gapOwners, cancellationToken: context.CancellationToken),
			rule: Rule,
			excludedBoundaries: ownedBoundaries
		);
	}
}
