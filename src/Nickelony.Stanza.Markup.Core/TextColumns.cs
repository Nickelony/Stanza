using System;

namespace Nickelony.Stanza.Markup.Core;

internal static class TextColumns
{
	public const int DefaultTabColumns = 4;

	/// <summary>The cap on columns, a quarter of <see cref="int.MaxValue"/>, so a sum of columns cannot wrap.</summary>
	public const int MaxColumns = int.MaxValue / 4;

	/// <summary>The most columns a code unit takes, a tab apart, which takes up to its tab stop.</summary>
	public const int MaxColumnsPerCharacter = 2;

	/// <summary>The first code unit that may need the table, U+00A9, which is the first emoji variation base.</summary>
	private const char FirstTabledUnit = (char)0xA9;

	/// <summary>The code point of the emoji variation selector, which turns a base of one column into two.</summary>
	private const int EmojiVariationSelector = 0xFE0F;

	/// <summary>A character counts the columns a terminal shows: wide two, combining none, others one.</summary>
	/// <remarks>
	/// The widths follow <see cref="TextWidthTable"/>, and a tab reaches the next tab stop from the start column.
	/// </remarks>
	public static int GetColumns(string text, int tabColumns, int startColumn = 0)
	{
		long columns = Measure(text, tabColumns, startColumn, limit: long.MaxValue, offsetPast: out _);
		return (int)Math.Min(columns - startColumn, MaxColumns);
	}

	/// <summary>Finds the offset of the first character that ends past a column, or -1 when none does.</summary>
	/// <remarks>
	/// The text starts at column 0, and a surrogate pair or a base with its variation selector is one character.
	/// </remarks>
	public static int FindOffsetPastColumn(string text, int tabColumns, int maxColumns)
	{
		Measure(text, tabColumns, startColumn: 0, limit: maxColumns, out int offsetPast);
		return offsetPast;
	}

	private static long Measure(string text, int tabColumns, long startColumn, long limit, out int offsetPast)
	{
		long columns = startColumn;

		int offset = 0;
		int previousStart = -1;

		bool previousWidens = false;

		offsetPast = -1;

		while (offset < text.Length)
		{
			char character = text[offset];
			int start = offset;

			if (character is < FirstTabledUnit and not '\t')
			{
				columns++;
				offset++;
				previousWidens = false;
			}
			else if (character == '\t')
			{
				columns += tabColumns > 0 ? tabColumns - (columns % tabColumns) : tabColumns;
				offset++;
				previousWidens = false;
			}
			else
			{
				int codePoint = character;

				if (char.IsHighSurrogate(character) && offset + 1 < text.Length && char.IsLowSurrogate(text[offset + 1]))
				{
					codePoint = char.ConvertToUtf32(character, text[offset + 1]);
					offset++;
				}

				offset++;

				if (codePoint == EmojiVariationSelector && previousWidens)
				{
					// The selector makes the base before it wide, so the character that ends past a column is that base.
					columns++;
					start = previousStart;
					previousWidens = false;
				}
				else
				{
					int width = GetWidth(codePoint);

					columns += width;
					previousStart = start;
					previousWidens = width == 1 && IsEmojiVariationBase(codePoint);
				}
			}

			if (columns > limit)
			{
				offsetPast = start;
				return columns;
			}
		}

		return columns;
	}

	private static int GetWidth(int codePoint)
	{
		if (codePoint < 0x300)
			return 1;

		if (Contains(TextWidthTable.ZeroRanges, codePoint))
			return 0;

		return Contains(TextWidthTable.WideRanges, codePoint) ? 2 : 1;
	}

	private static bool IsEmojiVariationBase(int codePoint) => Array.BinarySearch(TextWidthTable.EmojiVariationBases, codePoint) >= 0;

	/// <summary>Whether a code point lies in one of the sorted first-and-last pairs.</summary>
	private static bool Contains(int[] ranges, int codePoint)
	{
		int low = 0;
		int high = (ranges.Length / 2) - 1;

		while (low <= high)
		{
			int middle = low + ((high - low) / 2);

			if (codePoint < ranges[2 * middle])
				high = middle - 1;
			else if (codePoint > ranges[(2 * middle) + 1])
				low = middle + 1;
			else
				return true;
		}

		return false;
	}
}
