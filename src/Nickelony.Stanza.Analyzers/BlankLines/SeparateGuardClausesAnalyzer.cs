using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SeparateGuardClausesAnalyzer() : StanzaRuleAnalyzer(StanzaRules.SeparateGuardClauses)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		ImmutableArray<SyntaxNode> siblings = StanzaSyntax.ReadSiblings(context.Node);

		// A guard needs a statement below it, so a block of fewer than two has no gap to judge.
		if (siblings.Length < 2)
			return;

		var container = new ContainerContext(context, StanzaOptions.From(context.Options, context.Node.SyntaxTree), siblings);
		int boundary = BlankLineGuards.FindGuardBoundary(siblings, container.Options);

		if (boundary < 0 || BlankLineOwnership.IsGapOwnedByTwoUnitRule(container, rule: Rule))
			return;

		GapOwners gapOwners = BlankLineOwnership.ReadGapOwners(container, Rule);

		// The runs own the boundaries they ask for, and end the stanza there.
		ImmutableHashSet<int> owned =
		[
			.. BlankLineOwnership
				.ReadGatheredGroupingBorders(container, rule: Rule, gapOwners, gathered: null)
				.Select(static border => border.Boundary),
		];

		if (BlankLineGuards.IsAskedByJumpRule(container, boundary, gapOwners, standing: owned.Add(boundary), guardRule: Rule))
			return;

		RequiredBlankLineAnalysis.ReportGroupingBoundaries(container, boundaries: [boundary], rule: Rule, excludedBoundaries: owned);
	}
}
