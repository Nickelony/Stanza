using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Analyzers.Regions;

namespace Nickelony.Stanza.Analyzers.Tests.Regions;

[TestClass]
public sealed class RemoveDirectiveBlockPaddingTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> PaddingRuleOnly { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.RemoveDirectiveBlockPadding);

	private static Dictionary<string, string> AllTight { get; } =
		new()
		{
			["stanza_region_padding"] = "tight",
			["stanza_conditional_block_padding"] = "tight",
			["stanza_suppression_block_padding"] = "tight",
		};

	private static Dictionary<string, string> AllPadded { get; } =
		new()
		{
			["stanza_region_padding"] = "padded",
			["stanza_conditional_block_padding"] = "padded",
			["stanza_suppression_block_padding"] = "padded",
		};

	[TestMethod]
	public async Task TheBlankLinesInsideAConditionalBlockAreRemovedByDefault()
	{
		const string source = """
			class C
			{
				private int _total;

			#if TRACE

				private int _trace;

			#elif DEBUG

				private int _debug;

			#else

				private int _other;

			#endif

				private int _sum;
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _total;

			#if TRACE
				private int _trace;
			#elif DEBUG
				private int _debug;
			#else
				private int _other;
			#endif

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDirectiveBlockPaddingAnalyzer>(
				source,
				PaddingRuleOnly
			);

		Assert.HasCount(6, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);

		RoslynTestHarness.AssertChangeRidesDiagnostic(result);
	}

	[TestMethod]
	public async Task TheBlankLinesInsideASuppressionBlockAreRemovedByDefault()
	{
		const string source = """
			class C
			{
				void M()
				{
					var options = Build(1);

			#pragma warning disable CS0618

					var legacy = Build(options);

			#pragma warning restore CS0618

					DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					var options = Build(1);

			#pragma warning disable CS0618
					var legacy = Build(options);
			#pragma warning restore CS0618

					DoThing();
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDirectiveBlockPaddingAnalyzer>(
				source,
				PaddingRuleOnly
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARegionKeepsItsPaddingByDefault()
	{
		const string source = """
			class C
			{
				private int _total;

				#region Fields

				private int _count;

				#endregion

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDirectiveBlockPaddingAnalyzer>(
				source,
				PaddingRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics, "A region is padded unless its option says tight.");
	}

	[TestMethod]
	public async Task TheBlankLinesInsideARegionAreRemovedWhereItsOptionSaysTight()
	{
		const string source = """
			class C
			{
				private int _total;

				#region Fields

				private int _count;

				#endregion

				private int _sum;
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _total;

				#region Fields
				private int _count;
				#endregion

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDirectiveBlockPaddingAnalyzer>(
				source,
				PaddingRuleOnly,
				new Dictionary<string, string> { ["stanza_region_padding"] = "tight" }
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task EveryBlankLineOfARunInsideTheBlockGoes()
	{
		const string source = """
			class C
			{
				private int _total;

			#if TRACE



				private int _trace;



			#endif

				private int _sum;
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _total;

			#if TRACE
				private int _trace;
			#endif

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDirectiveBlockPaddingAnalyzer>(
				source,
				PaddingRuleOnly
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABlankLineWithWhitespaceGoesToo()
	{
		const string source = "class C\n{\n\tprivate int _total;\n\n#if TRACE\n \t \n\tprivate int _trace;\n#endif\n}\n";
		const string fixedSource = "class C\n{\n\tprivate int _total;\n\n#if TRACE\n\tprivate int _trace;\n#endif\n}\n";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDirectiveBlockPaddingAnalyzer>(
				source,
				PaddingRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABlockThatItsOptionPadsIsLeftAsWritten()
	{
		const string source = """
			class C
			{
				private int _total;

			#if TRACE

				private int _trace;

			#else

				private int _other;

			#endif

				#region Fields

				private int _count;

				#endregion

				void M()
				{
					var options = Build(1);

			#pragma warning disable CS0618

					var legacy = Build(options);

			#pragma warning restore CS0618

					DoThing();
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDirectiveBlockPaddingAnalyzer>(
				source,
				PaddingRuleOnly,
				AllPadded
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task EachKindFollowsItsOwnOption()
	{
		const string source = """
			class C
			{
				private int _total;

			#if TRACE

				private int _trace;

			#endif

				#region Fields

				private int _count;

				#endregion

				void M()
				{
					var options = Build(1);

			#pragma warning disable CS0618

					var legacy = Build(options);

			#pragma warning restore CS0618

					DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _total;

			#if TRACE

				private int _trace;

			#endif

				#region Fields
				private int _count;
				#endregion

				void M()
				{
					var options = Build(1);

			#pragma warning disable CS0618

					var legacy = Build(options);

			#pragma warning restore CS0618

					DoThing();
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDirectiveBlockPaddingAnalyzer>(
				source,
				PaddingRuleOnly,

				new Dictionary<string, string>
				{
					["stanza_region_padding"] = "tight",
					["stanza_conditional_block_padding"] = "padded",
					["stanza_suppression_block_padding"] = "padded",
				}
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("")]
	[DataRow("unknown")]
	[DataRow("loose")]
	public async Task AnyOtherValueReadsAsTheDefaultOfItsKey(string value)
	{
		const string source = """
			class C
			{
				private int _total;

			#if TRACE

				private int _trace;

			#endif

				#region Fields

				private int _count;

				#endregion

				void M()
				{
					var options = Build(1);

			#pragma warning disable CS0618

					var legacy = Build(options);

			#pragma warning restore CS0618

					DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _total;

			#if TRACE
				private int _trace;
			#endif

				#region Fields

				private int _count;

				#endregion

				void M()
				{
					var options = Build(1);

			#pragma warning disable CS0618
					var legacy = Build(options);
			#pragma warning restore CS0618

					DoThing();
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDirectiveBlockPaddingAnalyzer>(
				source,
				PaddingRuleOnly,

				new Dictionary<string, string>
				{
					["stanza_region_padding"] = value,
					["stanza_conditional_block_padding"] = value,
					["stanza_suppression_block_padding"] = value,
				}
			);

		Assert.HasCount(4, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AGapThatHoldsACommentStaysAsWritten()
	{
		const string source = """
			class C
			{
				private int _total;

			#if TRACE

				// The trace field.
				private int _trace;

				// Kept for the log.

			#endif

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDirectiveBlockPaddingAnalyzer>(
				source,
				PaddingRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics, "A comment in the gap keeps its blank lines.");
	}

	[TestMethod]
	public async Task ADocumentationCommentInTheGapIsACommentToo()
	{
		const string source = """
			class C
			{
				private int _total;

				#region Fields

				/// <summary>The count.</summary>
				private int _count;

				#endregion

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDirectiveBlockPaddingAnalyzer>(
				source,
				PaddingRuleOnly,
				AllTight
			);

		Assert.HasCount(1, result.Diagnostics, "Only the gap above the closing marker holds no comment.");
	}

	[TestMethod]
	public async Task AGapBesideAnotherDirectiveStaysAsWritten()
	{
		const string source = """
			class C
			{
				private int _total;

			#if !TRACE

			#pragma warning disable CS0169

				private int _trace;

			#pragma warning restore CS0169

			#endif

				private int _sum;
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _total;

			#if !TRACE

			#pragma warning disable CS0169
				private int _trace;
			#pragma warning restore CS0169

			#endif

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDirectiveBlockPaddingAnalyzer>(
				source,
				PaddingRuleOnly
			);

		// Only the gaps inside the pragma pair are padding: the others sit beside a directive.
		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AMarkerWithNoPartnerIsLeftAlone()
	{
		const string source = """
			class C
			{
				private int _total;

				#region Fields

				private int _count;
			#pragma warning disable CS0169

				private int _unused;
			}
			""";

		// The source is no code the compiler accepts, and a run over every analyzer fails if one throws on it.
		ImmutableArray<Diagnostic> diagnostics = await RoslynTestHarness.RunWithoutCompilingAsync(
			source,
			PaddingRuleOnly,
			AllTight
		);

		Assert.IsEmpty(diagnostics, "Neither marker has a partner, so neither opens a block.");
	}

	[TestMethod]
	public async Task ABlockInDisabledTextIsLeftAlone()
	{
		const string source = """
			class C
			{
				private int _total;
			#if NEVER
				private int _skipped;

			#pragma warning disable CS0169

				private int _unused;

			#pragma warning restore CS0169

				#region Fields

				private int _count;

				#endregion
			#endif

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDirectiveBlockPaddingAnalyzer>(
				source,
				PaddingRuleOnly,
				AllTight
			);

		Assert.IsEmpty(result.Diagnostics, "The directives of disabled text open no block.");
	}

	[TestMethod]
	public async Task ABlockInsideAnArgumentListIsLeftAlone()
	{
		const string source = """
			class C
			{
				void M()
				{
					Take(
						1,
			#if TRACE

						2,

			#endif
						3);
				}

				void Take(int first, int third) { }

				void Take(int first, int second, int third) { }
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDirectiveBlockPaddingAnalyzer>(
				source,
				PaddingRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics, "A directive inside an expression is no block between units.");
	}

	[TestMethod]
	public async Task ABlockThatHoldsPartOfAUnitIsLeftAlone()
	{
		const string source = """
			class C
			{
				void M(bool ready)
				{
			#if TRACE

					if (ready)
					{

			#endif
						DoThing();
			#if TRACE
					}
			#endif
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDirectiveBlockPaddingAnalyzer>(
				source,
				PaddingRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics, "The block opens a brace that another block closes, so it holds no whole units.");
	}

	[TestMethod]
	public async Task ABlockOfTopLevelStatementsIsJudged()
	{
		const string source = """
			var options = Build(1);

			#if TRACE

			Log(options);

			#endif

			DoThing();
			""";

		const string fixedSource = """
			var options = Build(1);

			#if TRACE
			Log(options);
			#endif

			DoThing();
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDirectiveBlockPaddingAnalyzer>(
				source,
				PaddingRuleOnly
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABlockBelowASwitchLabelIsJudgedInsideOnly()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1:
			#if TRACE

							DoThing();

			#endif
							break;
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1:
			#if TRACE
							DoThing();
			#endif
							break;
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDirectiveBlockPaddingAnalyzer>(
				source,
				PaddingRuleOnly
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABranchThatTheViewDisablesIsJudgedLikeTheActiveOne()
	{
		const string source = """
			class C
			{
				private int _total;

			#if NEVER

				private int _skipped;

			#else

				private int _kept;

			#endif

				private int _sum;
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _total;

			#if NEVER
				private int _skipped;
			#else
				private int _kept;
			#endif

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDirectiveBlockPaddingAnalyzer>(
				source,
				PaddingRuleOnly
			);

		Assert.HasCount(4, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABlockAtTheEdgesOfTheContainerIsJudged()
	{
		const string source = """
			class C
			{
			#if TRACE

				private int _trace;

			#endif
			}
			""";

		const string fixedSource = """
			class C
			{
			#if TRACE
				private int _trace;
			#endif
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDirectiveBlockPaddingAnalyzer>(
				source,
				PaddingRuleOnly
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task APragmaThatSilencesTheRuleLeavesTheBlockAsWritten()
	{
		const string source = """
			#pragma warning disable ZA0404
			class C
			{
				private int _total;

			#if TRACE

				private int _trace;

			#endif

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDirectiveBlockPaddingAnalyzer>(
				source,
				PaddingRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheRuleShipsOnAsASuggestion()
	{
		const string source = """
			class C
			{
				private int _total;

			#if TRACE

				private int _trace;

			#endif

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<RemoveDirectiveBlockPaddingAnalyzer>(source);

		Assert.HasCount(2, result.Diagnostics);
		Assert.AreEqual(DiagnosticSeverity.Info, result.Diagnostics[0].Severity);
	}

	[TestMethod]
	public async Task TheReportSitsOnTheLineWhereTheBlankLinesEnd()
	{
		const string source = """
			class C
			{
				private int _total;

			#if TRACE

				private int _trace;

			#endif

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<RemoveDirectiveBlockPaddingAnalyzer>(source);

		RoslynTestHarness.AssertEachGapIsClaimedOnce(result);

		Assert.AreEqual(6, result.Diagnostics[0].Location.GetLineSpan().StartLinePosition.Line);
		Assert.AreEqual(8, result.Diagnostics[1].Location.GetLineSpan().StartLinePosition.Line);
	}

	[TestMethod]
	public async Task ADiagnosticWithoutItsGapStillFindsItsFix()
	{
		const string source = """
			class C
			{
				private int _total;

			#if TRACE

				private int _trace;
			#endif

				private int _sum;
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _total;

			#if TRACE
				private int _trace;
			#endif

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<RemoveDirectiveBlockPaddingAnalyzer>(
				source,
				PaddingRuleOnly
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnEmptyRegionIsLeftToItsOwnRule()
	{
		const string source = """
			class C
			{
				private int _total;

				#region Fields

				#endregion

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDirectiveBlockPaddingAnalyzer>(
				source,
				PaddingRuleOnly,
				AllTight
			);

		Assert.IsEmpty(result.Diagnostics, "The gap beside the other marker belongs to the rule that removes the region.");
	}

	[TestMethod]
	public async Task TheRulesOfTheDirectivesAndTheCleanupRulesSettleOnOneTextInEveryFixOrder()
	{
		const string source = """
			class C
			{
				private int _total;
				#region Fields

				private int _count;
				#endregion
			#if TRACE
				private int _trace;

			#else

				private int _other;
			#endif
				void M()
				{
					var first = 1;
			#pragma warning disable CS0219

					var second = 2;
			#pragma warning restore CS0219
					var third = 3;
				}

				#region Empty

				#endregion
			}
			""";

		foreach (Dictionary<string, string> options in new[] { AllTight, AllPadded })
		{
			await RoslynTestHarness.AssertEveryStateSettlesAsync(
				source,

				toggled:
				[
					DiagnosticDescriptors.RemoveDirectiveBlockPadding,
					DiagnosticDescriptors.SeparateRegionMarker,
					DiagnosticDescriptors.SeparateConditionalBlock,
					DiagnosticDescriptors.SeparateSuppressionBlock,
					DiagnosticDescriptors.RemoveEmptyRegions,
				],

				alwaysOn: [DiagnosticDescriptors.CollapseExtraBlankLines],
				analyzerConfigOptions: options
			);
		}
	}

	[TestMethod]
	public async Task EveryMixOfPaddingOptionsSettlesWithTheRulesBesideTheBlocks()
	{
		const string source = """
			class C
			{
				private int _total;
				#region Fields
				private int _count;
				#endregion
			#if TRACE
				private int _trace;
			#elif DEBUG

				private int _debug;
			#endif
				void M()
				{
					var first = 1;
					var second = 2;
			#pragma warning disable CS0219
					var third = 3;

					var fourth = 4;
			#pragma warning restore CS0219
					Call();
					Call();
				}

				void Call() { }
			}
			""";

		foreach (Dictionary<string, string> options in PaddingChoices())
		{
			await RoslynTestHarness.AssertEveryStateSettlesAsync(
				source,

				toggled:
				[
					DiagnosticDescriptors.RemoveDirectiveBlockPadding,
					DiagnosticDescriptors.SeparateRegionMarker,
					DiagnosticDescriptors.SeparateConditionalBlock,
					DiagnosticDescriptors.SeparateSuppressionBlock,
				],

				alwaysOn: [DiagnosticDescriptors.SeparateDeclarationRuns],
				analyzerConfigOptions: options
			);
		}
	}

	[TestMethod]
	public async Task EachGapOfTheFourDirectiveRulesIsClaimedOnceInEveryOptionState()
	{
		const string source = """
			class C
			{
				private int _total;
				#region Fields

				private int _count;

				#endregion
			#if TRACE

				private int _trace;
			#elif DEBUG
				private int _debug;

			#else
				private int _other;
			#endif
				void M()
				{
					var first = 1;
			#pragma warning disable CS0219

					var second = 2;
			#pragma warning restore CS0219
					Call();
				}

				void Call() { }
			}
			""";

		foreach (string region in new[] { "padded", "tight" })
		{
			foreach (string conditional in new[] { "padded", "tight" })
			{
				foreach (string suppression in new[] { "padded", "tight" })
				{
					RunOptions options = new([
						new SeparateRegionMarkerAnalyzer(),
						new SeparateConditionalBlockAnalyzer(),
						new SeparateSuppressionBlockAnalyzer(),
						new RemoveDirectiveBlockPaddingAnalyzer(),
						new SeparateDeclarationRunsAnalyzer(),
						new SeparateMultiLineConstructsAnalyzer(),
					])
					{
						DiagnosticOptions = TestRuleSets.Only(
							DiagnosticDescriptors.SeparateRegionMarker,
							DiagnosticDescriptors.SeparateConditionalBlock,
							DiagnosticDescriptors.SeparateSuppressionBlock,
							DiagnosticDescriptors.RemoveDirectiveBlockPadding,
							DiagnosticDescriptors.SeparateDeclarationRuns,
							DiagnosticDescriptors.SeparateMultiLineConstructs
						),

						AnalyzerConfigOptions = new Dictionary<string, string>
						{
							["stanza_region_padding"] = region,
							["stanza_conditional_block_padding"] = conditional,
							["stanza_suppression_block_padding"] = suppression,
						},
					};

					AnalyzerRunResult result = await RoslynTestHarness.ReportAsync(source, options);

					RoslynTestHarness.AssertEachGapIsClaimedOnce(result);
				}
			}
		}
	}

	[TestMethod]
	public async Task TheDirectiveRulesSettleBesideTheNamingBraceAndSwitchLabelRules()
	{
		const string source = """
			class C
			{
				#region Fields
				private int _count;

				#endregion
			#if TRACE

				private int _trace;
			#endif
				void M(int value)
				{

			#pragma warning disable CS0219

					var legacy = 1;
			#pragma warning restore CS0219
					switch (value)
					{
						case 1:

			#if TRACE

							DoThing();

			#endif
							break;
					}

				}
			}
			""";

		foreach (Dictionary<string, string> options in PaddingChoices())
		{
			options["stanza_min_lines_to_name_endif"] = "0";

			await RoslynTestHarness.AssertEveryStateSettlesAsync(
				source,

				toggled:
				[
					DiagnosticDescriptors.RemoveDirectiveBlockPadding,
					DiagnosticDescriptors.SeparateRegionMarker,
					DiagnosticDescriptors.SeparateConditionalBlock,
					DiagnosticDescriptors.SeparateSuppressionBlock,
				],

				alwaysOn:
				[
					DiagnosticDescriptors.NameClosingRegionDirective,
					DiagnosticDescriptors.NameClosingConditionalDirective,
					DiagnosticDescriptors.RemoveBracePadding,
					DiagnosticDescriptors.RemoveSwitchLabelPadding,
					DiagnosticDescriptors.CollapseExtraBlankLines,
				],

				analyzerConfigOptions: options
			);
		}
	}

	[TestMethod]
	public async Task TheDirectiveRulesSettleBesideTheRunAndSeparationRulesInEveryFixOrder()
	{
		const string source = """
			class C
			{
				void M()
				{
					var first = 1;
					var second = 2;
					Call();
					Call();
			#pragma warning disable CS0219
					var third = 3;

					var fourth = 4;
			#pragma warning restore CS0219
					Call(
						1,
						2);
			#if TRACE
					Call();
					Call();
			#else

					Call();
			#endif
					var fifth = 5;
					Call();
				}

				void Call() { }

				void Call(int first, int second) { }
			}
			""";

		foreach (Dictionary<string, string> options in PaddingChoices())
		{
			await RoslynTestHarness.AssertEveryStateSettlesAsync(
				source,

				toggled:
				[
					DiagnosticDescriptors.SeparateMultiLineConstructs,
					DiagnosticDescriptors.SeparateDeclarationRuns,
					DiagnosticDescriptors.GatherRunOfLooseStatements,
					DiagnosticDescriptors.KeepRunOfStatementsTogether,
				],

				alwaysOn:
				[
					DiagnosticDescriptors.SeparateInvocationRuns,
					DiagnosticDescriptors.CollapseExtraBlankLines,
					DiagnosticDescriptors.RemoveDirectiveBlockPadding,
					DiagnosticDescriptors.SeparateRegionMarker,
					DiagnosticDescriptors.SeparateConditionalBlock,
					DiagnosticDescriptors.SeparateSuppressionBlock,
				],

				analyzerConfigOptions: options
			);
		}
	}

	[TestMethod]
	public async Task TheDefaultsPadARegionAndTightenTheOtherBlocks()
	{
		const string source = """
			class C
			{
				private int _total;
				#region Fields

				private int _count;
				#endregion
				private int _sum;
			#if TRACE

				private int _trace;
			#endif
				void M()
				{
					var options = Build(1);
			#pragma warning disable CS0618

					var legacy = Build(options);
			#pragma warning restore CS0618
					DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _total;

				#region Fields

				private int _count;

				#endregion

				private int _sum;

			#if TRACE
				private int _trace;
			#endif

				void M()
				{
					var options = Build(1);

			#pragma warning disable CS0618
					var legacy = Build(options);
			#pragma warning restore CS0618

					DoThing();
				}
			}
			""";

		RunOptions options = new([
			new SeparateRegionMarkerAnalyzer(),
			new SeparateConditionalBlockAnalyzer(),
			new SeparateSuppressionBlockAnalyzer(),
			new RemoveDirectiveBlockPaddingAnalyzer(),
		])
		{
			DiagnosticOptions = TestRuleSets.Only(
				DiagnosticDescriptors.SeparateRegionMarker,
				DiagnosticDescriptors.SeparateConditionalBlock,
				DiagnosticDescriptors.SeparateSuppressionBlock,
				DiagnosticDescriptors.RemoveDirectiveBlockPadding
			),
		};

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source, options);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheOppositeChoicesTightenARegionAndPadTheOtherBlocks()
	{
		const string source = """
			class C
			{
				private int _total;
				#region Fields

				private int _count;
				#endregion
				private int _sum;
			#if TRACE
				private int _trace;
			#endif
				void M()
				{
					var options = Build(1);
			#pragma warning disable CS0618
					var legacy = Build(options);
			#pragma warning restore CS0618
					DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _total;

				#region Fields
				private int _count;
				#endregion

				private int _sum;

			#if TRACE

				private int _trace;

			#endif

				void M()
				{
					var options = Build(1);

			#pragma warning disable CS0618

					var legacy = Build(options);

			#pragma warning restore CS0618

					DoThing();
				}
			}
			""";

		RunOptions options = new([
			new SeparateRegionMarkerAnalyzer(),
			new SeparateConditionalBlockAnalyzer(),
			new SeparateSuppressionBlockAnalyzer(),
			new RemoveDirectiveBlockPaddingAnalyzer(),
		])
		{
			DiagnosticOptions = TestRuleSets.Only(
				DiagnosticDescriptors.SeparateRegionMarker,
				DiagnosticDescriptors.SeparateConditionalBlock,
				DiagnosticDescriptors.SeparateSuppressionBlock,
				DiagnosticDescriptors.RemoveDirectiveBlockPadding
			),

			AnalyzerConfigOptions = new Dictionary<string, string>
			{
				["stanza_region_padding"] = "tight",
				["stanza_conditional_block_padding"] = "padded",
				["stanza_suppression_block_padding"] = "padded",
			},
		};

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source, options);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The choices a matrix runs: the defaults, every kind tight, every kind padded and a mix.
	private static IEnumerable<Dictionary<string, string>> PaddingChoices()
	{
		foreach (
			(string region, string conditional, string suppression) in new[]
			{
				("padded", "tight", "tight"),
				("tight", "tight", "tight"),
				("padded", "padded", "padded"),
				("tight", "padded", "tight"),
			}
		)
		{
			yield return new Dictionary<string, string>
			{
				["stanza_region_padding"] = region,
				["stanza_conditional_block_padding"] = conditional,
				["stanza_suppression_block_padding"] = suppression,
			};
		}
	}
}
