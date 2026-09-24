using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Documentation;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class OrderElementsOfDocumentationCommentAnalyzer() : StanzaTreeAnalyzer(DiagnosticDescriptors.OrderElementsOfDocumentationComment)
{
	protected override void Analyze(SyntaxTreeAnalysisContext context) => DocCommentAnalysis.ReportElementOrder(context, Descriptor);
}
