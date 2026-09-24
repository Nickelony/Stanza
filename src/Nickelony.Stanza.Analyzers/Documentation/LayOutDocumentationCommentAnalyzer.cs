using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Documentation;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class LayOutDocumentationCommentAnalyzer() : StanzaTreeAnalyzer(DiagnosticDescriptors.LayOutDocumentationComment)
{
	protected override void Analyze(SyntaxTreeAnalysisContext context) =>
		DocCommentAnalysis.ReportCommentLayout(context, descriptor: Descriptor, markParagraphs: false);
}
