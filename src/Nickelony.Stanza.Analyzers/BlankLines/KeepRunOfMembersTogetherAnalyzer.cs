using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class KeepRunOfMembersTogetherAnalyzer() : StanzaRuleAnalyzer(StanzaRules.KeepRunOfMembersTogether)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		var container = ContainerContext.From(context);
		BlankLineKeptRuns.KeptRuns kept = BlankLineKeptRuns.ReadRuns(container, rule: Rule, judgesMembers: true);

		RemovableBlankLineAnalysis.ReportRuns(container, runs: kept.Runs, rule: Rule, boundaries: kept.Written);
	}
}
