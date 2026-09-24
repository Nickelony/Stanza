using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Comments;

/// <summary>Creates the space a comment gains and finds the comment trivia a diagnostic reports in.</summary>
internal static class CommentEdits
{
	/// <summary>Writes the space a single-line comment gains at the anchor, or <see langword="null"/>.</summary>
	public static TextChange? CreateSingleLineMarkerSpace(SyntaxTrivia trivia, TextSpan anchor) =>
		CreateGapSpace(CommentGaps.FindSingleLineCommentGaps(trivia), anchor);

	/// <summary>Writes the space a delimited comment gains at the anchor, or <see langword="null"/>.</summary>
	public static TextChange? CreateDelimitedMarkerSpace(SyntaxTrivia trivia, TextSpan anchor) =>
		CreateGapSpace(CommentGaps.FindDelimitedCommentGaps(trivia), anchor);

	/// <summary>Writes the space before a trailing comment, when its gap starts at the position.</summary>
	public static TextChange? CreateTrailingSpace(SyntaxTrivia trivia, int position) =>
		CommentGaps.FindTrailingCommentGap(trivia) is { } gap && gap.Span.Start == position ? new TextChange(gap.Span, " ") : null;

	/// <summary>Finds the comment trivia whose span holds the position, or <c>default</c> when none does.</summary>
	/// <remarks>
	/// Only a documentation comment counts unless <paramref name="documentationOnly"/> is <see langword="false"/>.
	/// </remarks>
	public static SyntaxTrivia FindCommentTrivia(SyntaxNode root, int position, bool documentationOnly = true)
	{
		SyntaxTrivia trivia = FindCommentTrivia(root.FindToken(position), position, documentationOnly);

		// A comment on a directive line is trivia of a token inside the directive,
		// and a documentation comment never stands on one.
		return trivia.RawKind != 0 || documentationOnly
			? trivia
			: FindCommentTrivia(root.FindToken(position, findInsideTrivia: true), position, documentationOnly);
	}

	private static TextChange? CreateGapSpace(IReadOnlyList<CommentGap> gaps, TextSpan anchor)
	{
		foreach (CommentGap gap in gaps)
		{
			if (gap.Anchor == anchor)
				return new TextChange(gap.Span, " ");
		}

		return null;
	}

	private static SyntaxTrivia FindCommentTrivia(SyntaxToken token, int position, bool documentationOnly)
	{
		SyntaxTrivia leading = FindCommentTrivia(token.LeadingTrivia, position, documentationOnly);
		return leading.RawKind != 0 ? leading : FindCommentTrivia(token.TrailingTrivia, position, documentationOnly);
	}

	private static SyntaxTrivia FindCommentTrivia(SyntaxTriviaList trivia, int position, bool documentationOnly)
	{
		foreach (SyntaxTrivia item in trivia)
		{
			if (documentationOnly ? item.GetStructure() is not DocumentationCommentTriviaSyntax : !StanzaTrivia.IsComment(item))
				continue;

			// A token can carry several comments of the kind, such as a stray one before the real one,
			// so the span tells them apart rather than the token.
			if (item.FullSpan.Contains(position))
				return item;
		}

		return default;
	}
}
