using System.Collections.Generic;

namespace Nickelony.Stanza.Markup.Core.DocComments;

internal sealed class DocText(string text, IReadOnlyList<MarkupSpan>? breaks = null) : DocNode
{
	public string Text { get; } = text;
	public IReadOnlyList<MarkupSpan>? Breaks { get; } = breaks;

	/// <summary>The file spans the text was read from, in order, when its reader knows them.</summary>
	public IReadOnlyList<DocTextPiece>? Pieces { get; init; }

	/// <summary>The file offset of a text offset, or <see langword="null"/> when the text holds no pieces.</summary>
	public int? FindFileOffset(int textOffset, bool startsSpan)
	{
		if (Pieces is not { Count: > 0 } pieces)
			return null;

		// A text offset between two pieces belongs to the piece after it when it starts a span, and to the piece before it otherwise.
		// A span then never takes the line marker after a break, and a removal ends where the break does.
		for (int index = pieces.Count - 1; index >= 0; index--)
		{
			DocTextPiece piece = pieces[index];

			if (index > 0 && (startsSpan ? textOffset < piece.Offset : textOffset <= piece.Offset))
				continue;

			if (piece.IsBreak)
				return textOffset <= piece.Offset ? piece.Span.Start : piece.Span.Start + piece.Span.Length;

			return piece.Span.Start + (textOffset - piece.Offset);
		}

		return null;
	}
}
