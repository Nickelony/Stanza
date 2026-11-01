using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed class SeparateEventSubscriptionRunsTests
{
	private static readonly IReadOnlyDictionary<string, ReportDiagnostic> s_on = TestRuleSets.Only(
		DiagnosticDescriptors.SeparateEventSubscriptionRuns
	);

	private static readonly IReadOnlyDictionary<string, ReportDiagnostic> s_bothOn =
		TestRuleSets.Only(
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns
		);

	[TestMethod]
	public async Task RepeatedSubscriptionsGetSeparated()
	{
		string source = Method(
			"""
			DoThing();
			Changed += Handler;
			Changed -= Handler;
			DoThing();
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();

			Changed += Handler;
			Changed -= Handler;

			DoThing();
			"""
		);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateEventSubscriptionRunsAnalyzer>(source, s_on);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task SubscribingAndUnsubscribingAreOneKindWhateverTheEventOrItsOwner()
	{
		string source = Method(
			"""
			DoThing();
			Changed += Handler;
			button.Click -= Handler;
			Closed += Handler;
			Button.Shared += Handler;
			this.Changed -= Handler;
			DoThing();
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();

			Changed += Handler;
			button.Click -= Handler;
			Closed += Handler;
			Button.Shared += Handler;
			this.Changed -= Handler;

			DoThing();
			"""
		);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateEventSubscriptionRunsAnalyzer>(source, s_on);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ASubscriptionThroughANullConditionalAccessIsOneKind()
	{
		RoslynTestHarness.RequireRoslynWithCSharp14();

		string source = Method(
			"""
			DoThing();
			button?.Click += Handler;
			button?.Click -= Handler;
			DoThing();
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();

			button?.Click += Handler;
			button?.Click -= Handler;

			DoThing();
			"""
		);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateEventSubscriptionRunsAnalyzer>(source, s_on);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ALoneSubscriptionStaysTight()
	{
		string source = Method(
			"""
			DoThing();
			Changed += Handler;
			DoThing();
			Closed -= Handler;
			DoThing();
			"""
		);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateEventSubscriptionRunsAnalyzer>(source, s_on);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACompoundAssignmentOfANumberIsNoSubscription()
	{
		string source = Method(
			"""
			DoThing();
			a += 1;
			b -= 2;
			c += 3;
			DoThing();
			"""
		);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateEventSubscriptionRunsAnalyzer>(source, s_on);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ANumberBetweenTwoSubscriptionsEndsTheRun()
	{
		string source = Method(
			"""
			DoThing();
			Changed += Handler;
			a += 1;
			Closed += Handler;
			DoThing();
			"""
		);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateEventSubscriptionRunsAnalyzer>(source, s_on);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ADelegateFieldIsNoEvent()
	{
		string source = Method(
			"""
			DoThing();
			callback += Handler;
			callback -= Handler;
			DoThing();
			"""
		);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateEventSubscriptionRunsAnalyzer>(source, s_on);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ASymbolThatIsMissingIsNoSubscription()
	{
		string source = Method(
			"""
			DoThing();
			Missing += Handler;
			Missing -= Handler;
			Unknown.Changed += Handler;
			Unknown.Changed -= Handler;
			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync(
			source,
			WithoutCompileCheck(s_on)
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACompileErrorBesideTheRunDoesNotHideIt()
	{
		string source = Method(
			"""
			DoThing();
			Changed += Handler;
			Changed -= Handler;
			Missing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync(
			source,
			WithoutCompileCheck(s_on)
		);

		Assert.HasCount(2, result.Diagnostics);
	}

	[TestMethod]
	public async Task TheRuleShipsOffAndReportsNothingUntilASeverityEnablesIt()
	{
		string source = Method(
			"""
			DoThing();
			Changed += Handler;
			Changed -= Handler;
			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportRunRulesAsync(source);
		AnalyzerRunResult enabled = await RoslynTestHarness.ReportRunRulesAsync(source, s_on);

		Assert.IsEmpty(result.Diagnostics);
		Assert.HasCount(2, enabled.Diagnostics);
	}

	[TestMethod]
	public async Task AttachedCommentMovesWithTheRun()
	{
		string source = Method(
			"""
			DoThing();
			// Wire the pair.
			Changed += Handler;
			Closed += Handler;
			DoThing();
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();

			// Wire the pair.
			Changed += Handler;
			Closed += Handler;

			DoThing();
			"""
		);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateEventSubscriptionRunsAnalyzer>(source, s_on);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AReportNamesTheGapOfTheRunWithOneRuleOnlyWhereTheAssignmentRuleIsAlsoOn()
	{
		string source = Method(
			"""
			DoThing();
			Changed += Handler;
			Closed += Handler;
			DoThing();
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();

			Changed += Handler;
			Closed += Handler;

			DoThing();
			"""
		);

		AnalyzerRunResult both = await RoslynTestHarness.RunRunRulesAsync(source, s_bothOn);

		Assert.HasCount(2, both.Diagnostics);

		Assert.IsTrue(
			both.Diagnostics.All(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.SeparateEventSubscriptionRuns.Id
			)
		);

		RoslynTestHarness.AssertEachGapIsClaimedOnce(both);
		RoslynTestHarness.AssertFixedText(fixedSource, both);

		// With this rule off, the assignment rule reads the same statements as assignments and settles on the same text.
		AnalyzerRunResult handedOver = await RoslynTestHarness.RunRunRulesAsync(
			source,

			TestRuleSets.WithOneOff(
				on: [DiagnosticDescriptors.SeparateAssignmentRuns],
				off: DiagnosticDescriptors.SeparateEventSubscriptionRuns
			)
		);

		Assert.HasCount(2, handedOver.Diagnostics);

		Assert.IsTrue(
			handedOver.Diagnostics.All(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.SeparateAssignmentRuns.Id
			)
		);

		RoslynTestHarness.AssertFixedText(fixedSource, handedOver);
	}

	[TestMethod]
	public async Task ARunOfSubscriptionsInsideARunOfAssignmentsIsSetApartByThisRuleAlone()
	{
		string source = Method(
			"""
			a = 1;
			b = 2;
			Changed += Handler;
			Closed += Handler;
			c = 3;
			d = 4;
			"""
		);

		string fixedSource = Method(
			"""
			a = 1;
			b = 2;

			Changed += Handler;
			Closed += Handler;

			c = 3;
			d = 4;
			"""
		);

		AnalyzerRunResult both = await RoslynTestHarness.RunRunRulesAsync(source, s_bothOn);

		Assert.HasCount(2, both.Diagnostics);

		Assert.IsTrue(
			both.Diagnostics.All(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.SeparateEventSubscriptionRuns.Id
			)
		);

		RoslynTestHarness.AssertEachGapIsClaimedOnce(both);
		RoslynTestHarness.AssertFixedText(fixedSource, both);

		// The assignment rule alone reads one run of six and asks for no gap inside it.
		AnalyzerRunResult assignmentsOnly = await RoslynTestHarness.ReportRunRulesAsync(
			source,

			TestRuleSets.WithOneOff(
				on: [DiagnosticDescriptors.SeparateAssignmentRuns],
				off: DiagnosticDescriptors.SeparateEventSubscriptionRuns
			)
		);

		Assert.IsEmpty(assignmentsOnly.Diagnostics);
	}

	[TestMethod]
	public async Task AnAssignmentAboveASubscriptionRunSettlesTheSameWayInEveryFixOrder()
	{
		string source = Method(
			"""
			DoThing();
			a = 1;
			Changed += Handler;
			Closed += Handler;
			DoThing();
			"""
		);

		// The assignment is alone once this rule writes the blank line below it, so the assignment rule asks for nothing above it.
		string fixedSource = Method(
			"""
			DoThing();
			a = 1;

			Changed += Handler;
			Closed += Handler;

			DoThing();
			"""
		);

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			expected: fixedSource,
			source,
			options: RunRuleOptions(s_bothOn)
		);

		AnalyzerRunResult reported = await RoslynTestHarness.ReportRunRulesAsync(source, s_bothOn);

		Assert.HasCount(2, reported.Diagnostics);

		Assert.IsTrue(
			reported.Diagnostics.All(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.SeparateEventSubscriptionRuns.Id
			)
		);
	}

	[TestMethod]
	public async Task ASubscriptionThroughANullConditionalAccessEndsARunOfSubscriptionsTheSameWayInEveryFixOrder()
	{
		RoslynTestHarness.RequireRoslynWithCSharp14();

		string source = Method(
			"""
			DoThing();
			Closed += Handler;
			Changed += Handler;
			button?.Click += Handler;
			DoThing();
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();

			Closed += Handler;
			Changed += Handler;
			button?.Click += Handler;

			DoThing();
			"""
		);

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			expected: fixedSource,
			source,
			options: RunRuleOptions(s_bothOn)
		);

		AnalyzerRunResult reported = await RoslynTestHarness.ReportRunRulesAsync(source, s_bothOn);

		Assert.IsTrue(
			reported.Diagnostics.All(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.SeparateEventSubscriptionRuns.Id
			)
		);
	}

	[TestMethod]
	public async Task ALoneSubscriptionStaysInsideARunOfAssignments()
	{
		string source = Method(
			"""
			DoThing();
			a = 1;
			Changed += Handler;
			b = 2;
			DoThing();
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();

			a = 1;
			Changed += Handler;
			b = 2;

			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunRunRulesAsync(source, s_bothOn);

		Assert.HasCount(2, result.Diagnostics);

		Assert.IsTrue(
			result.Diagnostics.All(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.SeparateAssignmentRuns.Id
			)
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ALoneDeclarationAboveASubscriptionRunTakesItsBlankLineFromThisRule()
	{
		string source = Method(
			"""
			DoThing();
			var value = Handler;
			Changed += value;
			Closed += value;
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();

			var value = Handler;

			Changed += value;
			Closed += value;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunRunRulesAsync(source, s_on);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheDeclarationRuleOwnsTheGapBetweenADeclarationRunAndASubscriptionRun()
	{
		string source = Method(
			"""
			var first = Handler;
			var second = Handler;
			Changed += first;
			Closed += second;
			"""
		);

		string fixedSource = Method(
			"""
			var first = Handler;
			var second = Handler;

			Changed += first;
			Closed += second;
			"""
		);

		AnalyzerRunResult both = await RoslynTestHarness.RunRunRulesAsync(source, s_on);

		Assert.HasCount(1, both.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.SeparateDeclarationRuns.Id, both.Diagnostics[0].Id);

		RoslynTestHarness.AssertFixedText(fixedSource, both);

		AnalyzerRunResult handedOver = await RoslynTestHarness.RunRunRulesAsync(
			source,

			TestRuleSets.WithOneOff(
				on: [DiagnosticDescriptors.SeparateEventSubscriptionRuns],
				off: DiagnosticDescriptors.SeparateDeclarationRuns
			)
		);

		Assert.HasCount(1, handedOver.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.SeparateEventSubscriptionRuns.Id,
			handedOver.Diagnostics[0].Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, handedOver);
	}

	[TestMethod]
	public async Task TheInvocationRuleOwnsTheGapBetweenACallRunAndASubscriptionRun()
	{
		string source = Method(
			"""
			DoThing();
			DoThing();
			Changed += Handler;
			Closed += Handler;
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();
			DoThing();

			Changed += Handler;
			Closed += Handler;
			"""
		);

		AnalyzerRunResult both = await RoslynTestHarness.RunRunRulesAsync(source, s_on);

		Assert.HasCount(1, both.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.SeparateInvocationRuns.Id, both.Diagnostics[0].Id);

		RoslynTestHarness.AssertFixedText(fixedSource, both);

		AnalyzerRunResult handedOver = await RoslynTestHarness.RunRunRulesAsync(
			source,

			TestRuleSets.WithOneOff(
				on: [DiagnosticDescriptors.SeparateEventSubscriptionRuns],
				off: DiagnosticDescriptors.SeparateInvocationRuns
			)
		);

		Assert.HasCount(1, handedOver.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.SeparateEventSubscriptionRuns.Id,
			handedOver.Diagnostics[0].Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, handedOver);
	}

	[TestMethod]
	public async Task TheAssertionRuleOwnsTheGapBetweenAnAssertionRunAndASubscriptionRun()
	{
		string source = Method(
			"""
			Assert.That(1);
			Assert.That(2);
			Changed += Handler;
			Closed += Handler;
			"""
		);

		string fixedSource = Method(
			"""
			Assert.That(1);
			Assert.That(2);

			Changed += Handler;
			Closed += Handler;
			"""
		);

		AnalyzerRunResult both = await RoslynTestHarness.RunRunRulesAsync(source, s_on);

		Assert.HasCount(1, both.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.SeparateAssertionRuns.Id, both.Diagnostics[0].Id);

		RoslynTestHarness.AssertFixedText(fixedSource, both);

		AnalyzerRunResult handedOver = await RoslynTestHarness.RunRunRulesAsync(
			source,

			TestRuleSets.WithOneOff(
				on: [DiagnosticDescriptors.SeparateEventSubscriptionRuns],
				off: DiagnosticDescriptors.SeparateAssertionRuns
			)
		);

		Assert.HasCount(1, handedOver.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.SeparateEventSubscriptionRuns.Id,
			handedOver.Diagnostics[0].Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, handedOver);
	}

	[TestMethod]
	public async Task APragmaOverThisRuleHandsItsGapToTheAssignmentRule()
	{
		string source = Method(
			$$"""
			#pragma warning disable {{DiagnosticDescriptors.SeparateEventSubscriptionRuns.Id}}
			Changed += Handler;
			Closed += Handler;
			DoThing();
			#pragma warning restore {{DiagnosticDescriptors.SeparateEventSubscriptionRuns.Id}}
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportRunRulesAsync(source, s_bothOn);

		Assert.IsNotEmpty(result.Diagnostics);

		Assert.IsTrue(
			result.Diagnostics.All(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.SeparateAssignmentRuns.Id
			)
		);
	}

	[TestMethod]
	[DataRow("")]
	[DataRow("#define A\n")]
	public async Task AnInactiveConditionalBlockLeavesTheRunToTheViewThatHoldsIt(string view)
	{
		string source =
			view
			+ Method(
				"""
				DoThing();
				Changed += Handler;
				#if A
				Closed += Handler;
				#endif
				DoThing();
				"""
			);

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateEventSubscriptionRunsAnalyzer>(
				source,

				TestRuleSets.WithOneOff(
					on: [DiagnosticDescriptors.SeparateEventSubscriptionRuns],
					off: DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors
				)
			);

		// The run holds both subscriptions only in the view that defines A, and a directive keeps the gap below it as written.
		Assert.HasCount(view.Length == 0 ? 0 : 1, result.Diagnostics, $"View [{view.Trim()}].");
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
				Changed += Handler;
				#if A
				Closed += Handler;
				#endif
				DoThing();
				"""
			);

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateEventSubscriptionRunsAnalyzer>(
				source,

				TestRuleSets.Only(
					DiagnosticDescriptors.SeparateEventSubscriptionRuns,
					DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors
				)
			);

		Assert.IsEmpty(result.Diagnostics, $"View [{view.Trim()}].");
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
				Changed += Handler;
				#region Pair
				Closed += Handler;
				#endregion
				DoThing();
				"""
			);

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateEventSubscriptionRunsAnalyzer>(
				source,

				TestRuleSets.Only(
					DiagnosticDescriptors.SeparateEventSubscriptionRuns,
					DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors
				)
			);

		Assert.IsEmpty(result.Diagnostics, $"View [{view.Trim()}].");
	}

	[TestMethod]
	public async Task ABlankLineWrittenInOneProjectIsLeftAloneInAnotherThatCannotResolveTheEvents()
	{
		string resolved = Method(
			"""
			DoThing();
			Changed += Handler;
			Closed += Handler;
			DoThing();
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();

			Changed += Handler;
			Closed += Handler;

			DoThing();
			"""
		);

		// The same statements in a project whose references lack the events: nothing is a subscription there.
		string unresolved = resolved
			.Replace("\tevent Action Changed;\n", string.Empty)
			.Replace("\tevent Action Closed;\n", string.Empty);

		string unresolvedFixed = fixedSource
			.Replace("\tevent Action Changed;\n", string.Empty)
			.Replace("\tevent Action Closed;\n", string.Empty);

		AnalyzerRunResult withReferences =
			await RoslynTestHarness.RunAsync<SeparateEventSubscriptionRunsAnalyzer>(resolved, s_on);

		AnalyzerRunResult withoutReferences = await RoslynTestHarness.ReportAsync(
			unresolved,
			WithoutCompileCheck(s_on)
		);

		AnalyzerRunResult withoutReferencesFixed = await RoslynTestHarness.ReportAsync(
			unresolvedFixed,
			WithoutCompileCheck(s_on)
		);

		AnalyzerRunResult withReferencesFixed =
			await RoslynTestHarness.ReportAsync<SeparateEventSubscriptionRunsAnalyzer>(
				fixedSource,
				s_on
			);

		RoslynTestHarness.AssertFixedText(fixedSource, withReferences);

		Assert.IsEmpty(
			withoutReferences.Diagnostics,
			"Without the events the rule reports nothing."
		);

		Assert.IsEmpty(
			withoutReferencesFixed.Diagnostics,
			"Without the events the added blank lines raise nothing."
		);

		Assert.IsEmpty(
			withReferencesFixed.Diagnostics,
			"With the events the fixed text is a fixed point."
		);
	}

	[TestMethod]
	public void OnlyStatementsThatCanSubscribeAreBound()
	{
		SemanticModel model = CreateModel(
			"""
			using System;

			class C
			{
				int a;
				int[] list = new int[1];
				event Action Changed;

				void Handler()
				{
				}

				void M()
				{
					a = 1;
					a++;
					a *= 2;
					Handler();
					list[0] += 1;
					a += 1;
					a -= 1;
					Changed += Handler;
					Changed -= Handler;
				}
			}
			"""
		);

		StatementReads<bool> reads = BlankLineSubscriptions.GetReads(model);

		bool[] subscriptions =
		[
			.. model
				.SyntaxTree.GetRoot()
				.DescendantNodes()
				.OfType<ExpressionStatementSyntax>()
				.Select(statement => reads.Read(statement, CancellationToken.None)),
		];

		Assert.AreSequenceEqual(
			[false, false, false, false, false, false, false, true, true],
			subscriptions
		);

		Assert.AreEqual(
			4,
			reads.Bound,
			"Only the four += and -= statements that name something are bound."
		);
	}

	[TestMethod]
	public void ANodeIsBoundOnceHoweverOftenItIsRead()
	{
		SemanticModel model = CreateModel(
			"""
			using System;

			class C
			{
				event Action Changed;

				void Handler()
				{
				}

				void M()
				{
					Changed += Handler;
				}
			}
			"""
		);

		StatementReads<bool> reads = BlankLineSubscriptions.GetReads(model);

		SyntaxNode statement = model
			.SyntaxTree.GetRoot()
			.DescendantNodes()
			.OfType<ExpressionStatementSyntax>()
			.Single();

		for (int read = 0; read < 5; read++)
			Assert.IsTrue(reads.Read(statement, CancellationToken.None));

		Assert.AreSame(reads, BlankLineSubscriptions.GetReads(model), "One model has one cache.");
		Assert.AreEqual(1, reads.Bound);
	}

	private static SemanticModel CreateModel(string source)
	{
		SyntaxTree tree = CSharpSyntaxTree.ParseText(source);

		var compilation = CSharpCompilation.Create(
			"Subscriptions",
			[tree],

			[
				MetadataReference.CreateFromFile(typeof(object).Assembly.Location),

				MetadataReference.CreateFromFile(
					System.Reflection.Assembly.Load("System.Runtime").Location
				),
			]
		);

		return compilation.GetSemanticModel(tree);
	}

	private static RunOptions RunRuleOptions(IReadOnlyDictionary<string, ReportDiagnostic> rules)
	{
		return new RunOptions([
			new SeparateDeclarationRunsAnalyzer(),
			new SeparateInvocationRunsAnalyzer(),
			new SeparateAssertionRunsAnalyzer(),
			new SeparateEventSubscriptionRunsAnalyzer(),
			new SeparateAssignmentRunsAnalyzer(),
		])
		{
			DiagnosticOptions = rules,
		};
	}

	// A snippet in these tests may leave a symbol out on purpose, so the run does not require it to compile.
	private static RunOptions WithoutCompileCheck(
		IReadOnlyDictionary<string, ReportDiagnostic> rules
	)
	{
		return new([new SeparateEventSubscriptionRunsAnalyzer()])
		{
			DiagnosticOptions = rules,
			RequireCompiles = false,
		};
	}

	// Wraps statements in a method of a class that declares the fields, the events and the handler they use.
	private static string Method(string body)
	{
		return SourceFixtures.WrapBody(
			body,
			signature: "void M()",

			classHeader: "class Button\n{\n"
				+ "\tpublic event Action Click;\n\n"
				+ "\tpublic static event Action Shared;\n}\n\n"
				+ "class C",

			members: "\tint a, b, c, d;\n\n"
				+ "\tAction callback;\n\n"
				+ "\tButton button = new();\n\n"
				+ "\tevent Action Changed;\n"
				+ "\tevent Action Closed;\n\n"
				+ "\tvoid Handler()\n\t{\n\t}\n\n"
		);
	}
}
