using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Comments;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SeparateDelimitedCommentMarkerFromTextAnalyzer()
	: StanzaTreeAnalyzer(DiagnosticDescriptors.SeparateDelimitedCommentMarkerFromText)
{
	protected override void Analyze(SyntaxTreeAnalysisContext context) => CommentAnalysis.ReportDelimitedCommentGaps(context, Descriptor);
}
