using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Documentation;

/// <summary>Reads where a documentation comment stands among the lines of its source.</summary>
internal static class DocCommentLines
{
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
		end - start >= 3 && sourceText[start] == '/' && sourceText[start + 1] == '/' && sourceText[start + 2] == '/' ? 3 : 0;

	private static int GetBlockMarkerLength(SourceText sourceText, int start, int end)
	{
		if (end - start >= 3 && sourceText[start] == '/' && sourceText[start + 1] == '*' && sourceText[start + 2] == '*')
			return 3;

		return end - start >= 1 && sourceText[start] == '*' ? 1 : 0;
	}
}
