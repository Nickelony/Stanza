using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed class SeparateLocalFunctionRunsTests
{
	private const string ModifiersKey = "stanza_local_function_modifiers";

	private const string Members =
		"\tint _count;\n\n"
		+ "\tevent Action Changed;\n\n"
		+ "\tvoid Handler()\n\t{\n\t}\n\n"
		+ "\tvoid DoThing()\n\t{\n\t}\n\n";

	private static readonly IReadOnlyDictionary<string, ReportDiagnostic> s_on = TestRuleSets.Only(
		DiagnosticDescriptors.SeparateLocalFunctionRuns
	);

	// Each row is a run of another kind that stands directly above a run of local functions, and the rule that owns the gap between them.
	public static IEnumerable<object[]> EarlierRuns { get; } =
	[
		[
			"declarations",
			DiagnosticDescriptors.SeparateDeclarationRuns,

			"""
				var first = 1;
				var second = 2;
				int Twice(int value) => value * 2;
				int Thrice(int value) => value * 3;
				""",

			"""
				var first = 1;
				var second = 2;

				int Twice(int value) => value * 2;
				int Thrice(int value) => value * 3;
				""",
		],

		[
			"calls",
			DiagnosticDescriptors.SeparateInvocationRuns,

			"""
				DoThing();
				DoThing();
				int Twice(int value) => value * 2;
				int Thrice(int value) => value * 3;
				""",

			"""
				DoThing();
				DoThing();

				int Twice(int value) => value * 2;
				int Thrice(int value) => value * 3;
				""",
		],

		[
			"assertions",
			DiagnosticDescriptors.SeparateAssertionRuns,

			"""
				Assert.That(1);
				Assert.That(2);
				int Twice(int value) => value * 2;
				int Thrice(int value) => value * 3;
				""",

			"""
				Assert.That(1);
				Assert.That(2);

				int Twice(int value) => value * 2;
				int Thrice(int value) => value * 3;
				""",
		],

		[
			"event subscriptions",
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,

			"""
				Changed += Handler;
				Changed -= Handler;
				int Twice(int value) => value * 2;
				int Thrice(int value) => value * 3;
				""",

			"""
				Changed += Handler;
				Changed -= Handler;

				int Twice(int value) => value * 2;
				int Thrice(int value) => value * 3;
				""",
		],

		[
			"assignments",
			DiagnosticDescriptors.SeparateAssignmentRuns,

			"""
				_count = 1;
				_count = 2;
				int Twice(int value) => value * 2;
				int Thrice(int value) => value * 3;
				""",

			"""
				_count = 1;
				_count = 2;

				int Twice(int value) => value * 2;
				int Thrice(int value) => value * 3;
				""",
		],
	];

	[TestMethod]
	public async Task RepeatedSingleLineLocalFunctionsGetSeparated()
	{
		string source = Method(
			"""
			DoThing();
			int Twice(int value) => value * 2;
			int Thrice(int value) => value * 3;
			DoThing();
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();

			int Twice(int value) => value * 2;
			int Thrice(int value) => value * 3;

			DoThing();
			"""
		);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateLocalFunctionRunsAnalyzer>(source, s_on);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABlockBodyOnOneLineIsASingleLineLocalFunction()
	{
		string source = Method(
			"""
			DoThing();
			int Twice(int value) { return value * 2; }
			void Log() { DoThing(); }
			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync<SeparateLocalFunctionRunsAnalyzer>(
			source,
			s_on
		);

		Assert.HasCount(2, result.Diagnostics);
	}

	[TestMethod]
	public async Task ALoneLocalFunctionStaysTight()
	{
		string source = Method(
			"""
			DoThing();
			int Twice(int value) => value * 2;
			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync<SeparateLocalFunctionRunsAnalyzer>(
			source,
			s_on
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AMultiLineLocalFunctionIsNoPartOfARunAndKeepsItsGapsWithTheMultiLineRule()
	{
		string source = Method(
			"""
			DoThing();
			int Twice(int value) => value * 2;
			int Thrice(int value) => value * 3;
			int Sum(int value)
			{
				return value;
			}
			DoThing();
			"""
		);

		// The gap above the multi-line function belongs to ZA0001, which is on, so only the gap above the run is reported.
		AnalyzerRunResult owned = await RoslynTestHarness.ReportAsync<SeparateLocalFunctionRunsAnalyzer>(
			source,
			s_on
		);

		Assert.HasCount(1, owned.Diagnostics);

		AnalyzerRunResult handedOver = await RoslynTestHarness.ReportAsync<SeparateLocalFunctionRunsAnalyzer>(
			source,

			TestRuleSets.WithOneOff(
				on: [DiagnosticDescriptors.SeparateLocalFunctionRuns],
				off: DiagnosticDescriptors.SeparateMultiLineConstructs
			)
		);

		Assert.HasCount(2, handedOver.Diagnostics);
	}

	[TestMethod]
	public async Task TheRuleShipsOffAndReportsNothingUntilASeverityEnablesIt()
	{
		string source = Method(
			"""
			DoThing();
			int Twice(int value) => value * 2;
			int Thrice(int value) => value * 3;
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
			#pragma warning disable {{DiagnosticDescriptors.SeparateLocalFunctionRuns.Id}}
			int Twice(int value) => value * 2;
			int Thrice(int value) => value * 3;
			#pragma warning restore {{DiagnosticDescriptors.SeparateLocalFunctionRuns.Id}}
			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync<SeparateLocalFunctionRunsAnalyzer>(
			source,
			s_on
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	[DataRow(null)]
	[DataRow("merge")]
	[DataRow("other")]
	public async Task StaticAndOtherLocalFunctionsShareARunUnlessTheOptionSplitsThem(string? value)
	{
		string source = Method(
			"""
			DoThing();
			static int Twice(int number) => number * 2;
			static int Thrice(int number) => number * 3;
			int Four(int number) => number * 4;
			int Five(int number) => number * 5;
			DoThing();
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();

			static int Twice(int number) => number * 2;
			static int Thrice(int number) => number * 3;
			int Four(int number) => number * 4;
			int Five(int number) => number * 5;

			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateLocalFunctionRunsAnalyzer>(
			source,
			s_on,
			analyzerConfigOptions: value is null ? null : new Dictionary<string, string> { [ModifiersKey] = value }
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheSplitOptionSeparatesStaticLocalFunctionsFromTheOthers()
	{
		string source = Method(
			"""
			DoThing();
			static int Twice(int number) => number * 2;
			static int Thrice(int number) => number * 3;
			int Four(int number) => number * 4;
			int Five(int number) => number * 5;
			DoThing();
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();

			static int Twice(int number) => number * 2;
			static int Thrice(int number) => number * 3;

			int Four(int number) => number * 4;
			int Five(int number) => number * 5;

			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateLocalFunctionRunsAnalyzer>(
			source,
			s_on,
			analyzerConfigOptions: new Dictionary<string, string> { [ModifiersKey] = "split" }
		);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AsyncAndUnsafeNeverSplitARunWhateverTheOptionSays()
	{
		string source = Method(
			"""
			DoThing();
			async Task<int> First() => 1;
			unsafe int Second() => 2;
			int Third() => 3;
			DoThing();
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();

			async Task<int> First() => 1;
			unsafe int Second() => 2;
			int Third() => 3;

			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateLocalFunctionRunsAnalyzer>(
			source,
			s_on,
			analyzerConfigOptions: new Dictionary<string, string> { [ModifiersKey] = "split" }
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AStaticModifierBesideAnotherOneStillSplitsFromTheNonStaticOnes()
	{
		string source = Method(
			"""
			DoThing();
			static async Task<int> First() => 1;
			static unsafe int Second() => 2;
			int Third() => 3;
			int Fourth() => 4;
			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync<SeparateLocalFunctionRunsAnalyzer>(
			source,
			s_on,
			analyzerConfigOptions: new Dictionary<string, string> { [ModifiersKey] = "split" }
		);

		Assert.HasCount(3, result.Diagnostics);
	}

	[TestMethod]
	public async Task ALoneDeclarationAboveALocalFunctionRunTakesItsBlankLineFromThisRule()
	{
		string source = Method(
			"""
			DoThing();
			var value = 1;
			int Twice(int number) => number * 2;
			int Thrice(int number) => number * 3;
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();

			var value = 1;

			int Twice(int number) => number * 2;
			int Thrice(int number) => number * 3;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunRunRulesAsync(source, s_on);

		Assert.HasCount(2, result.Diagnostics);

		Assert.IsTrue(
			result.Diagnostics.All(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.SeparateLocalFunctionRuns.Id
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
	public async Task TheRuleOfAnEarlierRunOwnsTheGapBetweenThatRunAndALocalFunctionRun(
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
			TestRuleSets.WithOneOff(on: [DiagnosticDescriptors.SeparateLocalFunctionRuns, owner])
		);

		Assert.HasCount(1, both.Diagnostics, name);
		Assert.AreEqual(owner.Id, both.Diagnostics[0].Id, name);

		RoslynTestHarness.AssertFixedText(fixedSource, both, name);

		// With the earlier rule off, this rule takes the gap and writes the same text.
		AnalyzerRunResult handedOver = await RoslynTestHarness.RunRunRulesAsync(
			source,
			TestRuleSets.WithOneOff(on: [DiagnosticDescriptors.SeparateLocalFunctionRuns], off: owner)
		);

		Assert.HasCount(1, handedOver.Diagnostics, name);

		Assert.AreEqual(
			DiagnosticDescriptors.SeparateLocalFunctionRuns.Id,
			handedOver.Diagnostics[0].Id,
			name
		);

		RoslynTestHarness.AssertFixedText(fixedSource, handedOver, name);
	}

	[TestMethod]
	public async Task TheYieldRuleOwnsTheGapBetweenAYieldRunAndALocalFunctionRun()
	{
		string source = IteratorMethod(
			"""
			yield return 1;
			yield return 2;
			int Twice(int value) => value * 2;
			int Thrice(int value) => value * 3;
			"""
		);

		string fixedSource = IteratorMethod(
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
	}

	[TestMethod]
	[DataRow("")]
	[DataRow("#define A\n")]
	public async Task AConditionalBlockEndsTheRunInEveryViewWhereItsRuleIsOn(string view)
	{
		string source =
			view
			+ Method(
				"""
				DoThing();
				int Twice(int value) => value * 2;
				#if A
				int Thrice(int value) => value * 3;
				#endif
				DoThing();
				"""
			);

		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync<SeparateLocalFunctionRunsAnalyzer>(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.SeparateLocalFunctionRuns,
				DiagnosticDescriptors.SeparateConditionalBlock
			)
		);

		Assert.IsEmpty(result.Diagnostics, $"View [{view.Trim()}].");
	}

	[TestMethod]
	[DataRow("merge")]
	[DataRow("split")]
	public async Task EveryStateOfTheRunRulesSettlesOnOneTextInEveryFixOrder(string modifiers)
	{
		string source = Method(
			"""
			DoThing();
			var first = 1;
			var second = 2;
			static int Twice(int number) => number * 2;
			int Thrice(int number) => number * 3;
			int Four(int number) => number * 4;
			_count = 1;
			_count = 2;
			static int Five(int number) => number * 5;
			static int Six(int number) => number * 6;
			Changed += Handler;
			Changed -= Handler;
			int Seven(int number) => number * 7;
			Assert.That(1);
			Assert.That(2);
			int Eight(int number) => number * 8;
			int Nine(int number)
			{
				return number * 9;
			}
			DoThing();
			DoThing();
			int Ten(int number) => number * 10;
			"""
		);

		await RoslynTestHarness.AssertEveryStateSettlesAsync(
			source,
			toggled: TestRuleSets.RunRules,
			analyzerConfigOptions: new Dictionary<string, string> { [ModifiersKey] = modifiers }
		);
	}

	// Wraps statements in a method of a class that declares what they use.
	private static string Method(string body)
	{
		return SourceFixtures.WrapBody(
			body,
			signature: "void M()",
			classHeader: "using System;\nusing System.Threading.Tasks;\n\nclass C",
			members: Members
		);
	}

	private static string IteratorMethod(string body)
	{
		return SourceFixtures.WrapBody(
			body,
			signature: "System.Collections.Generic.IEnumerable<int> M()",
			classHeader: "using System;\n\nclass C",
			members: Members
		);
	}
}
