using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.MarkupLiterals;

/// <summary>Builds the changes the fixes of the markup rules apply.</summary>
internal static class MarkupLiteralEdits
{
	private const string CommentOpener = "<!--";
	private const string CommentCloser = "-->";

	/// <summary>
	/// Removes the blank lines above a line past the count to keep, or returns <see langword="null"/> when none.
	/// </summary>
	public static TextChange? CreateCapRemoval(SourceText sourceText, int position, int keptBlankLines)
	{
		int anchorLine = sourceText.Lines.GetLineFromPosition(position).LineNumber;
		int first = anchorLine;

		while (first > 0 && StanzaText.IsBlankLine(sourceText, first - 1))
			first--;

		int surplus = anchorLine - first - keptBlankLines;

		return surplus > 0
			? new TextChange(TextSpan.FromBounds(sourceText.Lines[first].Start, sourceText.Lines[first + surplus].Start), string.Empty)
			: null;
	}

	/// <summary>
	/// Writes the space after a comment marker, or returns <see langword="null"/> when the marker has one.
	/// </summary>
	public static TextChange? CreateCommentMarkerSpace(SourceText sourceText, TextSpan marker)
	{
		// The marker comes from the source text: the literal is one token, and the marker is a range inside it.
		(int TextPosition, int InsertPosition)? gap = sourceText.ToString(marker) switch
		{
			CommentOpener => (marker.End, marker.End),
			CommentCloser => (marker.Start - 1, marker.Start),
			_ => null,
		};

		return
			gap is { } found
			&& found.TextPosition >= 0
			&& found.TextPosition < sourceText.Length
			&& !char.IsWhiteSpace(sourceText[found.TextPosition])
			? new TextChange(new TextSpan(found.InsertPosition, 0), " ")
			: null;
	}
}
