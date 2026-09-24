using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Comments;

/// <summary>Finds where a comment lacks the space between its marker and its text, or between it and code.</summary>
internal static class CommentGaps
{
	public static List<CommentGap> FindSingleLineCommentGaps(SyntaxTrivia trivia)
	{
		if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && !trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia))
			return [];

		string text = trivia.ToFullString();
		List<CommentGap> gaps = [];
		int offset = 0;

		// A documentation comment of several lines is one trivia, so each of its lines is judged on its own,
		// and a line ends at every break Roslyn counts, not at a carriage return or a line feed alone.
		while (offset < text.Length)
		{
			while (offset < text.Length && (text[offset] is ' ' or '\t'))
				offset++;

			int marker = 0;

			while (offset + marker < text.Length && text[offset + marker] == '/')
				marker++;

			// Four or more slashes open the marker a file comments code out with.
			// The text after them is code rather than prose, so that line is left as it was written.
			// The offset counts from the full span, because `ToFullString` begins at its start.
			if (marker is >= 2 and < 4 && offset + marker < text.Length && !char.IsWhiteSpace(text[offset + marker]))
				gaps.Add(BuildGapAfterMarker(trivia.FullSpan.Start + offset + marker, markerLength: marker));

			while (offset < text.Length && !SyntaxFacts.IsNewLine(text[offset]))
				offset++;

			while (offset < text.Length && SyntaxFacts.IsNewLine(text[offset]))
				offset++;
		}

		return gaps;
	}

	public static CommentGap? FindTrailingCommentGap(SyntaxTrivia trivia)
	{
		if (!StanzaTrivia.IsComment(trivia) || StanzaTrivia.IsDocumentationComment(trivia))
			return null;

		SyntaxToken token = trivia.Token;

		// The trivia that starts where its own token ends is the first piece of that token's trailing trivia,
		// and that is what a comment trailing code is.
		// A token with no text of its own, such as an invented one or the end of file, has no gap to measure,
		// so the position test would be meaningless for it.
		if (token.IsMissing || token.Span.Length == 0 || trivia.FullSpan.Start != token.Span.End)
			return null;

		// A comment on a directive line trails the directive rather than code, so it is left alone.
		if (token.IsPartOfStructuredTrivia())
			return null;

		return EndsItsLine(trivia) ? new CommentGap(new TextSpan(trivia.FullSpan.Start, 0), trivia.Span) : null;
	}

	/// <summary>
	/// Whether a comment ends its line; the inline remark in <c>Run(/* flag */ true)</c> is no trailing comment.
	/// </summary>
	private static bool EndsItsLine(SyntaxTrivia comment)
	{
		SyntaxTriviaList trailing = comment.Token.TrailingTrivia;

		for (int index = trailing.IndexOf(comment) + 1; index < trailing.Count; index++)
		{
			if (!trailing[index].IsKind(SyntaxKind.WhitespaceTrivia))
				return trailing[index].IsKind(SyntaxKind.EndOfLineTrivia);
		}

		return comment.Token.GetNextToken(includeZeroWidth: true).IsKind(SyntaxKind.EndOfFileToken);
	}

	public static List<CommentGap> FindDelimitedCommentGaps(SyntaxTrivia trivia)
	{
		if (!trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) || StanzaTrivia.IsDocumentationComment(trivia))
			return [];

		string text = trivia.ToFullString();

		// An unterminated comment runs to the end of the file, so it has no closing marker to set apart.
		if (text.Length < 4 || !text.EndsWith("*/", StringComparison.Ordinal))
			return [];

		// A comment of nothing but stars and whitespace between its markers, such as /***/, has no text to stand apart from either marker.
		if (text.Substring(2, Math.Max(text.Length - 4, 0)).All(static character => character == '*' || char.IsWhiteSpace(character)))
			return [];

		List<CommentGap> gaps = [];

		if (!char.IsWhiteSpace(text[2]))
			gaps.Add(BuildGapAfterMarker(trivia.FullSpan.Start + 2, markerLength: 2));

		if (!char.IsWhiteSpace(text[text.Length - 3]))
			gaps.Add(BuildGapBeforeMarker(trivia.FullSpan.Start + text.Length - 2, markerLength: 2));

		return gaps;
	}

	private static CommentGap BuildGapAfterMarker(int position, int markerLength) =>
		new(new TextSpan(position, 0), new TextSpan(position - markerLength, markerLength));

	private static CommentGap BuildGapBeforeMarker(int position, int markerLength) =>
		new(new TextSpan(position, 0), new TextSpan(position, markerLength));
}
