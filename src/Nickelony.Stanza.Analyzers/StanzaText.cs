using System;
using Microsoft.CodeAnalysis.Text;
using Nickelony.Stanza.Markup.Core;

namespace Nickelony.Stanza.Analyzers;

/// <summary>Reads the text of a source file: its indentation, line endings, columns and blank lines.</summary>
internal static class StanzaText
{
	public const int DefaultTabColumns = MarkupColumns.DefaultTabColumns;

	/// <summary>The columns of one indent step when neither <c>indent_size</c> nor <c>tab_width</c> is set.</summary>
	public const int DefaultIndentStepColumns = 4;

	/// <summary>The columns the span takes in its line, where each tab reaches the next tab stop.</summary>
	public static int GetColumns(SourceText sourceText, TextSpan span, int tabColumns)
	{
		TextLine line = sourceText.Lines.GetLineFromPosition(span.Start);
		string before = sourceText.ToString(TextSpan.FromBounds(line.Start, span.Start));

		return MarkupColumns.GetColumns(sourceText.ToString(span), tabColumns, startColumn: MarkupColumns.GetColumns(before, tabColumns));
	}

	public static string GetIndentation(SourceText sourceText, int position)
	{
		TextLine line = sourceText.Lines.GetLineFromPosition(position);
		return sourceText.ToString(TextSpan.FromBounds(line.Start, line.Start + GetIndentationLength(sourceText, line)));
	}

	/// <summary>Whether the nearest indented line starts with a tab; a file with none reads as spaces.</summary>
	private static bool IndentsWithTabs(SourceText sourceText, int lineIndex)
	{
		for (int distance = 1; lineIndex - distance >= 0 || lineIndex + distance < sourceText.Lines.Count; distance++)
		{
			foreach (int neighbor in new[] { lineIndex + distance, lineIndex - distance })
			{
				if (neighbor < 0 || neighbor >= sourceText.Lines.Count)
					continue;

				TextLine line = sourceText.Lines[neighbor];

				if (GetIndentationLength(sourceText, line) > 0)
					return sourceText[line.Start] == '\t';
			}
		}

		return false;
	}

	private static int GetIndentationLength(SourceText sourceText, TextLine line)
	{
		int length = 0;

		while (line.Start + length < line.End && IsIndentationCharacter(sourceText[line.Start + length]))
			length++;

		return length;
	}

	public static int GetIndentationColumns(SourceText sourceText, TextLine line, int tabColumns)
	{
		int length = GetIndentationLength(sourceText, line);
		return GetColumns(sourceText, TextSpan.FromBounds(line.Start, line.Start + length), tabColumns);
	}

	public static int GetIndentationColumns(SourceText sourceText, int position, int tabColumns) =>
		GetIndentationColumns(sourceText, sourceText.Lines.GetLineFromPosition(position), tabColumns);

	public static bool IsIndentationCharacter(char character) => character is ' ' or '\t';

	/// <summary>The line break to write in front of a line, which is the one that ends the line above it.</summary>
	private static string GetLineEndingAbove(SourceText sourceText, int lineIndex) => ReadPreferredLineEnding(sourceText, Math.Max(lineIndex - 1, 0));

	/// <summary>Inserts a blank line in front of a line, with the line break the line above it ends with.</summary>
	public static TextChange CreateBlankLineAt(SourceText sourceText, int lineIndex) =>
		new(new TextSpan(sourceText.Lines[lineIndex].Start, 0), GetLineEndingAbove(sourceText, lineIndex));

	public static string ReadPreferredLineEnding(SourceText sourceText, int lineIndex)
	{
		// The break is the one of this line, or of the nearest earlier line that has one;
		// `\n` stands in when none does.
		// A Unicode separator such as U+2028 ends a line too, but a fix that wrote it would make lines no editor shows.
		for (int index = Math.Min(lineIndex, sourceText.Lines.Count - 1); index >= 0; index--)
		{
			TextLine line = sourceText.Lines[index];

			if (line.EndIncludingLineBreak == line.End)
				continue;

			string lineBreak = sourceText.ToString(TextSpan.FromBounds(line.End, line.EndIncludingLineBreak));

			if (lineBreak is "\r\n" or "\n" or "\r")
				return lineBreak;
		}

		return "\n";
	}

	/// <summary>
	/// Whether a span holds only <see cref="char.IsWhiteSpace(char)"/> characters, line breaks among them.
	/// </summary>
	public static bool IsWhitespaceOnly(SourceText sourceText, TextSpan span) => SkipWhitespace(sourceText, span.Start, span.End) == span.End;

	/// <summary>
	/// The first position from <paramref name="start"/> that is not whitespace, or <paramref name="end"/>.
	/// </summary>
	public static int SkipWhitespace(SourceText sourceText, int start, int end)
	{
		int position = start;

		while (position < end && char.IsWhiteSpace(sourceText[position]))
			position++;

		return position;
	}

	public static bool IsBlankLine(SourceText sourceText, int lineIndex)
	{
		TextLine textLine = sourceText.Lines[lineIndex];
		return IsWhitespaceOnly(sourceText, textLine.Span);
	}

	public static string WithOneIndentStep(SourceText sourceText, int position, string memberIndentation, int indentStepColumns) =>
		memberIndentation + (IndentsWithTabsAt(sourceText, position) ? "\t" : new string(' ', indentStepColumns));

	/// <summary>
	/// A line at column 0 carries no indentation to copy, so the nearest indented line in the file shows the style.
	/// </summary>
	public static bool IndentsWithTabsAt(SourceText sourceText, int position)
	{
		TextLine line = sourceText.Lines.GetLineFromPosition(position);
		int indentationLength = GetIndentationLength(sourceText, line);

		return indentationLength > 0 ? sourceText[line.Start + indentationLength - 1] == '\t' : IndentsWithTabs(sourceText, line.LineNumber);
	}

	public static bool IsExactly(SourceText sourceText, TextSpan span, string text) =>
		string.Equals(sourceText.ToString(span), text, StringComparison.Ordinal);
}
