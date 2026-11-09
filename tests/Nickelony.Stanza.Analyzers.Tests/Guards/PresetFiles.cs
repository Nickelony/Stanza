using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Nickelony.Stanza.Analyzers.MarkupLiterals;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Guards;

/// <summary>Reads the level table of the analyzer package and the files it writes.</summary>
internal static partial class PresetFiles
{
	internal const string NoRules = "none";
	internal const string CSharpier = "csharpier";
	internal const string OptIn = "opt-in";
	internal const string CSharpierOff = "csharpier-off";
	internal const string CSharpierOn = "csharpier-on";

	internal const int LevelRank = -100;
	internal const int OverlayRank = -99;

	/// <summary>The levels in nesting order: each holds the rules of the one before it.</summary>
	internal static string[] Levels { get; } = ["minimal", "moderate", "strong"];
	internal static string[] Severities { get; } = ["suggestion", "warning", "error"];

	/// <summary>The markup rules, which no level holds: their own line enables them.</summary>
	/// <remarks>
	/// A preset reaches the analyzer alone, and stanza-markup reads the same line.
	/// </remarks>
	internal static IReadOnlySet<string> MarkupRules { get; } =
		new HashSet<string>(
			RoslynTestHarness
				.AllAnalyzers.Where(analyzer =>
					analyzer.GetType().Namespace == typeof(MarkupLiteral).Namespace
				)
				.Select(analyzer => analyzer.SupportedDiagnostics.Single().Id),

			StringComparer.Ordinal
		);

	[GeneratedRegex(
		@"^dotnet_diagnostic\.(?<id>[^.\s]+)\.severity\s*=\s*(?<severity>\S+)$",
		RegexOptions.CultureInvariant
	)]
	private static partial Regex RuleLine();

	[GeneratedRegex(@"^global_level\s*=\s*(?<level>-?\d+)$", RegexOptions.CultureInvariant)]
	private static partial Regex GlobalLevelLine();

	internal static string DirectoryPath() =>
		RepositoryPaths.Below("src", "Nickelony.Stanza.Analyzers.Package", "build", "presets");

	internal static string[] ReadNames()
	{
		return
		[
			.. Directory
				.GetFiles(DirectoryPath(), "*.globalconfig")
				.Select(path => Path.GetFileNameWithoutExtension(path))
				.Order(StringComparer.Ordinal),
		];
	}

	internal static Preset Read(string name)
	{
		HashSet<string> rules = new(StringComparer.Ordinal);
		List<string> problems = [];
		bool isGlobal = false;
		int? level = null;

		foreach (string line in File.ReadAllLines(ToPath(name)))
		{
			string text = line.Trim();

			if (text.Length == 0 || text.StartsWith('#'))
				continue;

			if (RuleLine().Match(text) is { Success: true } rule)
			{
				string id = rule.Groups["id"].Value;

				if (!rules.Add(id))
					problems.Add($"{name}: {id} is stated twice.");
			}
			else if (text == "is_global = true")
			{
				isGlobal = true;
			}
			else if (GlobalLevelLine().Match(text) is { Success: true } global)
			{
				level = int.Parse(global.Groups["level"].Value, CultureInfo.InvariantCulture);
			}
			else
			{
				problems.Add($"{name}: '{text}' is neither a rule line nor a comment.");
			}
		}

		return new(Problems: problems, IsGlobal: isGlobal, GlobalLevel: level);
	}

	internal static LevelTable ReadTable()
	{
		string path = RepositoryPaths.Below(
			"src",
			"Nickelony.Stanza.Analyzers.Package",
			"levels.txt"
		);

		List<LevelRow> rows = [];
		List<string> problems = [];

		foreach (string line in File.ReadAllLines(path))
		{
			if (line.Length == 0 || line.StartsWith('#'))
				continue;

			string[] cells = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			string? change = cells.Length > 2 ? cells[2] : null;

			if (cells.Length is < 2 or > 3 || (cells[1] != OptIn && !Levels.Contains(cells[1])))
			{
				problems.Add($"'{line}' is not a rule ID and a level or {OptIn}.");
			}
			else if (change is not (null or CSharpierOff or CSharpierOn))
			{
				problems.Add(
					$"'{line}' names a change other than {CSharpierOff} or {CSharpierOn}."
				);
			}
			else
			{
				rows.Add(new(cells[0], cells[1], change));
			}
		}

		return new(rows, problems);
	}

	/// <summary>Builds the text of each file the package ships, by its name.</summary>
	/// <remarks>
	/// The moderate level at suggestion is what the descriptors ship, so no file is built for it.
	/// </remarks>
	internal static Dictionary<string, string> BuildFiles(LevelTable table)
	{
		Dictionary<string, string> files = new(StringComparer.Ordinal)
		{
			[NoRules] = BuildFile(
				$"# The {NoRules} level of Nickelony.Stanza.Analyzers, which turns every rule off.",
				LevelRank,
				table.Rows.Select(row => (row.Id, NoRules))
			),
		};

		foreach (string severity in Severities)
		{
			foreach (string level in Levels)
			{
				if (level == "moderate" && severity == "suggestion")
					continue;

				HashSet<string> enabled = [.. table.ReadEnabled(level)];

				files[$"{level}.{severity}"] = BuildFile(
					$"# The {level} level of Nickelony.Stanza.Analyzers at {severity}.",
					LevelRank,

					table.Rows.Select(row =>
						(row.Id, enabled.Contains(row.Id) ? severity : NoRules)
					)
				);
			}

			files[$"{CSharpier}.{severity}"] = BuildFile(
				$"# The {CSharpier} formatter of Nickelony.Stanza.Analyzers at {severity}, "
					+ "which the package adds over the strong level.\n"
					+ "# It turns off each rule that CSharpier undoes when it runs on its own, "
					+ "and turns on the other half of each such pair.",

				OverlayRank,

				table
					.Rows.Where(row => row.CSharpier is not null)
					.Select(row => (row.Id, row.CSharpier == CSharpierOn ? severity : NoRules))
			);
		}

		return files;
	}

	/// <summary>Writes the files, and deletes each preset file the table does not build.</summary>
	internal static void Write(Dictionary<string, string> files)
	{
		foreach (string name in ReadNames().Except(files.Keys))
			File.Delete(ToPath(name));

		foreach ((string name, string text) in files)
			File.WriteAllText(ToPath(name), text.ReplaceLineEndings("\r\n"));
	}

	internal static string ToPath(string name) =>
		Path.Combine(DirectoryPath(), $"{name}.globalconfig");

	private static string BuildFile(
		string heading,
		int rank,
		IEnumerable<(string Id, string Severity)> rules
	)
	{
		StringBuilder text = new();

		_ = text.Append(heading).Append('\n');

		_ = text.Append(
			"# Written from levels.txt. A .editorconfig line wins over this file, "
				+ "and so does a .globalconfig of the project.\n\n"
		);

		_ = text.Append("is_global = true\n");
		_ = text.Append(CultureInfo.InvariantCulture, $"global_level = {rank}\n\n");

		foreach ((string id, string severity) in rules)
		{
			_ = text.Append(
				CultureInfo.InvariantCulture,
				$"dotnet_diagnostic.{id}.severity = {severity}\n"
			);
		}

		return text.ToString();
	}

	/// <summary>A rule, the first level that holds it, and what csharpier does to it.</summary>
	internal sealed record LevelRow(string Id, string Level, string? CSharpier);

	/// <summary>The level table, with each line it cannot read.</summary>
	internal sealed record LevelTable(List<LevelRow> Rows, List<string> Problems)
	{
		/// <summary>The rules of the level and of each level before it.</summary>
		internal string[] ReadEnabled(string level)
		{
			string[] held = [.. Levels.Take(Array.IndexOf(Levels, level) + 1)];

			return
			[
				.. Rows.Where(row => held.Contains(row.Level))
					.Select(row => row.Id)
					.Order(StringComparer.Ordinal),
			];
		}

		internal string[] ReadChanged(string change)
		{
			return
			[
				.. Rows.Where(row => row.CSharpier == change)
					.Select(row => row.Id)
					.Order(StringComparer.Ordinal),
			];
		}

		internal string ReadLevel(string id) => Rows.Single(row => row.Id == id).Level;
	}

	/// <summary>The global config keys of a preset file, and each line it cannot read.</summary>
	internal sealed record Preset(List<string> Problems, bool IsGlobal, int? GlobalLevel);
}
