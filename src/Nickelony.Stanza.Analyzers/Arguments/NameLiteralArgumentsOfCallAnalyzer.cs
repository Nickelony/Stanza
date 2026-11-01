using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Arguments;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class NameLiteralArgumentsOfCallAnalyzer() : StanzaRuleAnalyzer(StanzaRules.NameLiteralArgumentsOfCall)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		// A call with no argument list has no argument to name, so options are read only after that check.
		if (ArgumentBinding.FindArgumentList(context.Node) is not { } argumentList)
			return;

		ArgumentAnalysis.ReportUnnamedLiterals(
			context,
			argumentList,
			options: StanzaOptions.From(context.Options, context.Node.SyntaxTree),
			rule: Rule
		);
	}
}
