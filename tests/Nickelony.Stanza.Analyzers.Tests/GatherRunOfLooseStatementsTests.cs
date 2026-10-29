using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class GatherRunOfLooseStatementsTests
{
	private const string LimitKey = "stanza_max_loose_statements";

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

		[
			"top-level statements",

			"""
				DoThing();

				DoThing();

				DoThing();

				DoThing();
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

		AnalyzerRunResult result = await RoslynTestHarness.RunFixAllAsync(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.GatherRunOfLooseStatements)
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
	public async Task OnlyTheStatementsOfABlockOrASwitchSectionAreJudged(string name, string source)
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

	// ZA0005 asks for the gap above the if,
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

	// Where ZA0005 is off, the fourth statement and the if form a stanza,
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
				DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns,
				DiagnosticDescriptors.SeparateSameTypeDeclarationGroups
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
					DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns,
					DiagnosticDescriptors.SeparateSameTypeDeclarationGroups
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

				[DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns.Id] =
					ReportDiagnostic.Suppress,
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
			#pragma warning disable {{DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns.Id}}
					first = 1;

					second = 2;

					DoThing();

					DoThing();

					third = 3;
			#pragma warning restore {{DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns.Id}}
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
			#pragma warning disable {{DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns.Id}}
					first = 1;
					second = 2;
					DoThing();
					DoThing();
					third = 3;
			#pragma warning restore {{DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns.Id}}
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
		// Gathering the blank line below `a` joins it to the declarations under it, and ZA0001 would then ask for a blank line above it.
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
				DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns
			),

			new Dictionary<string, string> { [LimitKey] = "1" }
		);

		Assert.IsEmpty(result.Diagnostics);
		RoslynTestHarness.AssertFixedText(source, result);
	}

	[TestMethod]
	public async Task ARunThatNoGatheredGapTouchesKeepsTheBlankLinesBesideIt()
	{
		// The blank line above the run of calls stays, so gathering the one below it would leave ZA0001 asking for it back.
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
				DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns
			),

			new Dictionary<string, string> { [LimitKey] = "1" }
		);

		Assert.IsEmpty(result.Diagnostics);
		RoslynTestHarness.AssertFixedText(source, result);
	}

	// Wraps statements in a method of a class that declares the fields they assign.
	private static string Method(string body)
	{
		string indented = string.Join(
			"\n",

			body.Split('\n')
				.Select(line => line.Trim().Length == 0 || line[0] == '#' ? line : "\t\t" + line)
		);

		return "class C\n{\n"
			+ "\tint first, second, third, fourth, fifth, sixth;\n\n"
			+ "\tvoid M()\n\t{\n"
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

	private static Task<AnalyzerRunResult> RunAsync(
		string source,
		IReadOnlyDictionary<string, string>? options = null
	)
	{
		return RoslynTestHarness.RunAsync<GatherRunOfLooseStatementsAnalyzer>(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.GatherRunOfLooseStatements),
			analyzerConfigOptions: options
		);
	}
}
