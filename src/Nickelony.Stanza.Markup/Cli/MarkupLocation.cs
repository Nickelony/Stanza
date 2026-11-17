namespace Nickelony.Stanza.Markup.Cli;

/// <summary>Writes where a report points, in the one shape both reporters use.</summary>
internal static class MarkupLocation
{
	/// <summary>Writes <c>path</c>, <c>path(line)</c> or <c>path(line,column)</c>, never a column alone.</summary>
	public static string Format(string displayPath, int? line = null, int? column = null)
	{
		if (line is null)
			return displayPath;

		return column is null ? $"{displayPath}({line})" : $"{displayPath}({line},{column})";
	}
}
