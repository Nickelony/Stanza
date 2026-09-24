using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Documentation;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class LayOutItemsOfDocumentationListAnalyzer() : StanzaCompilationTreeAnalyzer(DiagnosticDescriptors.LayOutItemsOfDocumentationList)
{
	protected override void Analyze(SyntaxTreeAnalysisContext context, CompilationOptions compilationOptions) =>
		DocCommentAnalysis.ReportListLayout(context, compilationOptions, descriptor: Descriptor);
}
