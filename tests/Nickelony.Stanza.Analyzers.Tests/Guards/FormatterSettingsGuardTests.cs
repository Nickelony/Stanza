using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Guards;

/// <summary>Guards that the editor settings, CSharpier and the pinned rules agree.</summary>
[TestClass]
[TestCategory("Repository")]
public sealed partial class FormatterSettingsGuardTests
{
	[GeneratedRegex(@"""printWidth""\s*:\s*(?<width>\d+)", RegexOptions.CultureInvariant)]
	private static partial Regex CSharpierPrintWidth();

	[GeneratedRegex(@"\{[^{}]*""files""[^{}]*\}", RegexOptions.CultureInvariant)]
	private static partial Regex CSharpierOverride();

	[GeneratedRegex(
		@"^\s*stanza_max_line_length\s*=\s*(?<width>\d+)\s*$",
		RegexOptions.CultureInvariant | RegexOptions.Multiline
	)]
	private static partial Regex EditorConfigLineLength();

	[GeneratedRegex(@"""(?<key>useTabs|indentSize|endOfLine)""\s*:\s*(?<value>[^,\r\n}]+)")]
	private static partial Regex CSharpierSetting();

	[GeneratedRegex(
		@"^\s*(?<key>indent_style|indent_size|end_of_line)\s*=\s*(?<value>\S+)\s*$",
		RegexOptions.CultureInvariant | RegexOptions.Multiline
	)]
	private static partial Regex EditorConfigSetting();

	// Which half of a pair of rules the repository runs is its own style, so StanzaRules cannot say it and the list stays by hand.
	// The pair test below pins what the registry can: the repository runs exactly one half of each pair.
	// A rule whose shape the tree does not write, such as ZA0502, is listed too.
	// So is a rule that reports so many sites that the tree is not rewritten for it, such as ZA1001.
	private static readonly string[] s_disabled =
	[
		"ZA1001",
		"ZA0101",
		"ZA0112",
		"ZA0122",
		"ZA0132",
		"ZA0142",
		"ZA0152",
		"ZA0162",
		"ZA0172",
		"ZA0201",
		"ZA0303",
		"ZA0307",
		"ZA0502",
		"ZA0652",
		"ZA0199",
		"ZA2002",
		"ZA2009",
		"ZA0907",
	];

	[TestMethod]
	public void TheRepositoryRunsOneHalfOfEachPairOfRules()
	{
		foreach (
			RulePair pair in StanzaRules.All.Select(rule => rule.Pair).OfType<RulePair>().Distinct()
		)
		{
			Assert.HasCount(
				1,
				new[] { pair.First.Id, pair.Second.Id }.Where(s_disabled.Contains),
				$"The repository runs both or neither of {pair.First.Id} and {pair.Second.Id}."
			);
		}
	}

	[TestMethod]
	public void TheEditorConfigPinsExactlyTheRulesTheRepositoryRuns()
	{
		// A raw string in a test is the input the test reads, so the markup-literal rules stay off there,
		// and a test sets its arrange, act and assert steps apart, which the loose-run and kept-run statement rules would close.
		string[] offInTests =
		[
			"ZA0052",
			"ZA0054",
			"ZA2001",
			"ZA2003",
			"ZA2006",
			"ZA2007",
			"ZA2004",
			"ZA2005",
			"ZA2008",
			"ZA2009",
			"ZA2010",
		];

		Dictionary<string, string> expected = new(StringComparer.Ordinal);

		foreach ((_, DiagnosticDescriptor descriptor) in TestRuleSets.Declared)
			expected[$"[*.cs] {descriptor.Id}"] = "warning";

		foreach (string id in s_disabled)
			expected[$"[*.cs] {id}"] = "none";

		foreach (string id in offInTests)
			expected[$"[tests/**/*.cs] {id}"] = "none";

		Dictionary<string, string> pinned = new(StringComparer.Ordinal);
		string section = string.Empty;

		foreach (string line in File.ReadAllLines(RepositoryPaths.Below(".editorconfig")))
		{
			string text = line.Trim();

			if (text.StartsWith('['))
			{
				section = text;
				continue;
			}

			int separator = text.IndexOf('=');

			if (separator < 0)
				continue;

			string key = text[..separator].Trim();

			if (
				!key.StartsWith("dotnet_diagnostic.ZA", StringComparison.Ordinal)
				|| !key.EndsWith(".severity", StringComparison.Ordinal)
			)
			{
				continue;
			}

			string id = key["dotnet_diagnostic.".Length..^".severity".Length];
			pinned[$"{section} {id}"] = text[(separator + 1)..].Trim();
		}

		Assert.AreEqual(
			Render(pinned),
			Render(expected),
			"The repository does not pin exactly the rules it runs."
		);
	}

	[TestMethod]
	public void EachLineWidthIsOneNumberForBothTools()
	{
		// Neither tool reads the other's file, so every scope's width has to stay one number across the two,
		// and both files list their scopes in the same order.
		string csharpier = File.ReadAllText(RepositoryPaths.Below(".csharpierrc.json"));
		string editorConfig = File.ReadAllText(RepositoryPaths.Below(".editorconfig"));

		MatchCollection configured = CSharpierPrintWidth().Matches(csharpier);
		MatchCollection pinned = EditorConfigLineLength().Matches(editorConfig);

		Assert.IsGreaterThanOrEqualTo(
			1,
			configured.Count,
			".csharpierrc.json carries no printWidth."
		);

		Assert.HasCount(configured.Count, pinned, "The widths are not paired a scope at a time.");

		for (int index = 0; index < configured.Count; index++)
		{
			Assert.AreEqual(
				configured[index].Groups["width"].Value,
				pinned[index].Groups["width"].Value,
				$"The width pair at position {index} has drifted between CSharpier and the rules."
			);
		}
	}

	[TestMethod]
	public void TheCSharpierSettingsAgreeWithTheEditorConfig()
	{
		Dictionary<string, string> editorConfig = new(StringComparer.Ordinal);

		foreach (
			Match match in EditorConfigSetting()
				.Matches(File.ReadAllText(RepositoryPaths.Below(".editorconfig")))
		)
		{
			string key = match.Groups["key"].Value;
			string value = match.Groups["value"].Value;

			if (editorConfig.TryGetValue(key, out string? seen))
			{
				Assert.AreEqual(
					seen,
					value,
					$".editorconfig names {key} twice with two different values."
				);

				continue;
			}

			editorConfig[key] = value;
		}

		(string CSharpier, string EditorConfig)[] pairs =
		[
			("indentSize", "indent_size"),
			("useTabs", "indent_style"),
			("endOfLine", "end_of_line"),
		];

		// Every scope must carry indentSize, useTabs and endOfLine,
		// each equal to the value .editorconfig pins.
		foreach (
			string scope in CSharpierScopes(
				File.ReadAllText(RepositoryPaths.Below(".csharpierrc.json"))
			)
		)
		{
			var csharpier = CSharpierSetting()
				.Matches(scope)
				.ToDictionary(
					match => match.Groups["key"].Value,
					match => match.Groups["value"].Value.Trim().Trim('"'),
					StringComparer.Ordinal
				);

			foreach ((string csharpierKey, string editorConfigKey) in pairs)
			{
				Assert.IsTrue(
					csharpier.ContainsKey(csharpierKey),
					$"A scope of .csharpierrc.json carries no {csharpierKey}."
				);

				Assert.IsTrue(
					editorConfig.ContainsKey(editorConfigKey),
					$".editorconfig carries no {editorConfigKey}."
				);

				string expected = csharpier[csharpierKey];

				if (csharpierKey == "useTabs")
					expected = csharpier[csharpierKey] == "true" ? "tab" : "space";

				Assert.AreEqual(
					expected,
					editorConfig[editorConfigKey],
					$"CSharpier's {csharpierKey} and the {editorConfigKey} it shadows have drifted."
				);
			}
		}
	}

	// The .csharpierrc.json holds one setting set per scope: the top level, then one object per override.
	private static IEnumerable<string> CSharpierScopes(string config)
	{
		int overrides = config.IndexOf("\"overrides\"", StringComparison.Ordinal);

		if (overrides < 0)
		{
			yield return config;
			yield break;
		}

		yield return config[..overrides];

		foreach (Match match in CSharpierOverride().Matches(config, overrides))
			yield return match.Value;
	}

	private static string Render(Dictionary<string, string> values)
	{
		return string.Join(
			",",

			values
				.OrderBy(pair => pair.Key, StringComparer.Ordinal)
				.Select(pair => $"{pair.Key}={pair.Value}")
		);
	}
}
