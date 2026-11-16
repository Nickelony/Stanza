using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SeparateYieldRunsAnalyzer() : StanzaRuleAnalyzer(StanzaRules.SeparateYieldRuns)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context) =>
		RequiredBlankLineAnalysis.ReportRunBoundaries(context, Rule, BlankLineGrouping.RunFamily.Yield);
}
