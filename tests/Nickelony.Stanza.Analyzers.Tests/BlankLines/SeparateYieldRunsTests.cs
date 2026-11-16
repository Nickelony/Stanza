using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed class SeparateYieldRunsTests
{
	private const string Members =
		"\tint _count;\n\n"
		+ "\tevent Action Changed;\n\n"
		+ "\tvoid Handler()\n\t{\n\t}\n\n"
		+ "\tstatic Task<int> Load() => Task.FromResult(1);\n\n"
		+ "\tstatic Task Save() => Task.CompletedTask;\n\n";

	private static readonly IReadOnlyDictionary<string, ReportDiagnostic> s_on = TestRuleSets.Only(
		DiagnosticDescriptors.SeparateYieldRuns
	);

	// Each row is a run of another kind that stands directly above a run of yields, and the rule that owns the gap between them.
	public static IEnumerable<object[]> EarlierRuns { get; } =
	[
		[
			"declarations",
			DiagnosticDescriptors.SeparateDeclarationRuns,

			"""
				var first = 1;
				var second = 2;
				yield return first;
				yield return second;
				""",

			"""
				var first = 1;
				var second = 2;

				yield return first;
				yield return second;
				""",
		],

		[
			"calls",
			DiagnosticDescriptors.SeparateInvocationRuns,

			"""
				DoThing();
				DoThing();
				yield return 1;
				yield return 2;
				""",

			"""
				DoThing();
				DoThing();

				yield return 1;
				yield return 2;
				""",
		],

		[
			"assertions",
			DiagnosticDescriptors.SeparateAssertionRuns,

			"""
				Assert.That(1);
				Assert.That(2);
				yield return 1;
				yield return 2;
				""",

			"""
				Assert.That(1);
				Assert.That(2);

				yield return 1;
				yield return 2;
				""",
		],

		[
			"event subscriptions",
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,

			"""
				Changed += Handler;
				Changed -= Handler;
				yield return 1;
				yield return 2;
				""",

			"""
				Changed += Handler;
				Changed -= Handler;

				yield return 1;
				yield return 2;
				""",
		],

		[
			"assignments",
			DiagnosticDescriptors.SeparateAssignmentRuns,

			"""
				_count = 1;
				_count = 2;
				yield return 1;
				yield return 2;
				""",

			"""
				_count = 1;
				_count = 2;

				yield return 1;
				yield return 2;
				""",
		],
	];

	[TestMethod]
	public async Task RepeatedYieldReturnsGetSeparated()
	{
		string source = Method(
			"""
			DoThing();
			yield return 1;
			yield return 2;
			DoThing();
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();

			yield return 1;
			yield return 2;

			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateYieldRunsAnalyzer>(
			source,
			s_on
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task EveryYieldReturnJoinsOneRunWhateverItReturns()
	{
		string source = Method(
			"""
			DoThing();
			yield return 1;
			yield return _count;
			yield return Build(3);
			DoThing();
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();

			yield return 1;
			yield return _count;
			yield return Build(3);

			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateYieldRunsAnalyzer>(
			source,
			s_on
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ALoneYieldReturnStaysTight()
	{
		string source = Method(
			"""
			DoThing();
			yield return 1;
			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync<SeparateYieldRunsAnalyzer>(
			source,
			s_on
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AYieldBreakIsNoYieldReturnAndEndsTheRun()
	{
		string source = Method(
			"""
			yield return 1;
			yield return 2;
			yield break;
			"""
		);

		string fixedSource = Method(
			"""
			yield return 1;
			yield return 2;

			yield break;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateYieldRunsAnalyzer>(
			source,
			s_on
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AYieldBreakStaysAJumpThatFormsNoRun()
	{
		string source = Method(
			"""
			DoThing();
			if (_count == 0) yield break;
			yield break;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync<SeparateYieldRunsAnalyzer>(
			source,
			s_on
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheRuleShipsOffAndReportsNothingUntilASeverityEnablesIt()
	{
		string source = Method(
			"""
			DoThing();
			yield return 1;
			yield return 2;
			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportRunRulesAsync(source);
		AnalyzerRunResult enabled = await RoslynTestHarness.ReportRunRulesAsync(source, s_on);

		Assert.IsEmpty(result.Diagnostics);
		Assert.HasCount(2, enabled.Diagnostics);
	}

	[TestMethod]
	public async Task APragmaThatSilencesTheRuleLeavesTheRunAsWritten()
	{
		string source = Method(
			$$"""
			DoThing();
			#pragma warning disable {{DiagnosticDescriptors.SeparateYieldRuns.Id}}
			yield return 1;
			yield return 2;
			#pragma warning restore {{DiagnosticDescriptors.SeparateYieldRuns.Id}}
			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync<SeparateYieldRunsAnalyzer>(
			source,
			s_on
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ALoneDeclarationAboveAYieldRunTakesItsBlankLineFromThisRule()
	{
		string source = Method(
			"""
			DoThing();
			var value = 1;
			yield return value;
			yield return 2;
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();

			var value = 1;

			yield return value;
			yield return 2;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunRunRulesAsync(source, s_on);

		Assert.HasCount(2, result.Diagnostics);

		Assert.IsTrue(
			result.Diagnostics.All(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.SeparateYieldRuns.Id
			)
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);

		// The declaration stays tight against the run while this rule is off.
		AnalyzerRunResult off = await RoslynTestHarness.ReportRunRulesAsync(source);

		Assert.IsEmpty(off.Diagnostics);
	}

	[TestMethod]
	[DynamicData(
		nameof(EarlierRuns),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task TheRuleOfAnEarlierRunOwnsTheGapBetweenThatRunAndAYieldRun(
		string name,
		DiagnosticDescriptor owner,
		string body,
		string fixedBody
	)
	{
		string source = Method(body);
		string fixedSource = Method(fixedBody);

		AnalyzerRunResult both = await RoslynTestHarness.RunRunRulesAsync(
			source,
			TestRuleSets.WithOneOff(on: [DiagnosticDescriptors.SeparateYieldRuns, owner])
		);

		Assert.HasCount(1, both.Diagnostics, name);
		Assert.AreEqual(owner.Id, both.Diagnostics[0].Id, name);

		RoslynTestHarness.AssertFixedText(fixedSource, both, name);

		// With the earlier rule off, this rule takes the gap and writes the same text.
		AnalyzerRunResult handedOver = await RoslynTestHarness.RunRunRulesAsync(
			source,
			TestRuleSets.WithOneOff(on: [DiagnosticDescriptors.SeparateYieldRuns], off: owner)
		);

		Assert.HasCount(1, handedOver.Diagnostics, name);

		Assert.AreEqual(
			DiagnosticDescriptors.SeparateYieldRuns.Id,
			handedOver.Diagnostics[0].Id,
			name
		);

		RoslynTestHarness.AssertFixedText(fixedSource, handedOver, name);
	}

	[TestMethod]
	public async Task TheAwaitRuleOwnsTheGapBetweenAnAwaitRunAndAYieldRun()
	{
		string source = AsyncMethod(
			"""
			await Load();
			await Save();
			yield return 1;
			yield return 2;
			"""
		);

		string fixedSource = AsyncMethod(
			"""
			await Load();
			await Save();

			yield return 1;
			yield return 2;
			"""
		);

		AnalyzerRunResult both = await RoslynTestHarness.RunRunRulesAsync(
			source,

			TestRuleSets.WithOneOff(
				on:
				[
					DiagnosticDescriptors.SeparateYieldRuns,
					DiagnosticDescriptors.SeparateAwaitRuns,
				]
			)
		);

		Assert.HasCount(1, both.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.SeparateAwaitRuns.Id, both.Diagnostics[0].Id);

		RoslynTestHarness.AssertFixedText(fixedSource, both);

		AnalyzerRunResult handedOver = await RoslynTestHarness.RunRunRulesAsync(source, s_on);

		Assert.HasCount(1, handedOver.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.SeparateYieldRuns.Id, handedOver.Diagnostics[0].Id);

		RoslynTestHarness.AssertFixedText(fixedSource, handedOver);
	}

	[TestMethod]
	public async Task ThisRuleOwnsTheGapBetweenAYieldRunAndALocalFunctionRun()
	{
		string source = Method(
			"""
			yield return 1;
			yield return 2;
			int Twice(int value) => value * 2;
			int Thrice(int value) => value * 3;
			"""
		);

		string fixedSource = Method(
			"""
			yield return 1;
			yield return 2;

			int Twice(int value) => value * 2;
			int Thrice(int value) => value * 3;
			"""
		);

		AnalyzerRunResult both = await RoslynTestHarness.RunRunRulesAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.SeparateYieldRuns,
				DiagnosticDescriptors.SeparateLocalFunctionRuns
			)
		);

		Assert.HasCount(1, both.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.SeparateYieldRuns.Id, both.Diagnostics[0].Id);

		RoslynTestHarness.AssertFixedText(fixedSource, both);

		AnalyzerRunResult handedOver = await RoslynTestHarness.RunRunRulesAsync(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.SeparateLocalFunctionRuns)
		);

		Assert.HasCount(1, handedOver.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.SeparateLocalFunctionRuns.Id,
			handedOver.Diagnostics[0].Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, handedOver);
	}

	[TestMethod]
	[DataRow("")]
	[DataRow("#define A\n")]
	public async Task ARegionMarkerEndsTheRunInEveryViewWhereItsRuleIsOn(string view)
	{
		string source =
			view
			+ Method(
				"""
				DoThing();
				yield return 1;
				#region Pair
				yield return 2;
				#endregion
				DoThing();
				"""
			);

		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync<SeparateYieldRunsAnalyzer>(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.SeparateYieldRuns,
				DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors
			)
		);

		Assert.IsEmpty(result.Diagnostics, $"View [{view.Trim()}].");
	}

	[TestMethod]
	public async Task EveryStateOfTheRunRulesSettlesOnOneTextInEveryFixOrder()
	{
		string source = Method(
			"""
			DoThing();
			var first = 1;
			var second = 2;
			yield return first;
			yield return second;
			_count = 1;
			_count = 2;
			yield return 3;
			yield return 4;
			Changed += Handler;
			Changed -= Handler;
			yield return 5;
			Assert.That(1);
			Assert.That(2);
			yield return 6;
			yield return 7;
			DoThing();
			DoThing();
			yield return 8;
			yield break;
			"""
		);

		await RoslynTestHarness.AssertEveryStateSettlesAsync(
			source,
			toggled: TestRuleSets.RunRules
		);
	}

	// Wraps statements in an iterator method of a class that declares what they use.
	private static string Method(string body)
	{
		return SourceFixtures.WrapBody(
			body,
			signature: "IEnumerable<int> M()",
			classHeader: "using System;\nusing System.Collections.Generic;\nusing System.Threading.Tasks;\n\nclass C",
			members: Members
		);
	}

	private static string AsyncMethod(string body)
	{
		return SourceFixtures.WrapBody(
			body,
			signature: "async IAsyncEnumerable<int> M()",
			classHeader: "using System;\nusing System.Collections.Generic;\nusing System.Threading.Tasks;\n\nclass C",
			members: Members
		);
	}
}
