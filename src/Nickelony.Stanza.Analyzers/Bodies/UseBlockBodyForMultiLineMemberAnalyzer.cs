using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Bodies;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class UseBlockBodyForMultiLineMemberAnalyzer()
	: StanzaRuleAnalyzer(StanzaRules.UseBlockBodyForMultiLineMember)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		BodyConversion.ReportMultiLineBody(
			context,
			options: StanzaOptions.From(context.Options, context.Node.SyntaxTree),
			rule: Rule,
			valueShaped: false
		);
	}
}
