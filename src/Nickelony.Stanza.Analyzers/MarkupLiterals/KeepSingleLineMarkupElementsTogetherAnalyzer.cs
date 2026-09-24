using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.MarkupLiterals;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class KeepSingleLineMarkupElementsTogetherAnalyzer() : StanzaRuleAnalyzer(StanzaRules.KeepSingleLineMarkupElementsTogether)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context) => MarkupLiteralAnalysis.ReportRemovals(context, Rule);
}
