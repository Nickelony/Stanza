using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Bodies;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class UseExpressionBodyForSingleLineMemberAnalyzer() : StanzaRuleAnalyzer(StanzaRules.UseExpressionBodyForSingleLineMember)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		BodyConversion.ReportSingleLineBody(
			context,
			options: StanzaOptions.From(context.Options, context.Node.SyntaxTree),
			rule: Rule,
			valueShaped: false
		);
	}
}
