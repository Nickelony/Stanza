using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Documentation;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class MarkDocumentationParagraphsAnalyzer() : StanzaTreeAnalyzer(DiagnosticDescriptors.MarkDocumentationParagraphs)
{
	protected override void Analyze(SyntaxTreeAnalysisContext context) =>
		DocCommentAnalysis.ReportCommentLayout(context, descriptor: Descriptor, markParagraphs: true);
}
