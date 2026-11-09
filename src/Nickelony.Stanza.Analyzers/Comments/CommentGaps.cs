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
	/// <summary>The length of a comment opener such as <c>//</c> or <c>/*</c>, and of the closer <c>*/</c>.</summary>
	private const int DelimiterLength = 2;

	/// <summary>The slashes from which a line comment holds commented-out code rather than prose.</summary>
	private const int CommentedOutSlashes = 4;

	/// <summary>Finds each marker that touches its text, except in a banner of at least the minimum run.</summary>
	/// <remarks>
	/// A banner is one punctuation mark repeated, such as <c>//-----</c>; a minimum of zero allows none.
	/// </remarks>
	public static List<CommentGap> FindSingleLineCommentGaps(SyntaxTrivia trivia, int minBannerRun)
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
			while (offset < text.Length && StanzaText.IsIndentationCharacter(text[offset]))
				offset++;

			int marker = 0;

			while (offset + marker < text.Length && text[offset + marker] == '/')
				marker++;

			// Four or more slashes open the marker a file comments code out with.
			// The text after them is code rather than prose, so that line is left as it was written.
			// The offset counts from the full span, because `ToFullString` begins at its start.
			int textStart = offset + marker;

			while (offset < text.Length && !SyntaxFacts.IsNewLine(text[offset]))
				offset++;

			if (
				marker is >= DelimiterLength and < CommentedOutSlashes
				&& textStart < offset
				&& !char.IsWhiteSpace(text[textStart])
				&& !IsBanner(text, start: textStart, lineEnd: offset, minRun: minBannerRun)
			)
			{
				gaps.Add(BuildGapAfterMarker(trivia.FullSpan.Start + textStart, markerLength: marker));
			}

			while (offset < text.Length && SyntaxFacts.IsNewLine(text[offset]))
				offset++;
		}

		return gaps;
	}

	/// <summary>Whether the rest of the line, trailing blanks aside, is one punctuation mark repeated.</summary>
	private static bool IsBanner(string text, int start, int lineEnd, int minRun)
	{
		int end = lineEnd;

		while (end > start && StanzaText.IsIndentationCharacter(text[end - 1]))
			end--;

		return IsMarkRun(text, start, end, minRun);
	}

	/// <summary>Whether the span is one punctuation mark or symbol repeated at least the minimum times.</summary>
	private static bool IsMarkRun(string text, int start, int end, int minRun)
	{
		if (minRun <= 0 || end - start < minRun)
			return false;

		char mark = text[start];

		if (!(char.IsPunctuation(mark) || char.IsSymbol(mark)))
			return false;

		for (int offset = start + 1; offset < end; offset++)
		{
			if (text[offset] != mark)
				return false;
		}

		return true;
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

	/// <summary>Finds each marker that touches its text, unless a banner run of the minimum touches it.</summary>
	/// <remarks>
	/// A run is one punctuation mark repeated up to a blank, such as the stars of <c>/***** Options *****/</c>.
	/// </remarks>
	public static List<CommentGap> FindDelimitedCommentGaps(SyntaxTrivia trivia, int minBannerRun)
	{
		if (!trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) || StanzaTrivia.IsDocumentationComment(trivia))
			return [];

		string text = trivia.ToFullString();

		// An unterminated comment runs to the end of the file, so it has no closing marker to set apart.
		if (text.Length < (2 * DelimiterLength) || !text.EndsWith("*/", StringComparison.Ordinal))
			return [];

		// A comment of nothing but stars and whitespace between its markers, such as /***/, has no text to stand apart from either marker.
		if (
			text.Substring(DelimiterLength, Math.Max(text.Length - (2 * DelimiterLength), 0))
				.All(static character => character == '*' || char.IsWhiteSpace(character))
		)
		{
			return [];
		}

		int textStart = DelimiterLength;
		int textEnd = text.Length - DelimiterLength;
		int openingEnd = ReadWordEnd(text, start: textStart, end: textEnd);
		int closingStart = ReadWordStart(text, start: textStart, end: textEnd);

		List<CommentGap> gaps = [];

		if (!char.IsWhiteSpace(text[textStart]) && !IsMarkRun(text, start: textStart, end: openingEnd, minRun: minBannerRun))
			gaps.Add(BuildGapAfterMarker(trivia.FullSpan.Start + textStart, markerLength: DelimiterLength));

		if (!char.IsWhiteSpace(text[textEnd - 1]) && !IsMarkRun(text, start: closingStart, end: textEnd, minRun: minBannerRun))
			gaps.Add(BuildGapBeforeMarker(trivia.FullSpan.Start + textEnd, markerLength: DelimiterLength));

		return gaps;
	}

	/// <summary>Reads where the word that opens at the start ends, at its first blank or at the end.</summary>
	private static int ReadWordEnd(string text, int start, int end)
	{
		int offset = start;

		while (offset < end && !char.IsWhiteSpace(text[offset]))
			offset++;

		return offset;
	}

	/// <summary>Reads where the word that closes at the end starts, after its last blank or at the start.</summary>
	private static int ReadWordStart(string text, int start, int end)
	{
		int offset = end;

		while (offset > start && !char.IsWhiteSpace(text[offset - 1]))
			offset--;

		return offset;
	}

	private static CommentGap BuildGapAfterMarker(int position, int markerLength) =>
		new(new TextSpan(position, 0), new TextSpan(position - markerLength, markerLength));

	private static CommentGap BuildGapBeforeMarker(int position, int markerLength) =>
		new(new TextSpan(position, 0), new TextSpan(position, markerLength));
}
