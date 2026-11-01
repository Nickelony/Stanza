using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Documentation;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class CapitalizeDocumentationTextAnalyzer() : StanzaTreeAnalyzer(DiagnosticDescriptors.CapitalizeDocumentationText)
{
	protected override void Analyze(SyntaxTreeAnalysisContext context) => DocCommentAnalysis.ReportMissingCapitals(context, Descriptor);
}
