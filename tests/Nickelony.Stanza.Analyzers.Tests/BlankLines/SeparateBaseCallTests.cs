using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed class SeparateBaseCallTests
{
	// Each row is a body that opens with a base call of one form, and the body with the blank line below the call.
	public static IEnumerable<object[]> OpeningCalls { get; } =
	[
		["a call", "base.OnStart();\na = 1;\nb = 2;", "base.OnStart();\n\na = 1;\nb = 2;"],

		[
			"an awaited call",
			"await base.OnStartAsync();\na = 1;\nb = 2;",
			"await base.OnStartAsync();\n\na = 1;\nb = 2;",
		],

		[
			"a discarded call",
			"_ = base.Fetch();\na = 1;\nb = 2;",
			"_ = base.Fetch();\n\na = 1;\nb = 2;",
		],

		[
			"a discarded awaited call",
			"_ = await base.FetchAsync();\na = 1;\nb = 2;",
			"_ = await base.FetchAsync();\n\na = 1;\nb = 2;",
		],

		[
			"a call through a member of base",
			"base.Items.Clear();\na = 1;\nb = 2;",
			"base.Items.Clear();\n\na = 1;\nb = 2;",
		],

		[
			"a call on what a base call returns",
			"base.Create().OnStart();\na = 1;\nb = 2;",
			"base.Create().OnStart();\n\na = 1;\nb = 2;",
		],

		[
			"a call through an element of base",
			"base[0].Clear();\na = 1;\nb = 2;",
			"base[0].Clear();\n\na = 1;\nb = 2;",
		],

		[
			"a configured awaited call",
			"await base.OnStartAsync().ConfigureAwait(false);\na = 1;\nb = 2;",
			"await base.OnStartAsync().ConfigureAwait(false);\n\na = 1;\nb = 2;",
		],

		[
			"a call on the line of the next statement",
			"base.OnStart(); a = 1;\nb = 2;",
			"base.OnStart();\n\na = 1;\nb = 2;",
		],
	];

	// Each row is a body the rule leaves as written.
	public static IEnumerable<object[]> QuietBodies { get; } =
	[
		["a base call alone", "base.OnStart();"],
		["a base call in the middle", "a = 1;\nbase.OnStart();\nb = 2;"],
		["a base call already set apart", "base.OnStart();\n\na = 1;\nb = 2;"],
		["a closing base call already set apart", "a = 1;\nb = 2;\n\nbase.OnStop();"],
		["an assignment to a member of base", "base.Value = 1;\na = 1;\nb = 2;"],
		["a call that passes a member of base", "Use(base.Value);\na = 1;\nb = 2;"],
		["a call of this", "this.OnStop();\na = 1;\nb = 2;"],
		["a call of the same name", "OnStop();\na = 1;\nb = 2;"],

		[
			"a base call in a nested block",
			"if (a == 0)\n{\n\tbase.OnStart();\n\ta = 1;\n\tb = 2;\n}",
		],

		[
			"a base call in a lambda",
			"Action act = () =>\n{\n\tbase.OnStart();\n\ta = 1;\n\tb = 2;\n};\n\nact();",
		],

		[
			"a base call in a local function",
			"void Local()\n{\n\tbase.OnStart();\n\ta = 1;\n\tb = 2;\n}\n\nLocal();",
		],

		["a directive in the gap", "base.OnStart();\n#if DEBUG\na = 1;\n#endif\nb = 2;"],
	];

	[TestMethod]
	[DynamicData(
		nameof(OpeningCalls),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task TheStatementBelowAnOpeningBaseCallIsSetApart(
		string name,
		string body,
		string fixedBody
	)
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateBaseCallAnalyzer>(
			Method(body),
			Alone()
		);

		Assert.HasCount(1, result.Diagnostics, name);
		RoslynTestHarness.AssertFixedText(Method(fixedBody), result, name);
	}

	[TestMethod]
	[DynamicData(
		nameof(QuietBodies),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ABodyThatAsksForNoBlankLineBesideABaseCallIsLeftAlone(
		string name,
		string body
	)
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateBaseCallAnalyzer>(
			Method(body),
			Alone()
		);

		Assert.IsEmpty(result.Diagnostics, name);
	}

	[TestMethod]
	public async Task TheStatementAboveAClosingBaseCallIsSetApart()
	{
		string source = Method("a = 1;\nb = 2;\nbase.OnStop();", "public override void OnStop()");

		string fixedSource = Method(
			"a = 1;\nb = 2;\n\nbase.OnStop();",
			"public override void OnStop()"
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateBaseCallAnalyzer>(
			source,
			Alone()
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABodyThatOpensAndClosesWithABaseCallSetsBothApart()
	{
		string source = Method("base.OnStart();\na = 1;\nbase.OnStop();");
		string fixedSource = Method("base.OnStart();\n\na = 1;\n\nbase.OnStop();");

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateBaseCallAnalyzer>(
			source,
			Alone()
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheBodyOfAConstructorAFinalizerAndAnAccessorIsJudged()
	{
		const string source = """
			class C : Stage
			{
				int a, b;

				public C()
				{
					base.OnStart();
					a = 1;
					b = 2;
				}

				~C()
				{
					a = 0;
					b = 0;
					base.OnStop();
				}

				public override int Value
				{
					get => base.Value;
					set
					{
						base.OnStop();
						a = value;
						b = value;
					}
				}
			}
			""";

		const string fixedSource = """
			class C : Stage
			{
				int a, b;

				public C()
				{
					base.OnStart();

					a = 1;
					b = 2;
				}

				~C()
				{
					a = 0;
					b = 0;

					base.OnStop();
				}

				public override int Value
				{
					get => base.Value;
					set
					{
						base.OnStop();

						a = value;
						b = value;
					}
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateBaseCallAnalyzer>(
			source,
			Alone()
		);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheRuleShipsOffAndReportsNothingUntilASeverityEnablesIt()
	{
		string source = Method("base.OnStart();\na = 1;\nb = 2;");

		AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(source);

		AnalyzerRunResult enabled = await RoslynTestHarness.ReportAsync<SeparateBaseCallAnalyzer>(
			source,
			Alone()
		);

		Assert.DoesNotContain(
			DiagnosticDescriptors.SeparateBaseCall.Id,
			result.Diagnostics.Select(diagnostic => diagnostic.Id)
		);

		Assert.HasCount(1, enabled.Diagnostics);
	}

	[TestMethod]
	public async Task ACommentAboveTheStatementBelowMovesWithItSoTheBlankLineGoesAboveTheComment()
	{
		string source = Method("base.OnStart();\n// Next.\na = 1;\nb = 2;");
		string fixedSource = Method("base.OnStart();\n\n// Next.\na = 1;\nb = 2;");

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateBaseCallAnalyzer>(
			source,
			Alone()
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheCommentedSiblingRuleOwnsTheGapAboveACommentWhereItIsOn()
	{
		string source = Method("base.OnStart();\n// Next.\na = 1;\nb = 2;");

		Dictionary<string, ReportDiagnostic> rules = Alone(
			DiagnosticDescriptors.SeparateCommentedSibling
		);

		AnalyzerRunResult baseCall = await RoslynTestHarness.ReportAsync<SeparateBaseCallAnalyzer>(
			source,
			rules
		);

		AnalyzerRunResult both = await RoslynTestHarness.ReportAllAsync(source, rules);

		Assert.IsEmpty(baseCall.Diagnostics, "ZA0012 owns the gap above the comment.");
		Assert.HasCount(1, both.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.SeparateCommentedSibling.Id, both.Diagnostics[0].Id);
	}

	[TestMethod]
	public async Task TheMultiLineRuleOwnsTheGapBesideAMultiLineStatementWhereItIsOn()
	{
		string source = Method("base.OnStart();\nif (a == 0)\n{\n\tb = 1;\n}\n\nb = 2;");

		AnalyzerRunResult on = await RoslynTestHarness.ReportAsync<SeparateBaseCallAnalyzer>(
			source,
			Alone(DiagnosticDescriptors.SeparateMultiLineConstructs)
		);

		AnalyzerRunResult off = await RoslynTestHarness.ReportAsync<SeparateBaseCallAnalyzer>(
			source,
			Alone()
		);

		Assert.IsEmpty(on.Diagnostics, "ZA0001 owns the gap beside the multi-line statement.");
		Assert.HasCount(1, off.Diagnostics, "ZA0001 is off, so it hands the gap back.");
	}

	[TestMethod]
	public async Task TheTwoUnitRuleOwnsABodyOfTwoStatementsWhereItIsOn()
	{
		string source = Method("base.OnStart();\na = 1;");

		AnalyzerRunResult on = await RoslynTestHarness.ReportAsync<SeparateBaseCallAnalyzer>(
			source,
			Alone(DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether)
		);

		AnalyzerRunResult off = await RoslynTestHarness.RunAsync<SeparateBaseCallAnalyzer>(
			source,
			Alone()
		);

		Assert.IsEmpty(on.Diagnostics, "ZA0051 owns the gap of a body of two statements.");
		Assert.HasCount(1, off.Diagnostics);

		RoslynTestHarness.AssertFixedText(Method("base.OnStart();\n\na = 1;"), off);
	}

	[TestMethod]
	public async Task ABaseCallBelowTheGuardsStillOpensTheBodyAndEachGapHasOneOwner()
	{
		string source = Method("if (a == 0) return;\nbase.OnStart();\na = 1;\nb = 2;");
		string fixedSource = Method("if (a == 0) return;\n\nbase.OnStart();\n\na = 1;\nb = 2;");

		Dictionary<string, ReportDiagnostic> rules = Alone(
			DiagnosticDescriptors.SeparateGuardClauses
		);

		AnalyzerRunResult baseCall = await RoslynTestHarness.RunAsync<SeparateBaseCallAnalyzer>(
			source,
			rules
		);

		AnalyzerRunResult both = await RoslynTestHarness.ReportAllAsync(source, rules);

		Assert.HasCount(1, baseCall.Diagnostics);

		RoslynTestHarness.AssertFixedText(
			Method("if (a == 0) return;\nbase.OnStart();\n\na = 1;\nb = 2;"),
			baseCall
		);

		Assert.HasCount(2, both.Diagnostics);
		RoslynTestHarness.AssertEachGapIsClaimedOnce(both);

		AnalyzerRunResult settled = await RoslynTestHarness.RunIterativelyAsync(source, rules);
		RoslynTestHarness.AssertFixedText(fixedSource, settled);
	}

	[TestMethod]
	public async Task TheGapAboveAClosingBaseCallBelowTheGuardsIsThisRules()
	{
		string source = Method("if (a == 0) return;\nif (a == 1) return;\nbase.OnStop();");
		string fixedSource = Method("if (a == 0) return;\nif (a == 1) return;\n\nbase.OnStop();");

		Dictionary<string, ReportDiagnostic> rules = Alone(
			DiagnosticDescriptors.SeparateGuardClauses
		);

		AnalyzerRunResult guard = await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
			source,
			rules
		);

		AnalyzerRunResult guardAlone =
			await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(source, GuardAlone());

		AnalyzerRunResult baseCall = await RoslynTestHarness.RunAsync<SeparateBaseCallAnalyzer>(
			source,
			rules
		);

		Assert.IsEmpty(guard.Diagnostics, "This rule owns the gap, so ZA0011 leaves it.");

		Assert.HasCount(
			1,
			guardAlone.Diagnostics,
			"With this rule off, ZA0011 writes the blank line."
		);

		Assert.HasCount(1, baseCall.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, baseCall);
	}

	[TestMethod]
	public async Task TheGapBelowAnOpeningBaseCallAboveAJumpIsThisRules()
	{
		string source = Method(
			"if (a == 0) return;\nif (a == 1) return;\nbase.OnStart();\nreturn;"
		);

		string fixedSource = Method(
			"if (a == 0) return;\nif (a == 1) return;\nbase.OnStart();\n\nreturn;"
		);

		Dictionary<string, ReportDiagnostic> rules = Alone(
			DiagnosticDescriptors.SeparateJumpStatement
		);

		AnalyzerRunResult jump = await RoslynTestHarness.ReportAsync<SeparateJumpStatementAnalyzer>(
			source,
			rules
		);

		AnalyzerRunResult jumpAlone =
			await RoslynTestHarness.ReportAsync<SeparateJumpStatementAnalyzer>(source, JumpAlone());

		AnalyzerRunResult baseCall = await RoslynTestHarness.RunAsync<SeparateBaseCallAnalyzer>(
			source,
			rules
		);

		Assert.IsEmpty(jump.Diagnostics, "This rule owns the gap, so ZA0009 leaves it.");

		Assert.HasCount(
			1,
			jumpAlone.Diagnostics,
			"With this rule off, ZA0009 asks for the gap above the jump."
		);

		Assert.HasCount(1, baseCall.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, baseCall);
	}

	[TestMethod]
	public async Task AReturnedBaseCallIsAJumpWhoseGapTheJumpRuleOwns()
	{
		string source = Method(
			"a = 1;\nb = 2;\nreturn base.Fetch();",
			"public override int Fetch()"
		);

		string fixedSource = Method(
			"a = 1;\nb = 2;\n\nreturn base.Fetch();",
			"public override int Fetch()"
		);

		Dictionary<string, ReportDiagnostic> rules = Alone(
			DiagnosticDescriptors.SeparateJumpStatement
		);

		AnalyzerRunResult baseCall = await RoslynTestHarness.ReportAsync<SeparateBaseCallAnalyzer>(
			source,
			rules
		);

		AnalyzerRunResult both = await RoslynTestHarness.RunAllAsync(source, rules);

		Assert.IsEmpty(baseCall.Diagnostics);
		Assert.HasCount(1, both.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.SeparateJumpStatement.Id, both.Diagnostics[0].Id);

		RoslynTestHarness.AssertFixedText(fixedSource, both);
	}

	[TestMethod]
	public async Task TheGapOfABaseCallEndsTheStanzaAboveAJumpInEitherFixOrder()
	{
		string source = Method("base.OnStart();\na = 1;\nreturn;");
		string fixedSource = Method("base.OnStart();\n\na = 1;\nreturn;");

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			expected: fixedSource,
			source,

			options: new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = Alone(DiagnosticDescriptors.SeparateJumpStatement),
			}
		);
	}

	[TestMethod]
	public async Task TheGapOfABaseCallEndsARunOfCallsOfTheSameNameInEitherFixOrder()
	{
		string source = Method(
			"base.Clear();\n_first.Clear();\nUse(0);",
			"public override void Clear()"
		);

		string fixedSource = Method(
			"base.Clear();\n\n_first.Clear();\nUse(0);",
			"public override void Clear()"
		);

		Dictionary<string, ReportDiagnostic> rules = Alone(
			DiagnosticDescriptors.SeparateInvocationRuns
		);

		AnalyzerRunResult reported = await RoslynTestHarness.ReportAllAsync(source, rules);

		Assert.HasCount(1, reported.Diagnostics, "A lone call below the base call is no run.");

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			expected: fixedSource,
			source,
			options: new RunOptions(RoslynTestHarness.AllAnalyzers) { DiagnosticOptions = rules }
		);
	}

	[TestMethod]
	public async Task TheGatherRuleKeepsTheGapOfABaseCall()
	{
		string source = Method("base.OnStart();\n\na = 1;\n\nb = 2;\n\nc = 3;");
		string fixedSource = Method("base.OnStart();\n\na = 1;\nb = 2;\nc = 3;");

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,
			Alone(DiagnosticDescriptors.GatherRunOfLooseStatements),
			new Dictionary<string, string> { ["stanza_max_loose_run_size"] = "1" }
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task APragmaThatSilencesTheRuleLeavesTheCallsToTheRunRules()
	{
		string source =
			"#pragma warning disable ZA0010\n"
			+ Method("base.Clear();\n_first.Clear();\nUse(0);", "public override void Clear()");

		string fixedSource =
			"#pragma warning disable ZA0010\n"
			+ Method("base.Clear();\n_first.Clear();\n\nUse(0);", "public override void Clear()");

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,
			Alone(DiagnosticDescriptors.SeparateInvocationRuns)
		);

		RoslynTestHarness.AssertFixedText(
			fixedSource,
			result,
			"The two calls of Clear stay a run, so ZA0004 sets it apart."
		);
	}

	// ZA0010 alone: the rules that share its gaps are off unless a test names them.
	private static Dictionary<string, ReportDiagnostic> Alone(
		params DiagnosticDescriptor[] alsoOn
	) => WithShippedRulesOff([DiagnosticDescriptors.SeparateBaseCall, .. alsoOn]);

	private static Dictionary<string, ReportDiagnostic> GuardAlone() =>
		WithShippedRulesOff([DiagnosticDescriptors.SeparateGuardClauses]);

	private static Dictionary<string, ReportDiagnostic> JumpAlone() =>
		WithShippedRulesOff([DiagnosticDescriptors.SeparateJumpStatement]);

	private static Dictionary<string, ReportDiagnostic> WithShippedRulesOff(
		DiagnosticDescriptor[] on
	) => TestRuleSets.OnSuppressingOthersOf(on, TestRuleSets.SharedGapRules);

	private static string Method(
		string body,
		string signature = "public override async System.Threading.Tasks.Task OnStartAsync()"
	)
	{
		return SourceFixtures.WrapBody(
			body,
			signature,
			classHeader: "class C : Stage",
			members: "\tint a, b, c;\n\tStage _first = new();\n\n",
			after: "\n\tvoid Use(int value) { }\n"
		);
	}
}
