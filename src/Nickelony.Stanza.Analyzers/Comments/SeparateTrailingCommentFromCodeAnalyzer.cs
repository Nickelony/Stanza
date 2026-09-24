using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Comments;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SeparateTrailingCommentFromCodeAnalyzer() : StanzaTreeAnalyzer(DiagnosticDescriptors.SeparateTrailingCommentFromCode)
{
	/// <summary>Runs on the whole syntax tree, because comments are trivia, not nodes.</summary>
	protected override void Analyze(SyntaxTreeAnalysisContext context) => CommentAnalysis.ReportTrailingCommentGaps(context, Descriptor);
}
