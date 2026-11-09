using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed class SeparateSameTypeDeclarationRunsTests
{
	private static readonly IReadOnlyDictionary<string, ReportDiagnostic> s_on = TestRuleSets.Only(
		DiagnosticDescriptors.SeparateSameTypeDeclarationRuns
	);

	[TestMethod]
	public async Task AModifierBoundaryIsLeftToTheModifierRuleWhileItIsOn()
	{
		const string source = """
			class C
			{
				private static int s_count;
				private static int s_limit;
				private string _first;
				private string _second;
			}
			""";

		const string fixedSource = """
			class C
			{
				private static int s_count;
				private static int s_limit;

				private string _first;
				private string _second;
			}
			""";

		AnalyzerRunResult modifierRuleOff =
			await RoslynTestHarness.RunAsync<SeparateSameTypeDeclarationRunsAnalyzer>(source, s_on);

		AnalyzerRunResult modifierRuleOn =
			await RoslynTestHarness.RunAsync<SeparateSameTypeDeclarationRunsAnalyzer>(
				source,

				TestRuleSets.Only(
					DiagnosticDescriptors.SeparateModifierGroups,
					DiagnosticDescriptors.SeparateSameTypeDeclarationRuns
				)
			);

		Assert.HasCount(1, modifierRuleOff.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, modifierRuleOff);
		Assert.IsEmpty(modifierRuleOn.Diagnostics);
	}

	[TestMethod]
	public async Task APragmaOverTheModifierRuleHandsItsBoundaryBack()
	{
		string source = $$"""
			class C
			{
			#pragma warning disable {{DiagnosticDescriptors.SeparateModifierGroups.Id}}
				private static int s_count;
				private static int s_limit;
				private string _first;
				private string _second;
			#pragma warning restore {{DiagnosticDescriptors.SeparateModifierGroups.Id}}
			}
			""";

		string fixedSource = $$"""
			class C
			{
			#pragma warning disable {{DiagnosticDescriptors.SeparateModifierGroups.Id}}
				private static int s_count;
				private static int s_limit;

				private string _first;
				private string _second;
			#pragma warning restore {{DiagnosticDescriptors.SeparateModifierGroups.Id}}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSameTypeDeclarationRunsAnalyzer>(
				source,

				TestRuleSets.Only(
					DiagnosticDescriptors.SeparateModifierGroups,
					DiagnosticDescriptors.SeparateSameTypeDeclarationRuns
				)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task SameTypeDeclarationRunsGetSeparated()
	{
		const string source = """
			class C
			{
				void M()
				{
					long nodeLineSpan = 1;
					int startLine = 2;
					int startPosition = 3;
					bool hasComment = false;
					bool hasDirective = false;
					string leadingTrivia = string.Empty;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					long nodeLineSpan = 1;

					int startLine = 2;
					int startPosition = 3;

					bool hasComment = false;
					bool hasDirective = false;

					string leadingTrivia = string.Empty;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSameTypeDeclarationRunsAnalyzer>(source, s_on);

		Assert.HasCount(3, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task SingleDeclarationsOfDifferentTypesStayTight()
	{
		const string source = """
			class C
			{
				void M()
				{
					long first = 1;
					int second = 2;
					bool third = false;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSameTypeDeclarationRunsAnalyzer>(source, s_on);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ADirectiveDoesNotHideTheRunBoundary()
	{
		const string source = """
			class C
			{
				void M()
				{
					long first = 1;
					int second = 2;
			#pragma warning disable CS0219
					int third = 3;
					int fourth = 4;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					long first = 1;

					int second = 2;
			#pragma warning disable CS0219
					int third = 3;
					int fourth = 4;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSameTypeDeclarationRunsAnalyzer>(source, s_on);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task NonDeclarationStatementBreaksTheRun()
	{
		const string source = """
			class C
			{
				void M()
				{
					int first = 1;
					DoThing();
					int second = 2;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSameTypeDeclarationRunsAnalyzer>(source, s_on);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task SameTypeFieldsAreSeparatedFromOtherDeclarations()
	{
		const string source = """
			class C
			{
				private int first = 1;
				private int second = 2;
				private string third = string.Empty;
			}
			""";

		const string fixedSource = """
			class C
			{
				private int first = 1;
				private int second = 2;

				private string third = string.Empty;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSameTypeDeclarationRunsAnalyzer>(source, s_on);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TopLevelSameTypeDeclarationsAreSeparated()
	{
		const string source = """
			long first = 1;
			int second = 2;
			int third = 3;
			""";

		const string fixedSource = """
			long first = 1;

			int second = 2;
			int third = 3;
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSameTypeDeclarationRunsAnalyzer>(source, s_on);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task EventFieldsAreSeparatedByTheirType()
	{
		const string source = """
			class C
			{
				private int value = 1;
				public event Action First;
				public event Action Second;
				private string name = string.Empty;
			}
			""";

		const string fixedSource = """
			class C
			{
				private int value = 1;

				public event Action First;
				public event Action Second;

				private string name = string.Empty;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSameTypeDeclarationRunsAnalyzer>(source, s_on);

		Assert.HasCount(2, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task APragmaRegionHandsTheBoundaryToTheRuleThatDefers()
	{
		string source = $$"""
			class C
			{
				void M()
				{
			#pragma warning disable {{DiagnosticDescriptors.SeparateDeclarationRuns.Id}}
					DoThing();
					int first = 1;
					int second = 2;
			#pragma warning restore {{DiagnosticDescriptors.SeparateDeclarationRuns.Id}}
				}
			}
			""";

		string fixedSource = $$"""
			class C
			{
				void M()
				{
			#pragma warning disable {{DiagnosticDescriptors.SeparateDeclarationRuns.Id}}
					DoThing();

					int first = 1;
					int second = 2;
			#pragma warning restore {{DiagnosticDescriptors.SeparateDeclarationRuns.Id}}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSameTypeDeclarationRunsAnalyzer>(source, s_on);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task APragmaNamesTheBoundariesOfItsOwnRuleOnly()
	{
		string source = $$"""
			class C
			{
			#pragma warning disable {{DiagnosticDescriptors.SeparateMemberGroups.Id}}
				void M() { }
				int first = 1;
				int second = 2;
			#pragma warning restore {{DiagnosticDescriptors.SeparateMemberGroups.Id}}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSameTypeDeclarationRunsAnalyzer>(source, s_on);

		Assert.IsEmpty(
			result.Diagnostics,
			"The declaration run rule still owns the boundary the pragma silences."
		);
	}

	[TestMethod]
	public async Task AGapWaitsForTheGapAnotherRuleAsksForDirectlyAboveIt()
	{
		const string source = """
			class C
			{
				private static int _a;
				private int _b;
				private string _c;
				private string _d;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.SeparateModifierGroups,
				DiagnosticDescriptors.SeparateSameTypeDeclarationRuns
			)
		);

		Assert.AreEqual(
			DiagnosticDescriptors.SeparateModifierGroups.Id,
			result.Diagnostics.Single().Id
		);
	}

	[TestMethod]
	public async Task AGapDoesNotWaitForAGapAPragmaSilences()
	{
		string source = $$"""
			class C
			{
				void M()
				{
			#pragma warning disable {{DiagnosticDescriptors.SeparateSameTypeDeclarationRuns.Id}}
					int first = 1;
					int second = 2;
					string third = "3";
					M();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.SeparateDeclarationRuns,
				DiagnosticDescriptors.SeparateSameTypeDeclarationRuns
			)
		);

		Assert.AreEqual(
			DiagnosticDescriptors.SeparateDeclarationRuns.Id,
			result.Diagnostics.Single().Id
		);
	}

	[TestMethod]
	public async Task AGapDoesNotWaitForAGapThatHoldsADirective()
	{
		const string source = """
			class C
			{
				void M()
				{
					int first = 1;
					int second = 2;
			#nullable enable
					string third = "3";
					M();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.SeparateDeclarationRuns,
				DiagnosticDescriptors.SeparateSameTypeDeclarationRuns
			)
		);

		Assert.AreEqual(
			DiagnosticDescriptors.SeparateDeclarationRuns.Id,
			result.Diagnostics.Single().Id
		);
	}

	[TestMethod]
	public async Task ARunTheModifierRuleSplitsIsNotReportedWhileThatRuleIsOn()
	{
		const string source = """
			class C
			{
				static readonly int s_static4 = 4;
				int _field5;
				string _text6 = "6";
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> both = TestRuleSets.Only(
			DiagnosticDescriptors.SeparateModifierGroups,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns
		);

		IReadOnlyDictionary<string, ReportDiagnostic> alone = TestRuleSets.Only(
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns
		);

		AnalyzerRunResult owned =
			await RoslynTestHarness.RunAsync<SeparateSameTypeDeclarationRunsAnalyzer>(source, both);

		AnalyzerRunResult handedOver =
			await RoslynTestHarness.RunAsync<SeparateSameTypeDeclarationRunsAnalyzer>(
				source,
				alone
			);

		Assert.IsEmpty(
			owned.Diagnostics,
			"The modifier rule writes a blank line inside the int run."
		);

		Assert.HasCount(1, handedOver.Diagnostics);

		const string fixedSource = """
			class C
			{
				static readonly int s_static4 = 4;
				int _field5;

				string _text6 = "6";
			}
			""";

		RoslynTestHarness.AssertFixedText(fixedSource, handedOver);
	}

	[TestMethod]
	public async Task ARegionMarkerSplitsASameTypeRunInEitherFixOrder()
	{
		const string source = """
			class C
			{
				void M()
				{
					long total = 0;
					int first = 1;
					#region Second
					int second = 2;
					#endregion Second
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					long total = 0;
					int first = 1;

					#region Second

					int second = 2;

					#endregion Second
				}
			}
			""";

		AnalyzerRunResult alone =
			await RoslynTestHarness.RunAsync<SeparateSameTypeDeclarationRunsAnalyzer>(source, s_on);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source);

		Assert.IsEmpty(alone.Diagnostics, "The marker ends the run, so the rule reports nothing.");
		RoslynTestHarness.AssertFixedText(fixedSource, result);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(expected: fixedSource, source);
	}

	[TestMethod]
	public async Task AConditionalBlockSplitsASameTypeRunInEitherFixOrder()
	{
		const string source = """
			#define TRACE
			class C
			{
				void M()
				{
					long total = 0;
					int first = 1;
					#if TRACE
					int second = 2;
					#endif
				}
			}
			""";

		const string fixedSource = """
			#define TRACE
			class C
			{
				void M()
				{
					long total = 0;
					int first = 1;

					#if TRACE
					int second = 2;
					#endif
				}
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.Only(
			DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns
		);

		AnalyzerRunResult alone =
			await RoslynTestHarness.RunAsync<SeparateSameTypeDeclarationRunsAnalyzer>(
				source,
				rules
			);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source, rules);

		Assert.IsEmpty(alone.Diagnostics, "The #if ends the run, so the rule reports nothing.");
		RoslynTestHarness.AssertFixedText(fixedSource, result);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			expected: fixedSource,
			source,
			new RunOptions(RoslynTestHarness.AllAnalyzers) { DiagnosticOptions = rules }
		);
	}
}
