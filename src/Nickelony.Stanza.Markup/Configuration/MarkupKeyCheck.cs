using System;
using System.Linq;
using Nickelony.Stanza.Markup.Core.Options;

namespace Nickelony.Stanza.Markup.Configuration;

/// <summary>Reads and judges the value of a key the tool reads from <c>.editorconfig</c>.</summary>
internal static class MarkupKeyCheck
{
	/// <summary>The problem a value has, or <see langword="null"/> when it has none.</summary>
	public static string? FindProblem(string key, string value, string rawValue)
	{
		// Keys outside the `stanza_` family pass unchecked, but the two indentation keys the layout reads;
		// a `stanza_` key the shared list does not name is warned as not read.
		if (!key.StartsWith(OptionKeys.StanzaPrefix, StringComparison.Ordinal) && key is not (OptionKeys.TabWidth or OptionKeys.IndentSize))
			return null;

		// Each problem is the tail of the warning `'{path}' sets {key} to '{value}': {problem}.` that EditorConfigReader writes.
		return key switch
		{
			OptionKeys.MarkupEnabled => OptionValues.Boolean.Names(value)
				? null
				: $"{OptionValues.Boolean.DescribeNeither()}, so the file stays in the run",

			OptionKeys.MarkupScope => OptionValues.MarkupScope.Names(value)
				? null
				: $"{OptionValues.MarkupScope.DescribeNeither()}, so the profile's own scope is used",

			OptionKeys.MarkupProfile => MarkupProfiles.FindByName(value) is null
				? $"not a profile name, so the extension decides instead; the profiles are {MarkupProfiles.Summary}"
				: null,

			OptionKeys.MarkupMaxBlankLines => CheckCount(key, value),

			OptionKeys.MarkupDocReprint => OptionValues.MarkupDocReprint.Names(value)
				? null
				: $"{OptionValues.MarkupDocReprint.DescribeNeither()}, so the blank line rules stay in charge",

			// A width that is not a count in range is skipped, so the next key of its chain applies.
			OptionKeys.MaxLineLength or OptionKeys.DocMaxLineLength => MarkupOptionReader.TryReadCount(key, value, out _)
				? null
				: $"not a whole number from 0 to {MarkupOptionReader.GetMaxCount(key)}, so the key is ignored",

			OptionKeys.DocMinTailWords or OptionKeys.DocMinLineWords or OptionKeys.DocMaxOverflowColumns => CheckCount(key, value),

			OptionKeys.DocIndentSize => MarkupOptionReader.Names(value, OptionValues.IndentTab) || MarkupOptionReader.TryReadCount(key, value, out _)
				? null
				: $"neither '{OptionValues.IndentTab}' nor a whole number from 0 to {MarkupOptionReader.GetMaxCount(key)}, so the default applies",

			// The tab columns are read from the first of these keys with a positive count, so a value out of range is skipped.
			OptionKeys.TabWidth => MarkupOptionReader.TryReadCount(key, value, out _)
				? null
				: $"not a whole number from 0 to {MarkupOptionReader.GetMaxCount(key)}, so the key is ignored",

			OptionKeys.IndentSize => MarkupOptionReader.Names(value, OptionValues.IndentTab) || MarkupOptionReader.TryReadCount(key, value, out _)
				? null
				: $"neither '{OptionValues.IndentTab}' nor a whole number from 0 to {MarkupOptionReader.GetMaxCount(key)}, so the key is ignored",

			OptionKeys.DocBlockElements => OptionValues.DocBlockElements.Names(value)
				? null
				: $"{OptionValues.DocBlockElements.DescribeNeither()}, so the default applies",

			OptionKeys.DocTextIndent => OptionValues.DocTextIndent.Names(value)
				? null
				: $"{OptionValues.DocTextIndent.DescribeNeither()}, so the default applies",

			// A word list takes any value, so only a misspelled key can be wrong.
			OptionKeys.DocWeakLineEndWords => null,
			// An entry that is no local name, such as one with a space, matches no element; a colon is cut as a severity.
			OptionKeys.MarkupSeparatedElements => CheckElementNames(value, rawValue),
			// A word the list cannot name is skipped, and a list with none falls back to `null` alone.
			OptionKeys.DocLangwords => CheckKeywords(value),
			// A key the shared list names that this tool does not read, such as an analyzer key, passes;
			// any other `stanza_` key is a misspelling.
			_ => OptionKeys.Known.Contains(key) ? null : "not a key this tool reads, so it was ignored",
		};
	}

	private static string? CheckKeywords(string value)
	{
		return MarkupOptionReader.FindUnlistableKeyword(value) is { } word
			? $"'{word}' is not 'null', 'true', 'false' or 'default', so it is skipped"
			: null;
	}

	/// <summary>Judges the listed entries, and a colon that cut the text short as a severity suffix.</summary>
	private static string? CheckElementNames(string value, string rawValue)
	{
		string listed = MarkupOptionReader.StripComment(rawValue, honorsEscapes: false);

		if (listed.Contains(':'))
			return $"'{listed}' holds a colon, which starts a severity suffix, so only '{value}' is read; write the local name alone";

		return MarkupOptionReader.FindInvalidElementName(value) is { } name
			? $"'{name}' is not a local element name, so it matches no element"
			: null;
	}

	private static string? CheckCount(string key, string value)
	{
		return MarkupOptionReader.TryReadCount(key, value, out _)
			? null
			: $"not a whole number from 0 to {MarkupOptionReader.GetMaxCount(key)}, so the default applies";
	}
}
