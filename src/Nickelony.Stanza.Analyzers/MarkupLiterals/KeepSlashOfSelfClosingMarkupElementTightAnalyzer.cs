using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.MarkupLiterals;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class KeepSlashOfSelfClosingMarkupElementTightAnalyzer() : StanzaRuleAnalyzer(StanzaRules.KeepSlashOfSelfClosingMarkupElementTight)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context) => MarkupLiteralAnalysis.ReportSlashGaps(context, Rule, spaced: false);
}
