using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Base of a rule that removes the blank line beside a multi-line sibling.</summary>
internal abstract class RemovableMultiLineGapAnalyzer(StanzaRule rule) : StanzaRuleAnalyzer(rule)
{
	protected sealed override void Analyze(SyntaxNodeAnalysisContext context) =>
		RemovableBlankLineAnalysis.ReportMultiLineBoundaries(context, StanzaSyntax.ReadSiblings(context.Node), Rule);
}
