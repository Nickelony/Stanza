using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Limits;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Limits;

[TestClass]
public sealed class FlattenDeeplyNestedCodeTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> Rule { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.FlattenDeeplyNestedCode);

	// Each statement is one level, and the one before the last holds it, so the whole nest is four deep.
	public static IEnumerable<object[]> Levels { get; } =
	[
		["an if", "if (a) { }"],
		["a for", "for (int i = 0; i < 1; i++) { }"],
		["a foreach", "foreach (int x in xs) { }"],
		["a while", "while (a) { }"],
		["a do", "do { } while (a);"],
		["a switch", "switch (n) { default: break; }"],
		["a try", "try { } catch { }"],
		["a lock", "lock (xs) { }"],
		["a using", "using (System.IO.Stream s = null) { }"],
	];

	[TestMethod]
	public async Task ANestOfFourLevelsIsReportedAtItsInnermostStatement()
	{
		const string statements = """
			if (a)
			{
				if (a)
				{
					if (a)
					{
						if (a)
						{
						}
					}
				}
			}
			""";

		AnalyzerRunResult result = await ReportAsync(Wrap(statements));

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual(10, result.Diagnostics[0].Location.GetLineSpan().StartLinePosition.Line);

		Assert.AreEqual(
			5,
			result.Diagnostics[0].Location.GetLineSpan().StartLinePosition.Character
		);
	}

	[TestMethod]
	public async Task ANestOfThreeLevelsPasses()
	{
		AnalyzerRunResult result = await ReportAsync(Wrap("if (a) { if (a) { if (a) { } } }"));
		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	[DynamicData(
		nameof(Levels),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task EachKindOfStatementAddsALevel(string name, string level)
	{
		string statements =
			$"if (a)\n{{\n\tif (a)\n\t{{\n\t\tif (a)\n\t\t{{\n\t\t\t{level}\n\t\t}}\n\t}}\n}}";

		AnalyzerRunResult result = await ReportAsync(Wrap(statements));

		Assert.HasCount(1, result.Diagnostics, name);

		Assert.AreEqual(
			10,
			result.Diagnostics[0].Location.GetLineSpan().StartLinePosition.Line,
			name
		);
	}

	[TestMethod]
	[DynamicData(
		nameof(Levels),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task EachKindOfStatementPassesAtTheLimit(string name, string level)
	{
		string statements = $"if (a)\n{{\n\tif (a)\n\t{{\n\t\t{level}\n\t}}\n}}";

		AnalyzerRunResult result = await ReportAsync(Wrap(statements));

		Assert.IsEmpty(result.Diagnostics, name);
	}

	[TestMethod]
	public async Task AnElseIfDoesNotAddALevel()
	{
		const string statements = """
			if (a) { }
			else if (a) { }
			else if (a) { }
			else if (a) { }
			else if (a) { }
			else if (a) { }
			else if (a) { }
			else { }
			""";

		AnalyzerRunResult result = await ReportAsync(Wrap(statements));

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheBodyOfAnElseIfNestsFromTheHeadOfItsChain()
	{
		const string passes = """
			if (a) { }
			else if (a) { }
			else if (a)
			{
				if (a)
				{
					if (a) { }
				}
			}
			""";

		const string reported = """
			if (a) { }
			else if (a) { }
			else if (a)
			{
				if (a)
				{
					if (a)
					{
						if (a) { }
					}
				}
			}
			""";

		Assert.IsEmpty((await ReportAsync(Wrap(passes))).Diagnostics);
		Assert.HasCount(1, (await ReportAsync(Wrap(reported))).Diagnostics);
	}

	[TestMethod]
	public async Task AnIfInsideAnElseBlockAddsALevel()
	{
		const string statements = """
			if (a) { }
			else
			{
				if (a)
				{
					if (a)
					{
						if (a) { }
					}
				}
			}
			""";

		AnalyzerRunResult result = await ReportAsync(Wrap(statements));

		Assert.HasCount(1, result.Diagnostics);
	}

	[TestMethod]
	public async Task ABareBlockAddsNoLevel()
	{
		AnalyzerRunResult result = await ReportAsync(Wrap("{ { { { { if (a) { } } } } } }"));
		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AMemberReportsOnceAtItsDeepestStatement()
	{
		const string statements = """
			if (a) { if (a) { if (a) { if (a) { } } } }
			if (a) { if (a) { if (a) { if (a) { if (a) { } } } } }
			if (a) { if (a) { if (a) { if (a) { } } } }
			""";

		AnalyzerRunResult result = await ReportAsync(Wrap(statements));

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual(5, result.Diagnostics[0].Location.GetLineSpan().StartLinePosition.Line);
	}

	[TestMethod]
	public async Task OfTwoNestsOfTheSameDepthTheFirstIsReported()
	{
		const string statements = """
			if (a) { if (a) { if (a) { if (a) { } } } }
			if (a) { if (a) { if (a) { if (a) { } } } }
			""";

		AnalyzerRunResult result = await ReportAsync(Wrap(statements));

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual(4, result.Diagnostics[0].Location.GetLineSpan().StartLinePosition.Line);
	}

	[TestMethod]
	public async Task EachMemberIsJudgedOnItsOwn()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _value;

				public Sample(bool a)
				{
					if (a) { if (a) { if (a) { if (a) { } } } }
				}

				public int Value
				{
					get
					{
						if (_value > 0) { if (_value > 1) { if (_value > 2) { if (_value > 3) { } } } }

						return _value;
					}
				}

				public static void Run(bool a)
				{
					if (a) { if (a) { if (a) { if (a) { } } } }
				}

				public static void Calm(bool a)
				{
					if (a) { if (a) { } }
				}
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source);

		Assert.HasCount(3, result.Diagnostics);
	}

	public static IEnumerable<object[]> Functions { get; } =
	[
		["a lambda", "System.Action f = () => { INNER };"],
		["an anonymous method", "System.Action f = delegate { INNER };"],
		["a local function", "void F() { INNER }"],
	];

	[TestMethod]
	[DynamicData(
		nameof(Functions),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AFunctionStartsACountOfItsOwn(string name, string function)
	{
		string inner = "if (a) { if (a) { if (a) { } } }";

		string statements =
			$"if (a)\n{{\n\tif (a)\n\t{{\n\t\t{function.Replace("INNER", inner, StringComparison.Ordinal)}\n\t}}\n}}";

		// The code around the function nests two deep and the function three, and a sum would be five.
		Assert.IsEmpty((await ReportAsync(Wrap(statements))).Diagnostics, name);
		Assert.HasCount(1, (await ReportAsync(Wrap(statements), "2")).Diagnostics, name);
	}

	[TestMethod]
	public async Task AFunctionReportsBesideTheMemberAroundIt()
	{
		const string statements = """
			if (a) { if (a) { if (a) { if (a) { } } } }
			System.Action f = () => { if (a) { if (a) { if (a) { if (a) { } } } } };
			""";

		AnalyzerRunResult result = await ReportAsync(Wrap(statements));

		Assert.HasCount(2, result.Diagnostics);
	}

	[TestMethod]
	public async Task TopLevelStatementsAreJudgedAsOneBody()
	{
		const string source = """
			bool a = args.Length > 0;

			if (a) { if (a) { if (a) { if (a) { } } } }
			""";

		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync(
			source,

			new RunOptions([new FlattenDeeplyNestedCodeAnalyzer()])
			{
				DiagnosticOptions = Rule,
				RequireCompiles = false,
			}
		);

		Assert.HasCount(1, result.Diagnostics);
	}

	[TestMethod]
	[DataRow("1", 1)]
	[DataRow("3", 1)]
	[DataRow("4", 0)]
	[DataRow("0", 0)]
	[DataRow("-1", 1)]
	[DataRow("1001", 1)]
	[DataRow("deep", 1)]
	[DataRow("", 1)]
	public async Task TheLimitComesFromTheOptionAndZeroTurnsTheRuleOff(string limit, int reports)
	{
		AnalyzerRunResult result = await ReportAsync(
			Wrap("if (a) { if (a) { if (a) { if (a) { } } } }"),
			limit
		);

		Assert.HasCount(reports, result.Diagnostics, $"A limit of '{limit}'.");
	}

	[TestMethod]
	public async Task ALevelUnderAConditionalDirectiveCountsInTheViewThatKeepsIt()
	{
		const string statements = """
			if (a)
			{
				if (a)
				{
					if (a)
					{
			#if DEEP
						if (a) { }
			#endif
					}
				}
			}
			""";

		string source = Wrap(statements);

		Assert.HasCount(1, (await ReportAsync("#define DEEP\n" + source)).Diagnostics);
		Assert.IsEmpty((await ReportAsync(source)).Diagnostics);
	}

	[TestMethod]
	public async Task AMissingBodyStillCountsTheLevelsAroundIt()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Run(bool a)
				{
					if (a) { if (a) { if (a) { if (a)
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync(
			source,

			new RunOptions([new FlattenDeeplyNestedCodeAnalyzer()])
			{
				DiagnosticOptions = Rule,
				RequireCompiles = false,
			}
		);

		Assert.HasCount(1, result.Diagnostics);
	}

	[TestMethod]
	public async Task APragmaSilencesTheStatementItCovers()
	{
		const string statements = """
			#pragma warning disable ZA1004
			if (a) { if (a) { if (a) { if (a) { } } } }
			#pragma warning restore ZA1004
			""";

		AnalyzerRunResult result = await ReportAsync(Wrap(statements));

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AVeryDeepNestIsWalkedWithoutRecursion()
	{
		const int depth = 400;

		string statements =
			string.Concat(Enumerable.Repeat("if (a) { ", depth))
			+ string.Concat(Enumerable.Repeat("} ", depth));

		AnalyzerRunResult result = await ReportAsync(Wrap(statements));

		Assert.HasCount(1, result.Diagnostics);
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
	public void TheCostGrowsLinearlyWithTheLengthOfABody()
	{
		static string Flat(int count)
		{
			string statements = string.Concat(Enumerable.Repeat("if (a) { if (a) { } }\n", count));
			return Wrap(statements);
		}

		AssertLinear(Flat(5_000), Flat(20_000));
	}

	private static void AssertLinear(string small, string large)
	{
		LinearCost.AssertLinear(
			small,
			large,
			root => NestedStatements.CollectDeepest(root, 3, default)
		);
	}

	private static string Wrap(string statements)
	{
		return LimitFixtures.Wrap(
			statements,
			signature: "internal static void Run(bool a, int n, int[] xs)"
		);
	}

	private static Task<AnalyzerRunResult> ReportAsync(string source, string? limit = null)
	{
		return RoslynTestHarness.ReportWithLimitAsync<FlattenDeeplyNestedCodeAnalyzer>(
			source,
			diagnosticOptions: Rule,
			key: "stanza_max_nesting_depth",
			limit
		);
	}
}
