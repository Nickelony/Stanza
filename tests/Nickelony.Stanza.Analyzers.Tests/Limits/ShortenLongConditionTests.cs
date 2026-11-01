using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Limits;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Limits;

[TestClass]
public sealed class ShortenLongConditionTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> Rule { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.ShortenLongCondition);

	// Each condition holds five operators, one more than the default allows.
	public static IEnumerable<object[]> LongConditions { get; } =
	[
		["an if statement", "if (a && b && c && a && b && c) { }"],
		["a while statement", "while (a && b && c && a && b && c) { }"],
		["an assignment", "bool x = a && b && c && a && b && c;"],
		["a return", "return a && b && c && a && b && c;"],
		["a conditional", "int x = a && b && c && a && b && c ? 1 : 0;"],
		["a branch of a conditional", "int x = a ? (b && c && a && b && c && a ? 1 : 0) : 0;"],
		["a mix of both operators", "bool x = a && b || c && a || b && c;"],
		["parentheses", "bool x = (a && b) || (c && a) || (b && c) || (a && b);"],
		["a negation", "bool x = !(a && b && c) || !(a && b && c);"],

		[
			"a switch expression arm",
			"bool x = a switch { true => b && c && a && b && c && a, _ => false };",
		],

		[
			"a catch filter",
			"try { } catch (System.Exception) when (a && b && c && a && b && c) { }",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(LongConditions),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AConditionPastTheLimitIsReportedOnce(string name, string statement)
	{
		AnalyzerRunResult result = await ReportAsync(Wrap(statement));
		Assert.HasCount(1, result.Diagnostics, name);
	}

	[TestMethod]
	public async Task TheReportCoversTheOutermostLogicalExpression()
	{
		string source = Wrap("if (a && (b || c) && a && b && c && a) { }");

		AnalyzerRunResult result = await ReportAsync(source);

		Assert.HasCount(1, result.Diagnostics);

		Assert.AreEqual(
			"a && (b || c) && a && b && c && a",

			source[
				result.Diagnostics[0].Location.SourceSpan.Start..result
					.Diagnostics[0]
					.Location
					.SourceSpan
					.End
			]
		);
	}

	[TestMethod]
	public async Task FourOperatorsPass()
	{
		AnalyzerRunResult result = await ReportAsync(Wrap("if (a && b && c && a && b) { }"));
		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ParenthesesAndNegationDoNotResetTheCount()
	{
		AnalyzerRunResult result = await ReportAsync(Wrap("bool x = (a && b) || (c && a);"), "2");

		Assert.HasCount(1, result.Diagnostics);

		result = await ReportAsync(Wrap("bool x = a && b || c;"), "2");

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task EachArgumentIsAConditionOfItsOwn()
	{
		const string source = """
			internal static class Sample
			{
				internal static void F(bool x, bool y)
				{
				}

				internal static void Run(bool a, bool b, bool c)
				{
					F(a && b && c, a && b && c);
				}
			}
			""";

		Assert.IsEmpty((await ReportAsync(source, "2")).Diagnostics);
		Assert.HasCount(2, (await ReportAsync(source, "1")).Diagnostics);
	}

	[TestMethod]
	public async Task AnArgumentDoesNotAddToTheConditionAroundIt()
	{
		const string source = """
			internal static class Sample
			{
				internal static bool F(bool x, bool y) => x;

				internal static bool Run(bool a, bool b, bool c) =>
					a && b && F(a && b, b || c);
			}
			""";

		Assert.IsEmpty((await ReportAsync(source, "2")).Diagnostics);
	}

	[TestMethod]
	public async Task ALambdaAndAnAnonymousMethodEachStartACountOfTheirOwn()
	{
		const string source = """
			using System;

			internal static class Sample
			{
				internal static bool Run(bool a, bool b, bool c)
				{
					Func<bool, bool> lambda = x => x && a && b;
					Func<bool, bool> method = delegate (bool x) { return x && a && b; };

					return a && lambda(c) && method(c);
				}
			}
			""";

		Assert.IsEmpty((await ReportAsync(source, "2")).Diagnostics);
		Assert.HasCount(3, (await ReportAsync(source, "1")).Diagnostics);
	}

	[TestMethod]
	public async Task ALongConditionInsideALambdaIsReportedAtThatLambda()
	{
		string source = Wrap("System.Func<bool, bool> f = x => x && a && b && c && a && b;");

		AnalyzerRunResult result = await ReportAsync(source);

		Assert.HasCount(1, result.Diagnostics);
	}

	[TestMethod]
	public async Task APatternOfOrsReadsAsAListAndDoesNotCount()
	{
		const string source = """
			internal static class Sample
			{
				internal static bool Run(int x) =>
					x is 1 or 2 or 3 or 4 or 5 or 6 or 7;
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task APatternDoesNotAddToTheConditionAroundIt()
	{
		const string source = """
			internal static class Sample
			{
				internal static bool Run(int x, bool a) =>
					a && x is 1 or 2 or 3 or 4 or 5 or 6 or 7 && a;
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AFieldByFieldComparisonInEqualsAndTheEqualityOperatorsIsSkipped()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _a;
				private int _b;
				private int _c;
				private int _d;
				private int _e;
				private int _f;

				public bool Equals(Sample other) =>
					_a == other._a && _b == other._b && _c == other._c && _d == other._d && _e == other._e && _f == other._f;

				public static bool operator ==(Sample left, Sample right) =>
					left._a == right._a && left._b == right._b && left._c == right._c && left._d == right._d && left._e == right._e && left._f == right._f;

				public static bool operator !=(Sample left, Sample right) =>
					left._a != right._a || left._b != right._b || left._c != right._c || left._d != right._d || left._e != right._e || left._f != right._f;
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ALongComparisonInAnotherMethodIsReported()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _a;
				private int _b;

				public bool Same(Sample other) =>
					_a == other._a && _b == other._b && _a == other._b && _b == other._a && _a == other._a && _b == other._b;
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source);

		Assert.HasCount(1, result.Diagnostics);
	}

	[TestMethod]
	[DataRow("1", 3)]
	[DataRow("2", 2)]
	[DataRow("5", 0)]
	[DataRow("0", 0)]
	[DataRow("-1", 1)]
	[DataRow("1001", 1)]
	[DataRow("many", 1)]
	[DataRow("", 1)]
	public async Task TheLimitComesFromTheOptionAndZeroTurnsTheRuleOff(string limit, int reports)
	{
		const string source = """
			internal static class Sample
			{
				internal static void Run(bool a, bool b, bool c)
				{
					if (a && b && c && a && b && c) { }
					if (a && b && c) { }
					if (a || b || c || a) { }
				}
			}
			""";

		// A missing or invalid limit reads as the default of four, which reports the one condition of five operators.
		AnalyzerRunResult result = await ReportAsync(source, limit);

		Assert.HasCount(reports, result.Diagnostics, $"A limit of '{limit}'.");
	}

	[TestMethod]
	public async Task AConditionWithAMissingOperandStillCountsItsOperators()
	{
		const string source = """
			internal static class Sample
			{
				internal static bool Run(bool a) =>
					a && a && a && a && a &&;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync(
			source,

			new RunOptions([new ShortenLongConditionAnalyzer()])
			{
				DiagnosticOptions = Rule,
				RequireCompiles = false,
			}
		);

		Assert.HasCount(1, result.Diagnostics);
	}

	[TestMethod]
	public async Task AVeryLongConditionIsOneDiagnosticAndIsWalkedWithoutRecursion()
	{
		string condition = string.Concat(Enumerable.Repeat("a && ", 300));
		string source = Wrap($"bool x = {condition}b;");

		AnalyzerRunResult result = await ReportAsync(source);

		Assert.HasCount(1, result.Diagnostics);
	}

	[TestMethod]
	public async Task APragmaSilencesTheConditionItCovers()
	{
		string source = Wrap(
			"""
			#pragma warning disable ZA1005
			if (a && b && c && a && b && c) { }
			#pragma warning restore ZA1005
			"""
		);

		AnalyzerRunResult result = await ReportAsync(source);

		Assert.IsEmpty(result.Diagnostics);
	}

	private static string Wrap(string statements)
	{
		return LimitFixtures.Wrap(
			statements,
			signature: "internal static bool Run(bool a, bool b, bool c)",
			tail: "\t\treturn a;\n"
		);
	}

	private static Task<AnalyzerRunResult> ReportAsync(string source, string? limit = null)
	{
		return RoslynTestHarness.ReportWithLimitAsync<ShortenLongConditionAnalyzer>(
			source,
			diagnosticOptions: Rule,
			key: "stanza_max_logical_operators",
			limit
		);
	}
}
