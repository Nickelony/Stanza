using System.Collections.Generic;

namespace Nickelony.Stanza.Analyzers.MarkupLiterals;

/// <summary>Maps each line of a markup literal's value to its line in the file, read once per literal.</summary>
internal sealed class MarkupValueLines
{
	private readonly string _text;

	/// <summary>For each value line, the file line it starts on and where it starts in the value.</summary>
	private readonly List<(int FileLine, int Start)> _lines = [(0, 0)];

	private readonly int _fileLineAtEnd;

	public MarkupValueLines(string text)
	{
		_text = text;

		int line = 0;

		for (int offset = 0; offset < text.Length; offset++)
		{
			char current = text[offset];

			if (current is '\r' or '\n')
			{
				if (current == '\r' && offset + 1 < text.Length && text[offset + 1] == '\n')
					offset++;

				line++;
				_lines.Add((line, offset + 1));
			}
			else if (IsExtraLineBreak(current))
			{
				line++;
			}
		}

		_fileLineAtEnd = line;
	}

	public int GetFileLineIndex(int lineIndex) => GetFileLineAndOffset(lineIndex, offsetInLine: 0, indentationLength: 0).FileLineIndex;

	public (int FileLineIndex, int FileLineOffset) GetFileLineAndOffset(int lineIndex, int offsetInLine, int indentationLength)
	{
		// A line past the value's last stands at its end.
		(int line, int start) = lineIndex < _lines.Count ? _lines[lineIndex] : (_fileLineAtEnd, _text.Length);

		int fileLineOffset = indentationLength;

		for (int offset = 0; offset < offsetInLine && start + offset < _text.Length; offset++)
		{
			if (IsExtraLineBreak(_text[start + offset]))
			{
				line++;
				fileLineOffset = indentationLength;
				continue;
			}

			fileLineOffset++;
		}

		return (line, fileLineOffset);
	}

	/// <summary>Whether a character is a line break that Roslyn counts but the engine's XML rule does not.</summary>
	private static bool IsExtraLineBreak(char character) => character is '\u0085' or '\u2028' or '\u2029';
}
