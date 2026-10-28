using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>Runs each page example through its rule and compares it with the page's text.</summary>
[TestClass]
[TestCategory("Repository")]
public sealed class RulePageExampleTests
{
	private const string Fence = "```csharp";
	private const string BeforeMarker = "// Before";
	private const string AfterMarker = "// After";
	private const string UnchangedMarker = "// Unchanged";
	private const string SilentNote = "the rule did not fire.";

	// An example is a fragment, so it is tried as written and then inside a class and inside a method.
	// The context members give the call rules a method to bind the arguments to.
	private static readonly (string Name, Func<string, string, string> Wrap)[] s_wrappers =
	[
		("as written", (snippet, _) => snippet + "\n"),
		("class members", (snippet, context) => $"class C\n{{\n{context}{Indent(snippet)}\n}}\n"),

		(
			"method statements",
			(snippet, context) =>
				$"class C\n{{\n{context}\tvoid M()\n\t{{\n{Indent(Indent(snippet))}\n\t}}\n}}\n"
		),
	];

	// The members a call rule binds against, keyed by rule id.
	private static readonly Dictionary<string, string> s_context = new(StringComparer.Ordinal)
	{
		["ZA0050"] = "\tstatic void Draw(int width, int height, bool smooth, string path) { }\n\n",

		["ZA0051"] =
			"\tstatic void Draw(int width, int height, bool smooth, string path) { }\n\n"
			+ "\tint width;\n\tint height;\n\tstring path;\n\n",
	};

	// The option pages whose examples are run, with the rule each page's examples show.
	private static readonly Dictionary<string, string> s_optionPages = new(StringComparer.Ordinal)
	{
		["stanza_markup_max_blank_lines"] = "ZA0104",
		["stanza_max_line_length"] = "ZA0041",
		["stanza_max_loose_statements"] = "ZA0052",
		["stanza_named_argument_count"] = "ZA0050",
	};

	// An option an example needs beyond the defaults, keyed by its page and a part of its marker.
	private static readonly (
		string Page,
		string Marker,
		IReadOnlyDictionary<string, string> Options
	)[] s_options =
	[
		// The page sets the width to 60, because its member fits at the default.
		(
			"ZA0044",
			string.Empty,
			new Dictionary<string, string> { ["stanza_max_line_length"] = "60" }
		),

		(
			"stanza_max_line_length",
			"at a width of 40",
			new Dictionary<string, string> { ["stanza_max_line_length"] = "40" }
		),

		(
			"stanza_max_loose_statements",
			"at a limit of 2",
			new Dictionary<string, string> { ["stanza_max_loose_statements"] = "2" }
		),
	];

	// The other rules whose output an example shows, keyed by its page and a part of its marker.
	private static readonly (string Page, string Marker, string[] Rules)[] s_alsoRun =
	[
		// ZA0001 writes the gap above the first property, and ZA0002 the one above the method.
		("ZA0002", string.Empty, ["ZA0001"]),
		("ZA0044", "with ZA0046 enabled", ["ZA0046"]),

		// ZA0001 would write back the blank line above the calls, so the rule has to leave it.
		("ZA0052", string.Empty, ["ZA0001"]),
	];

	public static IEnumerable<object[]> AfterExamples { get; } =
	[
		.. ReadAll()
			.Where(example => example.After is not null)
			.Select(example => new object[] { example.Name, example }),
	];

	public static IEnumerable<object[]> UnchangedExamples { get; } =
	[
		.. ReadAll()
			.Where(example => example.After is null)
			.Select(example => new object[] { example.Name, example }),
	];

	[TestMethod]
	[DynamicData(
		nameof(AfterExamples),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task TheExampleFixesToItsAfterBlock(string name, Example example)
	{
		string after = example.After!;

		Assert.AreNotEqual(
			example.Before,
			after,
			$"{name}: the Before and After blocks are the same."
		);

		List<string> outcomes = [];
		string? match = null;

		foreach ((string wrapperName, Func<string, string, string> wrap) in s_wrappers)
		{
			(string? fixedText, string note) = await TryAsync(
				example,
				wrap(example.Before, example.Context)
			);

			if (fixedText == wrap(after, example.Context))
			{
				match = wrapperName;
				break;
			}

			outcomes.Add($"[{wrapperName}] {note}");
		}

		Assert.IsNotNull(
			match,
			$"{name}: no wrapper turns Before into After. {string.Join(" ", outcomes)}"
		);
	}

	[TestMethod]
	[DynamicData(
		nameof(UnchangedExamples),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	[CrossCutting]
	public async Task TheUnchangedExampleIsLeftAsWritten(string name, Example example)
	{
		List<string> reported = [];
		int silent = 0;

		// A wrapper that cannot bind the example proves nothing, so the rule has to stay silent in every wrapper that can.
		foreach ((string wrapperName, Func<string, string, string> wrap) in s_wrappers)
		{
			(string? fixedText, string note) = await TryAsync(
				example,
				wrap(example.Before, example.Context)
			);

			if (fixedText is not null)
				reported.Add($"[{wrapperName}] {note}");
			else if (note == SilentNote)
				silent++;
		}

		Assert.IsEmpty(
			reported,
			$"{name}: the rule reports the example the page shows unchanged. {string.Join(" ", reported)}"
		);

		Assert.IsGreaterThan(0, silent, $"{name}: no wrapper ran the rule over the example.");
	}

	// Returns the fixed text only when the page's own rule reported, so a rule that stays silent fails.
	private static async Task<(string? FixedText, string Note)> TryAsync(
		Example example,
		string source
	)
	{
		string[] ruleIds = [example.Rule, .. example.AlsoRun];

		try
		{
			AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
				source,

				new RunOptions([
					.. RoslynTestHarness.AllAnalyzers.Where(analyzer =>
						analyzer.SupportedDiagnostics.Any(descriptor =>
							ruleIds.Contains(descriptor.Id)
						)
					),
				])
				{
					AnalyzerConfigOptions = example.Options,

					DiagnosticOptions = ruleIds.ToDictionary(
						id => id,
						_ => ReportDiagnostic.Warn,
						StringComparer.Ordinal
					),

					RequireCompiles = false,
				}
			);

			return result.Diagnostics.Any(diagnostic => diagnostic.Id == example.Rule)
				? (result.FixedText, $"fixed to {RoslynTestHarness.Describe(result.FixedText)}")
				: (null, SilentNote);
		}
		catch (AssertFailedException exception) when (exception is not AnalyzerCrashException)
		{
			// A wrapper that parses badly can fail to settle; the next wrapper may still fit.
			// An analyzer crash is a defect whatever the wrapper, so it fails the row.
			return (null, exception.Message.Split('\n')[0]);
		}
	}

	private static List<Example> ReadAll()
	{
		List<Example> examples = [];

		foreach (
			string path in Directory.EnumerateFiles(
				RepositoryPaths.Below("docs", "rules"),
				"ZA*.md"
			)
		)
		{
			string page = Path.GetFileName(path)[..6];
			examples.AddRange(ReadPage(path, page, rule: page));
		}

		foreach ((string page, string rule) in s_optionPages)
		{
			examples.AddRange(
				ReadPage(RepositoryPaths.Below("docs", "options", $"{page}.md"), page, rule)
			);
		}

		return examples;
	}

	private static List<Example> ReadPage(string path, string page, string rule)
	{
		string[] lines = File.ReadAllText(path)
			.Replace("\r\n", "\n", StringComparison.Ordinal)
			.Split('\n');

		List<Example> examples = [];
		int start = 0;

		while (true)
		{
			int open = Array.FindIndex(lines, start, line => line == Fence);

			if (open < 0)
				break;

			int close = Array.FindIndex(
				lines,
				open + 1,
				line => line.StartsWith("```", StringComparison.Ordinal)
			);

			examples.AddRange(ReadBlock(lines[(open + 1)..close], page, rule));
			start = close + 1;
		}

		Assert.IsNotEmpty(examples, $"{page}: the page has no example the harness can run.");

		// A page with one example keeps its own name, and a page with several numbers them.
		return
		[
			.. examples.Select(
				(example, index) =>
					example with
					{
						Name = examples.Count == 1 ? page : $"{page} example {index + 1}",
					}
			),
		];
	}

	// Each example is the text under a Before marker and one After marker, or the text under an Unchanged marker.
	private static IEnumerable<Example> ReadBlock(string[] block, string page, string rule)
	{
		int[] markers =
		[
			.. Enumerable.Range(0, block.Length).Where(index => IsMarker(block[index])),
		];

		string? before = null;

		for (int position = 0; position < markers.Length; position++)
		{
			int end = position + 1 < markers.Length ? markers[position + 1] : block.Length;

			string marker = block[markers[position]];
			string text = Join(block[(markers[position] + 1)..end]);

			if (marker.StartsWith(BeforeMarker, StringComparison.Ordinal))
				before = text;
			else if (marker.StartsWith(UnchangedMarker, StringComparison.Ordinal))
				yield return Create(page, rule, marker, before: text, after: null);
			else if (before is not null)
				yield return Create(page, rule, marker, before, after: text);
		}
	}

	private static bool IsMarker(string line)
	{
		return line.StartsWith(BeforeMarker, StringComparison.Ordinal)
			|| line.StartsWith(AfterMarker, StringComparison.Ordinal)
			|| line.StartsWith(UnchangedMarker, StringComparison.Ordinal);
	}

	private static Example Create(
		string page,
		string rule,
		string marker,
		string before,
		string? after
	)
	{
		string[] alsoRun =
		[
			.. s_alsoRun
				.Where(entry =>
					entry.Page == page && marker.Contains(entry.Marker, StringComparison.Ordinal)
				)
				.SelectMany(entry => entry.Rules),
		];

		IReadOnlyDictionary<string, string>? options = s_options
			.Where(entry =>
				entry.Page == page && marker.Contains(entry.Marker, StringComparison.Ordinal)
			)
			.Select(entry => entry.Options)
			.FirstOrDefault();

		return new Example(
			Name: page,
			Rule: rule,
			Before: before,
			After: after,
			AlsoRun: alsoRun,
			Options: options,
			Context: s_context.GetValueOrDefault(rule, string.Empty)
		);
	}

	private static string Join(IEnumerable<string> lines) => string.Join("\n", lines).Trim('\n');

	private static string Indent(string snippet)
	{
		return string.Join(
			"\n",
			snippet.Split('\n').Select(line => line.Length == 0 ? line : "\t" + line)
		);
	}

	/// <summary>One example of a page: its text before and after, and how to run it.</summary>
	public sealed record Example(
		string Name,
		string Rule,
		string Before,
		string? After,
		string[] AlsoRun,
		IReadOnlyDictionary<string, string>? Options,
		string Context
	);
}
