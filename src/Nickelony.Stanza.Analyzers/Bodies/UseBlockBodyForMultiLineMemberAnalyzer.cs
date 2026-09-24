using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Bodies;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class UseBlockBodyForMultiLineMemberAnalyzer()
	: StanzaNodeAnalyzer(DiagnosticDescriptors.UseBlockBodyForMultiLineMember, [SyntaxKind.ArrowExpressionClause])
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		BodyConversion.ReportMultiLineBody(
			context,
			options: StanzaOptions.From(context.Options, context.Node.SyntaxTree),
			descriptor: Descriptor,
			valueShaped: false
		);
	}
}
