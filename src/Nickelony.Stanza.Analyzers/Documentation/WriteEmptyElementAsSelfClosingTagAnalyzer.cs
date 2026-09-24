using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Documentation;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class WriteEmptyElementAsSelfClosingTagAnalyzer()
	: StanzaCompilationTreeAnalyzer(DiagnosticDescriptors.WriteEmptyElementAsSelfClosingTag)
{
	protected override void Analyze(SyntaxTreeAnalysisContext context, CompilationOptions compilationOptions) =>
		DocCommentAnalysis.ReportEmptyElements(context, compilationOptions, descriptor: Descriptor);
}
