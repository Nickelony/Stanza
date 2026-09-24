using System;
using System.Collections.Generic;
using System.Globalization;
using Nickelony.Stanza.Markup.Core.DocComments;

namespace Nickelony.Stanza.Markup.Core.Options;

/// <summary>Reads the <c>.editorconfig</c> values of the markup engine: counts, names, tab width and layout.</summary>
internal static class MarkupOptionReader
{
	public const int DefaultMaxLineLength = 100;

	/// <summary>
	/// Returns a value cut of its comment and, on a <c>stanza_</c> or <c>csharp_</c> key, of its severity suffix.
	/// </summary>
	public static string StripCommentAndSeverity(string key, string rawValue)
	{
		string value = StripComment(rawValue, honorsEscapes: false);

		if (key.StartsWith("stanza_", StringComparison.Ordinal) || key.StartsWith("csharp_", StringComparison.Ordinal))
		{
			int separator = value.IndexOf(':');

			if (separator >= 0)
				value = value.Substring(0, separator).TrimEnd();
		}

		return value;
	}

	/// <summary>Cuts the text at its first <c>#</c> or <c>;</c>, which a section header may escape.</summary>
	public static string StripComment(string text, bool honorsEscapes)
	{
		for (int offset = 0; offset < text.Length; offset++)
		{
			if (honorsEscapes && text[offset] == '\\')
			{
				offset++;
				continue;
			}

			if (text[offset] is '#' or ';')
				return text.Substring(0, offset).Trim();
		}

		return text.Trim();
	}

	/// <summary>
	/// A zero, a negative or an unreadable value is unset here, so the next key, then the default, applies.
	/// </summary>
	public static int ResolveTabColumns(Func<string, string?> read)
	{
		foreach (string key in s_indentKeys)
		{
			int columns = ReadCount(read, key, 0);

			if (columns > 0)
				return columns;
		}

		return MarkupColumns.DefaultTabColumns;
	}

	/// <summary>
	/// A zero is returned as itself, not replaced by the fallback; each Stanza key's option page says what zero means.
	/// </summary>
	public static int ReadCount(Func<string, string?> read, string key, int fallback) =>
		read(key) is { } value && TryReadCount(key, value, out int count) ? count : fallback;

	/// <summary>The one reading of a count, so a reader and the tool's warnings cannot disagree about it.</summary>
	public static bool TryReadCount(string key, string value, out int count) => TryReadCount(value, GetMaxCount(key), out count);

	public static bool TryReadCount(string? value, int max, out int count)
	{
		return int.TryParse(s: value, style: NumberStyles.Integer, provider: CultureInfo.InvariantCulture, result: out count)
			&& count >= 0
			&& count <= max;
	}

	public static int GetMaxCount(string key)
	{
		return key is MarkupKeys.StanzaTabWidth or MarkupKeys.DocIndentSize or MarkupKeys.TabWidth or MarkupKeys.IndentSize
			? MarkupLimits.MaxIndentColumns
			: MarkupLimits.MaxCount;
	}

	public static bool Names(string? value, string word) => string.Equals(value, word, StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// Some keys the layout reads carry no documentation prefix, so the prefix alone does not name them.
	/// </summary>
	public static bool IsDocumentationKey(string key) =>
		key.StartsWith(MarkupKeys.DocPrefix, StringComparison.Ordinal) || key is MarkupKeys.MaxLineLength or MarkupKeys.StanzaTabWidth;

	public static bool IsExpansionName(string? value) => Names(value, "none") || Names(value, "sections") || Names(value, "all");

	public static bool IsIndentStyleName(string? value) => Names(value, "nested") || Names(value, "hanging");

	public static DocLayoutOptions? ResolveDocLayout(Func<string, string?> read)
	{
		// The doc key wins when set; otherwise, the general key's width, then the default.
		int fallback = ReadCount(read, MarkupKeys.MaxLineLength, DefaultMaxLineLength);
		int maxLineLength = ReadCount(read, MarkupKeys.DocMaxLineLength, fallback);

		// A width of zero disables the layout.
		// ReadCount replaces a negative or unreadable width with the fallback,
		// so only zero reaches this branch.
		if (maxLineLength <= 0)
			return null;

		int tabColumns = ResolveTabColumns(read);

		// `tab` writes one tab per level where a number writes that many columns,
		// so the width of one level is the tab width.
		bool indentWithTabs = Names(read(MarkupKeys.DocIndentSize), "tab");

		return new DocLayoutOptions(
			ContentWidth: maxLineLength,
			TabColumns: tabColumns,
			MinTailWords: ReadCount(read, MarkupKeys.MinTailWords, DocLayoutOptions.DefaultMinTailWords),
			MinLineWords: ReadCount(read, MarkupKeys.MinLineWords, DocLayoutOptions.DefaultMinLineWords),
			MaxOverflowColumns: ReadCount(read, MarkupKeys.MaxOverflowColumns, DocLayoutOptions.DefaultMaxOverflowColumns),
			IndentSize: indentWithTabs ? tabColumns : ReadCount(read, MarkupKeys.DocIndentSize, DocLayoutOptions.DefaultIndentSize),
			IndentText: !Names(read(MarkupKeys.IndentStyle), "hanging"),
			Expansion: ReadExpansion(read(MarkupKeys.Expand)),
			WeakTailWords: ReadWordSet(read, MarkupKeys.WeakTailWords),
			IndentWithTabs: indentWithTabs
		);
	}

	private static DocExpansion ReadExpansion(string? value)
	{
		if (Names(value, "sections"))
			return DocExpansion.Sections;

		if (Names(value, "all"))
			return DocExpansion.All;

		return DocExpansion.None;
	}

	public static bool NamesASeverity(string? value)
	{
		// Roslyn reads `refactoring` as another name for `silent`.
		return Names(value, "none")
			|| Names(value, "silent")
			|| Names(value, "refactoring")
			|| Names(value, "suggestion")
			|| Names(value, "warning")
			|| Names(value, "error")
			|| Names(value, "default");
	}

	private static DocNameSet? ReadWordSet(Func<string, string?> read, string key)
	{
		if (read(key) is not { } value || Names(value.Trim(), "none"))
			return null;

		HashSet<string> words = new(StringComparer.Ordinal);

		foreach (string word in value.Split(s_wordSeparators, StringSplitOptions.RemoveEmptyEntries))
		{
			if (DocTextRules.NormalizeWord(word) is { Length: > 0 } normalized)
				words.Add(normalized);
		}

		return words.Count == 0 ? null : new DocNameSet([.. words]);
	}

	private static readonly List<string> s_indentKeys = [MarkupKeys.StanzaTabWidth, MarkupKeys.TabWidth, MarkupKeys.IndentSize];
	private static readonly char[] s_wordSeparators = [','];
}
