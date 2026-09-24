using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.MarkupLiterals;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class CollapseExtraBlankLinesBetweenMarkupElementsAnalyzer()
	: StanzaRuleAnalyzer(StanzaRules.CollapseExtraBlankLinesBetweenMarkupElements)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context) => MarkupLiteralAnalysis.ReportRemovals(context, Rule);
}
