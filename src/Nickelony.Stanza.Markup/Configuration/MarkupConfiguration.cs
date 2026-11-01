using System;
using System.Collections.Generic;
using Nickelony.Stanza.Markup.Core;
using Nickelony.Stanza.Markup.Core.DocComments;
using Nickelony.Stanza.Markup.Core.Options;
using Nickelony.Stanza.Markup.Notices;
using Nickelony.Stanza.Markup.Paths;
using Nickelony.Stanza.Shared;

namespace Nickelony.Stanza.Markup.Configuration;

/// <summary>Resolves the <c>.editorconfig</c> settings of a file into its profile, rules and options.</summary>
internal sealed class MarkupConfiguration(MarkupPaths paths, MarkupNotices notices, bool reprintForced = false)
{
	private readonly EditorConfigReader _reader = new(paths, notices);

	/// <summary>The values of each path, so the questions a run asks about one file evaluate its chain once.</summary>
	private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _values = new(PathComparison.Comparer);

	public bool IsEnabled(string path)
	{
		// Only an explicit `false` takes the file out of the run;
		// a value that is neither `true` nor `false` keeps it in, and the reader warns.
		return !GetValues(path).TryGetValue(OptionKeys.MarkupEnabled, out string? value) || OptionValues.Boolean.Find(value) is not false;
	}

	/// <summary>The forced profile wins over the profile key, which wins over the extension map.</summary>
	public MarkupProfile? FindProfile(string path, MarkupProfile? forced) => forced ?? FindProfileFromKey(path) ?? MarkupProfiles.FindByPath(path);

	public MarkupOptions GetOptions(string path, MarkupProfile profile, bool profileWasForced = false)
	{
		IReadOnlyDictionary<string, string> values = GetValues(path);

		MarkupOptions options = MarkupOptionReader.ReadOptions(
			read: key => FindValue(values, key),
			fallbackPolicy: profile.Policy,
			isPolicyForced: profileWasForced
		);

		return options with
		{
			Rules = ReadRules(values),
			DisabledRules = ReadDisabledRules(values),
		};
	}

	public DocLayoutOptions? ResolveReprintLayout(string path)
	{
		IReadOnlyDictionary<string, string> values = GetValues(path);

		if (
			!reprintForced
			&& (
				!values.TryGetValue(OptionKeys.MarkupDocReprint, out string? reprint) || !OptionValues.MarkupDocReprint.Read(reprint, fallback: false)
			)
		)
		{
			return null;
		}

		return MarkupOptionReader.ResolveDocLayout(key => FindValue(values, key));
	}

	private static string? FindValue(IReadOnlyDictionary<string, string> values, string key) =>
		values.TryGetValue(key, out string? value) ? value : null;

	private MarkupProfile? FindProfileFromKey(string path) =>
		GetValues(path).TryGetValue(OptionKeys.MarkupProfile, out string? name) && name.Length > 0 ? MarkupProfiles.FindByName(name) : null;

	private static MarkupRuleSet ReadRules(IReadOnlyDictionary<string, string> values) => MarkupRuleSet.From(ReadRuleIds(values, IsEnablingSeverity));

	private static MarkupRuleSet ReadDisabledRules(IReadOnlyDictionary<string, string> values) =>
		MarkupRuleSet.From(ReadRuleIds(values, static severity => MarkupOptionReader.Names(severity, "none")));

	private static List<string> ReadRuleIds(IReadOnlyDictionary<string, string> values, Func<string, bool> matches)
	{
		List<string> ids = [];

		foreach (KeyValuePair<string, string> pair in values)
		{
			if (ReadRuleId(pair.Key) is { } id && matches(pair.Value))
				ids.Add(id);
		}

		return ids;
	}

	private static string? ReadRuleId(string key)
	{
		if (
			!key.StartsWith(OptionKeys.SeverityPrefix, StringComparison.Ordinal) || !key.EndsWith(OptionKeys.SeveritySuffix, StringComparison.Ordinal)
		)
		{
			return null;
		}

		// The reader lower-cases a key, so the ID is upper-cased again to read as the rule's own.
		int start = OptionKeys.SeverityPrefix.Length;
		int length = key.Length - start - OptionKeys.SeveritySuffix.Length;

		if (length <= 0)
			return null;

		string id = key.Substring(start, length).ToUpperInvariant();
		return id.StartsWith(MarkupDiagnostics.Prefix, StringComparison.Ordinal) ? id : null;
	}

	private static bool IsEnablingSeverity(string severity)
	{
		return MarkupOptionReader.NamesASeverity(severity)
			&& !MarkupOptionReader.Names(severity, "none")
			&& !MarkupOptionReader.Names(severity, "default");
	}

	private IReadOnlyDictionary<string, string> GetValues(string path)
	{
		if (_values.TryGetValue(path, out IReadOnlyDictionary<string, string>? cached))
			return cached;

		IReadOnlyDictionary<string, string> values = _reader.GetChain(path).Evaluate(path);
		_values.Add(path, values);

		return values;
	}
}
