using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class GatherRunOfLooseStatementsAnalyzer() : StanzaRuleAnalyzer(StanzaRules.GatherRunOfLooseStatements)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		var container = ContainerContext.From(context);
		RemovableBlankLineAnalysis.ReportRuns(container, runs: BlankLineLooseRuns.ReadRuns(container, rule: Rule, judgesMembers: false), rule: Rule);
	}
}
