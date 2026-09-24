using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.MarkupLiterals;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SeparateMarkupCommentMarkerFromTextAnalyzer()
	: StanzaNodeAnalyzer(DiagnosticDescriptors.SeparateMarkupCommentMarkerFromText, StanzaKinds.MarkupLiteralKinds)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context) => MarkupLiteralAnalysis.ReportCommentMarkers(context, Descriptor);
}
