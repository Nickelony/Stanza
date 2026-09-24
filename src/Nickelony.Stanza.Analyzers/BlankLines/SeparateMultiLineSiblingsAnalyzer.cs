using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Base of a rule that wants a blank line on each side of a multi-line sibling.</summary>
internal abstract class SeparateMultiLineSiblingsAnalyzer(StanzaRule rule) : StanzaRuleAnalyzer(rule)
{
	protected sealed override void Analyze(SyntaxNodeAnalysisContext context) =>
		BlankLineAnalysis.ReportMultiLineSiblingGaps(context, StanzaSyntax.ReadSiblings(context.Node), Rule);
}
