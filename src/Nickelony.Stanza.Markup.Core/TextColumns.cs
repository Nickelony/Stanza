using System;

namespace Nickelony.Stanza.Markup.Core;

internal static class TextColumns
{
	public const int DefaultTabColumns = 4;

	/// <summary>The cap on columns, a quarter of <see cref="int.MaxValue"/>, so a sum of columns cannot wrap.</summary>
	public const int MaxColumns = int.MaxValue / 4;

	/// <summary>A code unit counts one column, a surrogate pair two, and a tab reaches the next tab stop.</summary>
	/// <remarks>
	/// The text starts at <paramref name="startColumn"/>, which sets where its tabs stop.
	/// </remarks>
	public static int GetColumns(string text, int tabColumns, int startColumn = 0)
	{
		long columns = startColumn;

		foreach (char character in text)
			columns = GetColumnsAfter(columns, character, tabColumns);

		return (int)Math.Min(columns - startColumn, MaxColumns);
	}

	/// <summary>Finds the offset of the first character that ends past a column, or -1 when none does.</summary>
	/// <remarks>
	/// The text starts at column 0, and the columns count as <see cref="GetColumns"/> does.
	/// </remarks>
	public static int FindOffsetPastColumn(string text, int tabColumns, int maxColumns)
	{
		long columns = 0;

		for (int offset = 0; offset < text.Length; offset++)
		{
			columns = GetColumnsAfter(columns, text[offset], tabColumns);

			if (columns > maxColumns)
				return offset;
		}

		return -1;
	}

	private static long GetColumnsAfter(long columns, char character, int tabColumns)
	{
		if (character != '\t')
			return columns + 1;

		return columns + (tabColumns > 0 ? tabColumns - (columns % tabColumns) : tabColumns);
	}
}
