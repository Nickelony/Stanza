using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Documentation;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class CollapseWhitespaceInsideTagAnalyzer() : StanzaTreeAnalyzer(DiagnosticDescriptors.CollapseWhitespaceInsideTag)
{
	protected override void Analyze(SyntaxTreeAnalysisContext context) => DocCommentAnalysis.ReportTagWhitespace(context, Descriptor);
}
