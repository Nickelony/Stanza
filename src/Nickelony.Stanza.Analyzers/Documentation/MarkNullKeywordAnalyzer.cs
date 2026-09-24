using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Documentation;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class MarkNullKeywordAnalyzer() : StanzaCompilationTreeAnalyzer(DiagnosticDescriptors.MarkNullKeyword)
{
	protected override void Analyze(SyntaxTreeAnalysisContext context, CompilationOptions compilationOptions) =>
		DocCommentAnalysis.ReportNullKeywords(context, compilationOptions, descriptor: Descriptor);
}
