using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Documentation;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class MarkLanguageKeywordsAnalyzer() : StanzaCompilationTreeAnalyzer(DiagnosticDescriptors.MarkLanguageKeywords)
{
	protected override void Analyze(SyntaxTreeAnalysisContext context, CompilationOptions compilationOptions) =>
		DocCommentAnalysis.ReportKeywords(context, compilationOptions, descriptor: Descriptor);
}
