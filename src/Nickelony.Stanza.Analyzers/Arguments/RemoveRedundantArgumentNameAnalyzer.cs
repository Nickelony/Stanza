using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Arguments;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class RemoveRedundantArgumentNameAnalyzer()
	: StanzaNodeAnalyzer(DiagnosticDescriptors.RemoveRedundantArgumentName, ArgumentAnalysis.CallKinds)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		// A call with no argument list has no name to remove, so options are read only after that check.
		if (ArgumentBinding.FindArgumentList(context.Node) is not { } argumentList)
			return;

		ArgumentAnalysis.ReportRedundantNames(
			context,
			argumentList,
			options: StanzaOptions.From(context.Options, context.Node.SyntaxTree),
			descriptor: Descriptor
		);
	}
}
