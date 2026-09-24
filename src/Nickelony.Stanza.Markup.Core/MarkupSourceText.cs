using System;
using System.Collections.Generic;
using System.Text;

namespace Nickelony.Stanza.Markup.Core;

/// <summary>The text of a markup file with its line table, whose line indices count from zero.</summary>
internal sealed class MarkupSourceText
{
	private readonly int[] _lineStarts;

	/// <summary>Only <see cref="Build"/> makes one, so the line table always matches the text.</summary>
	private MarkupSourceText(string text, int[] lineStarts)
	{
		Text = text;
		_lineStarts = lineStarts;
	}

	public string Text { get; }

	public int Length => Text.Length;
	public int LineCount => _lineStarts.Length;

	public char this[int offset] => Text[offset];

	/// <summary>Builds the line index, breaking a line only at <c>\r</c>, <c>\n</c> and <c>\r\n</c>.</summary>
	public static MarkupSourceText Build(string text)
	{
		List<int> starts = [0];

		// U+0085, U+2028 and U+2029 do not break a line, so the line numbers agree with XmlReader's.
		for (int offset = 0; offset < text.Length; offset++)
		{
			char current = text[offset];

			if (current == '\r')
			{
				if (offset + 1 < text.Length && text[offset + 1] == '\n')
					offset++;

				starts.Add(offset + 1);
			}
			else if (current == '\n')
			{
				starts.Add(offset + 1);
			}
		}

		return new(text, [.. starts]);
	}

	public MarkupLine GetLine(int lineIndex)
	{
		int start = _lineStarts[lineIndex];
		int endIncludingLineBreak = GetEndIncludingLineBreak(lineIndex);

		return new(LineIndex: lineIndex, Start: start, End: GetEnd(start, endIncludingLineBreak), EndIncludingLineBreak: endIncludingLineBreak);
	}

	public string GetLineEnding(int lineIndex)
	{
		MarkupLine line = GetLine(lineIndex);
		return line.HasLineBreak ? Text.Substring(line.End, line.EndIncludingLineBreak - line.End) : DetectLineEnding();
	}

	private string DetectLineEnding()
	{
		for (int index = 0; index < LineCount; index++)
		{
			MarkupLine line = GetLine(index);

			if (line.HasLineBreak)
				return Text.Substring(line.End, line.EndIncludingLineBreak - line.End);
		}

		return "\n";
	}

	private int GetEndIncludingLineBreak(int lineIndex) => lineIndex + 1 < _lineStarts.Length ? _lineStarts[lineIndex + 1] : Length;

	public MarkupLine GetLineFromOffset(int offset)
	{
		int index = Array.BinarySearch(_lineStarts, offset);

		if (index < 0)
			index = ~index - 1;

		if (index < 0)
			index = 0;

		return GetLine(index);
	}

	public bool IsBlank(MarkupSpan span)
	{
		for (int offset = span.Start; offset < span.End; offset++)
		{
			if (!MarkupWhitespace.IsXmlWhitespace(Text[offset]))
				return false;
		}

		return true;
	}

	public string Apply(IReadOnlyList<MarkupChange> changes)
	{
		if (changes.Count == 0)
			return Text;

		StringBuilder builder = new(Length);
		int cursor = 0;

		foreach (MarkupChange change in changes)
		{
			builder.Append(Text, cursor, change.Span.Start - cursor);
			builder.Append(change.NewText);

			cursor = change.Span.End;
		}

		builder.Append(Text, cursor, Length - cursor);

		return builder.ToString();
	}

	private int GetEnd(int start, int endIncludingLineBreak) => endIncludingLineBreak - GetBreakLength(start, endIncludingLineBreak);

	private int GetBreakLength(int start, int endIncludingLineBreak)
	{
		if (endIncludingLineBreak <= start)
			return 0;

		char last = Text[endIncludingLineBreak - 1];

		if (last == '\r')
			return 1;

		if (last != '\n')
			return 0;

		bool startsWithACarriageReturn = endIncludingLineBreak - 1 > start && Text[endIncludingLineBreak - 2] == '\r';
		return startsWithACarriageReturn ? 2 : 1;
	}
}
