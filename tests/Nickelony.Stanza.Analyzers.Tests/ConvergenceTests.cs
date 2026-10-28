using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>Fixes seeded layouts and damaged comments until nothing changes.</summary>
[TestClass]
public sealed partial class ConvergenceTests
{
	private const int DefaultSeedCount = 6;

	private const int SoakFirstSeed = 1000;

	private const string SoakVariable = "STANZA_SOAK_SEEDS";

	private static readonly string[] s_importLines = ["using System.Linq;", "using System.Text;"];

	// Each template is one statement, written without its indent.
	// They mix the kinds the grouping rules read, with comments, directives and multi-line statements among them.
	private static readonly Func<int, string[]>[] s_statements =
	[
		index => [$"var variable{index} = {index};"],
		index => [$"int number{index} = {index};"],
		index => [$"int count{index} = {index};"],
		index => [$"string text{index} = \"{index}\";"],
		index => [$"const int constant{index} = {index};"],
		index => [$"loose = {index};"],
		index => ["DoThing();"],
		index => [$"Build({index});"],
		index => ["Assert.That(1);"],
		index => [$"// Note {index}.", $"loose = {index};"],
		index => [$"DoThing(); // Trailing {index}."],
		index => ["Call(", "\t1,", "\t2);"],
		index => [$"#region Group{index}", "DoThing();", "#endregion"],
		index => ["if (loose > 0) DoThing();"],
		index => [$"void Local{index}() {{ }}"],
	];

	// A run is judged against the limit, so the limits take the rule through its quiet and its busy ends.
	private static readonly int[] s_looseLimits = [3, 2, 1, 4];

	// The rules that share a gap with the loose run rule, alone and all together.
	private static readonly DiagnosticDescriptor[][] s_looseRuleSets =
	[
		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns,
			DiagnosticDescriptors.SeparateSameTypeDeclarationGroups,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateSameTypeDeclarationGroups,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateCommentedSibling,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
		],
	];

	// Each template is one member, written without its indent. None of them needs more than the shared support code.
	private static readonly Func<int, string[]>[] s_members =
	[
		index => [$"private int _field{index};"],
		index => [$"public int Property{index} {{ get; set; }}"],
		index => [$"private int _trailing{index}; // trailing {index}"],
		index => [$"/* block {index} */", $"private int _blocked{index};"],
		index => [$"public void Empty{index}()", "{", "}"],
		index => [$"// note {index}", $"public int Noted{index}() => 1;"],
		index => [$"#region Group{index}", $"private int _grouped{index};", "#endregion"],

		index =>
			[$"public int Accessors{index}", "{", "\tget { return 1; }", "\tset { return; }", "}"],

		index =>
			[
				$"public Item Initializer{index}() => new Item",
				"{",
				"\tName = \"a\",",
				"\tCount = 1,",
				"};",
			],

		index =>
			[
				$"public Item Commented{index}() => new Item",
				"{",
				"\tName = \"a\",",
				"\t// The tags stay empty.",
				"\tTags = [],",
				"",
				"\t// The count is one.",
				"\tCount = 1,",
				"};",
			],

		index =>
			[
				$"public void Commented{index}()",
				"{",
				"\tDoThing();",
				"\t// The next step.",
				"\tDoThing();",
				"",
				"\t// The last step.",
				"\treturn;",
				"}",
			],

		index => [$"public List<int> Elements{index}() => [", "\t1,", "\t2,", "];"],

		index =>
			[
				$"public int Arms{index}(int value) => value switch",
				"{",
				"\t1 => 1,",
				"\t2 => 2,",
				"\t_ => 0,",
				"};",
			],

		index =>
			[
				$"public void Statements{index}()",
				"{",
				"\tvar first = 1;",
				"\tDoThing();",
				"\tDoThing();",
				"\treturn;",
				"}",
			],

		index =>
			[
				$"public void Calls{index}()",
				"{",
				"\tCall(",
				"\t\t1,",
				"\t\t2);",
				"\tBuild(",
				"\t\t3);",
				"}",
			],

		index =>
			[
				$"public int Switch{index}(int value)",
				"{",
				"\tswitch (value)",
				"\t{",
				"\t\tcase 1:",
				"\t\t\treturn 1;",
				"\t\tcase 2:",
				"\t\t\treturn 2;",
				"\t\tdefault:",
				"\t\t\treturn 0;",
				"\t}",
				"}",
			],

		index => [$"public sealed record Pair{index}(int First, int Second);"],

		index =>
			[
				$"public sealed record Named{index}(string Name)",
				"{",
				"\tpublic int Length { get; init; }",
				"\tpublic int Size() => 1;",
				"}",
			],

		index =>
			[
				$"public Func<int, int> Lambda{index}() => value =>",
				"{",
				"\tvar doubled = value * 2;",
				"\treturn doubled;",
				"};",
			],

		index =>
			[
				$"public int Lambdas{index}(List<int> values)",
				"{",
				"\tvar first = values.Select(value => value + 1);",
				"\tvar second = values.Where(value =>",
				"\t{",
				"\t\treturn value > 0;",
				"\t});",
				"\treturn first.Count() + second.Count();",
				"}",
			],

		index => ["#if !STANZA_SOAK", $"private int _enabled{index};", "#endif"],

		index =>
			[
				"#if STANZA_SOAK",
				$"public int Switched{index}() => 1;",
				"#else",
				$"public int Switched{index}() => 2;",
				"#endif",
			],
	];

	// The rules that rewrite a token, such as a block body into an arrow, are left off so the tokens can be compared.
	private static readonly IReadOnlyDictionary<string, ReportDiagnostic> s_layoutRules =
		TestRuleSets
			.StandaloneRulesEnabled.Concat(TestRuleSets.GapRulesEnabled)
			.Where(pair => !TestRuleSets.TokenRules.Contains(pair.Key))
			.DistinctBy(pair => pair.Key)
			.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

	private static readonly IReadOnlyDictionary<string, ReportDiagnostic> s_documentationRules =
		TestRuleSets.Only(
			DiagnosticDescriptors.LayOutDocumentationComment,
			DiagnosticDescriptors.MarkDocumentationParagraphs,
			DiagnosticDescriptors.RemoveDocumentationPadding,
			DiagnosticDescriptors.EndDocumentationTextWithPeriod,
			DiagnosticDescriptors.CollapseWhitespaceInsideTag,
			DiagnosticDescriptors.OrderElementsOfDocumentationComment,
			DiagnosticDescriptors.KeepDocumentationCommentWithDeclaration,
			DiagnosticDescriptors.LayOutItemsOfDocumentationList,
			DiagnosticDescriptors.OrderItemsOfDocumentationList,
			DiagnosticDescriptors.WriteEmptyElementAsSelfClosingTag,
			DiagnosticDescriptors.SeparateSlashOfSelfClosingTag
		);

	public static IEnumerable<object[]> Seeds { get; } =
	[.. Enumerable.Range(1, DefaultSeedCount).Select(seed => new object[] { seed })];

	[TestMethod]
	[DynamicData(
		nameof(Seeds),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task TheLayoutOfAGeneratedFileSettlesAndKeepsItsTokens(int seed)
	{
		int findings = await AssertLayoutSettlesAsync(seed);

		Assert.IsGreaterThan(
			0,
			findings,
			"The generated file needed no fix, so it proves nothing."
		);
	}

	[TestMethod]
	[DynamicData(
		nameof(Seeds),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task DamagedDocumentationCommentsSettleAndKeepTheirWords(int seed)
	{
		int findings = await AssertDocumentationSettlesAsync(seed);

		Assert.IsGreaterThan(
			0,
			findings,
			"The generated file needed no fix, so it proves nothing."
		);
	}

	[TestMethod]
	[DataRow(
		"members",
		"class C\n{\n\tpublic void A()\n\t{\n\t}\n\tpublic void B() { }\n\tprivate int _k;\n\tpublic void M1() { }\n\tpublic void M2()\n\t{\n\t}\n}\n"
	)]
	[DataRow(
		"statements",
		"class C\n{\n\tvoid M(bool x)\n\t{\n\t\tif (x)\n\t\t{\n\t\t}\n\t\tFoo();\n\t\tvar k = 1;\n\t\tBar(k);\n\t\tBar(\n\t\t\tk);\n\t}\n\tvoid Foo() { }\n\tvoid Bar(int v) { }\n}\n"
	)]
	public async Task AGroupBesideAMultiLineUnitSettlesTheSameWayInEitherFixOrder(
		string name,
		string source
	)
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			expected: result.FixedText,
			source,
			message: $"{name}:"
		);
	}

	// A grouping rule reads a run across a gap that the commented sibling rule separates, unless the gap ends the run.
	// The blank line the rule writes would then depend on which fix came first.
	public static IEnumerable<object[]> CommentedSiblingRows { get; } =
	[
		[
			"a trailing comment ends the call run above the commented sibling",
			CommentedSiblingRuleSet(),

			"""
			class C
			{
				void M()
				{
					DoThing(); // Trailing.
					// Note.
					DoThing();
					Build(1);
				}

				void DoThing() { }

				int Build(int value) => value;
			}
			""",

			"""
			class C
			{
				void M()
				{
					DoThing(); // Trailing.

					// Note.
					DoThing();
					Build(1);
				}

				void DoThing() { }

				int Build(int value) => value;
			}
			""",
		],

		[
			"a commented sibling ends the call run that stands above the boundary",
			CommentedSiblingRuleSet(),

			"""
			class C
			{
				void M()
				{
					Build(0);
					DoThing();
					// Note.
					DoThing();
				}

				void DoThing() { }

				int Build(int value) => value;
			}
			""",

			"""
			class C
			{
				void M()
				{
					Build(0);
					DoThing();

					// Note.
					DoThing();
				}

				void DoThing() { }

				int Build(int value) => value;
			}
			""",
		],

		[
			"a commented sibling leaves a lone declaration with its neighbors",
			CommentedSiblingRuleSet(),

			"""
			class C
			{
				void M()
				{
					var a = 1;
					// Note.
					var b = 2;
					DoThing();
				}

				void DoThing() { }
			}
			""",

			"""
			class C
			{
				void M()
				{
					var a = 1;

					// Note.
					var b = 2;
					DoThing();
				}

				void DoThing() { }
			}
			""",
		],

		[
			"a commented sibling ends the run of the same-type rule",
			CommentedSiblingRuleSet(DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns),

			"""
			class C
			{
				void M()
				{
					int a = 1;
					// Note.
					int b = 2;
					string c = "x";
				}
			}
			""",

			"""
			class C
			{
				void M()
				{
					int a = 1;

					// Note.
					int b = 2;
					string c = "x";
				}
			}
			""",
		],

		[
			"a commented sibling ends the run of the member rule",
			CommentedSiblingRuleSet(DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns),

			"""
			class C
			{
				private int _a;
				// Note.
				private int _b;
				public void M() { }
			}
			""",

			"""
			class C
			{
				private int _a;

				// Note.
				private int _b;
				public void M() { }
			}
			""",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(CommentedSiblingRows),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ACommentedSiblingSettlesTheSameWayInEitherFixOrder(
		string name,
		IReadOnlyDictionary<string, ReportDiagnostic> rules,
		string source,
		string fixedSource
	)
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,
			diagnosticOptions: rules
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result, name);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			expected: fixedSource,
			source,
			options: new RunOptions(RoslynTestHarness.AllAnalyzers) { DiagnosticOptions = rules },
			message: $"{name}:"
		);
	}

	// The commented sibling rule on beside the rules that ship on, less the ones named.
	private static Dictionary<string, ReportDiagnostic> CommentedSiblingRuleSet(
		params DiagnosticDescriptor[] disabled
	)
	{
		Dictionary<string, ReportDiagnostic> rules = new(
			TestRuleSets.Only(DiagnosticDescriptors.SeparateCommentedSibling),
			StringComparer.Ordinal
		);

		foreach (DiagnosticDescriptor descriptor in disabled)
			rules[descriptor.Id] = ReportDiagnostic.Suppress;

		return rules;
	}

	// Written by hand as the rules lay it out, so the fixes are checked against a text of their own.
	private const string FormattedFile = """
		using System.Linq;
		using System.Text;

		internal sealed class Sample
		{
			private int _first;
			private int _second;

			public int Count { get; set; }

			public void Run()
			{
				var first = 1;

				DoThing();
				DoThing();

				if (first > 0)
				{
					DoThing();
				}

				Call(
					1,
					2);

				return;
			}

			public int Pick(int value)
			{
				switch (value)
				{
					case 1:
						return 1;
					default:
						return 0;
				}
			}
		}
		""";

	[TestMethod]
	public async Task AFormattedFileWithItsBlankLinesTakenOutIsRestoredByEachFixOrder()
	{
		string formatted = FormattedFile.ReplaceLineEndings("\n");

		string damaged = string.Join(
			"\n",
			formatted.Split('\n').Where(line => line.Trim().Length > 0)
		);

		Assert.AreNotEqual(formatted, damaged);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			damaged,
			diagnosticOptions: s_layoutRules
		);

		RoslynTestHarness.AssertFixedText(formatted, result);

		RunOptions options = new(RoslynTestHarness.AllAnalyzers)
		{
			DiagnosticOptions = s_layoutRules,
		};

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			expected: formatted,
			damaged,
			options
		);
	}

	// The loose run rule and the rules that ask for blank lines write opposite fixes into the same gaps.
	// Each set of them has to claim a gap once and settle, whether one fix at a time or one Fix All per rule.
	[TestMethod]
	[DynamicData(
		nameof(Seeds),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task StatementsSetApartByBlankLinesSettleWithTheRulesThatShareTheirGaps(int seed)
	{
		int findings = await AssertLooseRunsSettleAsync(seed);

		Assert.IsGreaterThan(
			0,
			findings,
			"The generated statements needed no fix, so they prove nothing."
		);
	}

	[TestMethod]
	[TestCategory("Soak")]
	public async Task ALargeSeededSetSettlesTheSameWay()
	{
		string? setting = Environment.GetEnvironmentVariable(SoakVariable);

		bool isCount = int.TryParse(
			s: setting,
			style: NumberStyles.None,
			provider: CultureInfo.InvariantCulture,
			result: out int count
		);

		if (!isCount || count < 1)
			ExpectedSkip.Skip($"Set {SoakVariable} to the number of seeds to run.");

		List<string> failures = [];
		bool outlasted = false;

		// A spent budget stops the soak at its seed, and the failures so far are reported below.
		for (int seed = SoakFirstSeed; seed < SoakFirstSeed + count && !outlasted; seed++)
		{
			// Each seed gets the budget of a whole test, so the soak's grows with its seed count.
			HarnessBudget.Restart();

			foreach (
				Func<int, Task<int>> run in new[]
				{
					AssertLayoutSettlesAsync,
					AssertDocumentationSettlesAsync,
					AssertLooseRunsSettleAsync,
				}
			)
			{
				try
				{
					_ = await run(seed);
				}
				catch (AssertFailedException exception)
				{
					failures.Add($"seed {seed}: {exception.Message.Split('\n')[0]}");
				}
				catch (OperationCanceledException)
					when (HarnessBudget.Token.IsCancellationRequested)
				{
					failures.Add($"seed {seed}: the run outlasted its budget.");
					outlasted = true;
					break;
				}
			}
		}

		Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
	}

	private static async Task<int> AssertLayoutSettlesAsync(int seed)
	{
		var random = new Random(seed);

		string source = CreateLayoutSource(random);

		AnalyzerRunResult result = await RunAsync(
			source,
			rules: s_layoutRules,
			options: null,
			seed
		);

		Assert.AreSequenceEqual(
			ReadTokens(source),
			ReadTokens(result.FixedText),
			$"seed {seed}: a fix changed a token."
		);

		Assert.AreSequenceEqual(
			ReadComments(source),
			ReadComments(result.FixedText),
			$"seed {seed}: a fix changed a comment."
		);

		return result.Diagnostics.Length;
	}

	private static async Task<int> AssertLooseRunsSettleAsync(int seed)
	{
		string source = CreateStatementSource(new Random(seed));

		Dictionary<string, string> options = new()
		{
			["stanza_max_loose_statements"] = $"{s_looseLimits[seed % s_looseLimits.Length]}",
		};

		int findings = 0;

		foreach (DiagnosticDescriptor[] rules in s_looseRuleSets)
		{
			Dictionary<string, ReportDiagnostic> enabled = EnableOnly(rules);

			AnalyzerRunResult reported = await RoslynTestHarness.ReportAllAsync(
				source,
				enabled,
				options
			);

			if (reported.Diagnostics.Length > 0)
				RoslynTestHarness.AssertEachGapIsClaimedOnce(reported);

			AnalyzerRunResult result = await RunAsync(source, rules: enabled, options, seed);

			Assert.AreSequenceEqual(
				ReadTokens(source),
				ReadTokens(result.FixedText),
				$"seed {seed}: a fix changed a token."
			);

			Assert.AreSequenceEqual(
				ReadComments(source),
				ReadComments(result.FixedText),
				$"seed {seed}: a fix changed a comment."
			);

			findings += result.Diagnostics.Length;
		}

		await AssertGatheringLeavesNoWorkAsync(source, options);

		return findings;
	}

	// The loose run rule alone must never remove a blank line that a rule it defers to would write back.
	private static async Task AssertGatheringLeavesNoWorkAsync(
		string source,
		Dictionary<string, string> options
	)
	{
		DiagnosticDescriptor[] asking =
		[
			DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns,
			DiagnosticDescriptors.SeparateSameTypeDeclarationGroups,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateCommentedSibling,
		];

		// Only the loose run rule's fixes run, while the rules it defers to stay enabled, so it sees their gaps.
		AnalyzerRunResult gathered = await RoslynTestHarness.RunIterativelyAsync(
			source,

			new RunOptions([new GatherRunOfLooseStatementsAnalyzer()])
			{
				DiagnosticOptions = TestRuleSets.Only([
					DiagnosticDescriptors.GatherRunOfLooseStatements,
					.. asking,
				]),

				AnalyzerConfigOptions = options,
			}
		);

		AnalyzerRunResult before = await RoslynTestHarness.ReportAllAsync(
			source,
			TestRuleSets.Only(asking),
			options
		);

		AnalyzerRunResult after = await RoslynTestHarness.ReportAllAsync(
			gathered.FixedText,
			TestRuleSets.Only(asking),
			options
		);

		foreach (DiagnosticDescriptor rule in asking)
		{
			Assert.IsLessThanOrEqualTo(
				before.Diagnostics.Count(diagnostic => diagnostic.Id == rule.Id),
				after.Diagnostics.Count(diagnostic => diagnostic.Id == rule.Id),
				$"Gathering the loose runs made {rule.Id} report a gap it did not report before."
			);
		}
	}

	// The rules that ship on and share a gap with the loose run rule are off unless a set names them.
	private static Dictionary<string, ReportDiagnostic> EnableOnly(DiagnosticDescriptor[] rules)
	{
		Dictionary<string, ReportDiagnostic> enabled = new(
			TestRuleSets.Only(rules),
			StringComparer.Ordinal
		);

		foreach (
			DiagnosticDescriptor shipped in new[]
			{
				DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns,
				DiagnosticDescriptors.SeparateSameTypeDeclarationGroups,
				DiagnosticDescriptors.SeparateMultiLineConstructs,
			}
		)
		{
			if (!rules.Contains(shipped))
				enabled[shipped.Id] = ReportDiagnostic.Suppress;
		}

		return enabled;
	}

	internal static string CreateStatementSource(Random random)
	{
		List<string> lines = [];
		int count = random.Next(6, 13);

		for (int index = 0; index < count; index++)
		{
			if (index > 0)
				lines.AddRange(StatementGap(random));

			foreach (string line in s_statements[random.Next(s_statements.Length)](index))
				lines.Add(line.Length == 0 || line[0] == '#' ? line : "\t\t" + line);
		}

		return "internal sealed class Sample\n{\n\tpublic void Run()\n\t{\n\t\tint loose = 0;\n\n"
			+ string.Join("\n", lines)
			+ "\n\t}\n}\n";
	}

	// Most gaps hold one blank line, so runs of loose statements come up often.
	private static IEnumerable<string> StatementGap(Random random)
	{
		return Enumerable.Repeat(
			string.Empty,

			random.Next(20) switch
			{
				< 5 => 0,
				< 17 => 1,
				_ => 2,
			}
		);
	}

	private static async Task<int> AssertDocumentationSettlesAsync(int seed)
	{
		var random = new Random(seed);

		List<string> comments = [];
		List<string> members = [];

		for (int index = 0; index < 3; index++)
		{
			IReadOnlyList<string> body = GeneratedDocBody.Create(random, ["first", "second"]);
			comments.AddRange(body);

			List<string> lines =
			[
				.. body.Select(line => line.Length == 0 ? "\t///" : "\t/// " + line),
			];

			lines.Add($"\tinternal int Member{index}(int first, string second)");
			lines.Add("\t{");
			lines.Add("\t\treturn first;");
			lines.Add("\t}");

			members.Add(string.Join("\n", lines));
		}

		string source =
			"internal sealed class Sample\n{\n" + string.Join("\n\n", members) + "\n}\n";

		int[] widths = [60, 80, 100, 120];

		Dictionary<string, string> options = new()
		{
			["stanza_max_line_length"] = $"{widths[seed % widths.Length]}",
		};

		AnalyzerRunResult result = await RunAsync(
			source,
			rules: s_documentationRules,
			options,
			seed
		);

		string[] fixedLines =
		[
			.. result
				.FixedText.Split('\n')
				.Select(line => line.Trim())
				.Where(line => line.StartsWith("///", StringComparison.Ordinal))
				.Select(line => line[3..]),
		];

		Assert.AreSequenceEqual(
			[.. GeneratedDocBody.ReadWords(comments).Order(StringComparer.Ordinal)],
			[.. GeneratedDocBody.ReadWords(fixedLines).Order(StringComparer.Ordinal)],
			$"seed {seed}: a fix changed the words of a comment."
		);

		return result.Diagnostics.Length;
	}

	private static async Task<AnalyzerRunResult> RunAsync(
		string source,
		IReadOnlyDictionary<string, ReportDiagnostic> rules,
		IReadOnlyDictionary<string, string>? options,
		int seed
	)
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,
			diagnosticOptions: rules,
			analyzerConfigOptions: options
		);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			expected: result.FixedText,
			source,

			options: new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = rules,
				AnalyzerConfigOptions = options,
			},

			message: $"seed {seed}:"
		);

		return result;
	}

	internal static string CreateLayoutSource(Random random)
	{
		List<string> lines = [];

		foreach (string import in s_importLines)
		{
			lines.AddRange(Gap(random));
			lines.Add(import);
		}

		lines.AddRange(Gap(random));

		lines.Add("internal sealed class Sample");
		lines.Add("{");

		for (int index = 0; index < 6; index++)
		{
			lines.AddRange(Gap(random));

			foreach (string line in s_members[random.Next(s_members.Length)](index))
			{
				lines.Add(line.Length == 0 ? line : "\t" + line);

				// A blank line may fall between any two lines of a member, which is valid wherever it lands.
				if (random.Next(6) == 0)
					lines.AddRange(Gap(random));
			}
		}

		lines.Add("}");

		return string.Join("\n", lines) + "\n";
	}

	private static IEnumerable<string> Gap(Random random)
	{
		return Enumerable.Repeat(
			string.Empty,

			random.Next(10) switch
			{
				< 4 => 0,
				< 8 => 1,
				_ => 2,
			}
		);
	}

	private static string[] ReadTokens(string source)
	{
		return
		[
			.. CSharpSyntaxTree
				.ParseText(source)
				.GetRoot()
				.DescendantTokens()
				.Select(token => token.Text),
		];
	}

	// A comment keeps its words and its lines; only the spaces inside a line may change, as the marker rules change them.
	internal static string[] ReadComments(string source)
	{
		return
		[
			.. CSharpSyntaxTree
				.ParseText(source)
				.GetRoot()
				.DescendantTrivia()
				.Where(trivia =>
					trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
					|| trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
				)
				.Select(trivia =>
					LineSpace().Replace(trivia.ToString().ReplaceLineEndings("\n"), string.Empty)
				),
		];
	}

	[GeneratedRegex("[ \t]+")]
	private static partial Regex LineSpace();
}
