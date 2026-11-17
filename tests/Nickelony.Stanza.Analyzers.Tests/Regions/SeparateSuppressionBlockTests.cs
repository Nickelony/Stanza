using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Regions;

namespace Nickelony.Stanza.Analyzers.Tests.Regions;

[TestClass]
public sealed class SeparateSuppressionBlockTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> BlockRuleOnly { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.SeparateSuppressionBlock);

	private static Dictionary<string, string> PaddedBlocks { get; } =
		new() { ["stanza_suppression_block_padding"] = "padded" };

	[TestMethod]
	public async Task TheGapsAboveADisableAndBelowItsRestoreArePadded()
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
			await RoslynTestHarness.RunAsync<SeparateSuppressionBlockAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);

		RoslynTestHarness.AssertChangeRidesDiagnostic(result);
	}

	[TestMethod]
	public async Task ASeparatedBlockIsNotReportedAgain()
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

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSuppressionBlockAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics, "A separated block is separated again.");
	}

	[TestMethod]
	public async Task TheLinesInsideTheBlockStayAsWritten()
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

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSuppressionBlockAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics, "Only the outside of the block is judged.");
	}

	[TestMethod]
	public async Task MembersAreSeparatedFromABlockToo()
	{
		const string source = """
			class C
			{
				private int _total;
			#pragma warning disable CS0169
				private int _unused;
			#pragma warning restore CS0169
				private int _sum;
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _total;

			#pragma warning disable CS0169
				private int _unused;
			#pragma warning restore CS0169

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSuppressionBlockAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABlockIsSeparatedFromTheCodeButNotFromTheBracesOfItsContainer()
	{
		const string source = """
			class C
			{
				void M()
				{
			#pragma warning disable CS0168
					int unused;
			#pragma warning restore CS0168
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSuppressionBlockAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics, "The braces of the block that holds it stop the gap.");
	}

	[TestMethod]
	public async Task ABlockBelowASwitchLabelIsNotSeparatedFromTheLabel()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1:
			#pragma warning disable CS0618
							DoThing();
			#pragma warning restore CS0618
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
			#pragma warning disable CS0618
							DoThing();
			#pragma warning restore CS0618

							break;
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSuppressionBlockAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ADisableWithoutItsRestoreAndARestoreWithoutItsDisableAreLeftAsWritten()
	{
		const string source = """
			#pragma warning disable CS1591
			class C
			{
				private int _total;
			#pragma warning disable CS0169
				private int _unused;
				private int _sum;
			#pragma warning restore CS0649
				private int _count;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSuppressionBlockAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics, "A pragma with no pair is no block.");
	}

	[TestMethod]
	public async Task TheRestoreOfAnotherCodeIsNoPairOfTheDisable()
	{
		const string source = """
			class C
			{
				private int _total;
			#pragma warning disable CS0169
				private int _unused;
			#pragma warning restore CS0649
				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSuppressionBlockAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ADisableAndARestoreOfTheSameCodesPairWhateverTheOrderOfTheCodes()
	{
		const string source = """
			class C
			{
				private int _total;
			#pragma warning disable CS0169, CS0649
				private int _unused;
			#pragma warning restore CS0649, CS0169
				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateSuppressionBlockAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.HasCount(2, result.Diagnostics);
	}

	// The compiler reads the number 618 as CS0618, so a disable and a restore that spell one warning two ways pair.
	[TestMethod]
	[DataRow("618", "CS0618", 2)]
	[DataRow("CS0618", "618", 2)]
	[DataRow("0618", "CS0618", 2)]
	[DataRow("169, 649", "CS0649, CS0169", 2)]
	[DataRow("618", "619", 0)]
	[DataRow("CS0618", "CS618", 0)]
	[DataRow("CS0618", "CS0619", 0)]
	public async Task ANumberAndItsCsCodePairAsTheSameWarning(string disabled, string restored, int expected)
	{
		string source = $$"""
			class C
			{
				private int _total;
			#pragma warning disable {{disabled}}
				private int _unused;
			#pragma warning restore {{restored}}
				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateSuppressionBlockAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.HasCount(expected, result.Diagnostics);
	}

	[TestMethod]
	public async Task ABareDisableAndABareRestorePair()
	{
		const string source = """
			class C
			{
				private int _total;
			#pragma warning disable
				private int _unused;
			#pragma warning restore
				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateSuppressionBlockAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.HasCount(2, result.Diagnostics);
	}

	[TestMethod]
	public async Task NestedBlocksOfOneCodePairInnerFirst()
	{
		const string source = """
			class C
			{
				private int _total;
			#pragma warning disable CS0169
				private int _unused;
			#pragma warning disable CS0169
				private int _inner;
			#pragma warning restore CS0169
				private int _more;
			#pragma warning restore CS0169
				private int _sum;
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _total;

			#pragma warning disable CS0169
				private int _unused;

			#pragma warning disable CS0169
				private int _inner;
			#pragma warning restore CS0169

				private int _more;
			#pragma warning restore CS0169

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSuppressionBlockAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.HasCount(4, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnotherPairInsideAContainerOfItsOwnIsNoBlockOfTheOuterOne()
	{
		const string source = """
			class C
			{
				void M()
				{
					DoThing();
			#pragma warning disable CS0618
					DoThing();
				}
			#pragma warning restore CS0618

				void N()
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSuppressionBlockAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics, "A pair that stands in two containers keeps its gaps.");
	}

	[TestMethod]
	public async Task ABlockInsideAnArgumentListKeepsItsGap()
	{
		const string source = """
			class C
			{
				void M()
				{
					Take(
						1,
			#pragma warning disable CS0618
						Build(2),
			#pragma warning restore CS0618
						3);
				}

				void Take(int first, int second, int third) { }
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSuppressionBlockAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics, "A pragma inside an expression is no block between units.");
	}

	[TestMethod]
	public async Task ABlockBesideAnotherDirectiveKeepsThatGapTight()
	{
		const string source = """
			class C
			{
				private int _total;
			#if TRACE
			#pragma warning disable CS0169
				private int _unused;
			#pragma warning restore CS0169
			#endif
				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSuppressionBlockAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics, "The directives beside the pragma stop the gap.");
	}

	[TestMethod]
	public async Task ACommentAboveTheDisableMovesWithIt()
	{
		const string source = """
			class C
			{
				private int _total;
				// The old setting is read on purpose.
			#pragma warning disable CS0169
				private int _unused;
			#pragma warning restore CS0169
				private int _sum;
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _total;

				// The old setting is read on purpose.
			#pragma warning disable CS0169
				private int _unused;
			#pragma warning restore CS0169

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSuppressionBlockAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABlockOfTopLevelStatementsIsSeparated()
	{
		const string source = """
			var options = Build(1);
			#pragma warning disable CS0618
			var legacy = Build(options);
			#pragma warning restore CS0618
			DoThing();
			""";

		const string fixedSource = """
			var options = Build(1);

			#pragma warning disable CS0618
			var legacy = Build(options);
			#pragma warning restore CS0618

			DoThing();
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSuppressionBlockAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABlockAtTheEdgesOfAFileHasNoNeighborToSeparateFrom()
	{
		const string source = """
			#pragma warning disable CS0169
			class C
			{
				private int _total;
			}
			#pragma warning restore CS0169
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSuppressionBlockAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
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
				private int _more;
			#endif

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSuppressionBlockAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics, "The pragma sits in text no build reads.");
	}

	[TestMethod]
	public async Task TheRuleShipsOnAsASuggestion()
	{
		const string source = """
			class C
			{
				private int _total;
			#pragma warning disable CS0169
				private int _unused;
			#pragma warning restore CS0169
				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateSuppressionBlockAnalyzer>(source);

		Assert.HasCount(2, result.Diagnostics);
		Assert.AreEqual(DiagnosticSeverity.Info, result.Diagnostics[0].Severity);
	}

	[TestMethod]
	public async Task APragmaThatSilencesTheRuleLeavesTheBlockAsWritten()
	{
		const string source = """
			#pragma warning disable ZA0403
			class C
			{
				private int _total;
			#pragma warning disable CS0169
				private int _unused;
			#pragma warning restore CS0169
				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateSuppressionBlockAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheGroupingRulesEndARunAtTheDirectiveWhereThisRuleIsOn()
	{
		const string source = """
			class C
			{
				void M()
				{
					var first = 1;
					var second = 2;
			#pragma warning disable CS0219
					var third = 3;
			#pragma warning restore CS0219
					var fourth = 4;
					var fifth = 5;
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

			#pragma warning disable CS0219
					var third = 3;
			#pragma warning restore CS0219

					var fourth = 4;
					var fifth = 5;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.SeparateSuppressionBlock,
				DiagnosticDescriptors.SeparateDeclarationRuns
			)
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task EveryStateOfTheRulesBesideTheBlockSettlesOnOneTextInEveryFixOrder()
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
					var fifth = 5;
					Call(
						1,
						2);
			#pragma warning disable CS0219
					Call();
			#pragma warning restore CS0219
					Call();
					Call();
					Call();
				}

				void Call() { }

				void Call(int first, int second) { }
			}
			""";

		await RoslynTestHarness.AssertEveryStateSettlesAsync(
			source,

			toggled:
			[
				DiagnosticDescriptors.SeparateSuppressionBlock,
				DiagnosticDescriptors.SeparateDeclarationRuns,
				DiagnosticDescriptors.SeparateInvocationRuns,
				DiagnosticDescriptors.SeparateMultiLineConstructs,
				DiagnosticDescriptors.GatherRunOfLooseStatements,
				DiagnosticDescriptors.KeepRunOfStatementsTogether,
			]
		);
	}

	[TestMethod]
	public async Task APaddedBlockGetsABlankLineInsideItsMarkersToo()
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
			await RoslynTestHarness.RunAsync<SeparateSuppressionBlockAnalyzer>(
				source,
				BlockRuleOnly,
				PaddedBlocks
			);

		Assert.HasCount(4, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);

		RoslynTestHarness.AssertChangeRidesDiagnostic(result);
	}

	[TestMethod]
	public async Task APaddedBlockIsNotReportedAgain()
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

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSuppressionBlockAnalyzer>(
				source,
				BlockRuleOnly,
				PaddedBlocks
			);

		Assert.IsEmpty(result.Diagnostics, "A padded block is padded again.");
	}

	[TestMethod]
	public async Task APaddedBlockIsPaddedInsideBesideTheBracesOfItsContainer()
	{
		const string source = """
			class C
			{
				void M()
				{
			#pragma warning disable CS0219
					var legacy = 1;
			#pragma warning restore CS0219
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
			#pragma warning disable CS0219

					var legacy = 1;

			#pragma warning restore CS0219
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSuppressionBlockAnalyzer>(
				source,
				BlockRuleOnly,
				PaddedBlocks
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentAboveTheRestoreTakesTheBlankLineOfAPaddedBlock()
	{
		const string source = """
			class C
			{
				private int _total;

			#pragma warning disable CS0169

				private int _unused;
				// Kept for the log.
			#pragma warning restore CS0169

				private int _sum;
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _total;

			#pragma warning disable CS0169

				private int _unused;

				// Kept for the log.
			#pragma warning restore CS0169

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSuppressionBlockAnalyzer>(
				source,
				BlockRuleOnly,
				PaddedBlocks
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task NestedPairsOfAPaddedBlockKeepTheirDirectivesTogether()
	{
		const string source = """
			class C
			{
				void M()
				{
					var options = Build(1);
			#pragma warning disable CS0219
			#pragma warning disable CS0168
					var legacy = 1;
			#pragma warning restore CS0168
			#pragma warning restore CS0219
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

			#pragma warning disable CS0219
			#pragma warning disable CS0168

					var legacy = 1;

			#pragma warning restore CS0168
			#pragma warning restore CS0219

					DoThing();
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSuppressionBlockAnalyzer>(
				source,
				BlockRuleOnly,
				PaddedBlocks
			);

		Assert.HasCount(4, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task APaddedBlockInsideAnArgumentListKeepsEveryGap()
	{
		const string source = """
			class C
			{
				void M()
				{
					Take(
						1,
			#pragma warning disable CS0618
						Build(2),
			#pragma warning restore CS0618
						3);
				}

				void Take(int first, int second, int third) { }
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSuppressionBlockAnalyzer>(
				source,
				BlockRuleOnly,
				PaddedBlocks
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	[DataRow("tight")]
	[DataRow("TIGHT")]
	[DataRow("")]
	[DataRow("loose")]
	public async Task ATightBlockOrAnyOtherValueGetsTheOutsideGapsOnly(string value)
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
			await RoslynTestHarness.RunAsync<SeparateSuppressionBlockAnalyzer>(
				source,
				BlockRuleOnly,
				new Dictionary<string, string> { ["stanza_suppression_block_padding"] = value }
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
