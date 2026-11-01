using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Limits;

namespace Nickelony.Stanza.Analyzers.Tests.Limits;

[TestClass]
public sealed class BreakUpLongTernaryChainTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> Rule { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.BreakUpLongTernaryChain);

	[TestMethod]
	public async Task AChainOfThreeOperatorsIsReportedOnceAtItsOutermostConditional()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Pick(int value) =>
					value > 2 ? 3 : value > 1 ? 2 : value > 0 ? 1 : 0;
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source);

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual(3, result.Diagnostics[0].Location.GetLineSpan().StartLinePosition.Line);
	}

	[TestMethod]
	public async Task AChainOfTwoOperatorsIsLeftAlone()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Pick(int value) =>
					value > 1 ? 2 : value > 0 ? 1 : 0;
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AChainOfFiveOperatorsGivesOneDiagnostic()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Pick(int value) =>
					value > 4 ? 5
					: value > 3 ? 4
					: value > 2 ? 3
					: value > 1 ? 2
					: value > 0 ? 1
					: 0;
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source);

		Assert.HasCount(1, result.Diagnostics);
	}

	[TestMethod]
	[DataRow("a ? b : c ? d : a ? b : c")]
	[DataRow("(a ? b : c) ? d : a ? b : c")]
	[DataRow("a ? (b ? c : d) : (c ? d : a)")]
	[DataRow("a ? b : Same(c ? d : a, b ? c : d)")]
	[DataRow("((a ? b : c) ? d : a) ? b : c")]
	public async Task AConditionalInAnyPartOfTheChainCounts(string expression)
	{
		string source = $$"""
			internal static class Sample
			{
				internal static bool Same(bool x, bool y) => x == y;

				internal static bool Pick(bool a, bool b, bool c, bool d) =>
					{{expression}};
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source);

		Assert.HasCount(1, result.Diagnostics, expression);
	}

	[TestMethod]
	public async Task ANullCoalescingAndAConditionalAccessAreNotConditionals()
	{
		const string source = """
			internal static class Sample
			{
				internal static int? Count(string first, string second, bool a, bool b) =>
					(first?.Length ?? second?.Length ?? 0) > 1
						? (a ? first?.Length : second?[0])
						: first?.Length ?? second?.Length;
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ALambdaAndAnAnonymousMethodEachStartAChainOfTheirOwn()
	{
		const string source = """
			using System;

			internal static class Sample
			{
				internal static int Pick(bool a, bool b)
				{
					Func<int, int> lambda = value => value > 1 ? 2 : value > 0 ? 1 : 0;

					Func<int, int> method = delegate (int value)
					{
						return value > 1 ? 2 : value > 0 ? 1 : 0;
					};

					return a ? lambda(1) : b ? method(1) : 0;
				}
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AConditionalInsideALambdaOfAChainDoesNotAddToTheChainOutsideIt()
	{
		const string source = """
			using System;

			internal static class Sample
			{
				internal static int Apply(Func<int, int> function) => function(1);

				internal static int Pick(bool a, bool b) =>
					a ? Apply(value => value > 1 ? 2 : value > 0 ? 1 : 0) : b ? 1 : 0;
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ALongChainInsideALambdaIsReportedAtThatLambda()
	{
		const string source = """
			using System;

			internal static class Sample
			{
				internal static int Pick(bool a)
				{
					Func<int, int> lambda = value => value > 2 ? 3 : value > 1 ? 2 : value > 0 ? 1 : 0;

					return a ? lambda(1) : 0;
				}
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source);

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual(6, result.Diagnostics[0].Location.GetLineSpan().StartLinePosition.Line);
	}

	[TestMethod]
	[DataRow("1", 1)]
	[DataRow("2", 1)]
	[DataRow("3", 0)]
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
				internal static int Pick(int value) =>
					value > 2 ? 3 : value > 1 ? 2 : value > 0 ? 1 : 0;
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source, limit);

		Assert.HasCount(reports, result.Diagnostics, $"A limit of '{limit}'.");
	}

	[TestMethod]
	public async Task AChainWithAMissingBranchStillCountsItsOperators()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Pick(int value) =>
					value > 2 ? 3 : value > 1 ? 2 : value > 0 ? 1 :
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync(
			source,

			new RunOptions([new BreakUpLongTernaryChainAnalyzer()])
			{
				DiagnosticOptions = Rule,
				RequireCompiles = false,
			}
		);

		Assert.HasCount(1, result.Diagnostics);
	}

	[TestMethod]
	public async Task AVeryLongChainIsOneDiagnosticAndIsWalkedWithoutRecursion()
	{
		string chain = string.Concat(Enumerable.Repeat("value > 0 ? 1 : ", 300));

		string source = $$"""
			internal static class Sample
			{
				internal static int Pick(int value) => {{chain}}0;
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source);

		Assert.HasCount(1, result.Diagnostics);
	}

	[TestMethod]
	public async Task APragmaSilencesTheChainItCovers()
	{
		const string source = """
			internal static class Sample
			{
			#pragma warning disable ZA1007
				internal static int Pick(int value) =>
					value > 2 ? 3 : value > 1 ? 2 : value > 0 ? 1 : 0;
			#pragma warning restore ZA1007
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source);

		Assert.IsEmpty(result.Diagnostics);
	}

	private static Task<AnalyzerRunResult> ReportAsync(string source, string? limit = null)
	{
		return RoslynTestHarness.ReportWithLimitAsync<BreakUpLongTernaryChainAnalyzer>(
			source,
			diagnosticOptions: Rule,
			key: "stanza_max_ternary_operators",
			limit
		);
	}
}
