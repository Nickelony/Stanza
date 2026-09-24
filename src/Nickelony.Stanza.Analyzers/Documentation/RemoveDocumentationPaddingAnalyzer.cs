using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Documentation;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class RemoveDocumentationPaddingAnalyzer() : StanzaCompilationTreeAnalyzer(DiagnosticDescriptors.RemoveDocumentationPadding)
{
	protected override void Analyze(SyntaxTreeAnalysisContext context, CompilationOptions compilationOptions) =>
		DocCommentAnalysis.ReportPadding(context, compilationOptions, descriptor: Descriptor);
}
