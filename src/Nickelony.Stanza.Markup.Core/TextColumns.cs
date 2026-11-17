using System;

namespace Nickelony.Stanza.Markup.Core;

// Ported from CharacterSizeCalculator.CalculateWidth of CSharpier (https://github.com/belav/csharpier, MIT licence), as the pinned 1.3.0 counts.
/// <summary>Counts columns as CSharpier does, so no rule disagrees with the formatter on where a line ends.</summary>
/// <remarks>
/// A code unit takes one column, or two when <see cref="TextWidthTable"/> lists it, and a surrogate pair takes two.
/// </remarks>
internal static class TextColumns
{
	public const int DefaultTabColumns = 4;

	/// <summary>The cap on columns, a quarter of <see cref="int.MaxValue"/>, so a sum of columns cannot wrap.</summary>
	public const int MaxColumns = int.MaxValue / 4;

	/// <summary>The most columns a code unit takes, but for an indentation tab, which reaches its tab stop.</summary>
	public const int MaxColumnsPerCharacter = 2;

	/// <summary>The first code unit that may need the table, which is the first of its ranges.</summary>
	private const char FirstTabledUnit = (char)0x1100;

	/// <summary>The columns of a text, where only a tab in the leading blanks reaches a tab stop.</summary>
	/// <remarks>
	/// Any other tab counts one column, and <paramref name="afterContent"/> says that text stands before this one.
	/// </remarks>
	public static int GetColumns(string text, int tabColumns, int startColumn = 0, bool afterContent = false)
	{
		long columns = startColumn;
		bool inIndentation = !afterContent;

		foreach (char character in text)
		{
			columns += GetColumnsOf(character, tabColumns, column: columns, inIndentation);
			inIndentation = inIndentation && character is ' ' or '\t';
		}

		return (int)Math.Min(columns - startColumn, MaxColumns);
	}

	/// <summary>Whether a text holds a character other than a space or a tab.</summary>
	public static bool HasContent(string text)
	{
		foreach (char character in text)
		{
			if (character is not (' ' or '\t'))
				return true;
		}

		return false;
	}

	/// <summary>Finds the offset of the first character that ends past a column, or -1 when none does.</summary>
	/// <remarks>
	/// The text starts a line, and a surrogate pair that ends past the column is found at its high surrogate.
	/// </remarks>
	public static int FindOffsetPastColumn(string text, int tabColumns, int maxColumns)
	{
		long columns = 0;
		bool inIndentation = true;

		for (int offset = 0; offset < text.Length; offset++)
		{
			columns += GetColumnsOf(character: text[offset], tabColumns, column: columns, inIndentation);
			inIndentation = inIndentation && text[offset] is ' ' or '\t';

			if (columns > maxColumns)
				return offset > 0 && char.IsLowSurrogate(text[offset]) && char.IsHighSurrogate(text[offset - 1]) ? offset - 1 : offset;
		}

		return -1;
	}

	private static long GetColumnsOf(char character, int tabColumns, long column, bool inIndentation)
	{
		if (character == '\t' && inIndentation)
			return tabColumns > 0 ? tabColumns - (column % tabColumns) : tabColumns;

		return character >= FirstTabledUnit && IsWide(character) ? 2 : 1;
	}

	/// <summary>Whether a code unit lies in one of the sorted first-and-last pairs of the wide ranges.</summary>
	private static bool IsWide(char character)
	{
		int[] ranges = TextWidthTable.WideRanges;

		int low = 0;
		int high = (ranges.Length / 2) - 1;

		while (low <= high)
		{
			int middle = low + ((high - low) / 2);

			if (character < ranges[2 * middle])
				high = middle - 1;
			else if (character > ranges[(2 * middle) + 1])
				low = middle + 1;
			else
				return true;
		}

		return false;
	}
}
