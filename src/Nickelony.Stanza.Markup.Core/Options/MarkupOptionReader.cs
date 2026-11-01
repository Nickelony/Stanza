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

		if (key.StartsWith(OptionKeys.StanzaPrefix, StringComparison.Ordinal) || key.StartsWith("csharp_", StringComparison.Ordinal))
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

	/// <summary>Reads the markup options of a file, falling back to the policy an unset scope key leaves.</summary>
	/// <remarks>
	/// A forced policy keeps its own scope; the rule sets stay empty, since only the tool reads them.
	/// </remarks>
	public static MarkupOptions ReadOptions(Func<string, string?> read, MarkupPolicy fallbackPolicy, bool isPolicyForced = false)
	{
		MarkupPolicy policy = isPolicyForced
			? fallbackPolicy
			: fallbackPolicy with
			{
				ContainersOnly = OptionValues.MarkupScope.Read(read(OptionKeys.MarkupScope), fallback: fallbackPolicy.ContainersOnly),
			};

		return new MarkupOptions(
			Policy: policy,
			MaxBlankLines: ReadCount(read, OptionKeys.MarkupMaxBlankLines, MarkupOptions.DefaultMaxBlankLines),
			Langwords: ReadLangwords(read),
			SeparatedElements: ReadSeparatedElements(read)
		);
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

		return TextColumns.DefaultTabColumns;
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
		return key is OptionKeys.DocIndentSize or OptionKeys.TabWidth or OptionKeys.IndentSize
			? MarkupLimits.MaxIndentColumns
			: MarkupLimits.MaxCount;
	}

	public static bool Names(string? value, string word) => string.Equals(value, word, StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// A value that is not a count in range is unset here, so <c>max_line_length</c> applies, then the default.
	/// </summary>
	public static int ResolveMaxLineLength(Func<string, string?> read)
	{
		// The standard key takes a positive width or `off`, so its zero turns no width off and falls through.
		int standard = ReadCount(read, OptionKeys.StandardMaxLineLength, DefaultMaxLineLength);
		return ReadCount(read, OptionKeys.MaxLineLength, fallback: standard > 0 ? standard : DefaultMaxLineLength);
	}

	public static DocLayoutOptions? ResolveDocLayout(Func<string, string?> read)
	{
		// The doc key wins when set; otherwise, the code width.
		int maxLineLength = ReadCount(read, OptionKeys.DocMaxLineLength, ResolveMaxLineLength(read));

		// A width of zero disables the layout.
		// ReadCount replaces a negative or unreadable width with the fallback,
		// so only zero reaches this branch.
		if (maxLineLength <= 0)
			return null;

		int tabColumns = ResolveTabColumns(read);

		// `tab` writes one tab per level where a number writes that many columns,
		// so the width of one level is the tab width.
		bool indentWithTabs = Names(read(OptionKeys.DocIndentSize), OptionValues.IndentTab);

		return new DocLayoutOptions(
			ContentWidth: maxLineLength,
			TabColumns: tabColumns,
			MinTailWords: ReadCount(read, OptionKeys.DocMinTailWords, DocLayoutOptions.DefaultMinTailWords),
			MinLineWords: ReadCount(read, OptionKeys.DocMinLineWords, DocLayoutOptions.DefaultMinLineWords),
			MaxOverflowColumns: ReadCount(read, OptionKeys.DocMaxOverflowColumns, DocLayoutOptions.DefaultMaxOverflowColumns),
			IndentSize: indentWithTabs ? tabColumns : ReadCount(read, OptionKeys.DocIndentSize, DocLayoutOptions.DefaultIndentSize),
			IndentText: OptionValues.DocTextIndent.Read(read(OptionKeys.DocTextIndent), fallback: true),
			BlockElements: OptionValues.DocBlockElements.Read(read(OptionKeys.DocBlockElements), fallback: DocBlockElements.Minimal),
			WeakLineEndWords: ReadWordSet(read, OptionKeys.DocWeakLineEndWords),
			IndentWithTabs: indentWithTabs
		);
	}

	public static bool NamesASeverity(string? value)
	{
		foreach (string severity in s_severities)
		{
			if (Names(value, severity))
				return true;
		}

		return false;
	}

	/// <summary>Reads the keywords a file lists, or <see langword="null"/> when it lists none.</summary>
	public static MarkupNameSet? ReadLangwords(Func<string, string?> read)
	{
		if (read(OptionKeys.DocLangwords) is not { } value)
			return null;

		HashSet<string> words = new(StringComparer.Ordinal);

		foreach (string word in ReadKeywords(value))
		{
			string lowered = word.ToLowerInvariant();

			if (DocTextRules.ListableKeywords.Contains(lowered))
				words.Add(lowered);
		}

		return words.Count == 0 ? null : new MarkupNameSet([.. words]);
	}

	/// <summary>Reads the element names a file lists, or <see langword="null"/> when it lists none.</summary>
	/// <remarks>
	/// Only the lowercase <c>none</c> lists none, so <c>None</c> names an element; a name is kept as written.
	/// </remarks>
	public static MarkupNameSet? ReadSeparatedElements(Func<string, string?> read)
	{
		if (read(OptionKeys.MarkupSeparatedElements) is not { } value || value.Trim() == "none")
			return null;

		List<string> names = [.. ReadKeywords(value)];
		return names.Count == 0 ? null : new MarkupNameSet([.. names]);
	}

	/// <summary>Finds the first listed entry that is no local name, or <see langword="null"/>.</summary>
	public static string? FindInvalidElementName(string value)
	{
		foreach (string name in ReadKeywords(value))
		{
			if (!IsLocalName(name))
				return name;
		}

		return null;
	}

	/// <summary>Finds the first word of a keyword list that no file may list, or <see langword="null"/>.</summary>
	public static string? FindUnlistableKeyword(string value)
	{
		foreach (string word in ReadKeywords(value))
		{
			if (!DocTextRules.ListableKeywords.Contains(word.ToLowerInvariant()))
				return word;
		}

		return null;
	}

	/// <summary>Whether the text is a name without a prefix, such as <c>Separator</c>.</summary>
	private static bool IsLocalName(string name)
	{
		if (!char.IsLetter(name[0]) && name[0] != '_')
			return false;

		foreach (char character in name)
		{
			if (!char.IsLetterOrDigit(character) && character is not ('_' or '-' or '.'))
				return false;
		}

		return true;
	}

	private static IEnumerable<string> ReadKeywords(string value)
	{
		foreach (string word in value.Split(s_wordSeparators, StringSplitOptions.RemoveEmptyEntries))
		{
			string trimmed = word.Trim();

			if (trimmed.Length > 0)
				yield return trimmed;
		}
	}

	private static MarkupNameSet? ReadWordSet(Func<string, string?> read, string key)
	{
		if (read(key) is not { } value || Names(value.Trim(), "none"))
			return null;

		HashSet<string> words = new(StringComparer.Ordinal);

		foreach (string word in value.Split(s_wordSeparators, StringSplitOptions.RemoveEmptyEntries))
		{
			if (DocTextRules.NormalizeWord(word) is { Length: > 0 } normalized)
				words.Add(normalized);
		}

		return words.Count == 0 ? null : new MarkupNameSet([.. words]);
	}

	private static readonly List<string> s_indentKeys = [OptionKeys.TabWidth, OptionKeys.IndentSize];
	private static readonly char[] s_wordSeparators = [','];

	/// <summary>The severities Roslyn reads, <c>refactoring</c> being another name for <c>silent</c>.</summary>
	private static readonly string[] s_severities = ["none", "silent", "refactoring", "suggestion", "warning", "error", "default"];
}
