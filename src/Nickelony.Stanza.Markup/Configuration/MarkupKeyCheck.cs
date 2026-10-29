using System;
using Nickelony.Stanza.Markup.Core.Options;

namespace Nickelony.Stanza.Markup.Configuration;

/// <summary>Reads and judges the value of a key the tool reads from <c>.editorconfig</c>.</summary>
internal static class MarkupKeyCheck
{
	/// <summary>Reads <c>true</c> or <c>false</c> in any case, and anything else as <see langword="null"/>.</summary>
	public static bool? FromBooleanName(string value)
	{
		return value.ToLowerInvariant() switch
		{
			"true" => true,
			"false" => false,
			_ => null,
		};
	}

	/// <summary>Reads <c>containers</c> as <see langword="true"/> and <c>all</c> as <see langword="false"/>.</summary>
	public static bool? FromScopeName(string scope)
	{
		return scope.ToLowerInvariant() switch
		{
			"containers" => true,
			"all" => false,
			_ => null,
		};
	}

	/// <summary>The problem a value has, or <see langword="null"/> when it has none.</summary>
	public static string? FindProblem(string key, string value)
	{
		// Keys outside the markup and documentation families pass unchecked, but the two indentation keys the layout reads;
		// any other key in a family is warned as not read.
		if (
			!key.StartsWith(MarkupKeys.MarkupPrefix, StringComparison.Ordinal)
			&& !MarkupOptionReader.IsDocumentationKey(key)
			&& key is not (MarkupKeys.TabWidth or MarkupKeys.IndentSize)
		)
		{
			return null;
		}

		// Each problem is the tail of the warning `'{path}' sets {key} to '{value}': {problem}.` that EditorConfigReader writes.
		return key switch
		{
			MarkupKeys.Enabled => FromBooleanName(value) is null ? "neither 'true' nor 'false', so the file stays in the run" : null,
			MarkupKeys.Scope => FromScopeName(value) is null ? "neither 'containers' nor 'all', so the profile's own scope is used" : null,

			MarkupKeys.Profile => MarkupProfiles.FindByName(value) is null
				? $"not a profile name, so the extension decides instead; the profiles are {MarkupProfiles.Summary}"
				: null,

			MarkupKeys.MaxBlankLines => CheckCount(key, value),

			MarkupKeys.DocReprint => MarkupOptionReader.Names(value, "members") || MarkupOptionReader.Names(value, "off")
				? null
				: "neither 'members' nor 'off', so the blank line rules stay in charge",

			MarkupKeys.MaxLineLength
			or MarkupKeys.DocMaxLineLength
			or MarkupKeys.MinTailWords
			or MarkupKeys.MinLineWords
			or MarkupKeys.MaxOverflowColumns
			or MarkupKeys.StanzaTabWidth => CheckCount(key, value),

			MarkupKeys.DocIndentSize => MarkupOptionReader.Names(value, "tab") || MarkupOptionReader.TryReadCount(key, value, out _)
				? null
				: $"neither 'tab' nor a whole number from 0 to {MarkupOptionReader.GetMaxCount(key)}, so the default applies",

			// The tab columns are read from the first of these keys with a positive count, so a value out of range is skipped.
			MarkupKeys.TabWidth => MarkupOptionReader.TryReadCount(key, value, out _)
				? null
				: $"not a whole number from 0 to {MarkupOptionReader.GetMaxCount(key)}, so the key is ignored",

			MarkupKeys.IndentSize => MarkupOptionReader.Names(value, "tab") || MarkupOptionReader.TryReadCount(key, value, out _)
				? null
				: $"neither 'tab' nor a whole number from 0 to {MarkupOptionReader.GetMaxCount(key)}, so the key is ignored",

			MarkupKeys.Expand => MarkupOptionReader.IsExpansionName(value) ? null : "neither 'none', 'sections' nor 'all', so the default applies",
			MarkupKeys.IndentStyle => MarkupOptionReader.IsIndentStyleName(value) ? null : "neither 'nested' nor 'hanging', so the default applies",

			// A word list takes any value, so only a misspelled key can be wrong.
			MarkupKeys.WeakTailWords => null,
			_ => "not a key this tool reads, so it was ignored",
		};
	}

	private static string? CheckCount(string key, string value)
	{
		return MarkupOptionReader.TryReadCount(key, value, out _)
			? null
			: $"not a whole number from 0 to {MarkupOptionReader.GetMaxCount(key)}, so the default applies";
	}
}
