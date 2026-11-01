using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed class SeparateAssertionRunsTests
{
	[TestMethod]
	public async Task AssertionRootGroupsDifferentAssertionMethods()
	{
		const string source = """
			class C
			{
				void M()
				{
					var value = 1;
					Assert.That(value);
					Assert.AreEqual(1, value);
					return;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					var value = 1;

					Assert.That(value);
					Assert.AreEqual(1, value);

					return;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateAssertionRunsAnalyzer>(
			source
		);

		Assert.HasCount(2, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARootWithADotMatchesNothingAndLeavesTheOtherRootsAlone()
	{
		const string source = """
			internal static class Checks
			{
				internal static class Verify
				{
					internal static void That(int value)
					{
					}

					internal static void AreEqual(int left, int right)
					{
					}
				}
			}

			internal sealed class Verifier
			{
				internal void That(int value)
				{
				}

				internal void AreEqual(int left, int right)
				{
				}
			}

			internal sealed class Holder
			{
				internal Verifier Verify { get; } = new();
			}

			internal static class Factory
			{
				internal static Holder Get() => new();
			}

			class C
			{
				void Qualified(int value)
				{
					var actual = 1;
					Checks.Verify.That(value);
					Checks.Verify.AreEqual(1, value);
					return;
				}

				void Chained(int value)
				{
					var actual = 1;
					Factory.Get().Verify.That(value);
					Factory.Get().Verify.AreEqual(1, value);
					return;
				}
			}
			""";

		// Each method has an assertion run, so a root that names Verify finds two boundaries in each.
		AnalyzerRunResult plain =
			await RoslynTestHarness.ReportAsync<SeparateAssertionRunsAnalyzer>(
				source,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_assertion_roots"] = "Verify",
				}
			);

		Assert.HasCount(4, plain.Diagnostics);

		// A root with a dot names no segment, so it matches nothing and the other roots still match.
		AnalyzerRunResult dotted =
			await RoslynTestHarness.ReportAsync<SeparateAssertionRunsAnalyzer>(
				source,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_assertion_roots"] = "Verify, Checks.Verify",
				}
			);

		Assert.HasCount(4, dotted.Diagnostics);

		// Alone, the dotted root replaces the defaults and matches no call, so no run forms.
		AnalyzerRunResult dottedAlone =
			await RoslynTestHarness.ReportAsync<SeparateAssertionRunsAnalyzer>(
				source,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_assertion_roots"] = "Checks.Verify",
				}
			);

		Assert.IsEmpty(dottedAlone.Diagnostics);
	}

	[TestMethod]
	[DataRow("", DisplayName = "an empty value")]
	[DataRow(" , ,", DisplayName = "a value of separators only")]
	public async Task ARootsKeyThatListsNoRootKeepsTheDefaultRoots(string value)
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					var actual = 1;
					Assert.That(value);
					Assert.AreEqual(1, value);
					return;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateAssertionRunsAnalyzer>(
				source,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_assertion_roots"] = value,
				}
			);

		Assert.HasCount(2, result.Diagnostics);
	}

	[TestMethod]
	public async Task TheWordNoneSwitchesTheAssertionGroupingOffAndLeavesTheCallsToTheInvocationRule()
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

		var none = new Dictionary<string, string> { ["stanza_assertion_roots"] = "none" };

		AnalyzerRunResult defaults =
			await RoslynTestHarness.ReportAsync<SeparateAssertionRunsAnalyzer>(source);

		AnalyzerRunResult assertions =
			await RoslynTestHarness.ReportAsync<SeparateAssertionRunsAnalyzer>(
				source,
				analyzerConfigOptions: none
			);

		AnalyzerRunResult calls =
			await RoslynTestHarness.ReportAsync<SeparateInvocationRunsAnalyzer>(
				source,
				analyzerConfigOptions: none
			);

		Assert.HasCount(2, defaults.Diagnostics);
		Assert.IsEmpty(assertions.Diagnostics, "No call is an assertion when the key says none.");
		Assert.HasCount(2, calls.Diagnostics);
	}

	[TestMethod]
	public async Task ChangingAnalyzerOptionsOnTheSameTreeIsPickedUp()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					var actual = 1;
					Verify.That(value);
					Verify.AreEqual(1, value);
					return;
				}
			}
			""";

		var configuredRoots = new Dictionary<string, string>
		{
			["stanza_assertion_roots"] = "Verify",
		};

		(ImmutableArray<Diagnostic> first, ImmutableArray<Diagnostic> second) =
			await RoslynTestHarness.RunTwiceOnSameTreeAsync<SeparateAssertionRunsAnalyzer>(
				source,
				configuredRoots,
				new Dictionary<string, string>()
			);

		// The first run groups the Verify chain into one repeated group,
		// the second run falls back to the default roots even though it analyzes the same syntax tree.
		Assert.HasCount(2, first);
		Assert.IsEmpty(second);
	}

	[TestMethod]
	public async Task ARootThatTheInvocationRuleWouldNameMovesTheRunToThisRule()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					Prepare();
					Verify.That(value);
					Verify.That(value);
					Prepare();
				}

				void Prepare()
				{
				}
			}
			""";

		var roots = new Dictionary<string, string> { ["stanza_assertion_roots"] = "Verify" };

		// By default Verify is no root, so the two calls of That form an invocation run.
		AnalyzerRunResult asCalls =
			await RoslynTestHarness.ReportAsync<SeparateInvocationRunsAnalyzer>(source);

		AnalyzerRunResult asAssertions =
			await RoslynTestHarness.ReportAsync<SeparateAssertionRunsAnalyzer>(
				source,
				analyzerConfigOptions: roots
			);

		Assert.HasCount(2, asCalls.Diagnostics);
		Assert.HasCount(2, asAssertions.Diagnostics);

		AnalyzerRunResult withRoots =
			await RoslynTestHarness.ReportAsync<SeparateInvocationRunsAnalyzer>(
				source,
				analyzerConfigOptions: roots
			);

		Assert.IsEmpty(withRoots.Diagnostics, "The run belongs to ZA0005 once the root names it.");
	}

	[TestMethod]
	public async Task ALoneDeclarationAboveAnAssertionRunTakesItsBlankLineFromThisRule()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					Prepare();
					var actual = 1;
					Assert.That(value);
					Assert.That(actual);
				}

				void Prepare()
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int value)
				{
					Prepare();

					var actual = 1;

					Assert.That(value);
					Assert.That(actual);
				}

				void Prepare()
				{
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateAssertionRunsAnalyzer>(
			source
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);

		AnalyzerRunResult off = await RoslynTestHarness.ReportRunRulesAsync(
			source,
			TestRuleSets.WithOneOff(on: [], off: DiagnosticDescriptors.SeparateAssertionRuns)
		);

		Assert.IsEmpty(
			off.Diagnostics,
			"The declaration stays tight against a run whose rule is off."
		);
	}

	[TestMethod]
	public async Task AssertionsStayTightWhereThisRuleIsOff()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					var actual = 1;
					Assert.That(value);
					Assert.That(actual);
					return;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.ReportRunRulesAsync(
			source,
			TestRuleSets.WithOneOff(on: [], off: DiagnosticDescriptors.SeparateAssertionRuns)
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheInvocationRuleOwnsTheGapBetweenACallRunAndAnAssertionRun()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					Prepare();
					Prepare();
					Assert.That(value);
					Assert.That(value + 1);
				}

				void Prepare()
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int value)
				{
					Prepare();
					Prepare();

					Assert.That(value);
					Assert.That(value + 1);
				}

				void Prepare()
				{
				}
			}
			""";

		AnalyzerRunResult both = await RoslynTestHarness.RunRunRulesAsync(source);

		Assert.HasCount(1, both.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.SeparateInvocationRuns.Id, both.Diagnostics[0].Id);

		RoslynTestHarness.AssertFixedText(fixedSource, both);

		AnalyzerRunResult handedOver = await RoslynTestHarness.RunRunRulesAsync(
			source,
			TestRuleSets.WithOneOff(on: [], off: DiagnosticDescriptors.SeparateInvocationRuns)
		);

		Assert.HasCount(1, handedOver.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.SeparateAssertionRuns.Id,
			handedOver.Diagnostics[0].Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, handedOver);
	}

	[TestMethod]
	public async Task TheDeclarationRuleOwnsTheGapBetweenADeclarationRunAndAnAssertionRun()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					var first = 1;
					var second = 2;
					Assert.That(value);
					Assert.That(first);
				}
			}
			""";

		AnalyzerRunResult both = await RoslynTestHarness.ReportRunRulesAsync(source);

		Assert.HasCount(1, both.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.SeparateDeclarationRuns.Id, both.Diagnostics[0].Id);

		AnalyzerRunResult handedOver = await RoslynTestHarness.ReportRunRulesAsync(
			source,
			TestRuleSets.WithOneOff(on: [], off: DiagnosticDescriptors.SeparateDeclarationRuns)
		);

		Assert.HasCount(1, handedOver.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.SeparateAssertionRuns.Id,
			handedOver.Diagnostics[0].Id
		);
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
					void M(int value)
					{
						var first = 1;
						Assert.That(value);
				#if A
						Assert.AreEqual(1, value);
				#endif
						return;
					}
				}
				""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateAssertionRunsAnalyzer>(
				source,

				TestRuleSets.WithOneOff(
					on: [DiagnosticDescriptors.SeparateAssertionRuns],
					off: DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors
				)
			);

		// The run holds both assertions only in the view that defines A, and a directive keeps the gap below it as written.
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
					void M(int value)
					{
						var first = 1;
						Assert.That(value);
				#if A
						Assert.AreEqual(1, value);
				#endif
						return;
					}
				}
				""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateAssertionRunsAnalyzer>(
				source,

				TestRuleSets.Only(
					DiagnosticDescriptors.SeparateAssertionRuns,
					DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors
				)
			);

		AnalyzerRunResult handedOver =
			await RoslynTestHarness.ReportAsync<SeparateAssertionRunsAnalyzer>(
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
					void M(int value)
					{
						var first = 1;
						Assert.That(value);
				#region Pair
						Assert.AreEqual(1, value);
				#endregion
						return;
					}
				}
				""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateAssertionRunsAnalyzer>(
				source,

				TestRuleSets.Only(
					DiagnosticDescriptors.SeparateAssertionRuns,
					DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors
				)
			);

		AnalyzerRunResult handedOver =
			await RoslynTestHarness.ReportAsync<SeparateAssertionRunsAnalyzer>(
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
