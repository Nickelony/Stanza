using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Documentation;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class KeepDocumentationCommentWithDeclarationAnalyzer()
	: StanzaTreeAnalyzer(DiagnosticDescriptors.KeepDocumentationCommentWithDeclaration)
{
	protected override void Analyze(SyntaxTreeAnalysisContext context) => DocCommentAnalysis.ReportDeclarationGaps(context, Descriptor);
}
