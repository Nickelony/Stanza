using System;

namespace Nickelony.Stanza.Markup.Core;

internal static class MarkupColumns
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
		{
			if (character != '\t')
				columns++;
			else
				columns += tabColumns > 0 ? tabColumns - (columns % tabColumns) : tabColumns;
		}

		return (int)Math.Min(columns - startColumn, MaxColumns);
	}
}
