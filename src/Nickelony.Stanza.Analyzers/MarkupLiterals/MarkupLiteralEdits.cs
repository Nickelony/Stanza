using System;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.MarkupLiterals;

/// <summary>Builds the changes the fixes of the markup rules apply.</summary>
internal static class MarkupLiteralEdits
{
	private const string CommentOpener = "<!--";
	private const string CommentCloser = "-->";
	private const string SlashCloser = "/>";
	private const string EmptyElementOpener = "></";
	private const string EmptyElementCloser = ">";

	/// <summary>
	/// Removes the blank lines above a line past the count to keep, or returns <see langword="null"/> when none.
	/// </summary>
	public static TextChange? CreateCapRemoval(SourceText sourceText, int position, int keptBlankLines)
	{
		int anchorLineIndex = sourceText.Lines.GetLineFromPosition(position).LineNumber;
		int first = anchorLineIndex;

		while (first > 0 && StanzaText.IsBlankLine(sourceText, first - 1))
			first--;

		int surplus = anchorLineIndex - first - keptBlankLines;

		return surplus > 0
			? new TextChange(TextSpan.FromBounds(sourceText.Lines[first].Start, sourceText.Lines[first + surplus].Start), string.Empty)
			: null;
	}

	/// <summary>
	/// Writes a blank line above the line at the position, or returns <see langword="null"/> when that line is blank.
	/// </summary>
	public static TextChange? CreateBlankLineInsertion(SourceText sourceText, int position)
	{
		int lineIndex = sourceText.Lines.GetLineFromPosition(position).LineNumber;

		// A blank line in front of a blank line would add a second one, so none is offered there.
		return StanzaText.IsBlankLine(sourceText, lineIndex) ? null : StanzaText.CreateBlankLineAt(sourceText, lineIndex);
	}

	/// <summary>
	/// Writes the gap before a slash, or returns <see langword="null"/> when the slash stands as the shape asks.
	/// </summary>
	public static TextChange? CreateSlashGap(SourceText sourceText, TextSpan slash, bool spaced)
	{
		if (sourceText.ToString(slash) != SlashCloser)
			return null;

		int lineStart = sourceText.Lines.GetLineFromPosition(slash.Start).Start;
		int gapStart = StanzaText.SkipBlanksBackward(sourceText, slash.Start, lineStart);

		string gap = spaced ? " " : string.Empty;

		return sourceText.ToString(TextSpan.FromBounds(gapStart, slash.Start)) == gap
			? null
			: new TextChange(TextSpan.FromBounds(gapStart, slash.Start), gap);
	}

	/// <summary>Joins a bracket and its end tag into a self-closing tag, or returns <see langword="null"/>.</summary>
	/// <remarks>
	/// A gap that reaches the line start is the wrapped layout, so it stays and the tag takes no space.
	/// </remarks>
	public static TextChange? CreateEmptyElement(SourceText sourceText, TextSpan span, bool spaced)
	{
		string tags = sourceText.ToString(span);

		if (!tags.StartsWith(EmptyElementOpener, StringComparison.Ordinal) || !tags.EndsWith(EmptyElementCloser, StringComparison.Ordinal))
			return null;

		int lineStart = sourceText.Lines.GetLineFromPosition(span.Start).Start;
		int gapStart = StanzaText.SkipBlanksBackward(sourceText, span.Start, lineStart);

		return gapStart == lineStart
			? new TextChange(span, SlashCloser)
			: new TextChange(TextSpan.FromBounds(gapStart, span.End), spaced ? " " + SlashCloser : SlashCloser);
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
