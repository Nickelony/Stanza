using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.MarkupLiterals;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SeparateCommentedMarkupElementAnalyzer() : StanzaRuleAnalyzer(StanzaRules.SeparateCommentedMarkupElement)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context) => MarkupLiteralAnalysis.ReportCommentedSeparations(context, Rule);
}
