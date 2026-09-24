using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Bodies;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class BreakExpressionOffSignatureLineAnalyzer() : StanzaRuleAnalyzer(StanzaRules.BreakExpressionOffSignatureLine)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context) =>
		BodyArrows.ReportExpressionBreak(context, StanzaOptions.From(context.Options, context.Node.SyntaxTree), Rule);
}
