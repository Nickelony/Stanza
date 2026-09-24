using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Documentation;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SeparateSlashOfSelfClosingTagAnalyzer() : StanzaCompilationTreeAnalyzer(DiagnosticDescriptors.SeparateSlashOfSelfClosingTag)
{
	protected override void Analyze(SyntaxTreeAnalysisContext context, CompilationOptions compilationOptions) =>
		DocCommentAnalysis.ReportSelfClosingSlashGaps(context, compilationOptions, descriptor: Descriptor, spaced: true);
}
