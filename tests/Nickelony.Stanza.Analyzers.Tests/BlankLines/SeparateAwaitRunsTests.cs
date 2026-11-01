using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed class SeparateAwaitRunsTests
{
	// The line of the method's first statement, which the line of a diagnostic is counted from.
	private const int FirstBodyLine = 32;

	private static readonly IReadOnlyDictionary<string, ReportDiagnostic> s_on = TestRuleSets.Only(
		DiagnosticDescriptors.SeparateAwaitRuns
	);

	[TestMethod]
	public async Task RepeatedAwaitsGetSeparated()
	{
		string source = Method(
			"""
			DoThing();
			await Load();
			await Save();
			DoThing();
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();

			await Load();
			await Save();

			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateAwaitRunsAnalyzer>(
			source,
			s_on
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task EveryAwaitStatementJoinsOneRunWhateverItAwaits()
	{
		string source = Method(
			"""
			DoThing();
			await Load();
			_ = await Save();
			await Task.Delay(1);
			await _task.ConfigureAwait(false);
			await _task;
			_ = await _work;
			DoThing();
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();

			await Load();
			_ = await Save();
			await Task.Delay(1);
			await _task.ConfigureAwait(false);
			await _task;
			_ = await _work;

			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateAwaitRunsAnalyzer>(
			source,
			s_on
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ADeclarationAndAnAssignmentOfAnAwaitKeepTheirOwnKinds()
	{
		string source = Method(
			"""
			DoThing();
			var first = await Load();
			await Save();
			_count = await Load();
			await Save();
			_count += await Load();
			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync<SeparateAwaitRunsAnalyzer>(
			source,
			s_on
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnAwaitedCallOfAnAssertionRootStaysAnAssertion()
	{
		string source = Method(
			"""
			DoThing();
			await Expect.Async(1);
			await Expect.Async(2);
			DoThing();
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();

			await Expect.Async(1);
			await Expect.Async(2);

			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunRunRulesAsync(source, s_on);

		Assert.HasCount(2, result.Diagnostics);

		Assert.IsTrue(
			result.Diagnostics.All(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.SeparateAssertionRuns.Id
			)
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnAwaitedAssertionEndsARunOfAwaits()
	{
		string source = Method(
			"""
			await Load();
			await Save();
			await Expect.Async(1);
			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync<SeparateAwaitRunsAnalyzer>(
			source,
			s_on
		);

		Assert.HasCount(1, result.Diagnostics);

		Assert.AreEqual(
			2,
			result.Diagnostics[0].Location.GetLineSpan().StartLinePosition.Line - FirstBodyLine
		);
	}

	[TestMethod]
	public async Task AnAwaitForeachAndAnAwaitUsingAreNoAwaitStatements()
	{
		string source = Method(
			"""
			DoThing();
			await foreach (int item in Items()) Build(item);
			await foreach (int item in Items()) Build(item);
			await using (Resource()) DoThing();
			await using (Resource()) DoThing();
			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync<SeparateAwaitRunsAnalyzer>(
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
			await Load();
			await Save();
			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportRunRulesAsync(source);
		AnalyzerRunResult enabled = await RoslynTestHarness.ReportRunRulesAsync(source, s_on);

		Assert.IsEmpty(result.Diagnostics);
		Assert.HasCount(2, enabled.Diagnostics);
	}

	[TestMethod]
	public async Task TheInvocationRuleLeavesAwaitedCallsToThisRuleWhereItIsOn()
	{
		string source = Method(
			"""
			DoThing();
			await Load();
			await Load();
			DoThing();
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();

			await Load();
			await Load();

			DoThing();
			"""
		);

		AnalyzerRunResult on = await RoslynTestHarness.RunRunRulesAsync(source, s_on);

		Assert.HasCount(2, on.Diagnostics);

		Assert.IsTrue(
			on.Diagnostics.All(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.SeparateAwaitRuns.Id
			)
		);

		RoslynTestHarness.AssertFixedText(fixedSource, on);

		// With this rule off, the invocation rule groups the awaited calls by the method they call.
		AnalyzerRunResult off = await RoslynTestHarness.RunRunRulesAsync(source);

		Assert.HasCount(2, off.Diagnostics);

		Assert.IsTrue(
			off.Diagnostics.All(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.SeparateInvocationRuns.Id
			)
		);

		RoslynTestHarness.AssertFixedText(fixedSource, off);
	}

	[TestMethod]
	public async Task AnAwaitedCallSplitsARunOfCallsOfItsMethodWhereThisRuleIsOn()
	{
		string source = Method(
			"""
			Load();
			Load();
			await Load();
			await Save();
			"""
		);

		string fixedSource = Method(
			"""
			Load();
			Load();

			await Load();
			await Save();
			"""
		);

		AnalyzerRunResult on = await RoslynTestHarness.RunRunRulesAsync(source, s_on);

		Assert.HasCount(1, on.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.SeparateInvocationRuns.Id, on.Diagnostics[0].Id);

		RoslynTestHarness.AssertFixedText(fixedSource, on);

		// With this rule off, the awaited call of Load joins the run of Load calls above it.
		AnalyzerRunResult off = await RoslynTestHarness.ReportRunRulesAsync(source);

		Assert.HasCount(1, off.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.SeparateInvocationRuns.Id, off.Diagnostics[0].Id);

		Assert.AreEqual(
			3,
			off.Diagnostics[0].Location.GetLineSpan().StartLinePosition.Line - FirstBodyLine
		);
	}

	[TestMethod]
	public async Task ALoneDeclarationAboveAnAwaitRunTakesItsBlankLineFromThisRule()
	{
		string source = Method(
			"""
			DoThing();
			var value = 1;
			await Load(value);
			await Save();
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();

			var value = 1;

			await Load(value);
			await Save();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunRunRulesAsync(source, s_on);

		Assert.HasCount(2, result.Diagnostics);

		Assert.IsTrue(
			result.Diagnostics.All(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.SeparateAwaitRuns.Id
			)
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ALoneDeclarationStaysTightAgainstAnAwaitRunWhereThisRuleIsOff()
	{
		string source = Method(
			"""
			DoThing();
			var value = 1;
			await Load(value);
			await Save();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportRunRulesAsync(source);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheDeclarationRuleOwnsTheGapBetweenADeclarationRunAndAnAwaitRun()
	{
		string source = Method(
			"""
			var first = 1;
			var second = 2;
			await Load(first);
			await Load(second);
			"""
		);

		string fixedSource = Method(
			"""
			var first = 1;
			var second = 2;

			await Load(first);
			await Load(second);
			"""
		);

		await AssertOwnerAsync(
			source,
			fixedSource,
			owner: DiagnosticDescriptors.SeparateDeclarationRuns
		);
	}

	[TestMethod]
	public async Task TheInvocationRuleOwnsTheGapBetweenACallRunAndAnAwaitRun()
	{
		string source = Method(
			"""
			DoThing();
			DoThing();
			await Load();
			await Save();
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();
			DoThing();

			await Load();
			await Save();
			"""
		);

		await AssertOwnerAsync(
			source,
			fixedSource,
			owner: DiagnosticDescriptors.SeparateInvocationRuns
		);
	}

	[TestMethod]
	public async Task TheAssertionRuleOwnsTheGapBetweenAnAssertionRunAndAnAwaitRun()
	{
		string source = Method(
			"""
			Assert.That(1);
			Assert.That(2);
			await Load();
			await Save();
			"""
		);

		string fixedSource = Method(
			"""
			Assert.That(1);
			Assert.That(2);

			await Load();
			await Save();
			"""
		);

		await AssertOwnerAsync(
			source,
			fixedSource,
			owner: DiagnosticDescriptors.SeparateAssertionRuns
		);
	}

	[TestMethod]
	public async Task ThisRuleOwnsTheGapBetweenAnAwaitRunAndASubscriptionRun()
	{
		string source = Method(
			"""
			await Load();
			await Save();
			Changed += Handler;
			Changed -= Handler;
			"""
		);

		string fixedSource = Method(
			"""
			await Load();
			await Save();

			Changed += Handler;
			Changed -= Handler;
			"""
		);

		await AssertLaterRuleLeavesTheGapAsync(
			source,
			fixedSource,
			later: DiagnosticDescriptors.SeparateEventSubscriptionRuns
		);
	}

	[TestMethod]
	public async Task ThisRuleOwnsTheGapBetweenAnAwaitRunAndAnAssignmentRun()
	{
		string source = Method(
			"""
			await Load();
			await Save();
			_count = 1;
			_count = 2;
			"""
		);

		string fixedSource = Method(
			"""
			await Load();
			await Save();

			_count = 1;
			_count = 2;
			"""
		);

		await AssertLaterRuleLeavesTheGapAsync(
			source,
			fixedSource,
			later: DiagnosticDescriptors.SeparateAssignmentRuns
		);
	}

	[TestMethod]
	public async Task APragmaOverTheInvocationRuleHandsItsGapToThisRule()
	{
		string source = Method(
			$$"""
			#pragma warning disable {{DiagnosticDescriptors.SeparateInvocationRuns.Id}}
			DoThing();
			DoThing();
			await Load();
			await Save();
			#pragma warning restore {{DiagnosticDescriptors.SeparateInvocationRuns.Id}}
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportRunRulesAsync(source, s_on);

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.SeparateAwaitRuns.Id, result.Diagnostics[0].Id);
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
				await Load();
				#region Pair
				await Save();
				#endregion
				DoThing();
				"""
			);

		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync<SeparateAwaitRunsAnalyzer>(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.SeparateAwaitRuns,
				DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors
			)
		);

		Assert.IsEmpty(result.Diagnostics, $"View [{view.Trim()}].");
	}

	// The earlier rule reports the gap, and with it off the gap goes to this rule, which writes the same text.
	private static async Task AssertOwnerAsync(
		string source,
		string fixedSource,
		DiagnosticDescriptor owner
	)
	{
		AnalyzerRunResult both = await RoslynTestHarness.RunRunRulesAsync(
			source,
			TestRuleSets.WithOneOff(on: [DiagnosticDescriptors.SeparateAwaitRuns, owner])
		);

		Assert.HasCount(1, both.Diagnostics);
		Assert.AreEqual(owner.Id, both.Diagnostics[0].Id);

		RoslynTestHarness.AssertFixedText(fixedSource, both);

		AnalyzerRunResult handedOver = await RoslynTestHarness.RunRunRulesAsync(
			source,
			TestRuleSets.WithOneOff(on: [DiagnosticDescriptors.SeparateAwaitRuns], off: owner)
		);

		Assert.HasCount(1, handedOver.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.SeparateAwaitRuns.Id, handedOver.Diagnostics[0].Id);

		RoslynTestHarness.AssertFixedText(fixedSource, handedOver);
	}

	// This rule reports the gap, and with it off the later rule takes the gap and writes the same text.
	private static async Task AssertLaterRuleLeavesTheGapAsync(
		string source,
		string fixedSource,
		DiagnosticDescriptor later
	)
	{
		AnalyzerRunResult both = await RoslynTestHarness.RunRunRulesAsync(
			source,
			TestRuleSets.WithOneOff(on: [DiagnosticDescriptors.SeparateAwaitRuns, later])
		);

		Assert.HasCount(1, both.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.SeparateAwaitRuns.Id, both.Diagnostics[0].Id);

		RoslynTestHarness.AssertFixedText(fixedSource, both);

		AnalyzerRunResult handedOver = await RoslynTestHarness.RunRunRulesAsync(
			source,
			TestRuleSets.WithOneOff(on: [later])
		);

		Assert.HasCount(1, handedOver.Diagnostics);
		Assert.AreEqual(later.Id, handedOver.Diagnostics[0].Id);

		RoslynTestHarness.AssertFixedText(fixedSource, handedOver);
	}

	// Wraps statements in an async method of a class that declares what they await, assign and subscribe to.
	private static string Method(string body)
	{
		return SourceFixtures.WrapBody(
			body,
			signature: "async Task M()",
			classHeader: "using System.Threading.Tasks;\n\nclass C",

			members: "\tint _count;\n\n"
				+ "\tTask _task = Task.CompletedTask;\n\n"
				+ "\tTask<int> _work = Task.FromResult(1);\n\n"
				+ "\tevent Action Changed;\n\n"
				+ "\tvoid Handler()\n\t{\n\t}\n\n"
				+ "\tstatic async IAsyncEnumerable<int> Items()\n\t{\n\t\tawait Task.Yield();\n\n\t\tyield return 1;\n\t}\n\n"
				+ "\tstatic IAsyncDisposable Resource() => null!;\n\n"
				+ "\tstatic class Expect\n\t{\n\t\tpublic static Task Async(int value) => Task.CompletedTask;\n\t}\n\n"
		);
	}
}
