using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
