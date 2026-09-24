using System;
using System.Collections.Generic;

namespace Nickelony.Stanza.Markup.Core.DocComments;

/// <summary>The options that lay out a documentation comment.</summary>
/// <remarks>
/// The constructor takes one parameter for each option, since the one job is to hold them together.
/// </remarks>
internal readonly record struct DocLayoutOptions(
	int ContentWidth,
	int TabColumns,
	int MinTailWords,
	int MinLineWords,
	int MaxOverflowColumns,
	int IndentSize,
	bool IndentText,
	DocExpansion Expansion,
	DocNameSet? WeakTailWords = null,
	bool IndentWithTabs = false
)
{
	public const int DefaultMinTailWords = 2;
	public const int DefaultMinLineWords = 2;
	public const int DefaultMaxOverflowColumns = 4;
	public const int DefaultIndentSize = 0;

	public int GetColumns(string text) => MarkupColumns.GetColumns(text, TabColumns);

	private static readonly HashSet<string> s_blockElements = new(StringComparer.Ordinal) { "remarks", "example", "para" };

	private static readonly HashSet<string> s_sectionElements = new(StringComparer.Ordinal)
	{
		"summary",
		"returns",
		"value",
		"param",
		"typeparam",
		"exception",
		"seealso",
		"permission",
	};

	public bool IsExpanded(string name)
	{
		return s_blockElements.Contains(name)
			|| Expansion == DocExpansion.All
			|| (Expansion == DocExpansion.Sections && s_sectionElements.Contains(name));
	}

	public int GetIndentationColumns(int depth, bool text) => IndentSize <= 0 ? 0 : checked(GetIndentationLevels(depth, text) * IndentSize);

	public string GetIndent(int depth, bool text)
	{
		int columns = GetIndentationColumns(depth, text);

		if (columns <= 0)
			return string.Empty;

		return IndentWithTabs ? new string('\t', GetIndentationLevels(depth, text)) : new string(' ', columns);
	}

	private int GetIndentationLevels(int depth, bool text) => depth + (text && IndentText ? 1 : 0);

	/// <summary>Narrows the content width by <paramref name="columns"/>, which can leave it at zero or below.</summary>
	public DocLayoutOptions NarrowBy(int columns) => columns <= 0 ? this : this with { ContentWidth = ContentWidth - columns };
}
