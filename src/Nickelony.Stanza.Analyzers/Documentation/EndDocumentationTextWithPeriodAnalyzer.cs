using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Documentation;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class EndDocumentationTextWithPeriodAnalyzer() : StanzaTreeAnalyzer(DiagnosticDescriptors.EndDocumentationTextWithPeriod)
{
	protected override void Analyze(SyntaxTreeAnalysisContext context) => DocCommentAnalysis.ReportMissingPeriods(context, Descriptor);
}
