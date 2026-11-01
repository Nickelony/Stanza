using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Limits;

namespace Nickelony.Stanza.Analyzers.Tests.Limits;

[TestClass]
public sealed class BreakUpLongElseIfChainTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> Rule { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.BreakUpLongElseIfChain);

	[TestMethod]
	public async Task AChainOfFiveBranchesIsReportedOnceAtItsFirstIf()
	{
		const string statements = """
			if (n == 1) { }
			else if (n == 2) { }
			else if (n == 3) { }
			else if (n == 4) { }
			else if (n == 5) { }
			""";

		AnalyzerRunResult result = await ReportAsync(LimitFixtures.Wrap(statements));

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual("if", LimitFixtures.Text(result.Diagnostics[0]));
		Assert.AreEqual(4, result.Diagnostics[0].Location.GetLineSpan().StartLinePosition.Line);

		Assert.AreEqual(
			2,
			result.Diagnostics[0].Location.GetLineSpan().StartLinePosition.Character
		);
	}

	[TestMethod]
	public async Task AChainOfFourBranchesPasses()
	{
		const string statements = """
			if (n == 1) { }
			else if (n == 2) { }
			else if (n == 3) { }
			else if (n == 4) { }
			""";

		AnalyzerRunResult result = await ReportAsync(LimitFixtures.Wrap(statements));

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheClosingElseIsNotABranch()
	{
		const string statements = """
			if (n == 1) { }
			else if (n == 2) { }
			else if (n == 3) { }
			else if (n == 4) { }
			else { }
			""";

		AnalyzerRunResult result = await ReportAsync(LimitFixtures.Wrap(statements));

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnIfWithoutAnElseIfIsOneBranch()
	{
		const string statements = """
			if (n == 1) { }
			else { }
			""";

		AnalyzerRunResult result = await ReportAsync(LimitFixtures.Wrap(statements), "1");

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnIfInsideAnElseBlockIsAChainOfItsOwn()
	{
		const string statements = """
			if (n == 1) { }
			else
			{
				if (n == 2) { }
				else if (n == 3) { }
				else if (n == 4) { }
			}
			""";

		Assert.IsEmpty((await ReportAsync(LimitFixtures.Wrap(statements))).Diagnostics);
		Assert.HasCount(1, (await ReportAsync(LimitFixtures.Wrap(statements), "2")).Diagnostics);
	}

	[TestMethod]
	public async Task AChainInsideTheBranchOfAChainIsReportedOnItsOwn()
	{
		const string statements = """
			if (n == 1)
			{
				if (n == 2) { }
				else if (n == 3) { }
				else if (n == 4) { }
				else if (n == 5) { }
				else if (n == 6) { }
			}
			else if (n == 7) { }
			""";

		AnalyzerRunResult result = await ReportAsync(LimitFixtures.Wrap(statements));

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual(6, result.Diagnostics[0].Location.GetLineSpan().StartLinePosition.Line);
	}

	[TestMethod]
	public async Task TwoLongChainsAreTwoDiagnostics()
	{
		const string statements = """
			if (n == 1) { }
			else if (n == 2) { }
			else if (n == 3) { }
			else if (n == 4) { }
			else if (n == 5) { }

			if (n == 6)
			{
				if (n == 7) { }
				else if (n == 8) { }
				else if (n == 9) { }
				else if (n == 10) { }
				else if (n == 11) { }
			}
			else if (n == 12) { }
			else if (n == 13) { }
			else if (n == 14) { }
			else if (n == 15) { }
			""";

		AnalyzerRunResult result = await ReportAsync(LimitFixtures.Wrap(statements));

		Assert.HasCount(3, result.Diagnostics);
	}

	[TestMethod]
	public async Task AChainInALambdaIsReported()
	{
		const string statements = """
			System.Action f = () =>
			{
				if (n == 1) { }
				else if (n == 2) { }
				else if (n == 3) { }
				else if (n == 4) { }
				else if (n == 5) { }
			};
			""";

		AnalyzerRunResult result = await ReportAsync(LimitFixtures.Wrap(statements));

		Assert.HasCount(1, result.Diagnostics);
	}

	[TestMethod]
	public async Task AChainWithAMissingLastBodyStillCountsItsBranches()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Run(int n)
				{
					if (n == 1) ;
					else if (n == 2) ;
					else if (n == 3) ;
					else if (n == 4) ;
					else if (n == 5)
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync(
			source,

			new RunOptions([new BreakUpLongElseIfChainAnalyzer()])
			{
				DiagnosticOptions = Rule,
				RequireCompiles = false,
			}
		);

		Assert.HasCount(1, result.Diagnostics);
	}

	[TestMethod]
	[DataRow("1", 1)]
	[DataRow("5", 1)]
	[DataRow("6", 0)]
	[DataRow("0", 0)]
	[DataRow("-1", 1)]
	[DataRow("1001", 1)]
	[DataRow("many", 1)]
	[DataRow("", 1)]
	public async Task TheLimitComesFromTheOptionAndZeroTurnsTheRuleOff(string limit, int reports)
	{
		const string statements = """
			if (n == 1) { }
			else if (n == 2) { }
			else if (n == 3) { }
			else if (n == 4) { }
			else if (n == 5) { }
			else if (n == 6) { }
			""";

		// A missing or invalid limit reads as the default of four, which reports the chain of six branches.
		AnalyzerRunResult result = await ReportAsync(LimitFixtures.Wrap(statements), limit);

		Assert.HasCount(reports, result.Diagnostics, $"A limit of '{limit}'.");
	}

	[TestMethod]
	public async Task ABranchUnderAConditionalDirectiveCountsInTheViewThatKeepsIt()
	{
		const string statements = """
			if (n == 1) { }
			else if (n == 2) { }
			else if (n == 3) { }
			else if (n == 4) { }
			#if EXTRA
			else if (n == 5) { }
			#endif
			""";

		string source = LimitFixtures.Wrap(statements);

		Assert.HasCount(1, (await ReportAsync("#define EXTRA\n" + source)).Diagnostics);
		Assert.IsEmpty((await ReportAsync(source)).Diagnostics);
	}

	[TestMethod]
	public async Task APragmaSilencesTheChainItCovers()
	{
		const string statements = """
			#pragma warning disable ZA1006
			if (n == 1) { }
			else if (n == 2) { }
			else if (n == 3) { }
			else if (n == 4) { }
			else if (n == 5) { }
			#pragma warning restore ZA1006
			""";

		AnalyzerRunResult result = await ReportAsync(LimitFixtures.Wrap(statements));

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AVeryLongChainIsOneDiagnosticAndIsWalkedWithoutRecursion()
	{
		string statements =
			"if (n == 0) { }\n"
			+ string.Concat(Enumerable.Range(1, 300).Select(i => $"else if (n == {i}) {{ }}\n"));

		AnalyzerRunResult result = await ReportAsync(LimitFixtures.Wrap(statements));

		Assert.HasCount(1, result.Diagnostics);
	}

	[TestMethod]
	public void TheCostGrowsLinearlyWithTheLengthOfAChain()
	{
		static string Chain(int count)
		{
			return LimitFixtures.Wrap(
				"if (n == 0) { }\n"
					+ string.Concat(
						Enumerable.Range(1, count).Select(i => $"else if (n == {i}) {{ }}\n")
					)
			);
		}

		AssertLinear(Chain(250), Chain(1_000));
	}

	[TestMethod]
	public void TheCostGrowsLinearlyWithTheNumberOfChains()
	{
		static string Many(int count)
		{
			return LimitFixtures.Wrap(
				string.Concat(
					Enumerable.Repeat(
						"if (n == 1) { } else if (n == 2) { } else if (n == 3) { }\n",
						count
					)
				)
			);
		}

		AssertLinear(Many(5_000), Many(20_000));
	}

	[TestMethod]
	public void TheCostGrowsLinearlyWithTheDepthOfANest()
	{
		static string Nest(int depth)
		{
			return LimitFixtures.Wrap(
				string.Concat(Enumerable.Repeat("if (n == 1) { } else if (n == 2) { ", depth))
					+ string.Concat(Enumerable.Repeat("} ", depth))
			);
		}

		AssertLinear(Nest(250), Nest(1_000));
	}

	private static void AssertLinear(string small, string large)
	{
		LinearCost.AssertLinear(
			small,
			large,
			root => ElseIfChains.CollectLongChains(root, 3, default)
		);
	}

	private static Task<AnalyzerRunResult> ReportAsync(string source, string? limit = null)
	{
		return RoslynTestHarness.ReportWithLimitAsync<BreakUpLongElseIfChainAnalyzer>(
			source,
			diagnosticOptions: Rule,
			key: "stanza_max_else_if_branches",
			limit
		);
	}
}
