using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class SeparateSameTypeDeclarationGroupsTests
{
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
			await RoslynTestHarness.RunAsync<SeparateSameTypeDeclarationGroupsAnalyzer>(source);

		AnalyzerRunResult modifierRuleOn =
			await RoslynTestHarness.RunAsync<SeparateSameTypeDeclarationGroupsAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.SeparateModifierGroups)
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
			await RoslynTestHarness.RunAsync<SeparateSameTypeDeclarationGroupsAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.SeparateModifierGroups)
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
			await RoslynTestHarness.RunAsync<SeparateSameTypeDeclarationGroupsAnalyzer>(source);

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
			await RoslynTestHarness.RunAsync<SeparateSameTypeDeclarationGroupsAnalyzer>(source);

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
			await RoslynTestHarness.RunAsync<SeparateSameTypeDeclarationGroupsAnalyzer>(source);

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
			await RoslynTestHarness.RunAsync<SeparateSameTypeDeclarationGroupsAnalyzer>(source);

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
			await RoslynTestHarness.RunAsync<SeparateSameTypeDeclarationGroupsAnalyzer>(source);

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
			await RoslynTestHarness.RunAsync<SeparateSameTypeDeclarationGroupsAnalyzer>(source);

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
			await RoslynTestHarness.RunAsync<SeparateSameTypeDeclarationGroupsAnalyzer>(source);

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
			#pragma warning disable {{DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns.Id}}
					DoThing();
					int first = 1;
					int second = 2;
			#pragma warning restore {{DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns.Id}}
				}
			}
			""";

		string fixedSource = $$"""
			class C
			{
				void M()
				{
			#pragma warning disable {{DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns.Id}}
					DoThing();

					int first = 1;
					int second = 2;
			#pragma warning restore {{DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns.Id}}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSameTypeDeclarationGroupsAnalyzer>(source);

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
			await RoslynTestHarness.RunAsync<SeparateSameTypeDeclarationGroupsAnalyzer>(source);

		Assert.IsEmpty(
			result.Diagnostics,
			"The declaration run rule still owns the boundary the pragma silences."
		);
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
			await RoslynTestHarness.RunAsync<SeparateSameTypeDeclarationGroupsAnalyzer>(source);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source);

		Assert.IsEmpty(alone.Diagnostics, "The marker ends the run, so the rule reports nothing.");
		RoslynTestHarness.AssertFixedText(fixedSource, result);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(expected: fixedSource, source);
	}
}
