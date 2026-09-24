using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.MarkupLiterals;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SeparateMultiLineMarkupElementsAnalyzer() : StanzaRuleAnalyzer(StanzaRules.SeparateMultiLineMarkupElements)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context) => MarkupLiteralAnalysis.ReportSeparations(context, Rule);
}
