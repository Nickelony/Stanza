using System.Collections.Generic;

namespace Nickelony.Stanza.Markup.Core.DocComments;

internal static class DocTextLines
{
	/// <summary>Yields each run of text and each line feed, a <see langword="null"/> text, with its offset.</summary>
	public static IEnumerable<DocTextLine> Split(string body)
	{
		int offset = 0;

		while (offset < body.Length)
		{
			if (body[offset] == '\n')
			{
				offset++;
				yield return new DocTextLine(Text: null, Offset: offset - 1);

				continue;
			}

			int start = offset;
			int next = start;

			while (next < body.Length && body[next] != '\n')
				next++;

			string piece = body.Substring(offset, next - offset);

			offset = next;

			// Whitespace before a break is a blank line's indentation: the breaks around it still make one.
			if (next < body.Length && piece.Length > 0 && MarkupWhitespace.IsBlank(piece))
				continue;

			yield return new DocTextLine(Text: piece, Offset: start);
		}
	}
}
