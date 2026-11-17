using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Bodies;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class UseBlockBodyForMultiLineValueMemberAnalyzer()
	: StanzaRuleAnalyzer(StanzaRules.UseBlockBodyForMultiLineValueMember)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		BodyConversion.ReportMultiLineBody(
			context,
			options: StanzaOptions.From(context.Options, context.Node.SyntaxTree),
			rule: Rule,
			valueShaped: true
		);
	}
}
