using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.MarkupLiterals;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class WriteEmptyMarkupElementAsSelfClosingTagAnalyzer() : StanzaRuleAnalyzer(StanzaRules.WriteEmptyMarkupElementAsSelfClosingTag)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context) => MarkupLiteralAnalysis.ReportEmptyElements(context, Rule);
}
