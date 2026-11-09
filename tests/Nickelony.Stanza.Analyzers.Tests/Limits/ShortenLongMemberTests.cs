using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Limits;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Limits;

[TestClass]
public sealed class ShortenLongMemberTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> Rule { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.ShortenLongMember);

	// Each member holds four statements, one more than a limit of three allows.
	public static IEnumerable<object[]> Members { get; } =
	[
		["a method", "public void Run() { F(); F(); F(); F(); }", "Run"],
		["a constructor", "public Sample() { F(); F(); F(); F(); }", "Sample"],
		["a destructor", "~Sample() { F(); F(); F(); F(); }", "Sample"],
		["a getter", "public int Value { get { F(); F(); F(); return 0; } }", "get"],
		["a setter", "public int Value { get => 0; set { F(); F(); F(); F(); } }", "set"],
		["an initializer", "public int Value { get => 0; init { F(); F(); F(); F(); } }", "init"],

		[
			"an indexer accessor",
			"public int this[int i] { get { F(); F(); F(); return i; } }",
			"get",
		],

		[
			"an event accessor",
			"public event System.Action Changed { add { F(); F(); F(); F(); } remove { } }",
			"add",
		],

		[
			"an operator",
			"public static Sample operator +(Sample l, Sample r) { F(); F(); F(); return l; }",
			"+",
		],

		[
			"a conversion",
			"public static implicit operator int(Sample s) { F(); F(); F(); return 0; }",
			"operator",
		],

		[
			"a local function",
			"public void Run() { void Local() { F(); F(); F(); F(); } Local(); }",
			"Local",
		],

		[
			"a static local function",
			"public void Run() { static void Local() { F(); F(); F(); F(); } Local(); }",
			"Local",
		],
	];

	// A body, and the statements it holds: blocks are not counted, and a nested statement is.
	public static IEnumerable<object[]> Bodies { get; } =
	[
		["a flat body", "F(); F(); F(); F();", 4],
		["an if and an else", "if (a) { F(); F(); } else { F(); } F();", 5],
		["a switch", "switch (n) { case 1: F(); break; default: F(); break; }", 5],
		["a try", "try { F(); } catch { F(); } finally { F(); }", 4],
		["loops", "foreach (int x in xs) { F(); } while (a) { F(); }", 4],
		["a lock", "lock (xs) { F(); }", 2],
		["a using declaration", "using var stream = new System.IO.MemoryStream(); F();", 2],
		["a declaration", "int x = 1; x++;", 2],
		["bare blocks", "{ { { F(); F(); } } }", 2],
		["a lambda", "System.Action f = () => { F(); F(); F(); F(); };", 5],
		["an anonymous method", "System.Action f = delegate { F(); F(); F(); };", 4],
	];

	[TestMethod]
	[DynamicData(
		nameof(Members),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AMemberPastTheLimitIsReportedOnceAtItsName(
		string name,
		string member,
		string reportedAt
	)
	{
		string source =
			$"internal sealed class Sample\n{{\n\tprivate static void F() {{ }}\n\n\t{member}\n}}\n";

		AnalyzerRunResult result = await ReportAsync(source, "3");

		Assert.HasCount(1, result.Diagnostics, name);
		Assert.AreEqual(reportedAt, LimitFixtures.Text(result.Diagnostics[0]), name);

		Assert.AreEqual(
			4,
			result.Diagnostics[0].Location.GetLineSpan().StartLinePosition.Line,
			name
		);
	}

	[TestMethod]
	[DynamicData(
		nameof(Members),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AMemberAtTheLimitPasses(string name, string member, string reportedAt)
	{
		string source =
			$"internal sealed class Sample\n{{\n\tprivate static void F() {{ }}\n\n\t{member}\n}}\n";

		AnalyzerRunResult result = await ReportAsync(source, "5");

		Assert.IsEmpty(result.Diagnostics, $"{name} reported at {reportedAt}.");
	}

	[TestMethod]
	[DynamicData(
		nameof(Bodies),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task EveryStatementCountsButABlock(string name, string body, int statements)
	{
		string source = Wrap(body);

		AnalyzerRunResult atTheCount = await ReportAsync(source, $"{statements}");
		AnalyzerRunResult belowTheCount = await ReportAsync(source, $"{statements - 1}");

		Assert.IsEmpty(atTheCount.Diagnostics, name);
		Assert.HasCount(1, belowTheCount.Diagnostics, name);
	}

	[TestMethod]
	public async Task FortyStatementsPassAndAFortyFirstIsReported()
	{
		string body = string.Concat(Enumerable.Repeat("F();\n", 40));

		Assert.IsEmpty((await ReportAsync(Wrap(body))).Diagnostics);
		Assert.HasCount(1, (await ReportAsync(Wrap(body + "F();"))).Diagnostics);
	}

	[TestMethod]
	public async Task ALocalFunctionIsJudgedOnItsOwnAndLeavesTheCountOfTheMemberAroundIt()
	{
		const string source = """
			internal sealed class Sample
			{
				private static void F() { }

				public void Run()
				{
					void Local()
					{
						F();
						F();
						F();
						F();
					}

					F();
					F();
				}
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source, "3");

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual("Local", LimitFixtures.Text(result.Diagnostics[0]));
	}

	[TestMethod]
	public async Task ALambdaCountsTowardTheMemberItsInsideOf()
	{
		string source = Wrap("System.Action f = () => { F(); F(); F(); F(); };");

		AnalyzerRunResult result = await ReportAsync(source, "4");

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual("Run", LimitFixtures.Text(result.Diagnostics[0]));
	}

	[TestMethod]
	public async Task AMemberWithAnExpressionBodyHoldsNoStatements()
	{
		const string source = """
			internal sealed class Sample
			{
				public int Value => 1;

				public int Twice(int x) => x * 2;
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source, "1");

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ALambdaInAnExpressionBodyCountsTowardThatMember()
	{
		const string source = """
			internal sealed class Sample
			{
				private static void F() { }

				public System.Action Make() => () => { F(); F(); F(); F(); };
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source, "3");

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual("Make", LimitFixtures.Text(result.Diagnostics[0]));
	}

	[TestMethod]
	public async Task ALambdaInThePropertyOrIndexerWithAnExpressionBodyBelongsToNoMember()
	{
		const string source = """
			internal sealed class Sample
			{
				private static void F() { }

				public System.Action Run => () => { F(); F(); F(); F(); };

				public System.Action this[int index] => () => { F(); F(); F(); F(); };
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source, "3");

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task CodeOutsideAMemberIsNotJudged()
	{
		const string source = """
			internal sealed class Sample
			{
				private static void F() { }

				private static readonly System.Action s_run = () => { F(); F(); F(); F(); };
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source, "3");

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TopLevelStatementsAreNotMembers()
	{
		const string source = """
			System.Console.WriteLine(1);
			System.Console.WriteLine(2);
			System.Console.WriteLine(3);
			System.Console.WriteLine(4);
			""";

		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync(
			source,

			new RunOptions([new ShortenLongMemberAnalyzer()])
			{
				DiagnosticOptions = Rule,

				AnalyzerConfigOptions = new Dictionary<string, string>
				{
					["stanza_max_member_statements"] = "3",
				},

				RequireCompiles = false,
			}
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task EachMemberIsJudgedOnItsOwn()
	{
		const string source = """
			internal sealed class Sample
			{
				private static void F() { }

				public void First() { F(); F(); F(); F(); }

				public void Second() { F(); F(); F(); F(); }

				public void Third() { F(); }
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source, "3");

		Assert.HasCount(2, result.Diagnostics);
	}

	[TestMethod]
	[DataRow("1", 2)]
	[DataRow("2", 1)]
	[DataRow("41", 0)]
	[DataRow("0", 0)]
	[DataRow("-1", 1)]
	[DataRow("1001", 1)]
	[DataRow("many", 1)]
	[DataRow("", 1)]
	public async Task TheLimitComesFromTheOptionAndZeroTurnsTheRuleOff(string limit, int reports)
	{
		string source =
			$"internal sealed class Sample\n{{\n\tprivate static void F() {{ }}\n\n\tpublic void Big()\n\t{{\n{string.Concat(Enumerable.Repeat("\t\tF();\n", 41))}\t}}\n\n\tpublic void Small()\n\t{{\n\t\tF();\n\t\tF();\n\t}}\n}}\n";

		// A missing or invalid limit reads as the default of forty, which reports the one member of forty-one statements.
		AnalyzerRunResult result = await ReportAsync(source, limit);

		Assert.HasCount(reports, result.Diagnostics, $"A limit of '{limit}'.");
	}

	[TestMethod]
	public async Task AStatementUnderAConditionalDirectiveCountsInTheViewThatKeepsIt()
	{
		const string source = """
			internal sealed class Sample
			{
				private static void F() { }

				public void Run()
				{
					F();
					F();
					F();
			#if EXTRA
					F();
			#endif
				}
			}
			""";

		Assert.HasCount(1, (await ReportAsync("#define EXTRA\n" + source, "3")).Diagnostics);
		Assert.IsEmpty((await ReportAsync(source, "3")).Diagnostics);
	}

	[TestMethod]
	public async Task APragmaSilencesTheMemberItCovers()
	{
		const string source = """
			internal sealed class Sample
			{
				private static void F() { }

			#pragma warning disable ZA1002
				public void Run() { F(); F(); F(); F(); }
			#pragma warning restore ZA1002
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source, "3");

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public void TheCostGrowsLinearlyWithTheLengthOfAMember()
	{
		static string Long(int count)
		{
			string statements = string.Concat(Enumerable.Repeat("if (a) { F(); }\n", count));
			return Wrap(statements);
		}

		AssertLinear(Long(5_000), Long(20_000));
	}

	[TestMethod]
	public void TheCostGrowsLinearlyWithTheDepthOfANest()
	{
		static string Nest(int depth)
		{
			return Wrap(
				string.Concat(Enumerable.Repeat("if (a) { ", depth))
					+ string.Concat(Enumerable.Repeat("} ", depth))
			);
		}

		AssertLinear(Nest(250), Nest(1_000));
	}

	[TestMethod]
	public void TheCostGrowsLinearlyWithTheNumberOfMembers()
	{
		static string Many(int count)
		{
			string methods = string.Concat(
				Enumerable.Range(0, count).Select(i => $"\tpublic void M{i}() {{ F(); F(); }}\n")
			);

			return $"internal sealed class Sample\n{{\n{methods}}}\n";
		}

		AssertLinear(Many(5_000), Many(20_000));
	}

	private static void AssertLinear(string small, string large)
	{
		LinearCost.AssertLinear(
			small,
			large,
			root => MemberStatements.CollectLongMembers(root, 3, default)
		);
	}

	private static string Wrap(string statements)
	{
		return LimitFixtures.Wrap(
			statements,
			signature: "public void Run(bool a, int n, int[] xs)",
			classHeader: "internal sealed class Sample",
			members: "\tprivate static void F() { }\n\n"
		);
	}

	private static Task<AnalyzerRunResult> ReportAsync(string source, string? limit = null)
	{
		return RoslynTestHarness.ReportWithLimitAsync<ShortenLongMemberAnalyzer>(
			source,
			diagnosticOptions: Rule,
			key: "stanza_max_member_statements",
			limit
		);
	}
}
