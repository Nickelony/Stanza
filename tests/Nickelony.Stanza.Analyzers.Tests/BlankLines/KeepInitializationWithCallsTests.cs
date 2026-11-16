using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed class KeepInitializationWithCallsTests
{
	private static readonly IReadOnlyDictionary<string, ReportDiagnostic> s_on = TestRuleSets.Only(
		DiagnosticDescriptors.KeepInitializationWithCalls
	);

	[TestMethod]
	public async Task AnInitializationThatOpensAStanzaJoinsItsFirstCall()
	{
		const string source = """
			class C
			{
				void M()
				{
					Print();

					var item = new C();

					item.Call1();
					item.Call2();
				}
				void Print() { }
				void Call1() { }
				void Call2() { }
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					Print();

					var item = new C();
					item.Call1();
					item.Call2();
				}
				void Print() { }
				void Call1() { }
				void Call2() { }
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepInitializationWithCallsAnalyzer>(source, s_on);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ATightPrecedingStatementAMultiLineCallAndACommentKeepTheirGaps()
	{
		const string source = """
			class C
			{
				void M()
				{
					Print();
					var item = new C();

					item.Call();

					var other = new C();

					other.Call(
						1);

					var last = new C();

					// Configure the last item.
					last.Call();
				}
				void Print() { }
				void Call(int value = 0) { }
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepInitializationWithCallsAnalyzer>(source, s_on);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnInitializationAtTheBlockStartJoinsACallFromAFactory()
	{
		const string source = """
			class C
			{
				void M()
				{
					var item = Create();

					item.Call();
				}
				C Create() => new C();
				void Call() { }
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					var item = Create();
					item.Call();
				}
				C Create() => new C();
				void Call() { }
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepInitializationWithCallsAnalyzer>(source, s_on);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("(new C())")]
	[DataRow("((new C())!)")]
	[DataRow("(Create())")]
	[DataRow("((Create())!)")]
	[DataRow("(await CreateAsync())")]
	[DataRow("await (CreateAsync())!")]
	[DataRow("((await (CreateAsync())!)!)")]
	public async Task TransparentInitializerWrappersPreserveConstructionAndFactoryPairs(
		string value
	)
	{
		string source =
			"class C { async System.Threading.Tasks.Task M() {\nvar item = "
			+ value
			+ ";\n\nitem.Call();\n} static C Create() => new C(); static System.Threading.Tasks.Task<C> CreateAsync() => System.Threading.Tasks.Task.FromResult(new C()); void Call() {} }";

		string fixedSource = source.Replace(";\n\nitem.Call", ";\nitem.Call");

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepInitializationWithCallsAnalyzer>(source, s_on);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	public static IEnumerable<object[]> CheckedAndWeakSubsets { get; } =
		TestRuleSets.SubsetRows(
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.KeepRunOfStatementsTogether
		);

	[TestMethod]
	[CrossCutting]
	[DynamicData(
		nameof(CheckedAndWeakSubsets),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ProspectiveInitializationJoinsGiveJumpSeparationOneOwnerInEveryFixOrder(
		string name,
		DiagnosticDescriptor[] enabled
	)
	{
		const string source =
			"class ReviewItem { public void Check() {} } class C { ReviewItem M() {\nvar item = new ReviewItem();\n\nitem.Check();\nreturn item;\n} }";

		const string fixedSource =
			"class ReviewItem { public void Check() {} } class C { ReviewItem M() {\nvar item = new ReviewItem();\nitem.Check();\n\nreturn item;\n} }";

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepInitializationWithCalls,
			DiagnosticDescriptors.SeparateJumpStatement,
			.. enabled,
		]);

		Dictionary<string, string> options = new()
		{
			["stanza_guard_roots"] = "Check",
			["stanza_max_loose_run_size"] = "1",
		};

		AnalyzerRunResult reported = await RoslynTestHarness.ReportAllAsync(source, rules, options);

		// Without ZA0052, which owns the gaps of a run it gathers, the keeper and the jump rule report one gap each.
		if (!enabled.Contains(DiagnosticDescriptors.GatherRunOfLooseStatements))
		{
			Assert.HasCount(2, reported.Diagnostics);

			Assert.AreEqual(
				1,

				Enumerable.Count(
					reported.Diagnostics,
					diagnostic => diagnostic.Id == DiagnosticDescriptors.SeparateJumpStatement.Id
				)
			);
		}

		RoslynTestHarness.AssertEachGapIsClaimedOnce(reported);
		AnalyzerRunResult batch = await RoslynTestHarness.RunAllAsync(source, rules, options);
		RoslynTestHarness.AssertFixedText(fixedSource, batch, name);

		foreach (bool bottom in new[] { false, true })
		{
			AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
				source,

				new RunOptions(RoslynTestHarness.AllAnalyzers)
				{
					DiagnosticOptions = rules,
					AnalyzerConfigOptions = options,
					FixesBottomFirst = bottom,
				}
			);

			RoslynTestHarness.AssertFixedText(fixedSource, result);
		}

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			fixedSource,
			source,

			new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = rules,
				AnalyzerConfigOptions = options,
			}
		);
	}

	[TestMethod]
	[CrossCutting]
	[DataRow(false, false)]
	[DataRow(false, true)]
	[DataRow(true, false)]
	[DataRow(true, true)]
	public async Task NativePragmaFallbackAndDirectiveBarriersKeepTheirJumpOwnership(
		bool checks,
		bool barrier
	)
	{
		string source =
			"class ReviewItem { public void Check() {} } class C { ReviewItem M() {\nvar item = new ReviewItem();\n\nitem.Check();\nreturn item;\n} }";

		source = barrier
			? source.Replace("return item;", "#pragma warning disable ZA0009\nreturn item;")
			: "#pragma warning disable ZA0009\n" + source;

		string fixedSource = source.Replace("ReviewItem();\n\n", "ReviewItem();\n");

		if (checks && !barrier)
			fixedSource = fixedSource.Replace("return item;", "\nreturn item;");

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepInitializationWithCalls,
			DiagnosticDescriptors.SeparateJumpStatement,
			.. checks ? new[] { DiagnosticDescriptors.SeparateCheckedStanza } : [],
		]);

		Dictionary<string, string> options = new() { ["stanza_guard_roots"] = "Check" };
		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source, rules, options);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	[DataRow(false)]
	[DataRow(true)]
	public async Task ASilencedInitializationDoesNotInventAProspectiveJumpStanza(bool checks)
	{
		const string source =
			"#pragma warning disable ZA0062\nclass ReviewItem { public void Check() {} } class C { ReviewItem M() {\nvar item = new ReviewItem();\n\nitem.Check();\nreturn item;\n} }";

		string fixedSource = checks ? source.Replace("return item;", "\nreturn item;") : source;

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepInitializationWithCalls,
			DiagnosticDescriptors.SeparateJumpStatement,
			.. checks ? new[] { DiagnosticDescriptors.SeparateCheckedStanza } : [],
		]);

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,
			rules,
			new Dictionary<string, string> { ["stanza_guard_roots"] = "Check" }
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task AnOrdinaryCallAlsoJoinsBeforeJumpSeparationJudgesTheStanza()
	{
		const string source =
			"class ReviewItem { public void Call() {} } class C { ReviewItem M() {\nvar item = new ReviewItem();\n\nitem.Call();\nreturn item;\n} }";

		const string fixedSource =
			"class ReviewItem { public void Call() {} } class C { ReviewItem M() {\nvar item = new ReviewItem();\nitem.Call();\n\nreturn item;\n} }";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.OnSuppressingAllOthers([
				DiagnosticDescriptors.KeepInitializationWithCalls,
				DiagnosticDescriptors.SeparateJumpStatement,
			])
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	[DataRow(false)]
	[DataRow(true)]
	public async Task ARemovedUpperAnchorCannotInventAnInitializationJoinBeforeAJump(bool checks)
	{
		const string source =
			"class ReviewItem { public void Check() {} } class C { ReviewItem M() {\nvar previous = new ReviewItem();\n\nvar item = new ReviewItem();\n\nitem.Check();\nreturn item;\n} }";

		string fixedSource = source.Replace(
			"previous = new ReviewItem();\n\n",
			"previous = new ReviewItem();\n"
		);

		if (checks)
			fixedSource = fixedSource.Replace("return item;", "\nreturn item;");

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepInitializationWithCalls,
			DiagnosticDescriptors.SeparateJumpStatement,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			.. checks ? new[] { DiagnosticDescriptors.SeparateCheckedStanza } : [],
		]);

		Dictionary<string, string> options = new() { ["stanza_guard_roots"] = "Check" };
		AnalyzerRunResult reported = await RoslynTestHarness.ReportAllAsync(source, rules, options);

		Assert.IsFalse(
			reported.Diagnostics.Any(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.SeparateJumpStatement.Id
			)
		);

		AnalyzerRunResult batch = await RoslynTestHarness.RunAllAsync(source, rules, options);
		RoslynTestHarness.AssertFixedText(fixedSource, batch);

		foreach (bool bottom in new[] { false, true })
		{
			AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
				source,

				new RunOptions(RoslynTestHarness.AllAnalyzers)
				{
					DiagnosticOptions = rules,
					AnalyzerConfigOptions = options,
					FixesBottomFirst = bottom,
				}
			);

			RoslynTestHarness.AssertFixedText(fixedSource, result);
		}

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			fixedSource,
			source,

			new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = rules,
				AnalyzerConfigOptions = options,
			}
		);
	}
}
