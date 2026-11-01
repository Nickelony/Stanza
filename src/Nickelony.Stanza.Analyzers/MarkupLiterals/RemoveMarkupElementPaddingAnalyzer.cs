using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.MarkupLiterals;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class RemoveMarkupElementPaddingAnalyzer() : StanzaRuleAnalyzer(StanzaRules.RemoveMarkupElementPadding)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context) => MarkupLiteralAnalysis.ReportPadding(context, Rule);
}
