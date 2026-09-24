using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Core.DocComments;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>Guards that the pages, the readmes and the tracking rows agree with the code.</summary>
[TestClass]
[TestCategory("Repository")]
public sealed class RuleDocumentationGuardTests
{
	// The markup rules `stanza-markup` applies unless a file disables them, though the analyzer ships them off.
	private static readonly string[] s_appliedByMarkupTool = ["ZA0101", "ZA0104", "ZA0105"];

	[TestMethod]
	public void ThePairTableNamesEachPairAndItsDefaultHalf()
	{
		string[] rows =
		[
			.. File.ReadLines(RepositoryPaths.Below("docs", "Which-rule-owns-which-gap.md"))
				.SkipWhile(line => line != "## Pairs and their default halves")
				.Skip(1)
				.TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal))
				.Where(line => line.StartsWith("| ", StringComparison.Ordinal))
				.Skip(2),
		];

		GapPair[] pairs =
		[
			.. StanzaRules.All.Select(rule => rule.Pair).OfType<GapPair>().Distinct(),
		];

		Assert.HasCount(pairs.Length, rows, "The pair table does not hold one row for each pair.");

		foreach (GapPair pair in pairs)
		{
			string? row = rows.SingleOrDefault(line =>
				line.Contains($"[{pair.First.Id}]", StringComparison.Ordinal)
				&& line.Contains($"[{pair.Second.Id}]", StringComparison.Ordinal)
			);

			Assert.IsNotNull(
				row,
				$"The pair table has no row for {pair.First.Id} and {pair.Second.Id}."
			);

			string owners = row.Split('|')[3].Trim();

			Assert.StartsWith(
				$"`{pair.DefaultHalf.Id}`",
				owners,
				$"The pair table names another default half for {pair.First.Id}."
			);

			if (pair.OwnerWhenBothEnabled != pair.DefaultHalf)
				Assert.Contains($"`{pair.OwnerWhenBothEnabled.Id}` when both are enabled", owners);
		}
	}

	[TestMethod]
	public void EveryHelpLinkResolvesToThePageOfItsRule()
	{
		foreach (DiagnosticAnalyzer analyzer in RoslynTestHarness.AllAnalyzers)
		{
			DiagnosticDescriptor descriptor = analyzer.SupportedDiagnostics.Single();

			string page = descriptor.HelpLinkUri[(descriptor.HelpLinkUri.LastIndexOf('/') + 1)..];
			string path = RepositoryPaths.Below("docs", "rules", $"{page}.md");

			Assert.AreEqual(
				PageName(descriptor),
				page,
				$"{descriptor.Id} does not name the page its own title produces."
			);

			Assert.IsTrue(
				File.Exists(path),
				$"{descriptor.Id} links to {page}, which no documentation file backs."
			);

			Assert.AreEqual(
				$"# {descriptor.Id} - {descriptor.Title}",
				File.ReadLines(path).First(),
				$"{descriptor.Id} does not open its page with the heading its link implies."
			);
		}
	}

	private static string PageName(DiagnosticDescriptor descriptor) =>
		$"{descriptor.Id}-{descriptor.Title.ToString(CultureInfo.InvariantCulture).Replace(' ', '-')}";

	[TestMethod]
	public void EveryRulePageStatesTheTierItsDescriptorShipsIn()
	{
		foreach (DiagnosticAnalyzer analyzer in RoslynTestHarness.AllAnalyzers)
		{
			DiagnosticDescriptor descriptor = analyzer.SupportedDiagnostics.Single();
			string path = RepositoryPaths.Below("docs", "rules", $"{PageName(descriptor)}.md");

			Assert.IsTrue(File.Exists(path), $"{descriptor.Id} has no page under docs/rules.");

			string[] prose =
			[
				.. File.ReadLines(path)
					.SkipWhile(line => !line.StartsWith("# ", StringComparison.Ordinal))
					.Skip(1)
					.Where(line => line.Length != 0)
					.Take(1),
			];

			Assert.HasCount(1, prose, $"{descriptor.Id} has no prose under its heading.");

			Assert.AreEqual(
				TierLine(descriptor),
				prose[0],
				$"{descriptor.Id} does not open with the tier it ships in, alone on its line."
			);
		}
	}

	private static string TierLine(DiagnosticDescriptor descriptor)
	{
		string tier = NameTier(descriptor);

		if (descriptor.IsEnabledByDefault)
			return $"**{tier}, on by default.**";

		return s_appliedByMarkupTool.Contains(descriptor.Id, StringComparer.Ordinal)
			? $"**{tier}, off by default in the analyzer and on in `stanza-markup`.**"
			: $"**{tier}, off by default.**";
	}

	private static string NameTier(DiagnosticDescriptor descriptor) =>
		descriptor.DefaultSeverity == DiagnosticSeverity.Info ? "Suggestion" : "Warning";

	private static IEnumerable<string> ReadSharedOptionKeys(string text, string keysFile)
	{
		const string marker = "MarkupKeys.";

		int start = text.IndexOf(marker, StringComparison.Ordinal);

		while (start >= 0)
		{
			int end = start + marker.Length;

			while (end < text.Length && char.IsAsciiLetterOrDigit(text[end]))
				end++;

			string name = text[(start + marker.Length)..end];

			if (
				name.Length > 0
				&& FindSharedKey(keysFile, name) is { } key
				&& key.StartsWith("stanza_", StringComparison.Ordinal)
			)
			{
				yield return key;
			}

			start = text.IndexOf(marker, end, StringComparison.Ordinal);
		}
	}

	private static string? FindSharedKey(string keysFile, string name)
	{
		foreach (string line in keysFile.Split('\n'))
		{
			if (!line.Contains($"string {name} =", StringComparison.Ordinal))
				continue;

			int start = line.IndexOf('"');
			int end = line.IndexOf('"', start + 1);

			return start < 0 || end < 0 ? null : line[(start + 1)..end];
		}

		return null;
	}

	private static IEnumerable<string> ReadOptionKeys(string text)
	{
		int start = text.IndexOf("\"stanza_", StringComparison.Ordinal);

		while (start >= 0)
		{
			int end = text.IndexOf('"', start + 1);

			if (end < 0)
				yield break;

			yield return text[(start + 1)..end];
			start = text.IndexOf("\"stanza_", end, StringComparison.Ordinal);
		}
	}

	private static string SharedKeysPath() =>
		RepositoryPaths.Below("src", "Nickelony.Stanza.Markup.Core", "Options", "MarkupKeys.cs");

	private static IEnumerable<string> ReadDeclaredOptionKeys(string keysFile)
	{
		foreach (string line in keysFile.Split('\n'))
		{
			if (
				!line.Contains("const string ", StringComparison.Ordinal)
				|| line.Contains("Prefix =", StringComparison.Ordinal)
			)
			{
				continue;
			}

			int start = line.IndexOf("\"stanza_", StringComparison.Ordinal);

			if (start < 0)
				continue;

			int end = line.IndexOf('"', start + 1);

			if (end >= 0)
				yield return line[(start + 1)..end];
		}
	}

	[TestMethod]
	public void EveryOptionKeyHasAPageOfItsOwn()
	{
		string optionsFile = File.ReadAllText(
			RepositoryPaths.Below("src", "Nickelony.Stanza.Analyzers", "StanzaOptions.cs")
		);

		string sharedKeysFile = File.ReadAllText(SharedKeysPath());

		string[] keys =
		[
			.. ReadOptionKeys(optionsFile)
				.Concat(ReadSharedOptionKeys(optionsFile, sharedKeysFile))
				.Concat(ReadDeclaredOptionKeys(sharedKeysFile))
				.Distinct(StringComparer.Ordinal)
				.OrderBy(key => key, StringComparer.Ordinal),
		];

		string[] pages =
		[
			.. Directory
				.GetFiles(RepositoryPaths.Below("docs", "options"), "*.md")
				.Select(file => Path.GetFileNameWithoutExtension(file))
				.OrderBy(page => page, StringComparer.Ordinal),
		];

		Assert.AreEqual(
			string.Join(",", keys),
			string.Join(",", pages),
			"The keys the products read and the pages under docs/options do not line up."
		);
	}

	[TestMethod]
	public void EveryOptionPageOpensWithItsKeyHeading()
	{
		foreach (
			string file in Directory.GetFiles(RepositoryPaths.Below("docs", "options"), "*.md")
		)
		{
			string key = Path.GetFileNameWithoutExtension(file);
			string heading = File.ReadLines(file).First();

			Assert.AreEqual(
				$"# {key}",
				heading,
				$"docs/options/{key}.md does not open with its key."
			);
		}
	}

	[TestMethod]
	public void TheOptionTableNamesEachKeyWithItsDefault()
	{
		string optionsFile = File.ReadAllText(
			RepositoryPaths.Below("src", "Nickelony.Stanza.Analyzers", "StanzaOptions.cs")
		);

		string sharedKeysFile = File.ReadAllText(SharedKeysPath());

		HashSet<string> keys = new(
			ReadOptionKeys(optionsFile)
				.Concat(ReadSharedOptionKeys(optionsFile, sharedKeysFile))
				.Concat(ReadDeclaredOptionKeys(sharedKeysFile)),

			StringComparer.Ordinal
		);

		Dictionary<string, string> defaults = ReadOptionDefaults(
			StanzaOptions.From(new EmptyOptions())
		);

		HashSet<string> rowKeys = new(StringComparer.Ordinal);
		int sampleRows = 0;

		foreach (string line in File.ReadAllLines(ReadmePath()))
		{
			if (line.StartsWith("stanza_", StringComparison.Ordinal))
			{
				int separator = line.IndexOf('=');

				if (
					separator < 0
					|| !defaults.TryGetValue(line[..separator].Trim(), out string? sample)
				)
				{
					continue;
				}

				sampleRows++;

				Assert.AreEqual(
					Normalize(sample),
					Normalize(line[(separator + 1)..]),
					$"The configuration sample of the readme does not write the default {line[..separator].Trim()}. Change the sample line to the default the reader resolves."
				);

				continue;
			}

			string[] columns = [.. line.Split('|').Select(column => column.Trim())];

			if (
				columns.Length != 4
				|| !columns[1].StartsWith("[`stanza_", StringComparison.Ordinal)
			)
			{
				continue;
			}

			int open = columns[1].IndexOf('`', StringComparison.Ordinal);
			string key = columns[1][(open + 1)..columns[1].IndexOf('`', open + 1)];

			rowKeys.Add(key);

			Assert.Contains(
				key,
				keys,
				$"The readme lists {key}, which the options reader never reads. Remove its row, or rename the key to one the reader reads."
			);

			Assert.IsTrue(
				defaults.TryGetValue(key, out string? resolved),
				$"The readme lists {key}, which no default is mapped for. Remove its row, or add the key to ReadOptionDefaults."
			);

			Assert.EndsWith(
				Normalize(resolved),
				Normalize(columns[2]),
				$"The default cell of {key} in the readme does not end with the default the reader resolves. Change the cell to that default."
			);
		}

		foreach (string key in defaults.Keys)
		{
			Assert.Contains(
				key,
				rowKeys,
				$"The option table of the readme has no row for {key}, which the options reader resolves. Add its row."
			);
		}

		Assert.IsGreaterThan(
			0,
			sampleRows,
			"The configuration sample of the readme was not found."
		);
	}

	[TestMethod]
	public void TheConfigurationSampleNamesOnlyRulesThatShipInItsTier()
	{
		HashSet<string> expectedOn = new(StringComparer.Ordinal);
		HashSet<string> expectedOff = new(StringComparer.Ordinal);

		foreach (DiagnosticAnalyzer analyzer in RoslynTestHarness.AllAnalyzers)
		{
			DiagnosticDescriptor descriptor = analyzer.SupportedDiagnostics.Single();

			_ = descriptor.IsEnabledByDefault
				? expectedOn.Add(descriptor.Id)
				: expectedOff.Add(descriptor.Id);
		}

		List<string> on = [];
		List<string> off = [];

		HashSet<string> seen = new(StringComparer.Ordinal);

		bool inSection = false;
		bool inSample = false;

		foreach (string line in File.ReadAllLines(ReadmePath()))
		{
			if (line.StartsWith("## ", StringComparison.Ordinal))
			{
				if (inSection)
					break;

				inSection = line.StartsWith("## Configuration", StringComparison.Ordinal);
				continue;
			}

			if (!inSection)
				continue;

			if (line.StartsWith("```", StringComparison.Ordinal))
			{
				if (inSample)
					break;

				inSample = true;
				continue;
			}

			if (!inSample)
				continue;

			string candidate = line.TrimStart();
			bool commented = candidate.StartsWith('#');

			if (commented)
				candidate = candidate[1..].TrimStart();

			string[] parts = candidate.Split('.');

			if (parts.Length < 3 || !parts[0].Equals("dotnet_diagnostic", StringComparison.Ordinal))
				continue;

			string id = parts[1];

			if (!IsRuleId(id))
				continue;

			Assert.IsTrue(
				seen.Add(id),
				$"{id} is listed more than once in the configuration sample of the readme."
			);

			if (commented)
				off.Add(id);
			else
				on.Add(id);
		}

		Assert.IsGreaterThan(0, on.Count, "The configuration sample has no active line.");
		Assert.IsGreaterThan(0, off.Count, "The configuration sample has no commented line.");

		Assert.IsTrue(
			on.All(expectedOn.Contains),
			"An active line of the configuration sample names a rule that does not ship on."
		);

		Assert.IsTrue(
			off.All(expectedOff.Contains),
			"A commented line of the configuration sample names a rule that does not ship off."
		);

		AssertAscending(on, "On by default");
		AssertAscending(off, "Off by default");
	}

	private static void AssertAscending(List<string> ids, string block)
	{
		for (int index = 1; index < ids.Count; index++)
		{
			Assert.IsLessThan(
				ids[index],
				ids[index - 1],
				$"The {block} block of the readme is not in ascending order at {ids[index]}."
			);
		}
	}

	[TestMethod]
	public void EveryRuleIsListedInTheReadmeAsItShips()
	{
		Dictionary<string, string> readme = new(StringComparer.Ordinal);

		foreach ((string id, _, _, string shipsAs) in ReadmeRuleRows())
			readme[id] = shipsAs;

		foreach (DiagnosticAnalyzer analyzer in RoslynTestHarness.AllAnalyzers)
		{
			DiagnosticDescriptor descriptor = analyzer.SupportedDiagnostics.Single();

			Assert.IsTrue(
				readme.TryGetValue(descriptor.Id, out string? shipsAs),
				$"{descriptor.Id} is missing from the rule table of the readme."
			);

			Assert.AreEqual(
				ExpectedShipsAs(descriptor),
				shipsAs,
				$"{descriptor.Id} disagrees with its descriptor in the ships-as column of the readme."
			);
		}
	}

	[TestMethod]
	public void EveryRuleRowSitsUnderAGroupHeadingAndEveryRulePageInExactlyOneGroup()
	{
		Dictionary<string, string> groupOfRule = new(StringComparer.Ordinal);
		string? group = null;
		bool inRules = false;

		foreach (string line in File.ReadAllLines(ReadmePath()))
		{
			if (line.StartsWith("## ", StringComparison.Ordinal))
			{
				inRules = line.TrimEnd() == "## Rules";
				group = null;
				continue;
			}

			if (!inRules)
				continue;

			if (line.StartsWith("### ", StringComparison.Ordinal))
			{
				group = line[4..].Trim();
				continue;
			}

			string[] columns = [.. line.Split('|').Select(column => column.Trim())];

			if (columns.Length < 2 || !IsRuleId(columns[1].Trim('`')))
				continue;

			string id = columns[1].Trim('`');

			Assert.IsNotNull(
				group,
				$"{id} sits above the first group heading of the readme rule tables. Move its row under a ### heading."
			);

			Assert.IsTrue(
				groupOfRule.TryAdd(id, group),
				$"{id} has a row in two groups of the readme, '{groupOfRule.GetValueOrDefault(id)}' and '{group}'. Keep one row."
			);
		}

		string rulePages = RepositoryPaths.Below("docs", "rules");

		foreach (string path in Directory.EnumerateFiles(rulePages, "ZA*.md"))
		{
			string name = Path.GetFileName(path);

			Assert.IsTrue(
				groupOfRule.ContainsKey(name[..name.IndexOf('-')]),
				$"The rule page {name} is in no group of the readme rule tables. Add its row under a ### heading."
			);
		}
	}

	[TestMethod]
	public void EveryRuleOfTheReadmeTableHasItsOwnGapText()
	{
		Dictionary<string, string> ownerOfGap = new(StringComparer.Ordinal);

		foreach ((string id, string gap, _, _) in ReadmeRuleRows())
		{
			string text = LinkText(gap);

			Assert.IsFalse(
				ownerOfGap.TryGetValue(text, out string? owner),
				$"{id} repeats the gap text of {owner} in the readme rule table. Name the seam or subject that {id} owns in its own gap text."
			);

			ownerOfGap[text] = id;
		}
	}

	// The link target carries the rule ID, so only the link text can repeat across rows.
	private static string LinkText(string cell)
	{
		int close = cell.IndexOf("](", StringComparison.Ordinal);

		Assert.IsTrue(
			cell.StartsWith('[') && close > 0,
			$"The gap column '{cell}' of the readme rule table is not a link."
		);

		return cell[1..close];
	}

	private static string ExpectedShipsAs(DiagnosticDescriptor descriptor)
	{
		string tier = NameTier(descriptor);
		return descriptor.IsEnabledByDefault ? tier : $"{tier}, off by default";
	}

	// RS2000 and RS2001 fail the build on a missing row or a changed severity, but neither reads the notes column.
	[TestMethod]
	public void EveryTrackedRowNamesTheTitleOfItsRule()
	{
		Dictionary<string, string> trackedNotes = new(StringComparer.Ordinal);

		foreach ((string id, _, _, string last) in ReadRuleRows(TrackingFilePaths()))
			trackedNotes[id] = last;

		foreach (DiagnosticAnalyzer analyzer in RoslynTestHarness.AllAnalyzers)
		{
			DiagnosticDescriptor descriptor = analyzer.SupportedDiagnostics.Single();

			Assert.AreEqual(
				$"{descriptor.Title}.",
				trackedNotes[descriptor.Id],
				$"{descriptor.Id} disagrees with its descriptor in the notes column."
			);
		}
	}

	[TestMethod]
	public void EveryHelpLinkPointsAtTheRepositoryTheBuildPropertiesName()
	{
		string properties = File.ReadAllText(RepositoryPaths.Below("Directory.Build.props"));

		const string open = "<StanzaRepositoryUrl>";
		const string close = "</StanzaRepositoryUrl>";

		int start = properties.IndexOf(open, StringComparison.Ordinal);

		Assert.IsGreaterThan(
			0,
			start,
			"Directory.Build.props carries no StanzaRepositoryUrl property."
		);

		int end = properties.IndexOf(close, start, StringComparison.Ordinal);

		Assert.IsGreaterThan(start, end, "The StanzaRepositoryUrl property is not closed.");

		string repositoryUrl = properties[(start + open.Length)..end].Trim();

		foreach (DiagnosticAnalyzer analyzer in RoslynTestHarness.AllAnalyzers)
		{
			DiagnosticDescriptor descriptor = analyzer.SupportedDiagnostics.Single();

			Assert.StartsWith(
				$"{repositoryUrl}/",
				descriptor.HelpLinkUri,
				$"{descriptor.Id} links somewhere other than the repository the packages point at."
			);
		}

		foreach (
			string project in new[] { "Nickelony.Stanza.Analyzers", "Nickelony.Stanza.Markup" }
		)
		{
			string projectFile = File.ReadAllText(
				RepositoryPaths.Below("src", project, $"{project}.csproj")
			);

			Assert.DoesNotContain(
				"Nickelony/Stanza",
				projectFile,
				$"{project} spells the repository URL out instead of taking it from the shared build property."
			);
		}

		// The shared property file declares the repository URL
		// and takes its package metadata from `$(StanzaRepositoryUrl)`,
		// so the packages and the help links name the same repository.
		Assert.Contains(
			"$(StanzaRepositoryUrl)",
			properties,
			"The shared package metadata does not take its repository URL from the build property."
		);
	}

	private static string[] TrackingFilePaths()
	{
		return
		[
			RepositoryPaths.Below(
				"src",
				"Nickelony.Stanza.Analyzers",
				"AnalyzerReleases.Shipped.md"
			),

			RepositoryPaths.Below(
				"src",
				"Nickelony.Stanza.Analyzers",
				"AnalyzerReleases.Unshipped.md"
			),
		];
	}

	private static IEnumerable<(string Id, string Gap, string Severity, string Last)> ReadRuleRows(
		params string[] paths
	) => ParseRuleRows(paths.SelectMany(File.ReadAllLines));

	private static IEnumerable<(
		string Id,
		string Gap,
		string Severity,
		string Last
	)> ReadmeRuleRows()
	{
		List<string> lines = [];
		bool inRulesSection = false;

		foreach (string line in File.ReadAllLines(ReadmePath()))
		{
			if (line.StartsWith("## ", StringComparison.Ordinal))
			{
				if (inRulesSection)
					break;

				inRulesSection = line.StartsWith("## Rules", StringComparison.Ordinal);
				continue;
			}

			if (inRulesSection)
				lines.Add(line);
		}

		return ParseRuleRows(lines);
	}

	private static IEnumerable<(string Id, string Gap, string Severity, string Last)> ParseRuleRows(
		IEnumerable<string> lines
	)
	{
		foreach (string line in lines)
		{
			string[] columns = [.. line.Split('|').Select(column => column.Trim())];
			int first = columns[0].Length == 0 ? 1 : 0;

			if (columns.Length < first + 4)
				continue;

			string id = columns[first].Trim('`');

			if (!IsRuleId(id))
				continue;

			// The second column is the gap in the readme and the category in a release file.
			yield return (id, columns[first + 1], columns[first + 2], GetLastColumn(columns));
		}
	}

	private static string GetLastColumn(string[] columns)
	{
		int index = columns.Length - 1;

		while (index > 0 && columns[index].Length == 0)
			index--;

		return columns[index];
	}

	private static bool IsRuleId(string value)
	{
		return value.Length == 6
			&& value.StartsWith("ZA", StringComparison.Ordinal)
			&& value[2..].All(char.IsAsciiDigit);
	}

	private static string ReadmePath() =>
		RepositoryPaths.Below("src", "Nickelony.Stanza.Analyzers", "README.md");

	private static Dictionary<string, string> ReadOptionDefaults(StanzaOptions options)
	{
		DocLayoutOptions doc = options.DocLayout!.Value;

		return new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["stanza_assertion_roots"] = string.Join(
				",",
				options.AssertionRoots.OrderBy(root => root, StringComparer.Ordinal)
			),

			["stanza_max_line_length"] = $"{options.MaxLineLength}",
			["stanza_doc_max_line_length"] = $"{doc.ContentWidth}",
			["stanza_named_argument_count"] = $"{options.NamedArgumentCount}",
			["stanza_named_argument_match"] = ReadNamedArgumentMatch(options),
			["stanza_named_argument_case"] = ReadNamedArgumentCase(options),
			["stanza_multiline_body_trigger"] = ReadMultiLineTrigger(options),
			["stanza_tab_width"] = $"{options.TabColumns}",
			["stanza_markup_scope"] = options.Markup.Policy.ContainersOnly ? "containers" : "all",
			["stanza_markup_max_blank_lines"] = $"{options.Markup.MaxBlankLines}",
			["stanza_doc_expand"] = DescribeDocExpansion(doc),
			["stanza_doc_min_tail_words"] = $"{doc.MinTailWords}",
			["stanza_doc_min_line_words"] = $"{doc.MinLineWords}",
			["stanza_doc_weak_tail_words"] = doc.WeakTailWords?.ToString() ?? "none",
			["stanza_doc_max_overflow_columns"] = $"{doc.MaxOverflowColumns}",
			["stanza_doc_indent_size"] = $"{doc.IndentSize}",
			["stanza_doc_indent_style"] = doc.IndentText ? "nested" : "hanging",
		};
	}

	private static string ReadNamedArgumentMatch(StanzaOptions options) =>
		options.AllowsNamedArgumentShorthand ? "allow" : "require";

	private static string ReadNamedArgumentCase(StanzaOptions options) =>
		options.NamedArgumentComparison == StringComparison.Ordinal ? "sensitive" : "insensitive";

	private static string ReadMultiLineTrigger(StanzaOptions options) =>
		options.MultiLineTrigger.ToString().ToLowerInvariant();

	private static string DescribeDocExpansion(DocLayoutOptions doc) =>
		doc.Expansion.ToString().ToLowerInvariant();

	private static string Normalize(string value) =>
		string.Concat(value.Where(char.IsAsciiLetterOrDigit)).ToLowerInvariant();

	private sealed class EmptyOptions : AnalyzerConfigOptions
	{
		public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
		{
			value = null;
			return false;
		}
	}

	[TestMethod]
	public void EveryDescriptorKeepsTheIdItsPageIsNamedAfter()
	{
		string[] pages =
		[
			.. Directory
				.EnumerateFiles(RepositoryPaths.Below("docs", "rules"), "ZA*.md")
				.Select(path => Path.GetFileName(path)[..Path.GetFileName(path).IndexOf('-')])
				.OrderBy(id => id, StringComparer.Ordinal),
		];

		string[] declared =
		[
			.. TestRuleSets
				.Declared.Select(rule => rule.Descriptor.Id)
				.OrderBy(id => id, StringComparer.Ordinal),
		];

		Assert.AreSequenceEqual(
			pages,
			declared,

			"The diagnostic ids the analyzer declares are not the ids of the rule pages, so a rule was "
				+ "added or renumbered without its page moving with it."
		);
	}
}
