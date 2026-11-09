using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed class KeepRunOfStatementsTogetherTests
{
	// Each row is two statements of one run that a blank line sets apart, with the rule it needs besides this one.
	// The declaration, invocation and assertion rules ship on and need none, and every row turns the same-type rule on, which ships off.
	public static IEnumerable<object[]> JoinedRuns { get; } =
	[
		[
			"two declarations of one type",
			Array.Empty<DiagnosticDescriptor>(),

			"""
				class C
				{
					void M()
					{
						int first = 1;

						int second = 2;
					}
				}
				""",

			"""
				class C
				{
					void M()
					{
						int first = 1;
						int second = 2;
					}
				}
				""",
		],

		[
			"two calls of one method",
			Array.Empty<DiagnosticDescriptor>(),

			"""
				class C
				{
					void M()
					{
						DoThing(1);

						DoThing(2);
					}

					void DoThing(int value) { }
				}
				""",

			"""
				class C
				{
					void M()
					{
						DoThing(1);
						DoThing(2);
					}

					void DoThing(int value) { }
				}
				""",
		],

		[
			"two assertions of one root",
			Array.Empty<DiagnosticDescriptor>(),

			"""
				class C
				{
					void M(int value)
					{
						Assert.AreEqual(1, value);

						Assert.AreEqual(2, value);
					}
				}
				""",

			"""
				class C
				{
					void M(int value)
					{
						Assert.AreEqual(1, value);
						Assert.AreEqual(2, value);
					}
				}
				""",
		],

		[
			"two assignments",
			new[] { DiagnosticDescriptors.SeparateAssignmentRuns },

			"""
				class C
				{
					int _first;
					int _second;

					void M()
					{
						_first = 1;

						_second = 2;
					}
				}
				""",

			"""
				class C
				{
					int _first;
					int _second;

					void M()
					{
						_first = 1;
						_second = 2;
					}
				}
				""",
		],

		[
			"two await statements",
			new[] { DiagnosticDescriptors.SeparateAwaitRuns },

			"""
				class C
				{
					async System.Threading.Tasks.Task M()
					{
						await Load();

						await Save();
					}
				}
				""",

			"""
				class C
				{
					async System.Threading.Tasks.Task M()
					{
						await Load();
						await Save();
					}
				}
				""",
		],

		[
			"two event subscriptions",
			new[] { DiagnosticDescriptors.SeparateEventSubscriptionRuns },

			"""
				class C
				{
					event System.Action Changed;
					event System.Action Closed;

					void M()
					{
						Changed += OnChanged;

						Closed += OnChanged;
					}

					void OnChanged() { }
				}
				""",

			"""
				class C
				{
					event System.Action Changed;
					event System.Action Closed;

					void M()
					{
						Changed += OnChanged;
						Closed += OnChanged;
					}

					void OnChanged() { }
				}
				""",
		],

		[
			"two declarations of a type that are not the first of their neighbors",
			Array.Empty<DiagnosticDescriptor>(),

			"""
				class C
				{
					void M()
					{
						Prepare();

						int first = 1;

						int second = 2;

						Prepare();
					}

					void Prepare() { }
				}
				""",

			"""
				class C
				{
					void M()
					{
						Prepare();

						int first = 1;
						int second = 2;

						Prepare();
					}

					void Prepare() { }
				}
				""",
		],

		[
			"a lone statement before a section",
			Array.Empty<DiagnosticDescriptor>(),

			"""
				class C
				{
					void M()
					{
						DoThing(1);

						DoThing(2);
						DoThing(3);
					}

					void DoThing(int value) { }
				}
				""",

			"""
				class C
				{
					void M()
					{
						DoThing(1);
						DoThing(2);
						DoThing(3);
					}

					void DoThing(int value) { }
				}
				""",
		],

		[
			"a lone statement after a section",
			Array.Empty<DiagnosticDescriptor>(),

			"""
				class C
				{
					void M()
					{
						DoThing(1);
						DoThing(2);

						DoThing(3);
					}

					void DoThing(int value) { }
				}
				""",

			"""
				class C
				{
					void M()
					{
						DoThing(1);
						DoThing(2);
						DoThing(3);
					}

					void DoThing(int value) { }
				}
				""",
		],
	];

	// The window of a run holds the blank lines that stand asked for beside it, so the run is charged only for those it adds.
	// Here two blank lines go and one is written, though the window also holds the blank line asked for below the strings.
	[TestMethod]
	public async Task ARunIsChargedOnlyForTheBlankLinesItAdds()
	{
		string source = WrapInMethod(
			"string v6 = \"\";\n\t\tint v7 = 0;\n\n\t\tint v8 = 0;\n\n\t\tint v9 = 0;\n\t\tint v10 = 0;\n\t\tstring v11 = \"\";\n\t\tint v12 = 0;"
		);

		string fixedSource = WrapInMethod(
			"string v6 = \"\";\n\n\t\tint v7 = 0;\n\t\tint v8 = 0;\n\t\tint v9 = 0;\n\t\tint v10 = 0;\n\n\t\tstring v11 = \"\";\n\t\tint v12 = 0;"
		);

		AnalyzerRunResult result = await RunAsync(source, []);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// A blank line asked for below a run of strings is no cost of joining the two declarations below it.
	[TestMethod]
	public async Task ABlankLineAlreadyAskedForIsNotChargedToTheRunBesideIt()
	{
		string source = WrapInMethod(
			"string x = \"\";\n\t\tstring y = \"\";\n\t\tint a = 0;\n\n\t\tint b = 0;"
		);

		string fixedSource = WrapInMethod(
			"string x = \"\";\n\t\tstring y = \"\";\n\n\t\tint a = 0;\n\t\tint b = 0;"
		);

		AnalyzerRunResult result = await RunAsync(source, []);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The blank line below the strings stands asked for by the declaration rules, so joining the calls below it adds none.
	[TestMethod]
	public async Task ABlankLineAskedForByAnotherRuleIsNotChargedToTheRunBelowIt()
	{
		string source = WrapInMethod(
			"string s1 = \"\";\n\t\tstring s2 = \"\";\n\t\tDoThing(1);\n\n\t\tDoThing(2);"
		);

		string fixedSource = WrapInMethod(
			"string s1 = \"\";\n\t\tstring s2 = \"\";\n\n\t\tDoThing(1);\n\t\tDoThing(2);"
		);

		AnalyzerRunResult result = await RunAsync(
			source,

			[
				DiagnosticDescriptors.SeparateDeclarationRuns,
				DiagnosticDescriptors.SeparateInvocationRuns,
			]
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The calls between two declarations are charged for neither declaration's blank line, which the invocation rule asks for.
	[TestMethod]
	public async Task ARunBetweenTwoDeclarationsIsChargedOnlyForTheBlankLinesItAdds()
	{
		string source = WrapInMethod(
			"DoThing(0);\n\t\tDoThing(1);\n\t\tint i2 = 0;\n\n\t\tDoThing(3);\n\n\t\tDoThing(4);\n\n\t\tint i5 = 0;"
		);

		string fixedSource = WrapInMethod(
			"DoThing(0);\n\t\tDoThing(1);\n\n\t\tint i2 = 0;\n\n\t\tDoThing(3);\n\t\tDoThing(4);\n\n\t\tint i5 = 0;"
		);

		AnalyzerRunResult result = await RunAsync(
			source,
			[DiagnosticDescriptors.SeparateInvocationRuns]
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// A lone statement beside two sections is absorbed by the nearer one, and a lone statement beside another stays a lone one.
	[TestMethod]
	public async Task ALoneStatementIsAbsorbedUnlessItSitsBetweenTwoSections()
	{
		(string Name, string Statements, string Fixed, int Reported)[] rows =
		[
			(
				"1|2|2",
				"DoThing(1);\n\n\t\tDoThing(2);\n\t\tDoThing(3);\n\n\t\tDoThing(4);\n\t\tDoThing(5);",
				"DoThing(1);\n\t\tDoThing(2);\n\t\tDoThing(3);\n\n\t\tDoThing(4);\n\t\tDoThing(5);",
				1
			),

			(
				"2|2|1",
				"DoThing(1);\n\t\tDoThing(2);\n\n\t\tDoThing(3);\n\t\tDoThing(4);\n\n\t\tDoThing(5);",
				"DoThing(1);\n\t\tDoThing(2);\n\n\t\tDoThing(3);\n\t\tDoThing(4);\n\t\tDoThing(5);",
				1
			),

			(
				"1|1|2|1|1",
				"DoThing(1);\n\n\t\tDoThing(2);\n\n\t\tDoThing(3);\n\t\tDoThing(4);\n\n\t\tDoThing(5);\n\n\t\tDoThing(6);",
				"DoThing(1);\n\t\tDoThing(2);\n\t\tDoThing(3);\n\t\tDoThing(4);\n\t\tDoThing(5);\n\t\tDoThing(6);",
				4
			),
		];

		foreach ((string name, string statements, string fixedStatements, int reported) in rows)
		{
			AnalyzerRunResult result = await RunAsync(WrapInMethod(statements), []);

			Assert.HasCount(reported, result.Diagnostics, name);
			RoslynTestHarness.AssertFixedText(WrapInMethod(fixedStatements), result, name);
		}
	}

	private static string WrapInMethod(string statements) =>
		$"class C\n{{\n\tvoid M()\n\t{{\n\t\t{statements}\n\t}}\n\n\tvoid DoThing(int value) {{ }}\n}}\n";

	// Three statements in a switch section are set apart from the break the joined run now meets, which two statements would only trade for.
	[TestMethod]
	public async Task AJoinedRunIsSetApartFromTheBreakItMeets()
	{
		const string source = """
			class C
			{
				void M(int kind)
				{
					switch (kind)
					{
						case 1:
							DoThing(1);

							DoThing(2);

							DoThing(3);
							break;
					}
				}

				void DoThing(int value) { }
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int kind)
				{
					switch (kind)
					{
						case 1:
							DoThing(1);
							DoThing(2);
							DoThing(3);

							break;
					}
				}

				void DoThing(int value) { }
			}
			""";

		AnalyzerRunResult result = await RunAsync(source, []);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DynamicData(
		nameof(JoinedRuns),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AGapInsideARunIsKept(
		string name,
		DiagnosticDescriptor[] alsoOn,
		string source,
		string fixedSource
	)
	{
		AnalyzerRunResult result = await RunAsync(source, alsoOn);

		Assert.HasCount(1, result.Diagnostics, name);
		RoslynTestHarness.AssertFixedText(fixedSource, result, name);
	}

	// Each row is a gap that the rule leaves alone, because the key of a rule that is on does not join its two sides.
	public static IEnumerable<object[]> QuietGaps { get; } =
	[
		[
			"calls of two methods",

			"""
				class C
				{
					void M()
					{
						First();

						Second();
					}

					void First() { }

					void Second() { }
				}
				""",
		],

		[
			"declarations of two types, which the same-type rule reads apart",

			"""
				class C
				{
					void M()
					{
						int first = 1;

						string second = "";
					}
				}
				""",
		],

		[
			"a call and an assignment",

			"""
				class C
				{
					int _count;

					void M()
					{
						DoThing();

						_count = 1;
					}

					void DoThing() { }
				}
				""",
		],

		[
			"declarations of two types beside a run of each, which the same-type rule sets apart",

			"""
				class C
				{
					void M()
					{
						int first = 1;
						int second = 2;

						string third = "";
						string fourth = "";
					}
				}
				""",
		],

		[
			"a gap with a comment in it",

			"""
				class C
				{
					void M()
					{
						DoThing(1);

						// Why.
						DoThing(2);
					}

					void DoThing(int value) { }
				}
				""",
		],

		[
			"a statement with a trailing comment",

			"""
				class C
				{
					void M()
					{
						DoThing(1); // Why.

						DoThing(2);
					}

					void DoThing(int value) { }
				}
				""",
		],

		[
			"a gap with a directive in it",

			"""
				class C
				{
					void M()
					{
						DoThing(1);

				#pragma warning disable CS0219
						DoThing(2);
					}

					void DoThing(int value) { }
				}
				""",
		],

		[
			"a multi-line neighbor",

			"""
				class C
				{
					void M()
					{
						DoThing(1);

						DoThing(
							2
						);
					}

					void DoThing(int value) { }
				}
				""",
		],

		[
			"a gap with no blank line",

			"""
				class C
				{
					void M()
					{
						DoThing(1);
						DoThing(2);
					}

					void DoThing(int value) { }
				}
				""",
		],

		[
			"assignments, while the assignment rule is off",

			"""
				class C
				{
					int _first;
					int _second;

					void M()
					{
						_first = 1;

						_second = 2;
					}
				}
				""",
		],

		[
			"members, which the member rule judges",

			"""
				class C
				{
					int first;

					int second;
				}
				""",
		],

		[
			"two sections of two statements each",

			"""
				class C
				{
					void M()
					{
						DoThing(1);
						DoThing(2);

						DoThing(3);
						DoThing(4);
					}

					void DoThing(int value) { }
				}
				""",
		],

		[
			"three sections, two of them of three statements",

			"""
				class C
				{
					void M()
					{
						DoThing(1);
						DoThing(2);
						DoThing(3);

						DoThing(4);
						DoThing(5);

						DoThing(6);
						DoThing(7);
						DoThing(8);
					}

					void DoThing(int value) { }
				}
				""",
		],

		[
			"a lone statement between two sections",

			"""
				class C
				{
					void M()
					{
						DoThing(1);
						DoThing(2);

						DoThing(3);

						DoThing(4);
						DoThing(5);
					}

					void DoThing(int value) { }
				}
				""",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(QuietGaps),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AGapOutsideARunIsLeftAlone(string name, string source)
	{
		AnalyzerRunResult result = await RunAsync(source, []);
		Assert.IsEmpty(result.Diagnostics, name);
	}

	// Joining the width, the height and the depth makes a run of one type, which the same-type rule sets apart from the name.
	// The fix writes that blank line, so a single fix settles the text.
	[TestMethod]
	public async Task JoiningARunWritesTheBlankLineTheRegroupedStatementsAskFor()
	{
		const string source = """
			class C
			{
				void M()
				{
					int width = 0;

					int height = 0;

					int depth = 0;
					string name = "";
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					int width = 0;
					int height = 0;
					int depth = 0;

					string name = "";
				}
			}
			""";

		AnalyzerRunResult result = await RunAsync(source, []);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// Joining the width and the height would write the blank line the same-type rule asks for below them, so the fix would only move it.
	[TestMethod]
	public async Task AFixThatOnlyMovesABlankLineIsNotReported()
	{
		const string source = """
			class C
			{
				void M()
				{
					int width = 0;

					int height = 0;
					string name = "";
				}
			}
			""";

		AnalyzerRunResult result = await RunAsync(source, []);

		Assert.IsEmpty(result.Diagnostics);
	}

	// The gather rule leaves a container to this rule, and a gap this rule declines stays as written.
	// The gather rule already passes a gap beside a tight one, so this holds whether or not it also reads the declined gaps.
	[TestMethod]
	public async Task AGapThisRuleDeclinesIsLeftAsWrittenByTheLooseRunRuleToo()
	{
		const string source = """
			class C
			{
				void M()
				{
					DoThing(1);
					DoThing(2);

					DoThing(3);

					DoThing(4);
					DoThing(5);
				}

				void DoThing(int value) { }
			}
			""";

		DiagnosticDescriptor[] alsoOn =
		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateInvocationRuns,
		];

		RunOptions options = new([
			new KeepRunOfStatementsTogetherAnalyzer(),
			new GatherRunOfLooseStatementsAnalyzer(),
		])
		{
			DiagnosticOptions = Rules(alsoOn),

			AnalyzerConfigOptions = new Dictionary<string, string>
			{
				["stanza_max_loose_run_size"] = "1",
			},
		};

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			expected: source,
			source,
			options
		);
	}

	// The calls are tight against the declarations, and the run they border exists once the fix joins it, so each side gets its blank line.
	[TestMethod]
	public async Task JoiningARunWritesTheBlankLineOnBothSidesOfIt()
	{
		const string source = """
			class C
			{
				void M()
				{
					Prepare();
					int first = 1;

					int second = 2;

					int third = 3;

					int fourth = 4;
					Finish();
				}

				void Prepare() { }

				void Finish() { }
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					Prepare();

					int first = 1;
					int second = 2;
					int third = 3;
					int fourth = 4;

					Finish();
				}

				void Prepare() { }

				void Finish() { }
			}
			""";

		AnalyzerRunResult result = await RunAsync(source, []);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// Two runs of a container are fixed together by a Fix All, and the blank line between them is written once.
	[TestMethod]
	public async Task TwoRunsOfAContainerWriteTheBlankLineBetweenThemOnce()
	{
		const string source = """
			class C
			{
				void M()
				{
					int first = 1;

					int second = 2;
					string third = "";

					string fourth = "";
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					int first = 1;
					int second = 2;

					string third = "";
					string fourth = "";
				}
			}
			""";

		AnalyzerRunResult all = await RoslynTestHarness.RunFixAllAsync(source, Rules([]));

		RoslynTestHarness.AssertFixedText(fixedSource, all);

		string[] alone = await RoslynTestHarness.ApplyEachFixAloneAsync(
			source,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			Rules([])
		);

		Assert.HasCount(2, alone);

		foreach (string text in alone)
		{
			AnalyzerRunResult settled = await RunAsync(text, []);
			RoslynTestHarness.AssertFixedText(fixedSource, settled);
		}
	}

	// Every gap of a run holds its bounds, so the fix of any one of them removes the blank lines of the run.
	[TestMethod]
	public async Task TheFixOfAnyGapRemovesTheBlankLinesOfTheWholeRun()
	{
		const string source = """
			class C
			{
				void M()
				{
					DoThing(1);

					DoThing(2);

					DoThing(3);
				}

				void DoThing(int value) { }
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					DoThing(1);
					DoThing(2);
					DoThing(3);
				}

				void DoThing(int value) { }
			}
			""";

		string[] alone = await RoslynTestHarness.ApplyEachFixAloneAsync(
			source,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			Rules([])
		);

		Assert.AreSequenceEqual([fixedSource, fixedSource], alone);
	}

	// A rule that is off hands its runs back, so the calls it joined are no run.
	[TestMethod]
	public async Task ARunOfARuleThatIsOffIsLeftAlone()
	{
		const string source = """
			class C
			{
				void M()
				{
					DoThing(1);

					DoThing(2);
				}

				void DoThing(int value) { }
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = new(Rules([]), StringComparer.Ordinal)
		{
			[DiagnosticDescriptors.SeparateInvocationRuns.Id] = ReportDiagnostic.Suppress,
		};

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepRunOfStatementsTogetherAnalyzer>(source, rules);

		Assert.IsEmpty(result.Diagnostics);
	}

	// Without the same-type rule, two declarations of different types are one run of explicit declarations.
	[TestMethod]
	public async Task DeclarationsOfTwoTypesAreOneRunWhereTheSameTypeRuleIsOff()
	{
		const string source = """
			class C
			{
				void M()
				{
					int first = 1;

					string second = "";
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					int first = 1;
					string second = "";
				}
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = new(Rules([]), StringComparer.Ordinal)
		{
			[DiagnosticDescriptors.SeparateSameTypeDeclarationRuns.Id] = ReportDiagnostic.Suppress,
		};

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepRunOfStatementsTogetherAnalyzer>(source, rules);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// Assignments that a setting splits by their target are runs only within a target.
	[TestMethod]
	public async Task AssignmentsOfTwoTargetsAreNoRunWhereTheSettingSplitsThem()
	{
		const string source = """
			class C
			{
				int _field;
				int Property { get; set; }

				void M()
				{
					_field = 1;

					Property = 2;
				}
			}
			""";

		Dictionary<string, string> split = new() { ["stanza_assignment_targets"] = "split" };
		Dictionary<string, string> mixed = new() { ["stanza_assignment_targets"] = "mixed" };

		AnalyzerRunResult apart = await RunAsync(
			source,
			[DiagnosticDescriptors.SeparateAssignmentRuns],
			split
		);

		AnalyzerRunResult together = await RunAsync(
			source,
			[DiagnosticDescriptors.SeparateAssignmentRuns],
			mixed
		);

		Assert.IsEmpty(apart.Diagnostics);
		Assert.HasCount(1, together.Diagnostics);

		const string fixedSource = """
			class C
			{
				int _field;
				int Property { get; set; }

				void M()
				{
					_field = 1;
					Property = 2;
				}
			}
			""";

		RoslynTestHarness.AssertFixedText(fixedSource, together);
	}

	// A field and a member written through it share a root, so the split reads them as one run.
	[TestMethod]
	public async Task AFieldAndAMemberWrittenThroughItAreOneRunWhereTheSettingSplits()
	{
		const string source = """
			class Anchor
			{
				public int Movement { get; set; }
			}

			class C
			{
				Anchor _start;

				void M()
				{
					_start = new Anchor();

					_start.Movement = 1;
				}
			}
			""";

		const string fixedSource = """
			class Anchor
			{
				public int Movement { get; set; }
			}

			class C
			{
				Anchor _start;

				void M()
				{
					_start = new Anchor();
					_start.Movement = 1;
				}
			}
			""";

		Dictionary<string, string> split = new() { ["stanza_assignment_targets"] = "split" };

		AnalyzerRunResult result = await RunAsync(
			source,
			[DiagnosticDescriptors.SeparateAssignmentRuns],
			split
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// A container of two statements belongs to the two-unit rule where that rule is on.
	[TestMethod]
	public async Task ATwoStatementContainerBelongsToTheTwoUnitRuleWhereItIsOn()
	{
		const string source = """
			class C
			{
				void M()
				{
					DoThing(1);

					DoThing(2);
				}

				void DoThing(int value) { }
			}
			""";

		AnalyzerRunResult on = await RunAsync(
			source,
			[DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether]
		);

		AnalyzerRunResult off = await RunAsync(source, []);

		Assert.IsEmpty(on.Diagnostics);
		Assert.HasCount(1, off.Diagnostics);

		const string fixedSource = """
			class C
			{
				void M()
				{
					DoThing(1);
					DoThing(2);
				}

				void DoThing(int value) { }
			}
			""";

		RoslynTestHarness.AssertFixedText(fixedSource, off);
	}

	[TestMethod]
	public async Task AContainerWithAConditionalStatementIsLeftAsWritten()
	{
		const string source = """
			class C
			{
				void M()
				{
					DoThing(1);

					DoThing(2);
			#if VIEW
					DoThing(3);
			#endif
				}

				void DoThing(int value) { }
			}
			""";

		AnalyzerRunResult result = await RunAsync(source, []);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task APragmaOverTheRuleSilencesIt()
	{
		const string source = """
			class C
			{
				void M()
				{
			#pragma warning disable ZA0054
					DoThing(1);

					DoThing(2);
			#pragma warning restore ZA0054
				}

				void DoThing(int value) { }
			}
			""";

		AnalyzerRunResult result = await RunAsync(source, []);

		Assert.IsEmpty(result.Diagnostics);
	}

	// A gap that a rule keeps blank beside the base call is that rule's, so it ends the run.
	[TestMethod]
	public async Task AGapAnotherRuleAsksForIsNotInsideARun()
	{
		const string source = """
			class B
			{
				public virtual void M() { }
			}

			class C : B
			{
				public override void M()
				{
					base.M();

					DoThing(1);
					DoThing(2);
				}

				void DoThing(int value) { }
			}
			""";

		AnalyzerRunResult result = await RunAsync(source, [DiagnosticDescriptors.SeparateBaseCall]);

		Assert.IsEmpty(result.Diagnostics);
	}

	// The statements at the top of a file are judged, and the members below them are not.
	[TestMethod]
	public async Task TheTopLevelStatementsOfAFileAreJudged()
	{
		const string source = """
			DoThing(1);

			DoThing(2);

			void DoThing(int value) { }
			""";

		const string fixedSource = """
			DoThing(1);
			DoThing(2);

			void DoThing(int value) { }
			""";

		AnalyzerRunResult result = await RunAsync(source, []);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The same-type rule reads declarations by itself, so it makes the run when the declaration rule is off.
	[TestMethod]
	public async Task DeclarationsOfOneTypeAreARunWhereTheSameTypeRuleIsOnAlone()
	{
		const string source = """
			class C
			{
				void M()
				{
					int first = 1;

					int second = 2;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					int first = 1;
					int second = 2;
				}
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = new(
			Rules([DiagnosticDescriptors.SeparateSameTypeDeclarationRuns]),
			StringComparer.Ordinal
		)
		{
			[DiagnosticDescriptors.SeparateDeclarationRuns.Id] = ReportDiagnostic.Suppress,
		};

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepRunOfStatementsTogetherAnalyzer>(source, rules);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The unit beyond a run can share a line with the unit that the rules then set apart, and one fix splits that line.
	[TestMethod]
	public async Task AUnitSharingALineWithTheNextIsSplitInTheSameFix()
	{
		const string source = """
			class C
			{
				void M()
				{
					int first = 1;

					int second = 2;

					int third = 3; string fourth = "";
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					int first = 1;
					int second = 2;
					int third = 3;

					string fourth = "";
				}
			}
			""";

		DiagnosticDescriptor[] alsoOn =
		[
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
		];

		string[] alone = await RoslynTestHarness.ApplyEachFixAloneAsync(
			source,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			Rules(alsoOn)
		);

		Assert.AreSequenceEqual([fixedSource, fixedSource], alone);

		AnalyzerRunResult all = await RoslynTestHarness.RunFixAllAsync(source, Rules(alsoOn));

		RoslynTestHarness.AssertFixedText(fixedSource, all);
	}

	[TestMethod]
	public async Task AUnitTouchingThePreviousOneOnItsLineIsSplitInTheSameFix()
	{
		const string source = """
			class C
			{
				void M()
				{
					int first = 1;

					int second = 2;

					int third = 3;string fourth = "";
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					int first = 1;
					int second = 2;
					int third = 3;

					string fourth = "";
				}
			}
			""";

		DiagnosticDescriptor[] alsoOn =
		[
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
		];

		string[] alone = await RoslynTestHarness.ApplyEachFixAloneAsync(
			source,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			Rules(alsoOn)
		);

		Assert.AreSequenceEqual([fixedSource, fixedSource], alone);

		AnalyzerRunResult all = await RoslynTestHarness.RunFixAllAsync(source, Rules(alsoOn));

		RoslynTestHarness.AssertFixedText(fixedSource, all);
	}

	// Two runs write the borders of their container once between them, here with a line that two units share.
	[TestMethod]
	public async Task TwoRunsWriteOneBlankLineBeforeAUnitSharingALine()
	{
		const string source = """
			class C
			{
				void M()
				{
					int first = 1;

					int second = 2;

					int third = 3;
					string fourth = "";

					string fifth = "";

					string sixth = ""; DoThing();
				}

				void DoThing() { }
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					int first = 1;
					int second = 2;
					int third = 3;

					string fourth = "";
					string fifth = "";
					string sixth = "";

					DoThing();
				}

				void DoThing() { }
			}
			""";

		DiagnosticDescriptor[] alsoOn =
		[
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
		];

		AnalyzerRunResult all = await RoslynTestHarness.RunFixAllAsync(source, Rules(alsoOn));

		RoslynTestHarness.AssertFixedText(fixedSource, all);

		string[] alone = await RoslynTestHarness.ApplyEachFixAloneAsync(
			source,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			Rules(alsoOn)
		);

		Assert.HasCount(4, alone);

		foreach (string text in alone)
		{
			AnalyzerRunResult settled = await RunAsync(text, alsoOn);
			RoslynTestHarness.AssertFixedText(fixedSource, settled);
		}
	}

	// The declaration rule asks for the gaps around the two plain declarations and the same-type rule for those around the two of one type,
	// and the blank line of one ends the run of the other. So the fix writes the gaps of the lower run, and not those of the other.
	[TestMethod]
	public async Task TwoRunsThatCrossAreSetApartByTheBlankLinesOfOnlyOneOfThem()
	{
		const string source = """
			using System;

			class C
			{
				void M()
				{
					DoThing();

					DoThing();

					DoThing();

					DoThing();

					DoThing();
					using IDisposable first = Open();
					IDisposable second = Open();
					string third = "";
					Done();
				}

				void DoThing() { }

				void Done() { }

				IDisposable Open() => null!;
			}
			""";

		const string fixedSource = """
			using System;

			class C
			{
				void M()
				{
					DoThing();
					DoThing();
					DoThing();
					DoThing();
					DoThing();

					using IDisposable first = Open();

					IDisposable second = Open();
					string third = "";

					Done();
				}

				void DoThing() { }

				void Done() { }

				IDisposable Open() => null!;
			}
			""";

		DiagnosticDescriptor[] alsoOn =
		[
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
		];

		AnalyzerRunResult result = await RunAsync(source, alsoOn);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// Each group crosses as above, and its borders cost two tries: the gap below the using declaration, then the one above the call.
	// The fix of one run tries eight, so it writes the borders of four groups and leaves those of the fifth to the rules that ask for them.
	// The fixes of the other rules and runs then write them, and every fix order settles on the same text.
	[TestMethod]
	public async Task TheFixOfOneRunTriesEightCrossingsAndLeavesTheRestToTheRulesThatAskForThem()
	{
		const int groups = 5;

		// Six calls give the run five gaps to remove, more than the blank lines its fix writes around the group.
		static string Calls(bool joined) =>
			string.Join(joined ? string.Empty : "\n", Enumerable.Repeat("\t\tDoThing();\n", 6));

		static string Group(int index, bool joined, bool bordered)
		{
			string gap = bordered ? "\n" : string.Empty;

			return Calls(joined)
				+ $"\n\t\tusing IDisposable a{index} = Open();\n{gap}"
				+ $"\t\tIDisposable b{index} = Open();\n\t\tstring c{index} = \"\";\n{gap}\t\tBuild({index});\n";
		}

		static string Method(string statements)
		{
			return "using System;\n\nclass C\n{\n\tvoid M()\n\t{\n"
				+ statements
				+ "\t}\n\n\tvoid DoThing() { }\n\n\tvoid Build(int value) { }\n\n\tIDisposable Open() => null!;\n}\n";
		}

		IEnumerable<int> indexes = Enumerable.Range(0, groups);

		string source = Method(
			string.Concat(
				indexes.Select(index =>
					Calls(joined: false)
					+ $"\t\tusing IDisposable a{index} = Open();\n\t\tIDisposable b{index} = Open();\n\t\tstring c{index} = \"\";\n\t\tBuild({index});\n"
				)
			)
		);

		string fixedSource = Method(
			string.Join(
				"\n",

				indexes.Select(index =>
					Group(index, joined: index == 0, bordered: index < groups - 1)
				)
			)
		);

		string settledSource = Method(
			string.Join("\n", indexes.Select(index => Group(index, joined: true, bordered: true)))
		);

		DiagnosticDescriptor[] alsoOn =
		[
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
		];

		string[] alone = await RoslynTestHarness.ApplyEachFixAloneAsync(
			source,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			Rules(alsoOn)
		);

		Assert.AreEqual(fixedSource, alone[0]);

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			expected: settledSource,
			source,

			options: new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = Rules(alsoOn),
			}
		);
	}

	// Half the blocks of the method join to a shorter text, and half only move a blank line, a comment leading each unless the case says not.
	// A read that goes over the whole block for each run grows with the square of the runs, and the time tells it from a read of each run beside itself.
	// The walk times the whole report, so the workspace and the compilation are in it too, which a linear read leaves far under the limit.
	[TestMethod]
	[DoNotParallelize]
	public async Task ManyRunsInOneBlockAreJudgedInUnderFiveSeconds()
	{
		string source = CreateCostBlock(160, commented: true);

		AnalyzerRunResult result = await ReportCostBlockAsync(source, commentedSiblingRule: true);

		// The three blank lines of each joining block are reported, and the block that only moves one is not.
		Assert.HasCount(240, result.Diagnostics);

		LinearCost.AssertWalkEndsWithin(
			5_000,
			source,
			_ => ReportCostBlockAsync(source, commentedSiblingRule: true).GetAwaiter().GetResult()
		);
	}

	// With no comment between the blocks one chain of tight declarations runs through them, so a window that follows the chain reaches every run.
	[TestMethod]
	[DoNotParallelize]
	public async Task ManyRunsInOneChainOfTightDeclarationsAreJudgedInUnderFiveSeconds()
	{
		string source = CreateCostBlock(320, commented: false);

		AnalyzerRunResult result = await ReportCostBlockAsync(source, commentedSiblingRule: false);

		Assert.HasCount(480, result.Diagnostics);

		LinearCost.AssertWalkEndsWithin(
			5_000,
			source,
			_ => ReportCostBlockAsync(source, commentedSiblingRule: false).GetAwaiter().GetResult()
		);
	}

	private static string CreateCostBlock(int blocks, bool commented)
	{
		StringBuilder body = new();

		for (int index = 0; index < blocks; index++)
		{
			body.Append(
				CultureInfo.InvariantCulture,
				$"{(commented ? $"\t\t// Block {index}.\n" : string.Empty)}\t\tint a{index} = 0;\n\n\t\tint b{index} = 0;\n"
			);

			if (index < blocks / 2)
			{
				body.Append(
					CultureInfo.InvariantCulture,
					$"\n\t\tint c{index} = 0;\n\n\t\tint d{index} = 0;\n"
				);
			}

			body.Append(CultureInfo.InvariantCulture, $"\t\tstring s{index} = \"\";\n");
		}

		return $"class C\n{{\n\tvoid M()\n\t{{\n{body}\t}}\n}}\n";
	}

	private static Task<AnalyzerRunResult> ReportCostBlockAsync(
		string source,
		bool commentedSiblingRule
	)
	{
		DiagnosticDescriptor[] alsoOn = commentedSiblingRule
			? [DiagnosticDescriptors.SeparateCommentedSibling]
			: [];

		return RoslynTestHarness.ReportAsync<KeepRunOfStatementsTogetherAnalyzer>(
			source,
			Rules(alsoOn)
		);
	}

	// The same-type rule is on, so the rows show the runs it reads apart.
	private static IReadOnlyDictionary<string, ReportDiagnostic> Rules(
		DiagnosticDescriptor[] alsoOn
	)
	{
		DiagnosticDescriptor[] on =
		[
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			.. alsoOn,
		];

		return TestRuleSets.Only([.. on.Distinct()]);
	}

	private static Task<AnalyzerRunResult> RunAsync(
		string source,
		DiagnosticDescriptor[] alsoOn,
		IReadOnlyDictionary<string, string>? options = null
	)
	{
		return RoslynTestHarness.RunAsync<KeepRunOfStatementsTogetherAnalyzer>(
			source,
			Rules(alsoOn),
			analyzerConfigOptions: options
		);
	}
}
