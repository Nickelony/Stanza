using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SeparateInvocationRunsAnalyzer() : StanzaRuleAnalyzer(StanzaRules.SeparateInvocationRuns)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context) =>
		RequiredBlankLineAnalysis.ReportRunBoundaries(context, Rule, BlankLineGrouping.RunFamily.Invocation);
}
