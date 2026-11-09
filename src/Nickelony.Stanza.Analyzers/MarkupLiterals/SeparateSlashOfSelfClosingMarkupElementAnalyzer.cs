using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.MarkupLiterals;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SeparateSlashOfSelfClosingMarkupElementAnalyzer() : StanzaRuleAnalyzer(StanzaRules.SeparateSlashOfSelfClosingMarkupElement)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context) => MarkupLiteralAnalysis.ReportSlashGaps(context, Rule, spaced: true);
}
