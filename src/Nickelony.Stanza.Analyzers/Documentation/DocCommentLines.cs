using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Documentation;

/// <summary>Reads where a documentation comment stands among the lines of its source.</summary>
internal static class DocCommentLines
{
	/// <summary>The length of the <c>///</c> marker of a line and of the <c>/**</c> opener of a block.</summary>
	private const int MarkerLength = 3;

	/// <summary>The text from the start of the comment's line to the comment.</summary>
	public static TextSpan GetLinePrefix(SourceText sourceText, SyntaxTrivia trivia) =>
		TextSpan.FromBounds(sourceText.Lines.GetLineFromPosition(trivia.FullSpan.Start).Start, trivia.FullSpan.Start);

	/// <summary>Finds the indentation before a comment, or <see langword="null"/> when code shares its line.</summary>
	public static TextSpan? FindCommentPrefix(SourceText sourceText, SyntaxTrivia trivia)
	{
		TextSpan prefix = GetLinePrefix(sourceText, trivia);
		return StanzaText.IsWhitespaceOnly(sourceText, prefix) ? prefix : null;
	}

	public static string GetTrailingLineBreak(string text)
	{
		int offset = text.Length;

		while (offset > 0 && text[offset - 1] is '\r' or '\n')
			offset--;

		return text.Substring(offset);
	}

	public static int GetLastLineIndex(SourceText sourceText, TextSpan span) =>
		sourceText.Lines.GetLineFromPosition(span.End > span.Start ? span.End - 1 : span.Start).LineNumber;

	/// <summary>
	/// Whether the line holds a block comment's <c>/**</c>, which a rewrite would replace with a star marker.
	/// </summary>
	public static bool StandsOnTheOpeningLine(DocCommentDelimiters delimiters, int lineIndex, int openingLineIndex) =>
		delimiters.IsBlock && lineIndex == openingLineIndex;

	/// <summary>Finds where a line's text starts, past its marker and one space, or <see langword="null"/>.</summary>
	/// <remarks>
	/// A line has none when it carries no marker, or when its marker is followed by a character that is not a space.
	/// </remarks>
	public static int? FindTextStart(SourceText sourceText, TextLine line, DocCommentDelimiters delimiters)
	{
		int markerStart = line.Start;

		while (markerStart < line.End && char.IsWhiteSpace(sourceText[markerStart]))
			markerStart++;

		int marker = delimiters.IsBlock
			? GetBlockMarkerLength(sourceText, markerStart, line.End)
			: GetLineMarkerLength(sourceText, markerStart, line.End);

		if (marker == 0)
			return null;

		int start = markerStart + marker;

		if (start == line.End)
			return start;

		return sourceText[start] == ' ' ? start + 1 : null;
	}

	public static bool StandsAfterAMarker(SourceText sourceText, TextLine line, int position, DocCommentDelimiters delimiters)
	{
		int markerStart = line.Start;

		while (markerStart < position && char.IsWhiteSpace(sourceText[markerStart]))
			markerStart++;

		int marker = delimiters.IsBlock
			? GetBlockMarkerLength(sourceText, markerStart, position)
			: GetLineMarkerLength(sourceText, markerStart, position);

		// A `/** */` comment may leave out the star of each line, so there the indentation alone stands before the text.
		return (marker > 0 || delimiters.IsBlock) && StanzaText.IsWhitespaceOnly(sourceText, TextSpan.FromBounds(markerStart + marker, position));
	}

	private static int GetLineMarkerLength(SourceText sourceText, int start, int end) =>
		end - start >= MarkerLength && sourceText[start] == '/' && sourceText[start + 1] == '/' && sourceText[start + 2] == '/' ? MarkerLength : 0;

	private static int GetBlockMarkerLength(SourceText sourceText, int start, int end)
	{
		if (end - start >= MarkerLength && sourceText[start] == '/' && sourceText[start + 1] == '*' && sourceText[start + 2] == '*')
			return MarkerLength;

		return end - start >= 1 && sourceText[start] == '*' ? 1 : 0;
	}
}
