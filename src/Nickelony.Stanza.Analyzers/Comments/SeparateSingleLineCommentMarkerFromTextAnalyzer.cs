using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Comments;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SeparateSingleLineCommentMarkerFromTextAnalyzer()
	: StanzaTreeAnalyzer(DiagnosticDescriptors.SeparateSingleLineCommentMarkerFromText)
{
	protected override void Analyze(SyntaxTreeAnalysisContext context) => CommentAnalysis.ReportSingleLineCommentGaps(context, Descriptor);
}
