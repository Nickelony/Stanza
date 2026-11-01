using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed partial class GatherRunOfLooseStatementsTests
{
	private const string LimitKey = "stanza_max_loose_run_size";

	// The statements a run of loose statements is made of, written without an indent.
	private const string FourLooseStatements = """
		first = 1;

		second = 2;

		third = 3;

		fourth = 4;
		""";

	private const string FourGatheredStatements = """
		first = 1;
		second = 2;
		third = 3;
		fourth = 4;
		""";

	// Four loose statements, and a region marker directly below the last of them.
	private const string RegionSource = """
		class C
		{
			void M()
			{
				DoThing();

				DoThing();

				DoThing();

				DoThing();
		#region Rest
				DoThing();
		#endregion
			}

			void DoThing() { }
		}
		""";

	// Each row is a body that the rule leaves alone at the default limit of three.
	public static IEnumerable<object[]> QuietBodies { get; } =
	[
		[
			"three loose statements",

			"""
				first = 1;

				second = 2;

				third = 3;
				""",
		],

		[
			"a multi-line statement in the middle",

			"""
				first = 1;

				second = 2;

				Call(
					1,
					2);

				third = 3;

				fourth = 4;
				""",
		],

		[
			"a comment above a statement",

			"""
				first = 1;

				second = 2;

				// The third step.
				third = 3;

				fourth = 4;
				""",
		],

		[
			"a comment that trails a statement",

			"""
				first = 1;

				second = 2; // The second step.

				third = 3;

				fourth = 4;
				""",
		],

		[
			"a block comment in the gap",

			"""
				first = 1;

				second = 2;
				/* The rest. */

				third = 3;

				fourth = 4;
				""",
		],

		[
			"a region directive in the gap, which ends a run as any directive does",

			"""
				first = 1;

				second = 2;
				#region Rest

				third = 3;

				fourth = 4;
				#endregion
				""",
		],

		[
			"a conditional directive in the gap",

			"""
				first = 1;

				second = 2;

				#if DEBUG
				third = 3;
				#endif

				fourth = 4;
				""",
		],

		[
			"a gap with no blank line",

			"""
				first = 1;

				second = 2;
				third = 3;

				fourth = 4;
				""",
		],

		[
			"a gap the declaration run rule asks for once the run is gathered",

			"""
				first = 1;

				second = 2;

				DoThing();

				DoThing();

				third = 3;
				""",
		],

		[
			"a gap the same-type rule asks for once the run is gathered",

			"""
				int first = 1;

				string second = "a";

				string third = "b";

				int fourth = 4;

				int fifth = 5;
				""",
		],

		[
			"a gap the multi-line rule asks for beside a multi-line statement",

			"""
				first = 1;

				second = 2;

				if (first > 0)
				{
					DoThing();
				}

				third = 3;

				fourth = 4;
				""",
		],
	];

	// Each row is a place the rule does not judge, with four elements set apart by blank lines.
	public static IEnumerable<object[]> UnjudgedPlaces { get; } =
	[
		[
			"members",

			"""
				class C
				{
					int first;

					int second;

					int third;

					int fourth;
				}
				""",
		],

		[
			"collection elements",

			"""
				class C
				{
					int[] numbers = new[]
					{
						1,

						2,

						3,

						4,
					};
				}
				""",
		],

		[
			"arguments",

			"""
				class C
				{
					void M()
					{
						Combine(
							1,

							2,

							3,

							4
						);
					}

					static void Combine(int first, int second, int third, int fourth) { }
				}
				""",
		],

		[
			"switch sections",

			"""
				class C
				{
					void M(int value)
					{
						switch (value)
						{
							case 1: return;

							case 2: return;

							case 3: return;

							case 4: return;
						}
					}
				}
				""",
		],
	];

	[TestMethod]
	public async Task FourLooseStatementsAreGatheredAtTheDefaultLimit()
	{
		const string source = """
			class C
			{
				int first;
				int second;
				int third;
				int fourth;

				void M()
				{
					first = 1;

					second = 2;

					third = 3;

					fourth = 4;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				int first;
				int second;
				int third;
				int fourth;

				void M()
				{
					first = 1;
					second = 2;
					third = 3;
					fourth = 4;
				}
			}
			""";

		AnalyzerRunResult result = await RunAsync(source);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// Both runs hold the same statement indexes, so only the block tells the fixes of the two apart.
	[TestMethod]
	public async Task TwoBlocksThatHoldRunsOfTheSameBoundsAreBothGathered()
	{
		const string source = """
			class C
			{
				int first;
				int second;

				void One()
				{
					first = 1;

					second = 2;

					first = 3;

					second = 4;
				}

				void Two()
				{
					first = 5;

					second = 6;

					first = 7;

					second = 8;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				int first;
				int second;

				void One()
				{
					first = 1;
					second = 2;
					first = 3;
					second = 4;
				}

				void Two()
				{
					first = 5;
					second = 6;
					first = 7;
					second = 8;
				}
			}
			""";

		AnalyzerRunResult result = await RunAsync(source);

		Assert.HasCount(6, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARunInASwitchSectionIsGathered()
	{
		const string source = """
			class C
			{
				int first;
				int second;
				int third;
				int fourth;

				void M(int value)
				{
					switch (value)
					{
						case 1:
							first = 1;

							second = 2;

							third = 3;

							fourth = 4;

							break;
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				int first;
				int second;
				int third;
				int fourth;

				void M(int value)
				{
					switch (value)
					{
						case 1:
							first = 1;
							second = 2;
							third = 3;
							fourth = 4;
							break;
					}
				}
			}
			""";

		AnalyzerRunResult result = await RunAsync(source);

		Assert.HasCount(4, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AGapOfSeveralBlankLinesIsCollapsedWithTheRest()
	{
		const string source = """
			class C
			{
				int first;
				int second;
				int third;
				int fourth;

				void M()
				{
					first = 1;


					second = 2;

					third = 3;



					fourth = 4;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				int first;
				int second;
				int third;
				int fourth;

				void M()
				{
					first = 1;
					second = 2;
					third = 3;
					fourth = 4;
				}
			}
			""";

		AnalyzerRunResult result = await RunAsync(source);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task FixAllGathersTheWholeRunInOneStep()
	{
		const string source = """
			class C
			{
				int first;
				int second;
				int third;
				int fourth;
				int fifth;

				void M()
				{
					first = 1;

					second = 2;


					third = 3;

					fourth = 4;

					fifth = 5;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				int first;
				int second;
				int third;
				int fourth;
				int fifth;

				void M()
				{
					first = 1;
					second = 2;
					third = 3;
					fourth = 4;
					fifth = 5;
				}
			}
			""";

		// The extra blank line rule reports the double gap first and would take the Fix All, so it stays off.
		AnalyzerRunResult result = await RoslynTestHarness.RunFixAllAsync(
			source,

			TestRuleSets.WithOneOff(
				on: [DiagnosticDescriptors.GatherRunOfLooseStatements],
				off: DiagnosticDescriptors.CollapseExtraBlankLines
			)
		);

		Assert.HasCount(4, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// Every gap of a run reports the bounds of the whole run, so the fix of any gap gathers it.
	// It stays inside the run, and the next run of the container keeps its blank line.
	[TestMethod]
	public async Task TheFixOfAnyGapRemovesTheBlankLinesOfTheWholeRun()
	{
		string source = Method(
			"""
			first = 1;

			second = 2;

			third = 3;

			fourth = 4;

			Call(
				1,
				2);

			fifth = 5;

			sixth = 6;
			"""
		);

		string fixedSource = Method(
			"""
			first = 1;
			second = 2;
			third = 3;
			fourth = 4;

			Call(
				1,
				2);

			fifth = 5;

			sixth = 6;
			"""
		);

		string[] alone = await RoslynTestHarness.ApplyEachFixAloneAsync(
			source,
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			TestRuleSets.Only(DiagnosticDescriptors.GatherRunOfLooseStatements)
		);

		Assert.AreSequenceEqual([fixedSource, fixedSource, fixedSource], alone);
	}

	[TestMethod]
	public async Task TheFixOfTheLastGapGathersTheRunAsFixAllDoes()
	{
		string source = Method(FourLooseStatements);

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.Only(
			DiagnosticDescriptors.GatherRunOfLooseStatements
		);

		string[] alone = await RoslynTestHarness.ApplyEachFixAloneAsync(
			source,
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			rules
		);

		AnalyzerRunResult all = await RoslynTestHarness.RunFixAllAsync(source, rules);

		Assert.AreEqual(Method(FourGatheredStatements), alone[^1]);
		Assert.AreEqual(all.FixedText, alone[^1]);
	}

	// Gathering the gap between the two calls would set the lone declaration above them apart from a tight neighbor.
	// So that gap stays blank, and the statements below it are still gathered.
	[TestMethod]
	public async Task AGapThatCannotBeGatheredLeavesTheRestOfTheBlockToTheRule()
	{
		const string source = """
			class C
			{
				int x;

				void M()
				{
					x = 0;
					var a = 1;
					DoThing();

					DoThing();

					x = 1;

					x = 2;

					x = 3;

					x = 4;
				}

				void DoThing() { }
			}
			""";

		const string fixedSource = """
			class C
			{
				int x;

				void M()
				{
					x = 0;
					var a = 1;
					DoThing();

					DoThing();
					x = 1;
					x = 2;
					x = 3;
					x = 4;
				}

				void DoThing() { }
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.GatherRunOfLooseStatements)
		);

		Assert.HasCount(4, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			expected: fixedSource,
			source,

			options: new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = TestRuleSets.Only(
					DiagnosticDescriptors.GatherRunOfLooseStatements
				),
			}
		);
	}

	[TestMethod]
	public async Task EachGapOfARunIsReportedOnTheNextStatementWithTheOneBeforeItAttached()
	{
		string source = Method(FourLooseStatements);

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<GatherRunOfLooseStatementsAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.GatherRunOfLooseStatements)
			);

		Assert.HasCount(3, result.Diagnostics);

		string[] reported =
		[
			.. result.Diagnostics.Select(diagnostic =>
				source.Substring(diagnostic.Location.SourceSpan.Start, 6)
			),
		];

		string[] neighbors =
		[
			.. result.Diagnostics.Select(diagnostic =>
				source.Substring(diagnostic.AdditionalLocations[0].SourceSpan.Start, 5)
			),
		];

		Assert.AreSequenceEqual(["second", "third ", "fourth"], reported);
		Assert.AreSequenceEqual(["first", "secon", "third"], neighbors);
	}

	[TestMethod]
	[DataRow("3", 3)]
	[DataRow("2", 3)]
	[DataRow("1", 3)]
	[DataRow("4", 0)]
	[DataRow("1000", 0)]
	[DataRow("0", 0)]
	public Task TheLimitComesFromTheOption(string value, int expected) =>
		AssertFourLooseStatementsAtAsync(value, expected);

	// A value that is not a count from zero to a thousand leaves the default limit of three.
	[TestMethod]
	[DataRow("four")]
	[DataRow("-1")]
	[DataRow("1001")]
	[DataRow("")]
	public Task AnUnreadableLimitLeavesTheDefault(string value) =>
		AssertFourLooseStatementsAtAsync(value, expected: 3);

	[TestMethod]
	public async Task ALimitOfTwoReportsARunOfThree()
	{
		const string source = """
			class C
			{
				int first;
				int second;
				int third;

				void M()
				{
					first = 1;

					second = 2;

					third = 3;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				int first;
				int second;
				int third;

				void M()
				{
					first = 1;
					second = 2;
					third = 3;
				}
			}
			""";

		AnalyzerRunResult result = await RunAsync(
			source,
			new Dictionary<string, string> { [LimitKey] = "2" }
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ALimitOfFiveLeavesARunOfFiveAndGathersARunOfSix()
	{
		const string five = """
			first = 1;

			second = 2;

			third = 3;

			fourth = 4;

			fifth = 5;
			""";

		const string six = """
			first = 1;

			second = 2;

			third = 3;

			fourth = 4;

			fifth = 5;

			sixth = 6;
			""";

		string fixedSource = Method(
			"""
			first = 1;
			second = 2;
			third = 3;
			fourth = 4;
			fifth = 5;
			sixth = 6;
			"""
		);

		Dictionary<string, string> limit = new() { [LimitKey] = "5" };

		AnalyzerRunResult quiet = await RunAsync(Method(five), limit);
		AnalyzerRunResult gathered = await RunAsync(Method(six), limit);

		Assert.IsEmpty(quiet.Diagnostics);
		Assert.HasCount(5, gathered.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, gathered);
	}

	[TestMethod]
	[DynamicData(
		nameof(QuietBodies),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AGapThatEndsARunLeavesTheStatementsAsWritten(string name, string body)
	{
		AnalyzerRunResult result = await RunAsync(Method(body));
		Assert.IsEmpty(result.Diagnostics, name);
	}

	[TestMethod]
	[DynamicData(
		nameof(UnjudgedPlaces),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task OnlyStatementsAreJudged(string name, string source)
	{
		AnalyzerRunResult result = await RunAsync(source);
		Assert.IsEmpty(result.Diagnostics, name);
	}

	[TestMethod]
	public async Task TheStatementsAroundAGapAnotherRuleAsksForAreGatheredOnEachSide()
	{
		const string source = """
			class C
			{
				int first;
				int second;
				int third;
				int fourth;

				void M()
				{
					first = 1;

					second = 2;

					third = 3;

					fourth = 4;

					DoThing();

					DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				int first;
				int second;
				int third;
				int fourth;

				void M()
				{
					first = 1;
					second = 2;
					third = 3;
					fourth = 4;

					DoThing();

					DoThing();
				}
			}
			""";

		AnalyzerRunResult result = await RunAsync(source);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// Line3 and Line7 sit tight against a neighbor, so they belong to the stanzas beside the run and not to it.
	[TestMethod]
	public async Task ARunNeverTakesInTheStatementsOfTheTightStanzasBesideIt()
	{
		string source = Method(
			"""
			first = 1;
			second = 2;
			third = 3;

			fourth = 4;

			fifth = 5;

			sixth = 6;

			first = 7;
			second = 8;
			"""
		);

		string fixedSource = Method(
			"""
			first = 1;
			second = 2;
			third = 3;

			fourth = 4;
			fifth = 5;
			sixth = 6;

			first = 7;
			second = 8;
			"""
		);

		AnalyzerRunResult result = await RunAsync(
			source,
			new Dictionary<string, string> { [LimitKey] = "2" }
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheStanzasBesideARunOfThreeStayAsWrittenAtTheDefaultLimit()
	{
		string source = Method(
			"""
			first = 1;
			second = 2;
			third = 3;

			fourth = 4;

			fifth = 5;

			sixth = 6;

			first = 7;
			second = 8;
			"""
		);

		AnalyzerRunResult result = await RunAsync(source);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task OneLooseStatementBetweenTwoStanzasKeepsBothBlankLines()
	{
		string source = Method(
			"""
			first = 1;
			second = 2;

			third = 3;

			fourth = 4;
			fifth = 5;
			"""
		);

		AnalyzerRunResult result = await RunAsync(
			source,
			new Dictionary<string, string> { [LimitKey] = "1" }
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	// ZA0001 asks for the gap above the if,
	// so the fourth statement belongs to no stanza and joins the run.
	[TestMethod]
	public async Task AStatementTightAgainstAGapAnotherRuleAsksForJoinsTheRun()
	{
		string source = Method(
			"""
			first = 1;

			second = 2;

			third = 3;

			fourth = 4;
			if (first > 0)
			{
				DoThing();
			}
			"""
		);

		string fixedSource = Method(
			"""
			first = 1;
			second = 2;
			third = 3;
			fourth = 4;
			if (first > 0)
			{
				DoThing();
			}
			"""
		);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<GatherRunOfLooseStatementsAnalyzer>(
				source,

				TestRuleSets.Only(
					DiagnosticDescriptors.GatherRunOfLooseStatements,
					DiagnosticDescriptors.SeparateMultiLineConstructs
				)
			);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// Where ZA0001 is off, the fourth statement and the if form a stanza,
	// so the three above it hold no run at the default limit.
	[TestMethod]
	public async Task AStatementTightAgainstAMultiLineStatementStaysOutWhereNoRuleAsksForTheGap()
	{
		string source = Method(
			"""
			first = 1;

			second = 2;

			third = 3;

			fourth = 4;
			if (first > 0)
			{
				DoThing();
			}
			"""
		);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<GatherRunOfLooseStatementsAnalyzer>(
				source,

				new Dictionary<string, ReportDiagnostic>
				{
					[DiagnosticDescriptors.GatherRunOfLooseStatements.Id] = ReportDiagnostic.Warn,

					[DiagnosticDescriptors.SeparateMultiLineConstructs.Id] =
						ReportDiagnostic.Suppress,
				}
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	// A Fix All gathers the pair above and the pair of calls at once, and the calls are tight by the next pass.
	// So the gap between them is gathered in that step too, as one fix at a time gathers it.
	[TestMethod]
	public async Task ARunWaitsForNoSecondPassWhenAnotherRunIsGatheredFirst()
	{
		string source = Method(
			"""
			int number1 = 1;

			int count2 = 2;

			string text3 = "3";

			first = 4;

			DoThing();
			"""
		);

		string fixedSource = Method(
			"""
			int number1 = 1;
			int count2 = 2;

			string text3 = "3";
			first = 4;
			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,

			diagnosticOptions: TestRuleSets.Only(
				DiagnosticDescriptors.GatherRunOfLooseStatements,
				DiagnosticDescriptors.SeparateDeclarationRuns,
				DiagnosticDescriptors.SeparateInvocationRuns,
				DiagnosticDescriptors.SeparateAssertionRuns,
				DiagnosticDescriptors.SeparateSameTypeDeclarationRuns
			),

			analyzerConfigOptions: new Dictionary<string, string> { [LimitKey] = "1" }
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			expected: fixedSource,
			source,

			options: new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = TestRuleSets.Only(
					DiagnosticDescriptors.GatherRunOfLooseStatements,
					DiagnosticDescriptors.SeparateDeclarationRuns,
					DiagnosticDescriptors.SeparateInvocationRuns,
					DiagnosticDescriptors.SeparateAssertionRuns,
					DiagnosticDescriptors.SeparateSameTypeDeclarationRuns
				),

				AnalyzerConfigOptions = new Dictionary<string, string> { [LimitKey] = "1" },
			}
		);
	}

	// A conditional statement gives each view runs of its own, so gathering for one view would fight ZA0002 in the other.
	[TestMethod]
	[DataRow("")]
	[DataRow("#define B\n")]
	public async Task AContainerWithAConditionalStatementIsLeftAsWrittenInEveryView(string view)
	{
		string source =
			view
			+ Method(
				"""
				first = 1;

				DoThing();

				#if !B
				first = 2;
				#endif

				first = 3;

				DoThing();
				"""
			);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,

			diagnosticOptions: TestRuleSets.Only(
				DiagnosticDescriptors.GatherRunOfLooseStatements,
				DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether
			),

			analyzerConfigOptions: new Dictionary<string, string> { [LimitKey] = "1" }
		);

		RoslynTestHarness.AssertFixedText(source, result);
	}

	[TestMethod]
	public async Task AContainerWhollyInsideAConditionalIsGatheredInTheViewThatHoldsIt()
	{
		string source = Method(
			"""
			#if B
			first = 1;

			second = 2;

			third = 3;
			#endif
			"""
		);

		string fixedSource = Method(
			"""
			#if B
			first = 1;
			second = 2;
			third = 3;
			#endif
			"""
		);

		const string section = """
			class C
			{
				int first, second, third, fourth;

				void M()
				{
					switch (first)
					{
						case 1:
			#if B
							second = 1;

							third = 2;

							fourth = 3;

							break;
			#endif
						case 2:
							break;
					}
				}
			}
			""";

		const string sectionFixed = """
			class C
			{
				int first, second, third, fourth;

				void M()
				{
					switch (first)
					{
						case 1:
			#if B
							second = 1;
							third = 2;
							fourth = 3;
							break;
			#endif
						case 2:
							break;
					}
				}
			}
			""";

		await AssertGatheredOnlyWhereTheConditionalHoldsAsync(source, fixedSource);
		await AssertGatheredOnlyWhereTheConditionalHoldsAsync(section, sectionFixed);
	}

	// A conditional above the next switch section, or among the labels, moves none of the statements of the section.
	[TestMethod]
	public async Task AConditionalAboveTheNextSwitchSectionOrAmongTheLabelsLeavesTheSectionGatheredInEveryView()
	{
		const string nextSection = """
			class C
			{
				int first, second, third, fourth;

				void M()
				{
					switch (first)
					{
						case 1:
							second = 1;

							third = 2;

							break;
			#if B
						case 2:
							fourth = 3;
							break;
			#endif
						case 3:
							break;
					}
				}
			}
			""";

		const string nextSectionFixed = """
			class C
			{
				int first, second, third, fourth;

				void M()
				{
					switch (first)
					{
						case 1:
							second = 1;
							third = 2;
							break;
			#if B
						case 2:
							fourth = 3;
							break;
			#endif
						case 3:
							break;
					}
				}
			}
			""";

		const string amongLabels = """
			class C
			{
				int first, second, third, fourth;

				void M()
				{
					switch (first)
					{
						case 1:
			#if B
						case 2:
			#endif
							second = 1;

							third = 2;

							break;
					}
				}
			}
			""";

		const string amongLabelsFixed = """
			class C
			{
				int first, second, third, fourth;

				void M()
				{
					switch (first)
					{
						case 1:
			#if B
						case 2:
			#endif
							second = 1;
							third = 2;
							break;
					}
				}
			}
			""";

		// The conditional holds only a label, so a view without it joins the statements below to this section.
		const string hidesTheBoundary = """
			class C
			{
				int first, second, third, fourth;

				void M()
				{
					switch (first)
					{
						case 1:
							second = 1;

							third = 2;

							break;
			#if B
						case 2:
			#endif
							fourth = 3;
							break;
					}
				}
			}
			""";

		foreach (string view in new[] { "", "#define B\n" })
		{
			await AssertGatheredAsync(view + nextSection, view + nextSectionFixed);
			await AssertGatheredAsync(view + amongLabels, view + amongLabelsFixed);

			await AssertLeftAsWrittenAsync(view + hidesTheBoundary);
		}
	}

	// The gathering and the grouping rules settle on one text from the top and from the bottom, in the view that holds the conditional.
	[TestMethod]
	public async Task AContainerWhollyInsideAConditionalSettlesInEveryFixOrder()
	{
		string source =
			"#define B\n"
			+ Method(
				"""
				#if B
				int number1 = 1;

				int count1 = 2;

				string text1 = "3";

				first = 4;

				DoThing();
				#endif
				"""
			);

		string fixedSource =
			"#define B\n"
			+ Method(
				"""
				#if B
				int number1 = 1;
				int count1 = 2;

				string text1 = "3";
				first = 4;
				DoThing();
				#endif
				"""
			);

		Dictionary<string, ReportDiagnostic> rules = new(
			TestRuleSets.Only(
				DiagnosticDescriptors.GatherRunOfLooseStatements,
				DiagnosticDescriptors.SeparateDeclarationRuns,
				DiagnosticDescriptors.SeparateInvocationRuns,
				DiagnosticDescriptors.SeparateAssertionRuns,
				DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
				DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether
			)
		);

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			expected: fixedSource,
			source,

			options: new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = rules,
				AnalyzerConfigOptions = new Dictionary<string, string> { [LimitKey] = "1" },
			}
		);

		// The view without the conditional holds no statement, so it asks for nothing and the fixed text stays settled in it.
		AnalyzerRunResult hidden = await RoslynTestHarness.RunIterativelyAsync(
			fixedSource["#define B\n".Length..],
			rules,
			new Dictionary<string, string> { [LimitKey] = "1" }
		);

		RoslynTestHarness.AssertFixedText(fixedSource["#define B\n".Length..], hidden);
	}

	[TestMethod]
	public async Task ARegionAmongTheStatementsLeavesTheContainerJudged()
	{
		string source = Method(
			"""
			first = 1;

			first = 2;

			#region Rest

			first = 3;

			#endregion
			"""
		);

		string fixedSource = Method(
			"""
			first = 1;
			first = 2;

			#region Rest

			first = 3;

			#endregion
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,
			diagnosticOptions: TestRuleSets.Only(DiagnosticDescriptors.GatherRunOfLooseStatements),
			analyzerConfigOptions: new Dictionary<string, string> { [LimitKey] = "1" }
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// Each copy holds two held-back gaps, and the first pass retries them all, however many copies the block holds.
	// It gathers the gap above `first`, and the gap above `text` stays blank for ZA0003.
	// One fix at a time and Fix All per ID settle on the same text, whatever the count of copies.
	[TestMethod]
	[DataRow(2)]
	[DataRow(6)]
	[DataRow(25)]
	public async Task ManyRunsThatWaitForNoSecondPassSettleTheSameWayInEveryFixOrder(int copies)
	{
		string source = Method(
			string.Join(
				"\n\n",

				Enumerable
					.Range(1, copies)
					.Select(copy =>
						$"""
							int number{copy} = 1;

							int count{copy} = 2;

							string text{copy} = "3";

							first = 4;

							DoThing();
							"""
					)
			)
		);

		string fixedSource = Method(
			string.Join(
				"\n\n",

				Enumerable
					.Range(1, copies)
					.Select(copy =>
						$"""
							int number{copy} = 1;
							int count{copy} = 2;

							string text{copy} = "3";
							first = 4;
							DoThing();
							"""
					)
			)
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,

			diagnosticOptions: TestRuleSets.Only(
				DiagnosticDescriptors.GatherRunOfLooseStatements,
				DiagnosticDescriptors.SeparateDeclarationRuns,
				DiagnosticDescriptors.SeparateInvocationRuns,
				DiagnosticDescriptors.SeparateAssertionRuns,
				DiagnosticDescriptors.SeparateSameTypeDeclarationRuns
			),

			analyzerConfigOptions: new Dictionary<string, string> { [LimitKey] = "1" }
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			expected: fixedSource,
			source,

			options: new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = TestRuleSets.Only(
					DiagnosticDescriptors.GatherRunOfLooseStatements,
					DiagnosticDescriptors.SeparateDeclarationRuns,
					DiagnosticDescriptors.SeparateInvocationRuns,
					DiagnosticDescriptors.SeparateAssertionRuns,
					DiagnosticDescriptors.SeparateSameTypeDeclarationRuns
				),

				AnalyzerConfigOptions = new Dictionary<string, string> { [LimitKey] = "1" },
			}
		);
	}

	// A retried gap is judged from the runs beside it alone, so the run that follows them has to be read too.
	[TestMethod]
	public async Task AHeldBackGapStaysBlankForTheRunBelowIt()
	{
		string source = Method(
			"""
			first = 1;

			DoThing();

			const int limit1 = 1;

			var count1 = first;

			var count2 = first;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,

			diagnosticOptions: TestRuleSets.Only(
				DiagnosticDescriptors.GatherRunOfLooseStatements,
				DiagnosticDescriptors.SeparateDeclarationRuns,
				DiagnosticDescriptors.SeparateInvocationRuns,
				DiagnosticDescriptors.SeparateAssertionRuns,
				DiagnosticDescriptors.SeparateSameTypeDeclarationRuns
			),

			analyzerConfigOptions: new Dictionary<string, string> { [LimitKey] = "2" }
		);

		RoslynTestHarness.AssertFixedText(source, result);
	}

	// The run above a retried gap reaches up to the nearest gap that no run crosses, so a retry reads all of that run.
	[TestMethod]
	public async Task AHeldBackGapStaysBlankForTheRunAboveIt()
	{
		string source = Method(
			"""
			// note
			int count1 = 1;

			int count2 = 2;

			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,

			diagnosticOptions: TestRuleSets.Only(
				DiagnosticDescriptors.GatherRunOfLooseStatements,
				DiagnosticDescriptors.SeparateDeclarationRuns,
				DiagnosticDescriptors.SeparateInvocationRuns,
				DiagnosticDescriptors.SeparateAssertionRuns,
				DiagnosticDescriptors.SeparateSameTypeDeclarationRuns
			),

			analyzerConfigOptions: new Dictionary<string, string> { [LimitKey] = "2" }
		);

		RoslynTestHarness.AssertFixedText(source, result);
	}

	// A retried gap is judged from the runs beside it alone, which still has to see the lone declaration above the first of them.
	[TestMethod]
	public async Task AGapBesideALoneDeclarationSettlesTheSameWayInEveryFixOrder()
	{
		string source = Method(
			"""
			string text1 = "a";
			const int limit1 = 1;

			string text2 = "b";

			string text3 = "c";

			int count1 = 2;

			int count2 = 3;

			int count3 = 4;

			DoThing();

			int count4 = 5;

			int count5 = 6;
			"""
		);

		string fixedSource = Method(
			"""
			string text1 = "a";
			const int limit1 = 1;

			string text2 = "b";

			string text3 = "c";
			int count1 = 2;

			int count2 = 3;
			int count3 = 4;

			DoThing();

			int count4 = 5;
			int count5 = 6;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,

			diagnosticOptions: TestRuleSets.Only(
				DiagnosticDescriptors.GatherRunOfLooseStatements,
				DiagnosticDescriptors.SeparateDeclarationRuns,
				DiagnosticDescriptors.SeparateInvocationRuns,
				DiagnosticDescriptors.SeparateAssertionRuns,
				DiagnosticDescriptors.SeparateSameTypeDeclarationRuns
			),

			analyzerConfigOptions: new Dictionary<string, string> { [LimitKey] = "1" }
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			expected: fixedSource,
			source,

			options: new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = TestRuleSets.Only(
					DiagnosticDescriptors.GatherRunOfLooseStatements,
					DiagnosticDescriptors.SeparateDeclarationRuns,
					DiagnosticDescriptors.SeparateInvocationRuns,
					DiagnosticDescriptors.SeparateAssertionRuns,
					DiagnosticDescriptors.SeparateSameTypeDeclarationRuns
				),

				AnalyzerConfigOptions = new Dictionary<string, string> { [LimitKey] = "1" },
			}
		);
	}

	[TestMethod]
	public async Task ARuleThatIsOffHandsItsGapBackToTheRun()
	{
		string source = Method(
			"""
			first = 1;

			second = 2;

			DoThing();

			DoThing();

			third = 3;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			new Dictionary<string, ReportDiagnostic>
			{
				[DiagnosticDescriptors.GatherRunOfLooseStatements.Id] = ReportDiagnostic.Warn,
				[DiagnosticDescriptors.SeparateInvocationRuns.Id] = ReportDiagnostic.Suppress,
			}
		);

		string fixedSource = Method(
			"""
			first = 1;
			second = 2;
			DoThing();
			DoThing();
			third = 3;
			"""
		);

		Assert.HasCount(4, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task APragmaOverTheOwnerHandsItsGapBackToTheRun()
	{
		string source = $$"""
			class C
			{
				int first;
				int second;
				int third;

				void M()
				{
			#pragma warning disable {{DiagnosticDescriptors.SeparateInvocationRuns.Id}}
					first = 1;

					second = 2;

					DoThing();

					DoThing();

					third = 3;
			#pragma warning restore {{DiagnosticDescriptors.SeparateInvocationRuns.Id}}
				}
			}
			""";

		string fixedSource = $$"""
			class C
			{
				int first;
				int second;
				int third;

				void M()
				{
			#pragma warning disable {{DiagnosticDescriptors.SeparateInvocationRuns.Id}}
					first = 1;
					second = 2;
					DoThing();
					DoThing();
					third = 3;
			#pragma warning restore {{DiagnosticDescriptors.SeparateInvocationRuns.Id}}
				}
			}
			""";

		AnalyzerRunResult result = await RunAsync(source);

		Assert.HasCount(4, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The region marker rule writes a blank line beside its marker, which ends the run the statements would form.
	// So the run is gathered up to the marker whether or not that blank line stands yet.
	[TestMethod]
	public async Task ARegionMarkerRuleThatIsOnEndsTheRunBesideItsMarkerBeforeItsBlankLineStands()
	{
		const string fixedSource = """
			class C
			{
				void M()
				{
					DoThing();
					DoThing();
					DoThing();
					DoThing();

			#region Rest

					DoThing();

			#endregion
				}

				void DoThing() { }
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.Only(
			DiagnosticDescriptors.GatherRunOfLooseStatements
		);

		AnalyzerRunResult reported = await RoslynTestHarness.ReportAllAsync(RegionSource, rules);

		Assert.HasCount(
			3,

			[
				.. reported.Diagnostics.Where(diagnostic =>
					diagnostic.Id == DiagnosticDescriptors.GatherRunOfLooseStatements.Id
				),
			]
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(RegionSource, rules);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARegionMarkerRuleThatIsOffLeavesTheStatementTightAgainstItsMarkerOutOfTheRun()
	{
		const string fixedSource = """
			class C
			{
				void M()
				{
					DoThing();
					DoThing();
					DoThing();

					DoThing();
			#region Rest
					DoThing();
			#endregion
				}

				void DoThing() { }
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			RegionSource,

			new Dictionary<string, ReportDiagnostic>
			{
				[DiagnosticDescriptors.GatherRunOfLooseStatements.Id] = ReportDiagnostic.Warn,

				[DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors.Id] =
					ReportDiagnostic.Suppress,
			},

			new Dictionary<string, string> { [LimitKey] = "2" }
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheTwoUnitRuleOwnsAContainerOfTwoStatementsAtALimitOfOne()
	{
		string source = Method(
			"""
			first = 1;

			second = 2;
			"""
		);

		string fixedSource = Method(
			"""
			first = 1;
			second = 2;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
				DiagnosticDescriptors.GatherRunOfLooseStatements
			),

			new Dictionary<string, string> { [LimitKey] = "1" }
		);

		Assert.HasCount(1, result.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether.Id,
			result.Diagnostics[0].Id
		);

		RoslynTestHarness.AssertEachGapIsClaimedOnce(result);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARunOfTwoStatementsIsGatheredWhereTheTwoUnitRuleIsOff()
	{
		string source = Method(
			"""
			first = 1;

			second = 2;
			"""
		);

		string fixedSource = Method(
			"""
			first = 1;
			second = 2;
			"""
		);

		AnalyzerRunResult result = await RunAsync(
			source,
			new Dictionary<string, string> { [LimitKey] = "1" }
		);

		Assert.HasCount(1, result.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.GatherRunOfLooseStatements.Id,
			result.Diagnostics[0].Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARunOfTwoStatementsIsGatheredWhereAPragmaSilencesTheTwoUnitRule()
	{
		string source = $$"""
			class C
			{
				int first;
				int second;

				void M()
				{
			#pragma warning disable {{DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether.Id}}
					first = 1;

					second = 2;
			#pragma warning restore {{DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether.Id}}
				}
			}
			""";

		string fixedSource = $$"""
			class C
			{
				int first;
				int second;

				void M()
				{
			#pragma warning disable {{DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether.Id}}
					first = 1;
					second = 2;
			#pragma warning restore {{DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether.Id}}
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
				DiagnosticDescriptors.GatherRunOfLooseStatements
			),

			new Dictionary<string, string> { [LimitKey] = "1" }
		);

		Assert.HasCount(1, result.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.GatherRunOfLooseStatements.Id,
			result.Diagnostics[0].Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARunOfThreeStatementsIsGatheredAtALimitOfOneBesideTheTwoUnitRule()
	{
		string source = Method(
			"""
			first = 1;

			second = 2;

			third = 3;
			"""
		);

		string fixedSource = Method(
			"""
			first = 1;
			second = 2;
			third = 3;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
				DiagnosticDescriptors.GatherRunOfLooseStatements
			),

			new Dictionary<string, string> { [LimitKey] = "1" }
		);

		Assert.HasCount(2, result.Diagnostics);

		Assert.IsTrue(
			result.Diagnostics.All(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.GatherRunOfLooseStatements.Id
			)
		);

		RoslynTestHarness.AssertEachGapIsClaimedOnce(result);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ALocalFunctionLeavesTheTwoStatementsToTheRunRuleAtALimitOfOne()
	{
		string source = Method(
			"""
			first = 1;

			void Local() { }
			"""
		);

		string fixedSource = Method(
			"""
			first = 1;
			void Local() { }
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
				DiagnosticDescriptors.GatherRunOfLooseStatements
			),

			new Dictionary<string, string> { [LimitKey] = "1" }
		);

		Assert.HasCount(1, result.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.GatherRunOfLooseStatements.Id,
			result.Diagnostics[0].Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ALoneDeclarationKeepsItsBlankLineWhereGatheringWouldSetItApart()
	{
		// Gathering the blank line below `a` joins it to the declarations under it, and ZA0002 would then ask for a blank line above it.
		string source = Method(
			"""
			first = 0;
			int a = 1;

			int b = 1;
			int c = 2;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.GatherRunOfLooseStatements,
				DiagnosticDescriptors.SeparateDeclarationRuns,
				DiagnosticDescriptors.SeparateInvocationRuns,
				DiagnosticDescriptors.SeparateAssertionRuns
			),

			new Dictionary<string, string> { [LimitKey] = "1" }
		);

		Assert.IsEmpty(result.Diagnostics);
		RoslynTestHarness.AssertFixedText(source, result);
	}

	[TestMethod]
	public async Task ARunThatNoGatheredGapTouchesKeepsTheBlankLinesBesideIt()
	{
		// The blank line above the run of calls stays, so gathering the one below it would leave ZA0002 asking for it back.
		string source = """
			class C
			{
				int first;

				void A() { }

				void B() { }

				void M()
				{
					first = 0;
					int a = 1;

					A();
					A();

					B();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.GatherRunOfLooseStatements,
				DiagnosticDescriptors.SeparateDeclarationRuns,
				DiagnosticDescriptors.SeparateInvocationRuns,
				DiagnosticDescriptors.SeparateAssertionRuns
			),

			new Dictionary<string, string> { [LimitKey] = "1" }
		);

		Assert.IsEmpty(result.Diagnostics);
		RoslynTestHarness.AssertFixedText(source, result);
	}

	// Gathering the two statements makes a stanza of two lines, which ZA0009 then separates from the return.
	[TestMethod]
	public async Task TheGapAboveAJumpStaysBlankWhereGatheringTheRunAboveItMakesTheJumpRuleAskForIt()
	{
		string source = Method(
			"""
			first = 1;

			second = 2;

			return;
			"""
		);

		string fixedSource = Method(
			"""
			first = 1;
			second = 2;

			return;
			"""
		);

		AnalyzerRunResult result = await RunWithJumpRuleAsync(source, jumpRuleOn: true);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheGapAboveAJumpIsGatheredWhereTheJumpRuleIsOff()
	{
		string source = Method(
			"""
			first = 1;

			second = 2;

			return;
			"""
		);

		string fixedSource = Method(
			"""
			first = 1;
			second = 2;
			return;
			"""
		);

		AnalyzerRunResult result = await RunWithJumpRuleAsync(source, jumpRuleOn: false);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The jump rule writes the blank line above the return, which lets the statements below it join one run.
	[TestMethod]
	public async Task ARunBelowAJumpIsGatheredBeforeTheJumpRuleSeparatesTheJump()
	{
		string source = Method(
			"""
			first = 1;
			second = 2;
			return;

			third = 3;

			fourth = 4;
			"""
		);

		AnalyzerRunResult jumpRuleOn =
			await RoslynTestHarness.ReportAsync<GatherRunOfLooseStatementsAnalyzer>(
				source,
				JumpRules(jumpRuleOn: true),
				new Dictionary<string, string> { [LimitKey] = "1" }
			);

		AnalyzerRunResult jumpRuleOff =
			await RoslynTestHarness.ReportAsync<GatherRunOfLooseStatementsAnalyzer>(
				source,
				JumpRules(jumpRuleOn: false),
				new Dictionary<string, string> { [LimitKey] = "1" }
			);

		Assert.HasCount(2, jumpRuleOn.Diagnostics, "The return, third and fourth form a run.");

		Assert.HasCount(
			1,
			jumpRuleOff.Diagnostics,
			"The return is tight against second, so it stays out."
		);
	}

	[TestMethod]
	public async Task AnExemptBreakJoinsTheRunAboveIt()
	{
		string source = Method(
			"""
			switch (first)
			{
				case 1:
					second = 1;

					third = 2;

					fourth = 3;

					fifth = 4;

					break;
			}
			"""
		);

		string fixedSource = Method(
			"""
			switch (first)
			{
				case 1:
					second = 1;
					third = 2;
					fourth = 3;
					fifth = 4;
					break;
			}
			"""
		);

		AnalyzerRunResult result = await RunWithJumpRuleAsync(source, jumpRuleOn: true);

		Assert.HasCount(4, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task FourLooseTopLevelStatementsAreGatheredAtTheDefaultLimit()
	{
		const string source = """
			DoThing();

			DoThing();

			DoThing();

			DoThing();
			""";

		const string fixedSource = """
			DoThing();
			DoThing();
			DoThing();
			DoThing();
			""";

		AnalyzerRunResult result = await RunAsync(source);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);

		string[] alone = await RoslynTestHarness.ApplyEachFixAloneAsync(
			source,
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			TestRuleSets.Only(DiagnosticDescriptors.GatherRunOfLooseStatements)
		);

		Assert.AreSequenceEqual([fixedSource, fixedSource, fixedSource], alone);
	}

	// The import section rule owns the gap below the imports, so the first statement is no part of a run with them.
	[TestMethod]
	public async Task TheGapBelowTheImportsIsNoGapOfATopLevelRun()
	{
		const string source = """
			using System.Linq;

			DoThing();

			DoThing();

			DoThing();

			DoThing();
			""";

		const string fixedSource = """
			using System.Linq;

			DoThing();
			DoThing();
			DoThing();
			DoThing();
			""";

		Dictionary<string, ReportDiagnostic> rules = new(
			TestRuleSets.Only(DiagnosticDescriptors.GatherRunOfLooseStatements)
		)
		{
			[DiagnosticDescriptors.SeparateImportSection.Id] = ReportDiagnostic.Suppress,
		};

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<GatherRunOfLooseStatementsAnalyzer>(source, rules);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ImportsAreNeverUnitsOfARun()
	{
		const string source = """
			using System;

			using System.Linq;

			using System.Text;

			using System.Threading;

			DoThing();
			""";

		AnalyzerRunResult result = await RunAsync(source);

		Assert.IsEmpty(result.Diagnostics);
	}

	// The import section rule writes the blank line that gathering the statements below it would otherwise have to wait for.
	[TestMethod]
	public async Task TheImportSectionRuleAndTheStatementRuleSettleInEitherFixOrder()
	{
		const string source = """
			using System.Linq;
			DoThing();

			DoThing();

			DoThing();
			""";

		const string fixedSource = """
			using System.Linq;

			DoThing();
			DoThing();
			DoThing();
			""";

		Dictionary<string, ReportDiagnostic> rules = new(
			TestRuleSets.Only(DiagnosticDescriptors.GatherRunOfLooseStatements)
		);

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			expected: fixedSource,
			source,

			options: new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = rules,
				AnalyzerConfigOptions = new Dictionary<string, string> { [LimitKey] = "1" },
			}
		);
	}

	// The jump rule judges the top-level statements too, so gathering the calls keeps the blank line above the return.
	[TestMethod]
	public async Task ATopLevelJumpKeepsItsBlankLineWhereGatheringTheRunAboveWouldAskForIt()
	{
		const string source = """
			DoThing();

			DoThing();

			return;
			""";

		const string fixedSource = """
			DoThing();
			DoThing();

			return;
			""";

		// The invocation rule is off, so only the jump rule asks for a blank line above the return.
		Dictionary<string, ReportDiagnostic> rules = new(JumpRules(jumpRuleOn: true))
		{
			[DiagnosticDescriptors.SeparateInvocationRuns.Id] = ReportDiagnostic.Suppress,
		};

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<GatherRunOfLooseStatementsAnalyzer>(
				source,
				rules,
				new Dictionary<string, string> { [LimitKey] = "1" }
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The two-unit rule counts the imports and the types of the file too, so a file of two statements is its container.
	[TestMethod]
	public async Task TwoTopLevelStatementsBelongToTheTwoUnitRuleWhereItIsOn()
	{
		const string source = """
			DoThing();

			DoThing();
			""";

		Dictionary<string, ReportDiagnostic> rules = new(
			TestRuleSets.Only(
				DiagnosticDescriptors.GatherRunOfLooseStatements,
				DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether
			)
		);

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<GatherRunOfLooseStatementsAnalyzer>(
				source,
				rules,
				new Dictionary<string, string> { [LimitKey] = "1" }
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	// The types below the statements are the member rule's, so a run of statements ends where they start.
	[TestMethod]
	public async Task TheTypesBelowTheStatementsAreNoPartOfTheirRun()
	{
		const string source = """
			DoThing();

			DoThing();

			DoThing();

			DoThing();

			delegate void First();

			delegate void Second();
			""";

		const string fixedSource = """
			DoThing();
			DoThing();
			DoThing();
			DoThing();

			delegate void First();

			delegate void Second();
			""";

		AnalyzerRunResult result = await RunAsync(source);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AVeryLongRunOfJumpsIsReadWithoutRecursion()
	{
		// The stanza above a jump is read from the stanza of the jump before it, so a read that recurses per jump overflows the stack.
		// The jumps share one line, so none asks for a blank line as written, and each stanza stays one line until the gaps below are gathered.
		string jumps = string.Concat(Enumerable.Repeat("return; ", 100_000));
		string source = Method(jumps + "\n\nreturn;\n\nreturn;");

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<GatherRunOfLooseStatementsAnalyzer>(
				source,
				JumpRules(jumpRuleOn: true),
				new Dictionary<string, string> { [LimitKey] = "1" }
			);

		Assert.HasCount(1, result.Diagnostics);
	}

	// The guard rule asks for the blank line below the guards that open a block, so gathering the run below them keeps it.
	[TestMethod]
	public async Task TheGapBelowTheGuardsStaysBlankWhereTheGuardRuleIsOn()
	{
		string source = Method("if (first == 0) return;\n\nsecond = 1;\n\nthird = 2;");
		string fixedSource = Method("if (first == 0) return;\n\nsecond = 1;\nthird = 2;");

		AnalyzerRunResult result = await RunWithGuardRuleAsync(source, guardRuleOn: true);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheGapBelowTheGuardsIsGatheredWhereTheGuardRuleIsOff()
	{
		string source = Method("if (first == 0) return;\n\nsecond = 1;\n\nthird = 2;");
		string fixedSource = Method("if (first == 0) return;\nsecond = 1;\nthird = 2;");

		AnalyzerRunResult result = await RunWithGuardRuleAsync(source, guardRuleOn: false);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AGapBetweenTwoGuardsIsGatheredAndTheOneBelowThemStaysBlank()
	{
		string source = Method(
			"if (first == 0) return;\n\nif (first == 1) return;\n\nsecond = 1;\n\nthird = 2;"
		);

		string fixedSource = Method(
			"if (first == 0) return;\nif (first == 1) return;\n\nsecond = 1;\nthird = 2;"
		);

		AnalyzerRunResult result = await RunWithGuardRuleAsync(source, guardRuleOn: true);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// A container with a conditional directive around the guard can hold other guards in another view, so it is left as written.
	[TestMethod]
	[DataRow("")]
	[DataRow("#define B\n")]
	public async Task AContainerWithAConditionalDirectiveAroundTheGuardIsLeftAsWrittenInEveryView(
		string view
	)
	{
		string source =
			view
			+ Method(
				"""
				#if B
				if (first == 0) return;
				#endif

				second = 1;

				third = 2;
				"""
			);

		AnalyzerRunResult result = await RunWithGuardRuleAsync(source, guardRuleOn: true);

		Assert.IsEmpty(result.Diagnostics, $"View [{view.Trim()}].");
		RoslynTestHarness.AssertFixedText(source, result);
	}

	private static Task<AnalyzerRunResult> RunWithGuardRuleAsync(string source, bool guardRuleOn)
	{
		Dictionary<string, ReportDiagnostic> rules = new(
			TestRuleSets.Only(DiagnosticDescriptors.GatherRunOfLooseStatements),
			StringComparer.Ordinal
		);

		if (guardRuleOn)
			rules[DiagnosticDescriptors.SeparateGuardClauses.Id] = ReportDiagnostic.Warn;

		return RoslynTestHarness.RunAsync<GatherRunOfLooseStatementsAnalyzer>(
			source,
			rules,
			analyzerConfigOptions: new Dictionary<string, string> { [LimitKey] = "1" }
		);
	}

	private static Dictionary<string, ReportDiagnostic> JumpRules(bool jumpRuleOn)
	{
		Dictionary<string, ReportDiagnostic> rules = new(
			TestRuleSets.Only(DiagnosticDescriptors.GatherRunOfLooseStatements),
			StringComparer.Ordinal
		);

		if (jumpRuleOn)
			rules[DiagnosticDescriptors.SeparateJumpStatement.Id] = ReportDiagnostic.Warn;

		return rules;
	}

	private static Task<AnalyzerRunResult> RunWithJumpRuleAsync(string source, bool jumpRuleOn)
	{
		return RoslynTestHarness.RunAsync<GatherRunOfLooseStatementsAnalyzer>(
			source,
			JumpRules(jumpRuleOn),
			analyzerConfigOptions: new Dictionary<string, string> { [LimitKey] = "1" }
		);
	}

	// A deconstruction, a discard and a chain are named as the declaration run rule reads them, and the jump rule reads the same borders.
	[TestMethod]
	public async Task TheBordersOfADeconstructionADiscardAndAChainSettleInEveryFixOrder()
	{
		(string Name, string Body, string Expected, bool IsAsync)[] cases =
		[
			(
				"a deconstruction among var declarations",

				"""
				first = 1;

				var (a, b) = Read();

				var c = 1;

				return;
				""",

				"""
				first = 1;

				var (a, b) = Read();
				var c = 1;

				return;
				""",

				false
			),

			(
				"a discarded call beside a call",

				"""
				first = 1;

				Read();

				_ = Read();

				return;
				""",

				"""
				first = 1;

				Read();
				_ = Read();

				return;
				""",

				false
			),

			(
				"awaited calls of two methods that chain on ConfigureAwait",

				"""
				first = 1;

				await Load().ConfigureAwait(false);

				await Save().ConfigureAwait(false);

				return;
				""",

				"""
				first = 1;
				await Load().ConfigureAwait(false);
				await Save().ConfigureAwait(false);

				return;
				""",

				true
			),

			(
				"chains of two roots that end on one method",

				"""
				first = 1;

				First().ToArray();

				Second().ToArray();

				return;
				""",

				"""
				first = 1;
				First().ToArray();
				Second().ToArray();

				return;
				""",

				false
			),

			(
				"chains of one root that end on two methods",

				"""
				first = 1;

				First().ToArray();

				First().ToList();

				return;
				""",

				"""
				first = 1;

				First().ToArray();
				First().ToList();

				return;
				""",

				false
			),
		];

		Dictionary<string, ReportDiagnostic> rules = new(
			TestRuleSets.Only(
				DiagnosticDescriptors.GatherRunOfLooseStatements,
				DiagnosticDescriptors.SeparateDeclarationRuns,
				DiagnosticDescriptors.SeparateInvocationRuns,
				DiagnosticDescriptors.SeparateAssertionRuns,
				DiagnosticDescriptors.SeparateJumpStatement
			)
		);

		foreach ((string name, string body, string expected, bool isAsync) in cases)
		{
			string source = Method(body, isAsync);
			string fixedSource = Method(expected, isAsync);

			await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
				expected: fixedSource,
				source,

				options: new RunOptions(RoslynTestHarness.AllAnalyzers)
				{
					DiagnosticOptions = rules,
					AnalyzerConfigOptions = new Dictionary<string, string> { [LimitKey] = "1" },
				},

				name
			);
		}
	}

	// Gathering the assignments makes the assignment rule keep the gap below them, so the first return has one line above it.
	// The gap above the first return is settled before the one below it, so the first joins the call and the second keeps its blank line.
	[TestMethod]
	public async Task TheGapAboveAJumpIsSettledBeforeTheJumpBelowItInEveryFixOrder()
	{
		string source = Method(
			"""
			first = 1;

			second = 2;

			DoThing();

			return;

			return;
			"""
		);

		string fixedSource = Method(
			"""
			first = 1;
			second = 2;

			DoThing();
			return;

			return;
			"""
		);

		RunOptions options = new(RoslynTestHarness.AllAnalyzers)
		{
			DiagnosticOptions = TestRuleSets.Only(
				DiagnosticDescriptors.GatherRunOfLooseStatements,
				DiagnosticDescriptors.SeparateAssignmentRuns,
				DiagnosticDescriptors.SeparateJumpStatement
			),

			AnalyzerConfigOptions = new Dictionary<string, string> { [LimitKey] = "1" },
		};

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source, options);

		RoslynTestHarness.AssertFixedText(fixedSource, result);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			expected: fixedSource,
			source,
			options
		);
	}

	// The assignment rule asks for a blank line around its run, so gathering keeps those and gathers the rest in every fix order.
	[TestMethod]
	public async Task TheBordersOfAnAssignmentRunSettleInEveryFixOrder()
	{
		(string Name, string Body, string Expected)[] cases =
		[
			(
				"an assignment pair between calls",

				"""
				DoThing();

				first = 1;

				second = 2;

				DoThing();
				""",

				"""
				DoThing();

				first = 1;
				second = 2;

				DoThing();
				"""
			),

			(
				"an assignment pair above a jump",

				"""
				first = 1;

				second = 2;

				return;
				""",

				"""
				first = 1;
				second = 2;

				return;
				"""
			),

			(
				"a lone declaration above an assignment pair",

				"""
				DoThing();

				var value = 1;

				first = value;

				second = value;
				""",

				"""
				DoThing();

				var value = 1;

				first = value;
				second = value;
				"""
			),

			(
				"a compound assignment and an increment among calls",

				"""
				DoThing();

				first += 1;

				second++;

				DoThing();

				third = 3;

				fourth = 4;
				""",

				"""
				DoThing();

				first += 1;
				second++;

				DoThing();

				third = 3;
				fourth = 4;
				"""
			),
		];

		Dictionary<string, ReportDiagnostic> rules = new(
			TestRuleSets.Only(
				DiagnosticDescriptors.GatherRunOfLooseStatements,
				DiagnosticDescriptors.SeparateDeclarationRuns,
				DiagnosticDescriptors.SeparateInvocationRuns,
				DiagnosticDescriptors.SeparateAssertionRuns,
				DiagnosticDescriptors.SeparateAssignmentRuns,
				DiagnosticDescriptors.SeparateJumpStatement
			)
		);

		foreach ((string name, string body, string expected) in cases)
		{
			string source = Method(body);
			string fixedSource = Method(expected);

			await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
				expected: fixedSource,
				source,

				options: new RunOptions(RoslynTestHarness.AllAnalyzers)
				{
					DiagnosticOptions = rules,
					AnalyzerConfigOptions = new Dictionary<string, string> { [LimitKey] = "1" },
				},

				name
			);
		}
	}

	// A rule that is off hands its gap back, so the loose run rule gathers the pair as it gathers any loose statements.
	[TestMethod]
	public async Task AnAssignmentRunRuleThatIsOffLeavesItsPairToTheLooseRunRule()
	{
		string source = Method(
			"""
			DoThing();

			first = 1;

			second = 2;

			DoThing();
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();
			first = 1;
			second = 2;
			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,
			diagnosticOptions: TestRuleSets.Only(DiagnosticDescriptors.GatherRunOfLooseStatements),
			analyzerConfigOptions: new Dictionary<string, string> { [LimitKey] = "1" }
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The subscription rule asks for a blank line around its run, so gathering keeps those and gathers the rest in every fix order.
	[TestMethod]
	public async Task TheBordersOfASubscriptionRunSettleInEveryFixOrder()
	{
		(string Name, string Body, string Expected)[] cases =
		[
			(
				"a subscription pair between calls",

				"""
				DoThing();

				Changed += Handler;

				Closed -= Handler;

				DoThing();
				""",

				"""
				DoThing();

				Changed += Handler;
				Closed -= Handler;

				DoThing();
				"""
			),

			(
				"a subscription pair above a jump",

				"""
				Changed += Handler;

				Closed += Handler;

				return;
				""",

				"""
				Changed += Handler;
				Closed += Handler;

				return;
				"""
			),

			(
				"a lone declaration above a subscription pair",

				"""
				DoThing();

				var value = Handler;

				Changed += value;

				Closed += value;
				""",

				"""
				DoThing();

				var value = Handler;

				Changed += value;
				Closed += value;
				"""
			),

			(
				"a subscription pair inside a run of assignments",

				"""
				first = 1;
				second = 2;

				Changed += Handler;

				Closed += Handler;

				third = 3;
				""",

				"""
				first = 1;
				second = 2;

				Changed += Handler;
				Closed += Handler;

				third = 3;
				"""
			),

			(
				"an assignment above a subscription pair",

				"""
				first = 1;

				Changed += Handler;
				Closed += Handler;
				""",

				"""
				first = 1;

				Changed += Handler;
				Closed += Handler;
				"""
			),

			(
				"a number between two subscriptions",

				"""
				Changed += Handler;

				first += 1;

				Closed += Handler;
				""",

				"""
				Changed += Handler;
				first += 1;
				Closed += Handler;
				"""
			),
		];

		Dictionary<string, ReportDiagnostic> rules = new(
			TestRuleSets.Only(
				DiagnosticDescriptors.GatherRunOfLooseStatements,
				DiagnosticDescriptors.SeparateDeclarationRuns,
				DiagnosticDescriptors.SeparateInvocationRuns,
				DiagnosticDescriptors.SeparateAssertionRuns,
				DiagnosticDescriptors.SeparateEventSubscriptionRuns,
				DiagnosticDescriptors.SeparateAssignmentRuns,
				DiagnosticDescriptors.SeparateJumpStatement
			)
		);

		foreach ((string name, string body, string expected) in cases)
		{
			string source = SubscriptionMethod(body);
			string fixedSource = SubscriptionMethod(expected);

			await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
				expected: fixedSource,
				source,

				options: new RunOptions(RoslynTestHarness.AllAnalyzers)
				{
					DiagnosticOptions = rules,
					AnalyzerConfigOptions = new Dictionary<string, string> { [LimitKey] = "1" },
				},

				name
			);
		}
	}

	// A rule that is off hands its gap back, so the loose run rule gathers the pair as it gathers any loose statements.
	[TestMethod]
	public async Task ASubscriptionRunRuleThatIsOffLeavesItsPairToTheLooseRunRule()
	{
		string source = SubscriptionMethod(
			"""
			DoThing();

			Changed += Handler;

			Closed += Handler;

			DoThing();
			"""
		);

		string fixedSource = SubscriptionMethod(
			"""
			DoThing();
			Changed += Handler;
			Closed += Handler;
			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,
			diagnosticOptions: TestRuleSets.Only(DiagnosticDescriptors.GatherRunOfLooseStatements),
			analyzerConfigOptions: new Dictionary<string, string> { [LimitKey] = "1" }
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// Wraps statements in a method of a class that declares the fields they assign and the events they subscribe to.
	private static string SubscriptionMethod(string body)
	{
		string indented = string.Join(
			"\n",

			body.Split('\n')
				.Select(line => line.Trim().Length == 0 || line[0] == '#' ? line : "\t\t" + line)
		);

		return "class C\n{\n"
			+ "\tint first, second, third;\n\n"
			+ "\tevent Action Changed;\n"
			+ "\tevent Action Closed;\n\n"
			+ "\tvoid Handler() { }\n\n"
			+ "\tvoid M()\n\t{\n"
			+ indented
			+ "\n\t}\n}\n";
	}

	// Wraps statements in a method of a class that declares the fields they assign.
	private static string Method(string body, bool isAsync = false)
	{
		string indented = string.Join(
			"\n",

			body.Split('\n')
				.Select(line => line.Trim().Length == 0 || line[0] == '#' ? line : "\t\t" + line)
		);

		return "class C\n{\n"
			+ "\tint first, second, third, fourth, fifth, sixth;\n\n"
			+ (isAsync ? "\tasync System.Threading.Tasks.Task M()\n\t{\n" : "\tvoid M()\n\t{\n")
			+ indented
			+ "\n\t}\n}\n";
	}

	private static async Task AssertFourLooseStatementsAtAsync(string value, int expected)
	{
		string source = Method(FourLooseStatements);
		string fixedSource = expected == 0 ? source : Method(FourGatheredStatements);

		AnalyzerRunResult result = await RunAsync(
			source,
			new Dictionary<string, string> { [LimitKey] = value }
		);

		Assert.HasCount(expected, result.Diagnostics, value);
		RoslynTestHarness.AssertFixedText(fixedSource, result, value);
	}

	// The container exists in the view that defines B and has no statement in the other, and the fix leaves both views settled.
	private static async Task AssertGatheredOnlyWhereTheConditionalHoldsAsync(
		string source,
		string fixedSource
	)
	{
		await AssertGatheredAsync("#define B\n" + source, "#define B\n" + fixedSource);

		Dictionary<string, string> limit = new() { [LimitKey] = "1" };

		AnalyzerRunResult hidden = await RunAsync(source, limit);

		Assert.IsEmpty(hidden.Diagnostics, "view without B");
		RoslynTestHarness.AssertFixedText(source, hidden, "view without B");

		AnalyzerRunResult settled = await RunAsync(fixedSource, limit);

		Assert.IsEmpty(settled.Diagnostics, "the fixed text in the view without B");
	}

	private static async Task AssertGatheredAsync(string source, string fixedSource)
	{
		Dictionary<string, string> limit = new() { [LimitKey] = "1" };

		AnalyzerRunResult result = await RunAsync(source, limit);

		Assert.IsGreaterThan(
			0,
			result.Diagnostics.Length,
			source.StartsWith('#') ? "view with B" : "view without B"
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);

		foreach (string view in new[] { "", "#define B\n" })
		{
			string text = fixedSource.StartsWith("#define B\n", StringComparison.Ordinal)
				? fixedSource["#define B\n".Length..]
				: fixedSource;

			AnalyzerRunResult settled = await RunAsync(view + text, limit);

			Assert.IsEmpty(settled.Diagnostics, $"the fixed text in the view [{view.Trim()}]");
		}
	}

	private static async Task AssertLeftAsWrittenAsync(string source)
	{
		AnalyzerRunResult result = await RunAsync(
			source,
			new Dictionary<string, string> { [LimitKey] = "1" }
		);

		Assert.IsEmpty(
			result.Diagnostics,
			source.StartsWith('#') ? "view with B" : "view without B"
		);

		RoslynTestHarness.AssertFixedText(source, result);
	}

	private static Task<AnalyzerRunResult> RunAsync(
		string source,
		IReadOnlyDictionary<string, string>? options = null
	)
	{
		// The same-type rule is on, so the rows show the gaps it asks for once a run is gathered.
		return RoslynTestHarness.RunAsync<GatherRunOfLooseStatementsAnalyzer>(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.GatherRunOfLooseStatements,
				DiagnosticDescriptors.SeparateSameTypeDeclarationRuns
			),

			analyzerConfigOptions: options
		);
	}
}
