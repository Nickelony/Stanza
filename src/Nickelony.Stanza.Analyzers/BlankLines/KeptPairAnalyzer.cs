using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Base of a rule that removes the blank line between a pair of statements it keeps together.</summary>
/// <remarks>
/// Separating rules leave a claimed gap alone, and a gap ZA0052, ZA0054 or ZA0055 reports is theirs.
/// </remarks>
internal abstract class KeptPairAnalyzer(StanzaRule rule) : StanzaRuleAnalyzer(rule)
{
	protected sealed override void Analyze(SyntaxNodeAnalysisContext context)
	{
		var container = ContainerContext.From(context);

		if (container.Siblings.Length < 2 || BlankLineOwnership.IsGapOwnedByTwoUnitRule(container, Rule))
			return;

		RemovableBlankLineAnalysis.ReportClaimedBoundaries(
			container,
			rule: Rule,

			// An anchoring keeper reads the runs without its own claims, and the run rules report from a read with them, so both stand.
			readRunBoundaries: () =>
				BlankLineKeptPairs.ReadRunBoundaries(container, Rule).Union(BlankLineKeptPairs.ReadReportedRunBoundaries(container, Rule)),

			connectedBoundaries: BlankLineCheckedStanzas.ReadConnectedBindingGaps(container, Rule)
		);
	}
}
