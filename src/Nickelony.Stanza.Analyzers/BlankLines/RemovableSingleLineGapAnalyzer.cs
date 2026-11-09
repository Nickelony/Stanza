using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Base of a rule that removes the blank line between single-line siblings.</summary>
internal abstract class RemovableSingleLineGapAnalyzer(StanzaRule rule) : StanzaRuleAnalyzer(rule)
{
	protected sealed override void Analyze(SyntaxNodeAnalysisContext context) =>
		RemovableBlankLineAnalysis.ReportSingleLineBoundaries(context, StanzaSyntax.ReadSiblings(context.Node), Rule);
}
