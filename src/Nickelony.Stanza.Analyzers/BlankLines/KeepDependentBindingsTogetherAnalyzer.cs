using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class KeepDependentBindingsTogetherAnalyzer() : StanzaRuleAnalyzer(StanzaRules.KeepDependentBindingsTogether)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		var container = ContainerContext.From(context);
		BlankLineKeptRuns.KeptRuns kept = BlankLineKeptRuns.ReadRuns(container, Rule, judgesMembers: false);
		RemovableBlankLineAnalysis.ReportRuns(container, runs: kept.Runs, rule: Rule, boundaries: kept.Written);
	}
}
