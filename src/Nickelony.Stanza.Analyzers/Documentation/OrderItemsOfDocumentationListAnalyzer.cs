using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Documentation;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class OrderItemsOfDocumentationListAnalyzer() : StanzaTreeAnalyzer(DiagnosticDescriptors.OrderItemsOfDocumentationList)
{
	protected override void Analyze(SyntaxTreeAnalysisContext context) => DocCommentAnalysis.ReportListItemOrder(context, Descriptor);
}
