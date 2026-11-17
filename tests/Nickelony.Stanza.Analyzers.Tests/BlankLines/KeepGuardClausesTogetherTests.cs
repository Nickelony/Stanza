using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed class KeepGuardClausesTogetherTests
{
	private static readonly IReadOnlyDictionary<string, ReportDiagnostic> s_on = TestRuleSets.Only(
		DiagnosticDescriptors.KeepGuardClausesTogether
	);

	[TestMethod]
	public async Task TwoOneLineGuardsLoseTheBlankLineBetweenThem()
	{
		const string source = """
			class C
			{
				void M(string first, string second)
				{
					if (first == null) return;

					if (second == null) return;
					Use(first, second);
				}

				void Use(string first, string second)
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(string first, string second)
				{
					if (first == null) return;
					if (second == null) return;
					Use(first, second);
				}

				void Use(string first, string second)
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepGuardClausesTogetherAnalyzer>(source, s_on);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task BracedGuardsAndAThrowHelperFormAChainAnywhereInTheBlock()
	{
		const string source = """
			class C
			{
				void M(string first, string second, int count)
				{
					Use(first, second);

					if (first == null)
					{
						return;
					}

					System.ArgumentNullException.ThrowIfNull(second);

					if (count < 0)
					{
						throw new System.ArgumentOutOfRangeException(nameof(count));
					}
					Use(first, second);
				}

				void Use(string first, string second)
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(string first, string second, int count)
				{
					Use(first, second);

					if (first == null)
					{
						return;
					}
					System.ArgumentNullException.ThrowIfNull(second);
					if (count < 0)
					{
						throw new System.ArgumentOutOfRangeException(nameof(count));
					}
					Use(first, second);
				}

				void Use(string first, string second)
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepGuardClausesTogetherAnalyzer>(source, s_on);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AGuardInALoopJoinsTheGuardBelowIt()
	{
		const string source = """
			class C
			{
				void M(string[] names)
				{
					foreach (string name in names)
					{
						if (name == null) continue;

						if (name.Length == 0) break;
						Use(name);
					}
				}

				void Use(string name)
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(string[] names)
				{
					foreach (string name in names)
					{
						if (name == null) continue;
						if (name.Length == 0) break;
						Use(name);
					}
				}

				void Use(string name)
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepGuardClausesTogetherAnalyzer>(source, s_on);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentOrADirectiveInTheGapKeepsTheBlankLine()
	{
		const string source = """
			class C
			{
				void M(string first, string second)
				{
					if (first == null) return;

					// The second name is optional elsewhere.
					if (second == null) return;
					Use(first, second);
				}

				void N(string first, string second)
				{
					if (first == null) return; // the caller checks this too

					if (second == null) return;
					Use(first, second);
				}

				void O(string first, string second)
				{
					if (first == null) return;

			#if DEBUG
					if (second == null) return;
			#endif
					Use(first, second);
				}

				void Use(string first, string second)
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepGuardClausesTogetherAnalyzer>(source, s_on);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnIfWithAnElseOrMoreThanAnExitIsNoGuard()
	{
		const string source = """
			class C
			{
				void M(string first, string second)
				{
					if (first == null) return; else Use(first, first);

					if (second == null) return;
					Use(first, second);
				}

				void N(string first, string second)
				{
					if (first == null)
					{
						Use(second, second);
						return;
					}

					if (second == null) return;
					Use(first, second);
				}

				void O(string first, string second)
				{
					Use(first, second);

					if (second == null) return;
				}

				void Use(string first, string second)
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepGuardClausesTogetherAnalyzer>(source, s_on);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheRuleWinsTheGapOverTheMultiLineRuleAndSettles()
	{
		const string source = """
			class C
			{
				void M(string first, string second)
				{
					if (first is null)
					{
						return;
					}

					if (second is null)
					{
						return;
					}
					Use(first, second);
				}

				void Use(string first, string second) { }
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(string first, string second)
				{
					if (first is null)
					{
						return;
					}
					if (second is null)
					{
						return;
					}

					Use(first, second);
				}

				void Use(string first, string second) { }
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.Only(
			DiagnosticDescriptors.KeepGuardClausesTogether,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateGuardClauses
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source, rules);

		RoslynTestHarness.AssertFixedText(fixedSource, result);

		AnalyzerRunResult settled = await RoslynTestHarness.ReportAllAsync(fixedSource, rules);

		Assert.IsEmpty(settled.Diagnostics);

		// With the rule off, the multi-line rule owns the gap again and writes its blank line back.
		AnalyzerRunResult off = await RoslynTestHarness.RunIterativelyAsync(
			fixedSource,
			TestRuleSets.Only(DiagnosticDescriptors.SeparateMultiLineConstructs)
		);

		const string separatedSource = """
			class C
			{
				void M(string first, string second)
				{
					if (first is null)
					{
						return;
					}

					if (second is null)
					{
						return;
					}

					Use(first, second);
				}

				void Use(string first, string second) { }
			}
			""";

		RoslynTestHarness.AssertFixedText(separatedSource, off);
	}

	[TestMethod]
	public async Task TheKeepRulesLeaveAGapThatTheTwoUnitOrTheRunRulesClaimAndSettle()
	{
		const string source = """
			class C
			{
				void TwoUnits(string first)
				{
					string name = first;

					if (name == null) return;
				}

				void Loose(string first, string second, string third)
				{
					if (first == null) return;

					if (second == null) return;

					if (third == null) return;

					first = second;
				}

				void Helpers(string first, string second)
				{
					System.ArgumentNullException.ThrowIfNull(first);

					System.ArgumentNullException.ThrowIfNull(second);
					first = second;
					second = first;
				}

				void Mixed(string text)
				{
					text = text + "1";

					string name = text;

					if (name == null) return;

					text = text + "2";
					int count = 0;

					foreach (char letter in text) count++;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void TwoUnits(string first)
				{
					string name = first;
					if (name == null) return;
				}

				void Loose(string first, string second, string third)
				{
					if (first == null) return;
					if (second == null) return;
					if (third == null) return;

					first = second;
				}

				void Helpers(string first, string second)
				{
					System.ArgumentNullException.ThrowIfNull(first);
					System.ArgumentNullException.ThrowIfNull(second);

					first = second;
					second = first;
				}

				void Mixed(string text)
				{
					text = text + "1";

					string name = text;
					if (name == null) return;

					text = text + "2";
					int count = 0;
					foreach (char letter in text) count++;
				}
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepGuardClausesTogether,
			DiagnosticDescriptors.KeepGuardWithBinding,
			DiagnosticDescriptors.KeepAccumulatorWithLoop,
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
		]);

		Dictionary<string, string> options = new() { ["stanza_max_loose_run_size"] = "2" };

		AnalyzerRunResult reported = await RoslynTestHarness.ReportAllAsync(source, rules, options);

		RoslynTestHarness.AssertEachGapIsClaimedOnce(reported);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,
			rules,
			options
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow(false)]
	[DataRow(true)]
	public async Task MultilineAssertionsDoNotReenterThroughAnOverlappingGuardRoot(bool separates)
	{
		const string source = """
			using System.Diagnostics;
			class C
			{
				void M(object? first, object? second)
				{
					Debug.Assert(
						first is not null);

					Debug.Assert(
						second is not null);
				}
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepGuardClausesTogether,
		]);

		if (separates)
			rules[DiagnosticDescriptors.SeparateMultiLineConstructs.Id] = ReportDiagnostic.Warn;

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepGuardClausesTogetherAnalyzer>(
				source,
				rules,
				new Dictionary<string, string> { ["stanza_guard_roots"] = "Debug" }
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ATopLevelChainOfGuardsJoins()
	{
		const string source = """
			var a = System.Environment.GetCommandLineArgs();
			if (a.Length == 0) return 1;

			if (a.Length > 9) return 2;
			return 0;
			""";

		const string fixedSource = """
			var a = System.Environment.GetCommandLineArgs();
			if (a.Length == 0) return 1;
			if (a.Length > 9) return 2;
			return 0;
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepGuardClausesTogetherAnalyzer>(source, s_on);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task APragmaOverTheChainReleasesItsGapToTheMultiLineRule()
	{
		const string source = """
			class C
			{
				void M(int x)
				{
			#pragma warning disable ZA0057
					System.GC.KeepAlive(x);
					if (x == 0)
					{
						return;
					}
					if (x == 1)
					{
						return;
					}
			#pragma warning restore ZA0057
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int x)
				{
			#pragma warning disable ZA0057
					System.GC.KeepAlive(x);

					if (x == 0)
					{
						return;
					}

					if (x == 1)
					{
						return;
					}
			#pragma warning restore ZA0057
				}
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepGuardClausesTogether,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
		]);

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			fixedSource,
			source,
			new RunOptions(RoslynTestHarness.AllAnalyzers) { DiagnosticOptions = rules }
		);
	}
}
