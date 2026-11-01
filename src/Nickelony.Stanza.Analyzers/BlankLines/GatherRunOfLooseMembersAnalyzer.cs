using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class GatherRunOfLooseMembersAnalyzer() : StanzaRuleAnalyzer(StanzaRules.GatherRunOfLooseMembers)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		var container = ContainerContext.From(context);
		RemovableBlankLineAnalysis.ReportRuns(container, runs: BlankLineLooseRuns.ReadRuns(container, rule: Rule, judgesMembers: true), rule: Rule);
	}
}
