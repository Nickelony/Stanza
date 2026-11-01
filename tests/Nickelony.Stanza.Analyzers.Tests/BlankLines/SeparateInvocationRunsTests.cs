using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed class SeparateInvocationRunsTests
{
	[TestMethod]
	public async Task RepeatedInvocationGroupGetsSeparated()
	{
		const string source = """
			class C
			{
				void M()
				{
					var value = 1;
					DoThing();
					DoThing();
					return;
				}

				void DoThing()
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					var value = 1;

					DoThing();
					DoThing();

					return;
				}

				void DoThing()
				{
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateInvocationRunsAnalyzer>(
			source
		);

		Assert.HasCount(2, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AttachedCommentMovesWithTheNextRun()
	{
		const string source = """
			class C
			{
				void M()
				{
					var value = 1;
					// first call
					DoThing();
					DoThing();
					return;
				}

				void DoThing()
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					var value = 1;

					// first call
					DoThing();
					DoThing();

					return;
				}

				void DoThing()
				{
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateInvocationRunsAnalyzer>(
			source
		);

		Assert.HasCount(2, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task RepeatedNullConditionalInvocationsGetSeparated()
	{
		const string source = """
			class C
			{
				void M(C? other)
				{
					var value = 1;
					other?.DoThing();
					other?.DoThing();
					return;
				}

				void DoThing()
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(C? other)
				{
					var value = 1;

					other?.DoThing();
					other?.DoThing();

					return;
				}

				void DoThing()
				{
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateInvocationRunsAnalyzer>(
			source
		);

		Assert.HasCount(2, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ANullConditionalInvocationAndAPlainOneOfTheSameNameFormOneRun()
	{
		const string source = """
			class C
			{
				void M(C? other)
				{
					var value = 1;
					other?.DoThing();
					other!.DoThing();
					return;
				}

				void DoThing()
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(C? other)
				{
					var value = 1;

					other?.DoThing();
					other!.DoThing();

					return;
				}

				void DoThing()
				{
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateInvocationRunsAnalyzer>(
			source
		);

		Assert.HasCount(2, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ADiscardedCallJoinsTheRunOfTheMethodItCalls()
	{
		const string source = """
			class C
			{
				void M()
				{
					var value = 1;
					Read();
					_ = Read();
					value = 2;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					var value = 1;

					Read();
					_ = Read();

					value = 2;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateInvocationRunsAnalyzer>(
			source
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ADiscardedAwaitedCallIsNamedLikeTheCallItAwaits()
	{
		const string source = """
			class C
			{
				async System.Threading.Tasks.Task M()
				{
					var value = 1;
					_ = await Load();
					await Load();
					_ = Save();
					_ = value;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				async System.Threading.Tasks.Task M()
				{
					var value = 1;

					_ = await Load();
					await Load();

					_ = Save();
					_ = value;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateInvocationRunsAnalyzer>(
			source
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task DiscardsOfDifferentMethodsAndOfNoCallFormNoRun()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					var other = 1;
					_ = Load();
					_ = Save();
					_ = value;
					_ = value;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateInvocationRunsAnalyzer>(
			source
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AwaitedCallsOfDifferentMethodsFormNoRunWhateverTheyChainOn()
	{
		const string source = """
			class C
			{
				async System.Threading.Tasks.Task M()
				{
					var value = 1;
					await Load().ConfigureAwait(false);
					await Save().ConfigureAwait(false);
					await Load();
					await Save();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateInvocationRunsAnalyzer>(
			source
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ChainsOfDifferentRootsFormNoRunWhateverTheyEndOn()
	{
		const string source = """
			class C
			{
				void M()
				{
					var value = 1;
					First().ToArray();
					Second().ToArray();
					First().ToList();
					Second().ToList();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateInvocationRunsAnalyzer>(
			source
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ChainsOfOneRootFormARunWhateverTheyEndOn()
	{
		const string source = """
			class C
			{
				async System.Threading.Tasks.Task M()
				{
					var value = 1;
					await Load(1).ConfigureAwait(false);
					await Load(2);
					First().ToArray();
					First().ToList();
					value = 2;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				async System.Threading.Tasks.Task M()
				{
					var value = 1;

					await Load(1).ConfigureAwait(false);
					await Load(2);

					First().ToArray();
					First().ToList();

					value = 2;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateInvocationRunsAnalyzer>(
			source
		);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ALoneDeclarationAboveACallRunTakesItsBlankLineFromThisRule()
	{
		const string source = """
			class C
			{
				void M()
				{
					Prepare();
					var value = 1;
					DoThing();
					DoThing();
				}

				void Prepare()
				{
				}

				void DoThing()
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					Prepare();

					var value = 1;

					DoThing();
					DoThing();
				}

				void Prepare()
				{
				}

				void DoThing()
				{
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateInvocationRunsAnalyzer>(
			source
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ALoneDeclarationStaysTightAgainstACallRunWhereThisRuleIsOff()
	{
		const string source = """
			class C
			{
				void M()
				{
					Prepare();
					var value = 1;
					DoThing();
					DoThing();
				}

				void Prepare()
				{
				}

				void DoThing()
				{
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.ReportRunRulesAsync(
			source,
			TestRuleSets.WithOneOff(on: [], off: DiagnosticDescriptors.SeparateInvocationRuns)
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheDeclarationRuleOwnsTheGapBetweenADeclarationRunAndACallRun()
	{
		const string source = """
			class C
			{
				void M()
				{
					var first = 1;
					var second = 2;
					DoThing();
					DoThing();
				}

				void DoThing()
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					var first = 1;
					var second = 2;

					DoThing();
					DoThing();
				}

				void DoThing()
				{
				}
			}
			""";

		AnalyzerRunResult both = await RoslynTestHarness.RunRunRulesAsync(source);

		Assert.HasCount(1, both.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.SeparateDeclarationRuns.Id, both.Diagnostics[0].Id);

		RoslynTestHarness.AssertFixedText(fixedSource, both);

		// With the declaration rule off, the same gap goes to this rule and settles on the same text.
		AnalyzerRunResult handedOver = await RoslynTestHarness.RunRunRulesAsync(
			source,
			TestRuleSets.WithOneOff(on: [], off: DiagnosticDescriptors.SeparateDeclarationRuns)
		);

		Assert.HasCount(1, handedOver.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.SeparateInvocationRuns.Id,
			handedOver.Diagnostics[0].Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, handedOver);
	}

	[TestMethod]
	public async Task APragmaOverTheDeclarationRuleHandsItsGapToThisRule()
	{
		string source = $$"""
			class C
			{
				void M()
				{
			#pragma warning disable {{DiagnosticDescriptors.SeparateDeclarationRuns.Id}}
					var first = 1;
					var second = 2;
					DoThing();
					DoThing();
			#pragma warning restore {{DiagnosticDescriptors.SeparateDeclarationRuns.Id}}
				}

				void DoThing()
				{
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.ReportRunRulesAsync(source);

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.SeparateInvocationRuns.Id, result.Diagnostics[0].Id);
	}

	[TestMethod]
	public async Task AnAssertionIsNoInvocationWhereTheAssertionRuleIsOff()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					Prepare();
					Assert.That(value);
					Assert.That(value);
					Prepare();
				}

				void Prepare()
				{
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.ReportRunRulesAsync(
			source,
			TestRuleSets.WithOneOff(on: [], off: DiagnosticDescriptors.SeparateAssertionRuns)
		);

		Assert.IsEmpty(result.Diagnostics, "ZA0004 does not take over a run that ZA0005 owns.");
	}

	[TestMethod]
	[DataRow("")]
	[DataRow("#define A\n")]
	public async Task AnInactiveConditionalBlockLeavesTheRunToTheViewThatHoldsIt(string view)
	{
		string source =
			view
			+ """
				class C
				{
					void M()
					{
						var value = 1;
						DoThing();
				#if A
						DoThing();
				#endif
						return;
					}

					void DoThing()
					{
					}
				}
				""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateInvocationRunsAnalyzer>(
				source,

				TestRuleSets.WithOneOff(
					on: [DiagnosticDescriptors.SeparateInvocationRuns],
					off: DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors
				)
			);

		// The run holds both calls only in the view that defines A, and a directive keeps the gap below it as written.
		Assert.HasCount(view.Length == 0 ? 0 : 1, result.Diagnostics, $"View [{view.Trim()}].");
	}

	[TestMethod]
	[DataRow("")]
	[DataRow("#define A\n")]
	public async Task AConditionalBlockEndsTheRunInEveryViewWhereItsRuleIsOn(string view)
	{
		string source =
			view
			+ """
				class C
				{
					void M()
					{
						var value = 1;
						DoThing();
				#if A
						DoThing();
				#endif
						return;
					}

					void DoThing()
					{
					}
				}
				""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateInvocationRunsAnalyzer>(
				source,

				TestRuleSets.Only(
					DiagnosticDescriptors.SeparateInvocationRuns,
					DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors
				)
			);

		AnalyzerRunResult handedOver =
			await RoslynTestHarness.ReportAsync<SeparateInvocationRunsAnalyzer>(
				source,

				TestRuleSets.WithOneOff(
					on: [],
					off: DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors
				)
			);

		Assert.IsEmpty(result.Diagnostics, $"View [{view.Trim()}].");

		Assert.HasCount(
			view.Length == 0 ? 0 : 1,
			handedOver.Diagnostics,
			$"View [{view.Trim()}], with ZA0402 off."
		);
	}

	[TestMethod]
	[DataRow("")]
	[DataRow("#define A\n")]
	public async Task ARegionMarkerEndsTheRunInEveryViewWhereItsRuleIsOn(string view)
	{
		string source =
			view
			+ """
				class C
				{
					void M()
					{
						var value = 1;
						DoThing();
				#region Pair
						DoThing();
				#endregion
						return;
					}

					void DoThing()
					{
					}
				}
				""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateInvocationRunsAnalyzer>(
				source,

				TestRuleSets.Only(
					DiagnosticDescriptors.SeparateInvocationRuns,
					DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors
				)
			);

		AnalyzerRunResult handedOver =
			await RoslynTestHarness.ReportAsync<SeparateInvocationRunsAnalyzer>(
				source,

				TestRuleSets.WithOneOff(
					on: [],
					off: DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors
				)
			);

		Assert.IsEmpty(result.Diagnostics, $"View [{view.Trim()}].");
		Assert.HasCount(1, handedOver.Diagnostics, $"View [{view.Trim()}], with ZA0401 off.");
	}
}
