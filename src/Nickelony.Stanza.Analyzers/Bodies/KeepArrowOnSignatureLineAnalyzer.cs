using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Bodies;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class KeepArrowOnSignatureLineAnalyzer() : StanzaRuleAnalyzer(StanzaRules.KeepArrowOnSignatureLine)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		var options = StanzaOptions.From(context.Options, context.Node.SyntaxTree);

		if (!StanzaRuleState.OwnsTheSharedGap(scope: context, options, rule: Rule, position: context.Node.SpanStart))
			return;

		BodyArrows.ReportArrowPlacement(context, options, descriptor: Descriptor, placement: ArrowPlacement.Trailing);
	}
}
