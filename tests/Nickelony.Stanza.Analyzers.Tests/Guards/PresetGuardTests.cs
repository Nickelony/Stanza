using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Guards;

/// <summary>Guards the level table of the analyzer package and the files it writes.</summary>
[TestClass]
[TestCategory("Repository")]
public sealed partial class PresetGuardTests
{
	private const string UndoneSection = "## Layout that CSharpier undoes";
	private const string UndoneAnchor = "Working-with-formatters#layout-that-csharpier-undoes";

	// A test seam: set to 1, it writes the preset files from the level table before the comparison.
	private const string WriteVariable = "STANZA_WRITE_PRESETS";

	// The strong rules that the .editorconfig of the repository pins off, since the tree takes their other shape.
	private static readonly string[] s_strongRulesTheRepositoryRunsOff = ["ZA0303", "ZA0502"];

	// The opt-in rules that the repository pins on, since Stanza's own code is the extreme example of its rules working together.
	private static readonly string[] s_optInRulesTheRepositoryRuns = ["ZA0054", "ZA0055"];

	// A rule that never settles beside CSharpier, so no level enables it under either formatter value:
	// ZA0199 removes the comma that CSharpier writes back, and ZA1001 has no fix and would report a width that CSharpier owns.
	// A level that enabled one would leave a run that formats and then fails its own check.
	private static readonly string[] s_neverSettle = ["ZA0199", "ZA1001"];

	// The Level cell of a markup rule's readme row, since no level holds it.
	private const string NoLevel = "No level";

	[GeneratedRegex(
		@"^\| `(?<id>ZA\d{4})` \| .+ \| (?<shipsAs>[^|]+) \| (?<level>[^|]+) \|$",
		RegexOptions.CultureInvariant | RegexOptions.Multiline
	)]
	private static partial Regex ReadmeRuleRow();

	[GeneratedRegex(@"/wiki/(?<id>ZA\d{4})-", RegexOptions.CultureInvariant)]
	private static partial Regex RuleLink();

	[GeneratedRegex(@"\[(?<id>ZA\d{4})\]", RegexOptions.CultureInvariant)]
	private static partial Regex LinkedRule();

	[GeneratedRegex(@"`(?<id>ZA\d{4})`", RegexOptions.CultureInvariant)]
	private static partial Regex NamedRule();

	[GeneratedRegex(
		@"^dotnet_diagnostic\.(?<id>ZA\d{4})\.severity\s*=\s*(?<severity>\S+)",
		RegexOptions.CultureInvariant
	)]
	private static partial Regex PinLine();

	[TestMethod]
	public void ThePackageShipsAFileForEachLevelAndSeverityAndTheCSharpierOverlay()
	{
		string[] expected =
		[
			"csharpier.error",
			"csharpier.suggestion",
			"csharpier.warning",
			"minimal.error",
			"minimal.suggestion",
			"minimal.warning",
			"moderate.error",
			"moderate.warning",
			"none",
			"strong.error",
			"strong.suggestion",
			"strong.warning",
		];

		Assert.AreSequenceEqual(
			expected,
			PresetFiles.ReadNames(),
			"The files the package ships are not the ones the targets pick."
		);
	}

	[TestMethod]
	public void EachShippedFileIsTheOneTheLevelTableWrites()
	{
		Dictionary<string, string> expected = PresetFiles.BuildFiles(PresetFiles.ReadTable());

		if (Environment.GetEnvironmentVariable(WriteVariable) == "1")
			PresetFiles.Write(expected);

		string fix = $"Run this test with {WriteVariable}=1 to write the files again.";

		Assert.AreSequenceEqual(
			expected.Keys.Order(StringComparer.Ordinal),
			PresetFiles.ReadNames(),
			$"The files under build/presets are not the ones levels.txt writes. {fix}"
		);

		string[] differing =
		[
			.. expected
				.Where(file =>
					File.ReadAllText(PresetFiles.ToPath(file.Key)).ReplaceLineEndings("\n")
					!= file.Value
				)
				.Select(file => file.Key),
		];

		GuardAssert.None(differing, "A preset file differs from what levels.txt writes", fix);
	}

	[TestMethod]
	public void TheLevelTableStatesEachRuleOnceInIdOrder()
	{
		PresetFiles.LevelTable table = PresetFiles.ReadTable();

		GuardAssert.None(table.Problems, "levels.txt holds a line it cannot read");

		Assert.AreSequenceEqual(
			ReadRuleIds(),
			table.Rows.Select(row => row.Id),
			"levels.txt must state each rule but the markup rules once, in ID order, so a new rule gets its level."
		);
	}

	[TestMethod]
	public void EachFileIsAGlobalConfigThatAProjectFileWinsOver()
	{
		List<string> problems = [];

		foreach (string name in PresetFiles.ReadNames())
		{
			PresetFiles.Preset preset = PresetFiles.Read(name);

			// A global file that sets no level ranks at 0, above both ranks, so a project's own wins.
			int expected = name.StartsWith(PresetFiles.CSharpier, StringComparison.Ordinal)
				? PresetFiles.OverlayRank
				: PresetFiles.LevelRank;

			problems.AddRange(preset.Problems);

			if (!preset.IsGlobal)
				problems.Add($"{name} does not say is_global = true.");

			if (preset.GlobalLevel != expected)
				problems.Add($"{name} sets global_level {preset.GlobalLevel}, not {expected}.");
		}

		GuardAssert.None(problems, "A preset file is not the global config the targets expect");
	}

	[TestMethod]
	public void NoLevelEnablesBothHalvesOfAPair()
	{
		PresetFiles.LevelTable table = PresetFiles.ReadTable();

		Dictionary<string, HashSet<string>> levels = new(StringComparer.Ordinal)
		{
			["strong with csharpier"] = ReadStrongWithCSharpier(table),
		};

		foreach (string level in PresetFiles.Levels)
			levels[level] = [.. table.ReadEnabled(level)];

		List<string> problems = [];

		foreach ((string name, HashSet<string> enabled) in levels)
		{
			foreach ((string first, string second, _) in ReadPairs())
			{
				if (enabled.Contains(first) && enabled.Contains(second))
					problems.Add($"{name}: {first} and {second} are both enabled.");
			}
		}

		GuardAssert.None(problems, "A level enables both halves of a pair");
	}

	[TestMethod]
	public void TheModerateLevelAtSuggestionIsWhatTheAnalyzersShip()
	{
		DiagnosticDescriptor[] descriptors =
		[
			.. RoslynTestHarness.AllAnalyzers.Select(analyzer =>
				analyzer.SupportedDiagnostics.Single()
			),
		];

		Assert.AreSequenceEqual(
			PresetFiles.ReadTable().ReadEnabled("moderate"),

			descriptors
				.Where(descriptor => descriptor.IsEnabledByDefault)
				.Select(descriptor => descriptor.Id)
				.Order(StringComparer.Ordinal),

			"No file is added for moderate at suggestion, so the rules that ship on must be that level."
		);

		Assert.IsEmpty(
			descriptors
				.Where(descriptor => descriptor.DefaultSeverity != DiagnosticSeverity.Info)
				.Select(descriptor => descriptor.Id),

			"A rule ships at a severity other than suggestion, which StanzaSeverity assumes."
		);
	}

	[TestMethod]
	public void TheStrongLevelIsTheRulesTheRepositoryRuns()
	{
		string[] expected =
		[
			.. ReadRepositoryPins()
				.Where(pin =>
					pin.Value != PresetFiles.NoRules
					&& !PresetFiles.MarkupRules.Contains(pin.Key)
					&& !s_optInRulesTheRepositoryRuns.Contains(pin.Key)
				)
				.Select(pin => pin.Key)
				.Union(s_strongRulesTheRepositoryRunsOff)
				.Order(StringComparer.Ordinal),
		];

		Assert.AreSequenceEqual(
			expected,
			PresetFiles.ReadTable().ReadEnabled("strong"),

			"The strong level must be the rules the [*.cs] section of .editorconfig runs, but the markup rules, "
				+ "the opt-in rules this guard names as pinned on, and the rules it names as pinned off."
		);
	}

	[TestMethod]
	public void TheCSharpierOverlaySwapsEachStrongRuleThatCSharpierUndoesForItsOtherHalf()
	{
		PresetFiles.LevelTable table = PresetFiles.ReadTable();
		HashSet<string> strong = [.. table.ReadEnabled("strong")];
		string[] undone = ReadUndoneByCSharpier();

		Assert.IsNotEmpty(undone, "The formatters page lists no rule that CSharpier undoes.");

		string[] undoneInStrong = [.. undone.Where(strong.Contains)];

		Assert.AreSequenceEqual(
			undoneInStrong,
			table.ReadChanged(PresetFiles.CSharpierOff),
			"The overlay must turn off each strong rule the formatters page says CSharpier undoes."
		);

		Dictionary<string, string> otherHalves = new(StringComparer.Ordinal);

		foreach ((string first, string second, _) in ReadPairs())
		{
			otherHalves[first] = second;
			otherHalves[second] = first;
		}

		Assert.AreSequenceEqual(
			undoneInStrong
				.Where(otherHalves.ContainsKey)
				.Select(id => otherHalves[id])
				.Order(StringComparer.Ordinal),

			table.ReadChanged(PresetFiles.CSharpierOn),
			"The overlay must turn on the other half of each pair whose strong half it turns off."
		);

		Assert.IsEmpty(
			undone.Where(id => table.ReadLevel(id) is not ("strong" or PresetFiles.OptIn)),
			"A rule that CSharpier undoes is in a level below strong, which the overlay leaves alone."
		);
	}

	[TestMethod]
	public void TheReadmeLevelColumnIsTheLevelTable()
	{
		string readme = File.ReadAllText(
			RepositoryPaths.Below("src", "Nickelony.Stanza.Analyzers", "README.md")
		);

		var rows = ReadmeRuleRow()
			.Matches(readme.ReplaceLineEndings("\n"))
			.ToDictionary(
				match => match.Groups["id"].Value,
				match => match.Groups["level"].Value.Trim(),
				StringComparer.Ordinal
			);

		List<string> problems = [];

		foreach (PresetFiles.LevelRow row in PresetFiles.ReadTable().Rows)
		{
			string expected = char.ToUpperInvariant(row.Level[0]) + row.Level[1..];

			if (!rows.TryGetValue(row.Id, out string? level))
				problems.Add($"{row.Id} has no row with a Level column.");
			else if (level != expected)
				problems.Add($"{row.Id} says {level}, and levels.txt says {expected}.");
		}

		foreach (string id in PresetFiles.MarkupRules)
		{
			if (!rows.TryGetValue(id, out string? level))
				problems.Add($"{id} has no row with a Level column.");
			else if (level != NoLevel)
				problems.Add($"{id} says {level}, and a markup rule says {NoLevel}.");
		}

		GuardAssert.None(
			problems,
			"The Level column of the analyzer readme differs from levels.txt"
		);
	}

	[TestMethod]
	public void TheRulesThatNeverSettleBesideCSharpierStayOptInAtEveryLevel()
	{
		PresetFiles.LevelTable table = PresetFiles.ReadTable();
		HashSet<string> strongWithCSharpier = ReadStrongWithCSharpier(table);

		DiagnosticDescriptor[] descriptors =
		[
			.. RoslynTestHarness.AllAnalyzers.Select(analyzer =>
				analyzer.SupportedDiagnostics.Single()
			),
		];

		List<string> problems = [];

		foreach (string id in s_neverSettle)
		{
			if (table.ReadLevel(id) != PresetFiles.OptIn)
			{
				problems.Add(
					$"{id} is {table.ReadLevel(id)} in levels.txt, not {PresetFiles.OptIn}."
				);
			}

			foreach (string level in PresetFiles.Levels)
			{
				if (table.ReadEnabled(level).Contains(id))
					problems.Add($"{id} is enabled at {level}.");
			}

			if (strongWithCSharpier.Contains(id))
				problems.Add($"{id} is enabled at strong with the csharpier formatter.");

			if (descriptors.Single(descriptor => descriptor.Id == id).IsEnabledByDefault)
				problems.Add($"{id} ships enabled.");
		}

		GuardAssert.None(
			problems,
			"A rule that never settles beside CSharpier is not opt-in everywhere"
		);
	}

	private static string[] ReadRuleIds()
	{
		return
		[
			.. RoslynTestHarness
				.AllAnalyzers.Select(analyzer => analyzer.SupportedDiagnostics.Single().Id)
				.Where(id => !PresetFiles.MarkupRules.Contains(id))
				.Order(StringComparer.Ordinal),
		];
	}

	private static HashSet<string> ReadStrongWithCSharpier(PresetFiles.LevelTable table)
	{
		HashSet<string> enabled = [.. table.ReadEnabled("strong")];

		enabled.ExceptWith(table.ReadChanged(PresetFiles.CSharpierOff));
		enabled.UnionWith(table.ReadChanged(PresetFiles.CSharpierOn));

		return enabled;
	}

	// The rule pins of the [*.cs] section of the repository's .editorconfig, not those for tests.
	private static Dictionary<string, string> ReadRepositoryPins()
	{
		Dictionary<string, string> pins = new(StringComparer.Ordinal);
		string section = string.Empty;

		foreach (string line in File.ReadAllLines(RepositoryPaths.Below(".editorconfig")))
		{
			string text = line.Trim();

			if (text.StartsWith('['))
				section = text;
			else if (section == "[*.cs]" && PinLine().Match(text) is { Success: true } pin)
				pins[pin.Groups["id"].Value] = pin.Groups["severity"].Value;
		}

		Assert.IsNotEmpty(pins, "The [*.cs] section of .editorconfig pins no rule.");

		return pins;
	}

	// The pair table of the gap owners page, which the site reads too.
	private static List<(string First, string Second, string DefaultHalf)> ReadPairs()
	{
		string[] lines = ReadPage("Which-rule-owns-which-gap.md");
		int start = Array.IndexOf(lines, "## Pairs and their default halves");

		Assert.IsGreaterThanOrEqualTo(0, start, "The gap owners page has no pair table.");

		List<(string, string, string)> pairs = [];

		foreach (
			string line in lines
				.Skip(start + 1)
				.TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal))
		)
		{
			string[] cells = line.Split('|');

			if (cells.Length < 4)
				continue;

			string[] halves =
			[
				.. LinkedRule().Matches(cells[2]).Select(match => match.Groups["id"].Value),
			];

			Match named = NamedRule().Match(cells[3]);

			if (halves.Length == 2 && named.Success)
				pairs.Add((halves[0], halves[1], named.Groups["id"].Value));
		}

		Assert.IsNotEmpty(pairs, "The pair table holds no row.");

		return pairs;
	}

	// The rules the site marks as undone by CSharpier: those the section links, and those whose page links it.
	// A markup rule is left out, since no level holds one for the overlay to swap.
	private static string[] ReadUndoneByCSharpier()
	{
		string[] lines = ReadPage("Working-with-formatters.md");
		int start = Array.IndexOf(lines, UndoneSection);

		Assert.IsGreaterThanOrEqualTo(
			0,
			start,
			"The formatters page has no section on layout that CSharpier undoes."
		);

		string section = string.Join(
			'\n',

			lines
				.Skip(start + 1)
				.TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal))
		);

		HashSet<string> undone =
		[
			.. RuleLink().Matches(section).Select(match => match.Groups["id"].Value),
		];

		foreach (
			string path in Directory.GetFiles(RepositoryPaths.Below("docs", "rules"), "ZA*.md")
		)
		{
			if (File.ReadAllText(path).Contains(UndoneAnchor, StringComparison.Ordinal))
				undone.Add(Path.GetFileName(path)[..6]);
		}

		return
		[
			.. undone
				.Where(id => !PresetFiles.MarkupRules.Contains(id))
				.Order(StringComparer.Ordinal),
		];
	}

	private static string[] ReadPage(string name) =>
		File.ReadAllText(RepositoryPaths.Below("docs", name)).ReplaceLineEndings("\n").Split('\n');
}
