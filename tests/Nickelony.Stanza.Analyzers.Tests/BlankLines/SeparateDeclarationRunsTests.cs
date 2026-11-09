using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Analyzers.Regions;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed class SeparateDeclarationRunsTests
{
	[TestMethod]
	public async Task AVarRunAndAnExplicitlyTypedRunAreSeparated()
	{
		const string source = """
			class C
			{
				void M()
				{
					var first = 1;
					var second = 2;
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
					var first = 1;
					var second = 2;

					int third = 3;
					int fourth = 4;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDeclarationRunsAnalyzer>(source);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task PreprocessorBoundariesAreLeftUntouched()
	{
		const string source = """
			class C
			{
				void M()
				{
					var value = 1;
			#if DEBUG
					DoThing();
					DoThing();
			#endif
					return;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDeclarationRunsAnalyzer>(source);

		Assert.IsEmpty(result.Diagnostics);
	}

	// The comment starts on the line below the label, so it must not set the indent step.
	private const string BlockCommentBelowTheLabel = """

					/*
					 * The end of the switch.
					 */
		""";

	public static IEnumerable<object[]> TextsBelowTheCaseLabel { get; } =
	[
		["nothing below the label", ""],
		["a block comment below the label", BlockCommentBelowTheLabel],
	];

	[TestMethod]
	[DynamicData(
		nameof(TextsBelowTheCaseLabel),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task UnitsThatFollowACaseLabelOnItsLineAreIndentedUnderIt(
		string name,
		string below
	)
	{
		string source = $$"""
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1: var a = 1; var b = 2; Call(a, b); break;{{below}}
					}
				}

				void Call(int first, int second)
				{
				}
			}
			""";

		string fixedSource = $$"""
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1: var a = 1; var b = 2;

							Call(a, b); break;{{below}}
					}
				}

				void Call(int first, int second)
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDeclarationRunsAnalyzer>(source);

		Assert.HasCount(1, result.Diagnostics, name);
		RoslynTestHarness.AssertFixedText(fixedSource, result, name);
	}

	[TestMethod]
	public async Task UnitsThatFollowABraceOnItsLineAreIndentedUnderIt()
	{
		const string source = """
			class C
			{
				void M()
				{
					if (true) { var a = 1; var b = 2; Call(a, b); }
				}

				void Call(int first, int second)
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					if (true) { var a = 1; var b = 2;

						Call(a, b); }
				}

				void Call(int first, int second)
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDeclarationRunsAnalyzer>(source);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task RepeatedConstDeclarationsGetSeparated()
	{
		const string source = """
			class C
			{
				void M()
				{
					Prepare();
					const int first = 1;
					const int second = 2;
					return;
				}

				void Prepare()
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

					const int first = 1;
					const int second = 2;

					return;
				}

				void Prepare()
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDeclarationRunsAnalyzer>(source);

		Assert.HasCount(2, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TopLevelStatementsAreGrouped()
	{
		const string source = """
			var value = 1;
			Run();
			Run();
			return;

			static void Run()
			{
			}
			""";

		const string fixedSource = """
			var value = 1;

			Run();
			Run();

			return;

			static void Run()
			{
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunRunRulesAsync(source);

		Assert.HasCount(2, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task LoneDeclarationBeforeRepeatedGroupIsSeparated()
	{
		const string source = """
			class C
			{
				void M()
				{
					DoThing();
					const int first = 1;
					int second = 2;
					int third = 3;
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
					DoThing();

					const int first = 1;

					int second = 2;
					int third = 3;
				}

				void DoThing()
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDeclarationRunsAnalyzer>(source);

		Assert.HasCount(2, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ALoneDeclarationAlreadySeparatedFromTheRunStaysWithItsNeighbor()
	{
		const string source = """
			class C
			{
				void M()
				{
					DoThing();
					const int first = 1;

					int second = 2;
					int third = 3;
				}

				void DoThing()
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDeclarationRunsAnalyzer>(source);

		Assert.IsEmpty(result.Diagnostics);
	}

	// A blank line inside an inactive block is no blank line of the compiled code, so the run reaches across the block.
	[TestMethod]
	public async Task ABlankLineInsideAnInactiveBlockDoesNotEndARun()
	{
		const string source = """
			class C
			{
				void M()
				{
					DoThing();
					var a = 1;
			#if NEVER

			#endif
					var b = 2;
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
					DoThing();

					var a = 1;
			#if NEVER

			#endif
					var b = 2;

					DoThing();
				}

				void DoThing()
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDeclarationRunsAnalyzer>(
				source,

				TestRuleSets.WithOneOff(
					on: [],
					off: DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors
				)
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheGroupingRulesTakeTheMultiLineBoundariesOverWhenThatRuleIsOff()
	{
		const string source = """
			class C
			{
				void M(bool value)
				{
					DoThing();

					if (value)
					{
						DoThing();
					}
					int first = 1;
					int second = 2;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool value)
				{
					DoThing();

					if (value)
					{
						DoThing();
					}

					int first = 1;
					int second = 2;
				}
			}
			""";

		// While the owner is on, it is the only rule that reports the boundary.
		AnalyzerRunResult owned = await RoslynTestHarness.ReportAllAsync(source);

		Assert.HasCount(1, owned.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.SeparateMultiLineConstructs.Id,
			owned.Diagnostics[0].Id
		);

		var multiLineRuleOff = new Dictionary<string, ReportDiagnostic>
		{
			[DiagnosticDescriptors.SeparateMultiLineConstructs.Id] = ReportDiagnostic.Suppress,
		};

		AnalyzerRunResult handedOver =
			await RoslynTestHarness.RunAsync<SeparateDeclarationRunsAnalyzer>(
				source,
				multiLineRuleOff
			);

		Assert.HasCount(1, handedOver.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, handedOver);
	}

	[TestMethod]
	public async Task OnlyTheNoneSeveritySwitchesABoundaryOwnerOff()
	{
		const string source = """
			class C
			{
				void M(bool value)
				{
					DoThing();

					if (value)
					{
						DoThing();
					}
					int first = 1;
					int second = 2;
				}
			}
			""";

		(ReportDiagnostic Severity, int Expected)[] cases =
		[
			(ReportDiagnostic.Suppress, 1),
			(ReportDiagnostic.Hidden, 0),
			(ReportDiagnostic.Info, 0),
			(ReportDiagnostic.Warn, 0),
		];

		foreach ((ReportDiagnostic severity, int expected) in cases)
		{
			var ownerSeverity = new Dictionary<string, ReportDiagnostic>
			{
				[DiagnosticDescriptors.SeparateMultiLineConstructs.Id] = severity,
			};

			AnalyzerRunResult result =
				await RoslynTestHarness.ReportAsync<SeparateDeclarationRunsAnalyzer>(
					source,
					treeOptions: ownerSeverity
				);

			Assert.HasCount(
				expected,
				result.Diagnostics,
				$"The owner severity '{severity}' changed how the boundary after it was reported."
			);
		}
	}

	[TestMethod]
	public async Task APragmaRegionHandsAMultiLineBoundaryToTheGroupingRule()
	{
		string source = $$"""
			class C
			{
				void M(bool value)
				{
			#pragma warning disable {{DiagnosticDescriptors.SeparateMultiLineConstructs.Id}}
					DoThing();

					if (value)
					{
						DoThing();
					}
					int first = 1;
					int second = 2;
			#pragma warning restore {{DiagnosticDescriptors.SeparateMultiLineConstructs.Id}}
				}
			}
			""";

		string fixedSource = $$"""
			class C
			{
				void M(bool value)
				{
			#pragma warning disable {{DiagnosticDescriptors.SeparateMultiLineConstructs.Id}}
					DoThing();

					if (value)
					{
						DoThing();
					}

					int first = 1;
					int second = 2;
			#pragma warning restore {{DiagnosticDescriptors.SeparateMultiLineConstructs.Id}}
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source);

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.SeparateDeclarationRuns.Id, result.Diagnostics[0].Id);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ANullConditionalAssignmentEndsARunAsAnAssignmentDoes()
	{
		RoslynTestHarness.RequireRoslynWithCSharp14();

		const string source = """
			class C
			{
				int Value;

				void M(C? other)
				{
					var first = 1;
					var second = 2;
					other?.Value = first;
					DoThing();
					DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				int Value;

				void M(C? other)
				{
					var first = 1;
					var second = 2;

					other?.Value = first;

					DoThing();
					DoThing();
				}
			}
			""";

		AnalyzerRunResult conditional = await RoslynTestHarness.RunRunRulesAsync(source);

		AnalyzerRunResult plain = await RoslynTestHarness.RunRunRulesAsync(
			source.Replace("other?.Value", "other!.Value", StringComparison.Ordinal)
		);

		Assert.IsNotEmpty(conditional.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, conditional);

		RoslynTestHarness.AssertFixedText(
			fixedSource.Replace("other?.Value", "other!.Value", StringComparison.Ordinal),
			plain
		);
	}

	// A pragma over ZA0001 leaves the gaps a multi-line unit borders as `none` does, so ZA0002 asks for none of them.
	[TestMethod]
	public async Task APragmaOverTheMultiLineRuleHandsItsGapsToNoOtherRule()
	{
		const string source = """
			class C
			{
				int M()
				{
					int x = 0;

			#pragma warning disable ZA0001
					var a = 1;
					var a2 = 1;
					var b = Build(
						1);
					var c = 2;
					var c2 = 2;
			#pragma warning restore ZA0001

					return x + a + a2 + b + c + c2;
				}

				static int Build(int v) => v;
			}
			""";

		string multiLine = DiagnosticDescriptors.SeparateMultiLineConstructs.Id;

		AnalyzerRunResult silenced =
			await RoslynTestHarness.RunAsync<SeparateDeclarationRunsAnalyzer>(source);

		AnalyzerRunResult disabled =
			await RoslynTestHarness.RunAsync<SeparateDeclarationRunsAnalyzer>(
				source,

				treeOptions: new Dictionary<string, ReportDiagnostic>
				{
					[multiLine] = ReportDiagnostic.Suppress,
				}
			);

		Assert.IsEmpty(disabled.Diagnostics);
		Assert.IsEmpty(silenced.Diagnostics, "The pragma hands the gaps ZA0001 keeps to ZA0002.");
	}

	// The compiler ignores a pragma in a branch the build leaves out, so ZA0002 still leaves ZA0001 its gaps.
	[TestMethod]
	public async Task APragmaInABranchTheBuildLeavesOutSilencesNoRule()
	{
		const string source = """
			#if NEVER
			#pragma warning disable ZA0001
			#endif
			class C
			{
				int M()
				{
					int x = 0;
					var a = 1;
					var b = Build(
						1);
					var c = 2;
					return x + a + b + c;
				}

				static int Build(int v) => v;
			}
			""";

		// In force, the pragma hands the gaps ZA0001 keeps to this rule, which then writes them.
		const string fixedSource = """
			#if !NEVER
			#pragma warning disable ZA0001
			#endif
			class C
			{
				int M()
				{
					int x = 0;

					var a = 1;
					var b = Build(
						1);
					var c = 2;

					return x + a + b + c;
				}

				static int Build(int v) => v;
			}
			""";

		AnalyzerRunResult inactive =
			await RoslynTestHarness.RunAsync<SeparateDeclarationRunsAnalyzer>(source);

		AnalyzerRunResult inForce =
			await RoslynTestHarness.RunAsync<SeparateDeclarationRunsAnalyzer>(
				source.Replace("#if NEVER", "#if !NEVER", StringComparison.Ordinal)
			);

		RoslynTestHarness.AssertFixedText(fixedSource, inForce);
		Assert.IsEmpty(inactive.Diagnostics, "A pragma left out changes nothing.");
	}

	// ZA0401 writes a blank line beside the marker, so the fields make no run in either fix order.
	[TestMethod]
	public async Task ARegionMarkerSplitsAFieldRunSoTheMemberAboveStaysTight()
	{
		const string source = """
			class C
			{
				public int Count { get; set; }
				private int _first;
				#region Fields
				private int _second;
				#endregion Fields
			}
			""";

		const string fixedSource = """
			class C
			{
				public int Count { get; set; }
				private int _first;

				#region Fields

				private int _second;

				#endregion Fields
			}
			""";

		AnalyzerRunResult alone = await RoslynTestHarness.RunAsync<SeparateDeclarationRunsAnalyzer>(
			source
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source);

		Assert.IsEmpty(alone.Diagnostics, "The marker ends the run, so the rule reports nothing.");
		RoslynTestHarness.AssertFixedText(fixedSource, result);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(expected: fixedSource, source);
	}

	[TestMethod]
	public async Task ARegionMarkerSplitsAFieldRunSoTheMemberBelowStaysTight()
	{
		const string source = """
			class C
			{
				#region Fields
				private int _first;
				#endregion Fields
				private int _second;
				public int Count { get; set; }
			}
			""";

		const string fixedSource = """
			class C
			{
				#region Fields

				private int _first;

				#endregion Fields

				private int _second;
				public int Count { get; set; }
			}
			""";

		AnalyzerRunResult alone = await RoslynTestHarness.RunAsync<SeparateDeclarationRunsAnalyzer>(
			source
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source);

		Assert.IsEmpty(alone.Diagnostics, "The marker ends the run, so the rule reports nothing.");
		RoslynTestHarness.AssertFixedText(fixedSource, result);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(expected: fixedSource, source);
	}

	// The first and the last directive of a gap decide, so a marker ends a run from either end of the gap.
	[TestMethod]
	public async Task ARegionMarkerFirstInTheGapEndsTheRunBeforeAnotherDirective()
	{
		const string source = """
			class C
			{
				public int Count { get; set; }
				private int _first;
				#region Fields
				#nullable enable
				private int _second;
				#endregion Fields
			}
			""";

		const string fixedSource = """
			class C
			{
				public int Count { get; set; }
				private int _first;

				#region Fields
				#nullable enable
				private int _second;

				#endregion Fields
			}
			""";

		AnalyzerRunResult alone = await RoslynTestHarness.RunAsync<SeparateDeclarationRunsAnalyzer>(
			source
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source);

		Assert.IsEmpty(alone.Diagnostics, "The marker ends the run, so the rule reports nothing.");
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARegionMarkerLastInTheGapEndsTheRunAfterAnotherDirective()
	{
		const string source = """
			class C
			{
				public int Count { get; set; }
				private int _first;
				#nullable enable
				#region Fields
				private int _second;
				#endregion Fields
			}
			""";

		const string fixedSource = """
			class C
			{
				public int Count { get; set; }
				private int _first;
				#nullable enable
				#region Fields

				private int _second;

				#endregion Fields
			}
			""";

		AnalyzerRunResult alone = await RoslynTestHarness.RunAsync<SeparateDeclarationRunsAnalyzer>(
			source
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source);

		Assert.IsEmpty(alone.Diagnostics, "The marker ends the run, so the rule reports nothing.");
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARegionMarkerSplitsNoRunWhereTheRegionRuleIsOff()
	{
		const string source = """
			class C
			{
				public int Count { get; set; }
				private int _first;
				#region Fields
				private int _second;
				#endregion Fields
			}
			""";

		const string fixedSource = """
			class C
			{
				public int Count { get; set; }

				private int _first;
				#region Fields
				private int _second;
				#endregion Fields
			}
			""";

		string regionMarker = DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors.Id;

		AnalyzerRunResult disabled =
			await RoslynTestHarness.RunAsync<SeparateDeclarationRunsAnalyzer>(
				source,

				new Dictionary<string, ReportDiagnostic>
				{
					[regionMarker] = ReportDiagnostic.Suppress,
				}
			);

		AnalyzerRunResult silenced =
			await RoslynTestHarness.ReportAsync<SeparateDeclarationRunsAnalyzer>(
				$"#pragma warning disable {regionMarker}\n{source}"
			);

		RoslynTestHarness.AssertFixedText(fixedSource, disabled);
		Assert.HasCount(1, silenced.Diagnostics, "The pragma leaves the marker inside the run.");
	}

	// ZA0401 reports above the marker and not below it, where the pragma is the next directive,
	// so the pragma after the marker leaves the gap above it to ZA0401 and the run ends there.
	[TestMethod]
	public async Task APragmaAfterTheRegionMarkerLeavesTheMarkerEndingTheRun()
	{
		const string source = """
			class C
			{
				public int Count { get; set; }
				private int _first;
				#region Fields
			#pragma warning disable ZA0401
				private int _second;
				#endregion Fields
			}
			""";

		AnalyzerRunResult runs =
			await RoslynTestHarness.ReportAsync<SeparateDeclarationRunsAnalyzer>(source);

		AnalyzerRunResult markers =
			await RoslynTestHarness.ReportAsync<SeparateRegionMarkerFromNeighborsAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors)
			);

		Assert.IsEmpty(runs.Diagnostics, "The marker still ends the run.");
		Assert.IsNotEmpty(markers.Diagnostics, "ZA0401 reports above the marker.");
	}

	// ZA0402 writes a blank line above an #if, so the fields make no run in either fix order.
	[TestMethod]
	public async Task AConditionalBlockSplitsAFieldRunSoTheMemberAboveStaysTight()
	{
		const string source = """
			#define TRACE
			class C
			{
				public int Count { get; set; }
				private int _first;
				#if TRACE
				private int _second;
				#endif
			}
			""";

		const string fixedSource = """
			#define TRACE
			class C
			{
				public int Count { get; set; }
				private int _first;

				#if TRACE
				private int _second;
				#endif
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.Only(
			DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors
		);

		AnalyzerRunResult alone = await RoslynTestHarness.RunAsync<SeparateDeclarationRunsAnalyzer>(
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

	// ZA0402 writes a blank line below an #endif, so the fields make no run in either fix order.
	[TestMethod]
	public async Task AConditionalBlockSplitsAFieldRunSoTheMemberBelowStaysTight()
	{
		const string source = """
			#define TRACE
			class C
			{
				#if TRACE
				private int _first;
				#endif
				private int _second;
				public int Count { get; set; }
			}
			""";

		const string fixedSource = """
			#define TRACE
			class C
			{
				#if TRACE
				private int _first;
				#endif

				private int _second;
				public int Count { get; set; }
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.Only(
			DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors
		);

		AnalyzerRunResult alone = await RoslynTestHarness.RunAsync<SeparateDeclarationRunsAnalyzer>(
			source,
			rules
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source, rules);

		Assert.IsEmpty(alone.Diagnostics, "The #endif ends the run, so the rule reports nothing.");
		RoslynTestHarness.AssertFixedText(fixedSource, result);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			expected: fixedSource,
			source,
			new RunOptions(RoslynTestHarness.AllAnalyzers) { DiagnosticOptions = rules }
		);
	}

	// The first and the last directive of a gap decide, so an #if ends a run from the start of the gap
	// and an #endif from its end.
	[TestMethod]
	public async Task AnIfFirstInTheGapEndsTheRunBeforeAnotherDirective()
	{
		const string source = """
			#define TRACE
			class C
			{
				public int Count { get; set; }
				private int _first;
				#if TRACE
				#nullable enable
				private int _second;
				#endif
			}
			""";

		const string fixedSource = """
			#define TRACE
			class C
			{
				public int Count { get; set; }
				private int _first;

				#if TRACE
				#nullable enable
				private int _second;
				#endif
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.Only(
			DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors
		);

		AnalyzerRunResult alone = await RoslynTestHarness.RunAsync<SeparateDeclarationRunsAnalyzer>(
			source,
			rules
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source, rules);

		Assert.IsEmpty(alone.Diagnostics, "The #if ends the run, so the rule reports nothing.");
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnEndifLastInTheGapEndsTheRunAfterAnotherDirective()
	{
		const string source = """
			#define TRACE
			class C
			{
				#if TRACE
				private int _first;
				#nullable enable
				#endif
				private int _second;
				public int Count { get; set; }
			}
			""";

		const string fixedSource = """
			#define TRACE
			class C
			{
				#if TRACE
				private int _first;
				#nullable enable
				#endif

				private int _second;
				public int Count { get; set; }
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.Only(
			DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors
		);

		AnalyzerRunResult alone = await RoslynTestHarness.RunAsync<SeparateDeclarationRunsAnalyzer>(
			source,
			rules
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source, rules);

		Assert.IsEmpty(alone.Diagnostics, "The #endif ends the run, so the rule reports nothing.");
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AConditionalBlockSplitsNoRunWhereTheConditionalRuleIsOff()
	{
		const string source = """
			#define TRACE
			class C
			{
				public int Count { get; set; }
				private int _first;
				#if TRACE
				private int _second;
				#endif
			}
			""";

		const string fixedSource = """
			#define TRACE
			class C
			{
				public int Count { get; set; }

				private int _first;
				#if TRACE
				private int _second;
				#endif
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDeclarationRunsAnalyzer>(
				source,

				TestRuleSets.WithOneOff(
					on: [],
					off: DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors
				)
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// ZA0402 judges a block by the text of each view, and the run ends only where it does.
	[TestMethod]
	public async Task ABlockThatHoldsPartOfAUnitEndsNoRunInEitherView()
	{
		const string kept = """
			internal sealed class Sample
			{
				public int Count { get; set; }
				private int _first;
			#if A
				[System.Obsolete]
			#endif
				private int _second;
			}
			""";

		const string keptActive = """
			internal sealed class Sample
			{
			#if A
				private sealed class Inner
				{
			#endif
				public int Count { get; set; }
				private int _first;
			#if A
				}
			#endif
				private int _second;
			}
			""";

		const string judged = """
			internal sealed class Sample
			{
				public int Count { get; set; }
				private int _first;
			#if A
				private int _extra;
			#endif
				private int _second;
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.Only(
			DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors
		);

		foreach (string symbols in new[] { string.Empty, "#define A\n" })
		{
			foreach (string keptSource in new[] { kept, keptActive })
			{
				AnalyzerRunResult keptWith =
					await RoslynTestHarness.ReportAsync<SeparateDeclarationRunsAnalyzer>(
						symbols + keptSource,
						rules
					);

				AnalyzerRunResult keptWithout =
					await RoslynTestHarness.ReportAsync<SeparateDeclarationRunsAnalyzer>(
						symbols + keptSource
					);

				Assert.HasCount(
					keptWithout.Diagnostics.Length,
					keptWith.Diagnostics,
					$"A block that ZA0402 leaves alone ends no run with [{symbols.Trim()}]."
				);
			}

			AnalyzerRunResult judgedWith =
				await RoslynTestHarness.ReportAsync<SeparateDeclarationRunsAnalyzer>(
					symbols + judged,
					rules
				);

			Assert.IsEmpty(
				judgedWith.Diagnostics,
				$"A block that ZA0402 judges ends the run with [{symbols.Trim()}]."
			);
		}
	}

	[TestMethod]
	public async Task ADeconstructionJoinsTheVarDeclarationsAroundIt()
	{
		const string source = """
			class C
			{
				void M()
				{
					var first = 1;
					var (second, third) = Read();
					var fourth = 2;
					DoThing();
					DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					var first = 1;
					var (second, third) = Read();
					var fourth = 2;

					DoThing();
					DoThing();
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDeclarationRunsAnalyzer>(source);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task APositionalDeconstructionJoinsTheTypedDeclarationsAroundIt()
	{
		const string source = """
			class C
			{
				void M()
				{
					int first = 1;
					(int second, (string third, int fourth)) = ReadNested();
					int fifth = 2;
					DoThing();
					DoThing();
				}

				(int, (string, int)) ReadNested() => default;
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					int first = 1;
					(int second, (string third, int fourth)) = ReadNested();
					int fifth = 2;

					DoThing();
					DoThing();
				}

				(int, (string, int)) ReadNested() => default;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDeclarationRunsAnalyzer>(source);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task APositionalDeconstructionOfVarJoinsTheVarDeclarationsAroundIt()
	{
		const string source = """
			class C
			{
				void M()
				{
					var first = 1;
					(var second, var third) = Read();
					var fourth = 2;
					DoThing();
					DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					var first = 1;
					(var second, var third) = Read();
					var fourth = 2;

					DoThing();
					DoThing();
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDeclarationRunsAnalyzer>(source);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ADeconstructionThatWritesAnExistingNameIsNoDeclaration()
	{
		const string source = """
			class C
			{
				void M(int first)
				{
					int second = 1;
					int third = 2;
					(int fourth, first) = Read();
					DoThing();
					DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int first)
				{
					int second = 1;
					int third = 2;

					(int fourth, first) = Read();
					DoThing();
					DoThing();
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDeclarationRunsAnalyzer>(source);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ALoneDeconstructionAboveARunIsSetApartLikeADeclaration()
	{
		const string source = """
			class C
			{
				void M()
				{
					DoThing();
					var (first, second) = Read();
					Build(1);
					Build(1);
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					DoThing();

					var (first, second) = Read();

					Build(1);
					Build(1);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunRunRulesAsync(source);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task UsingDeclarationsAreAKindOfTheirOwn()
	{
		const string source = """
			class C
			{
				void M()
				{
					using var first = Open();
					using var second = Open();
					var third = 3;
					var fourth = 4;
				}

				System.IDisposable Open() => null;
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					using var first = Open();
					using var second = Open();

					var third = 3;
					var fourth = 4;
				}

				System.IDisposable Open() => null;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDeclarationRunsAnalyzer>(source);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnAwaitedUsingDeclarationRunsWithAUsingDeclaration()
	{
		const string source = """
			class C
			{
				async System.Threading.Tasks.Task M()
				{
					using var first = Open();
					await using var second = OpenAsync();
					var third = 3;
					var fourth = 4;
				}

				System.IDisposable Open() => null;

				System.IAsyncDisposable OpenAsync() => null;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateDeclarationRunsAnalyzer>(source);

		Assert.HasCount(1, result.Diagnostics);
	}

	[TestMethod]
	public async Task AUsingDeclarationAndAVarDeclarationFormNoRun()
	{
		const string source = """
			class C
			{
				void M()
				{
					var first = 1;
					using var stream = Open();
					var second = 2;
					using var other = Open();
				}

				System.IDisposable Open() => null;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateDeclarationRunsAnalyzer>(source);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ALoneDeclarationStaysTightAgainstADeclarationRunWhereThisRuleIsOff()
	{
		const string source = """
			class C
			{
				void M()
				{
					Prepare();
					using var stream = Open();
					var first = 1;
					var second = 2;
				}

				void Prepare()
				{
				}

				System.IDisposable Open() => null;
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					Prepare();

					using var stream = Open();

					var first = 1;
					var second = 2;
				}

				void Prepare()
				{
				}

				System.IDisposable Open() => null;
			}
			""";

		AnalyzerRunResult on = await RoslynTestHarness.RunAsync<SeparateDeclarationRunsAnalyzer>(
			source
		);

		Assert.HasCount(2, on.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, on);

		AnalyzerRunResult off =
			await RoslynTestHarness.ReportAsync<SeparateDeclarationRunsAnalyzer>(
				source,

				new Dictionary<string, ReportDiagnostic>
				{
					[DiagnosticDescriptors.SeparateDeclarationRuns.Id] = ReportDiagnostic.Suppress,
				}
			);

		Assert.IsEmpty(off.Diagnostics);
	}
}
