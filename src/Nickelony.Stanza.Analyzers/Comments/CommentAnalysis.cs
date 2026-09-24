using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Comments;

internal static class CommentAnalysis
{
	public static void ReportSingleLineCommentGaps(SyntaxTreeAnalysisContext context, DiagnosticDescriptor descriptor) =>
		ReportGaps(context, descriptor, CommentGaps.FindSingleLineCommentGaps);

	public static void ReportTrailingCommentGaps(SyntaxTreeAnalysisContext context, DiagnosticDescriptor descriptor) =>
		ReportGaps(context, descriptor, static trivia => CommentGaps.FindTrailingCommentGap(trivia) is { } gap ? [gap] : []);

	public static void ReportDelimitedCommentGaps(SyntaxTreeAnalysisContext context, DiagnosticDescriptor descriptor) =>
		ReportGaps(context, descriptor, CommentGaps.FindDelimitedCommentGaps);

	/// <summary>
	/// Reports each gap with its whole comment as the second location, so the fix reads the comment it changes.
	/// </summary>
	private static void ReportGaps(
		SyntaxTreeAnalysisContext context,
		DiagnosticDescriptor descriptor,
		Func<SyntaxTrivia, IReadOnlyList<CommentGap>> findGaps
	)
	{
		if (!StanzaCommentCache.HasComment(context.Tree, context.CancellationToken))
			return;

		foreach (SyntaxTrivia trivia in StanzaCommentCache.GetComments(context.Tree, context.CancellationToken))
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			foreach (CommentGap gap in findGaps(trivia))
			{
				context.ReportDiagnostic(
					Diagnostic.Create(
						descriptor,
						Location.Create(context.Tree, gap.Anchor),
						additionalLocations: [Location.Create(context.Tree, trivia.FullSpan)]
					)
				);
			}
		}
	}
}
