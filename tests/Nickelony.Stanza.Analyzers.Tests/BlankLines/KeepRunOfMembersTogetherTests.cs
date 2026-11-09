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
public sealed class KeepRunOfMembersTogetherTests
{
	// Each row is two members of one group that a blank line sets apart, with the rule it needs besides this one.
	// The member group rule ships on, and the modifier group rule does not.
	public static IEnumerable<object[]> JoinedRuns { get; } =
	[
		[
			"two fields",
			Array.Empty<DiagnosticDescriptor>(),

			"""
				class C
				{
					int first;

					int second;
				}
				""",

			"""
				class C
				{
					int first;
					int second;
				}
				""",
		],

		[
			"two auto-properties",
			Array.Empty<DiagnosticDescriptor>(),

			"""
				class C
				{
					public int First { get; set; }

					public int Second { get; set; }
				}
				""",

			"""
				class C
				{
					public int First { get; set; }
					public int Second { get; set; }
				}
				""",
		],

		[
			"two single-line methods",
			Array.Empty<DiagnosticDescriptor>(),

			"""
				class C
				{
					void First() { }

					void Second() { }
				}
				""",

			"""
				class C
				{
					void First() { }
					void Second() { }
				}
				""",
		],

		[
			"two enum members",
			Array.Empty<DiagnosticDescriptor>(),

			"""
				enum E
				{
					First,

					Second,
				}
				""",

			"""
				enum E
				{
					First,
					Second,
				}
				""",
		],

		[
			"two members of other access while the modifier rule is off",
			Array.Empty<DiagnosticDescriptor>(),

			"""
				class C
				{
					public int First;

					private int Second;
				}
				""",

			"""
				class C
				{
					public int First;
					private int Second;
				}
				""",
		],

		[
			"two members that share their modifiers while the modifier rule is on",
			new[] { DiagnosticDescriptors.SeparateModifierGroups },

			"""
				class C
				{
					private int _first;

					private int _second;
				}
				""",

			"""
				class C
				{
					private int _first;
					private int _second;
				}
				""",
		],

		[
			"two members in an interface",
			Array.Empty<DiagnosticDescriptor>(),

			"""
				interface I
				{
					void First();

					void Second();
				}
				""",

			"""
				interface I
				{
					void First();
					void Second();
				}
				""",
		],

		[
			"two delegates in a namespace",
			Array.Empty<DiagnosticDescriptor>(),

			"""
				namespace N
				{
					delegate void First();

					delegate void Second();
				}
				""",

			"""
				namespace N
				{
					delegate void First();
					delegate void Second();
				}
				""",
		],

		[
			"a lone field before a section",
			Array.Empty<DiagnosticDescriptor>(),

			"""
				class C
				{
					int first;

					int second;
					int third;
				}
				""",

			"""
				class C
				{
					int first;
					int second;
					int third;
				}
				""",
		],

		[
			"a lone field after a section",
			Array.Empty<DiagnosticDescriptor>(),

			"""
				class C
				{
					int first;
					int second;

					int third;
				}
				""",

			"""
				class C
				{
					int first;
					int second;
					int third;
				}
				""",
		],
	];

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
			"a field and a property",
			Array.Empty<DiagnosticDescriptor>(),

			"""
				class C
				{
					int first;

					public int Second { get; set; }
				}
				""",
		],

		[
			"a constant and a field",
			Array.Empty<DiagnosticDescriptor>(),

			"""
				class C
				{
					const int First = 1;

					int second;
				}
				""",
		],

		[
			"two members of other access while the modifier rule is on",
			new[] { DiagnosticDescriptors.SeparateModifierGroups },

			"""
				class C
				{
					public int First;

					private int Second;
				}
				""",
		],

		[
			"fields of two types, which the same-type rule sets apart",
			Array.Empty<DiagnosticDescriptor>(),

			"""
				class C
				{
					int first;
					int second;

					string third;
					string fourth;
				}
				""",
		],

		[
			"a documented member",
			Array.Empty<DiagnosticDescriptor>(),

			"""
				class C
				{
					int first;

					/// <summary>The second.</summary>
					int second;
				}
				""",
		],

		[
			"a member with a comment above it",
			Array.Empty<DiagnosticDescriptor>(),

			"""
				class C
				{
					int first;

					// Why.
					int second;
				}
				""",
		],

		[
			"a member with a trailing comment",
			Array.Empty<DiagnosticDescriptor>(),

			"""
				class C
				{
					int first; // Why.

					int second;
				}
				""",
		],

		[
			"a gap with a directive in it",
			Array.Empty<DiagnosticDescriptor>(),

			"""
				class C
				{
					int first;

				#pragma warning disable CS0169
					int second;
				}
				""",
		],

		[
			"a multi-line member",
			Array.Empty<DiagnosticDescriptor>(),

			"""
				class C
				{
					void First() { }

					void Second()
					{
					}
				}
				""",
		],

		[
			"a container of two members while the two-unit rule is on",
			new[] { DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether },

			"""
				class C
				{
					int first;

					int second;
				}
				""",
		],

		[
			"two sections of two fields each",
			Array.Empty<DiagnosticDescriptor>(),

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
			"two sections of two fields each, beside a property",
			Array.Empty<DiagnosticDescriptor>(),

			"""
				class C
				{
					int first;
					int second;

					int third;
					int fourth;

					public int Fifth { get; set; }
				}
				""",
		],

		[
			"a lone field between two sections",
			Array.Empty<DiagnosticDescriptor>(),

			"""
				class C
				{
					int first;
					int second;
					int third;
					int fourth;

					int fifth;

					int sixth;
					int seventh;
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
	public async Task AGapOutsideARunIsLeftAlone(
		string name,
		DiagnosticDescriptor[] alsoOn,
		string source
	)
	{
		AnalyzerRunResult result = await RunAsync(source, alsoOn);
		Assert.IsEmpty(result.Diagnostics, name);
	}

	// Joining three fields makes a run of fields, which the member group rule sets apart from the property below.
	// The fix writes that blank line, so a single fix settles the text.
	[TestMethod]
	public async Task JoiningARunWritesTheBlankLineTheRegroupedMembersAskFor()
	{
		const string source = """
			class C
			{
				int first;

				int second;

				int third;
				public int Fourth { get; set; }
			}
			""";

		const string fixedSource = """
			class C
			{
				int first;
				int second;
				int third;

				public int Fourth { get; set; }
			}
			""";

		AnalyzerRunResult result = await RunAsync(source, []);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// Joining the two fields would write the blank line the member group rule asks for below them, so the fix would only move it.
	[TestMethod]
	public async Task AFixThatOnlyMovesABlankLineIsNotReported()
	{
		const string source = """
			class C
			{
				int first;

				int second;
				public int Third { get; set; }
			}
			""";

		AnalyzerRunResult result = await RunAsync(source, []);

		Assert.IsEmpty(result.Diagnostics);
	}

	// The modifier rule is the finer key, so it asks for the run it joins even where the member rule sees one group.
	[TestMethod]
	public async Task JoiningARunWritesTheBlankLineTheModifierRuleAsksFor()
	{
		const string source = """
			class C
			{
				private int _first;

				private int _second;

				private int _third;
				public int Fourth;
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _first;
				private int _second;
				private int _third;

				public int Fourth;
			}
			""";

		AnalyzerRunResult result = await RunAsync(
			source,
			[DiagnosticDescriptors.SeparateModifierGroups]
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// A run that the modifier rule reads apart from the member below it is set apart there, whichever fix comes first.
	[TestMethod]
	public async Task TwoRunsOfAContainerWriteTheBlankLineBetweenThemOnce()
	{
		const string source = """
			class C
			{
				private int _first;

				private int _second;
				public int Third;

				public int Fourth;
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _first;
				private int _second;

				public int Third;
				public int Fourth;
			}
			""";

		DiagnosticDescriptor[] alsoOn = [DiagnosticDescriptors.SeparateModifierGroups];

		AnalyzerRunResult all = await RoslynTestHarness.RunFixAllAsync(source, Rules(alsoOn));

		RoslynTestHarness.AssertFixedText(fixedSource, all);

		string[] alone = await RoslynTestHarness.ApplyEachFixAloneAsync(
			source,
			DiagnosticDescriptors.KeepRunOfMembersTogether,
			Rules(alsoOn)
		);

		Assert.HasCount(2, alone);

		foreach (string text in alone)
		{
			AnalyzerRunResult settled = await RunAsync(text, alsoOn);
			RoslynTestHarness.AssertFixedText(fixedSource, settled);
		}
	}

	// The modifier rule splits the fields, which leaves the static one alone, so the member rule asks for no blank line below it.
	// The keep fix writes the blank line of the modifier rule, so it reads the member runs with that blank line written.
	[TestMethod]
	public async Task ABorderTheFixWritesEndsTheRunsItSplits()
	{
		const string source = """
			class C
			{
				private int _first;
				private int _second;
				private static int s_third;
				public void Run() { }

				public int Fourth;

				public int Fifth;
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _first;
				private int _second;

				private static int s_third;
				public void Run() { }

				public int Fourth;
				public int Fifth;
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rules = Rules([
			DiagnosticDescriptors.SeparateModifierGroups,
		]);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source, rules);

		RoslynTestHarness.AssertFixedText(fixedSource, result);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			expected: fixedSource,
			source,
			options: new RunOptions(RoslynTestHarness.AllAnalyzers) { DiagnosticOptions = rules }
		);
	}

	// Every gap of a run holds its bounds, so the fix of any one of them removes the blank lines of the run.
	[TestMethod]
	public async Task TheFixOfAnyGapRemovesTheBlankLinesOfTheWholeRun()
	{
		const string source = """
			class C
			{
				int first;

				int second;

				int third;
			}
			""";

		const string fixedSource = """
			class C
			{
				int first;
				int second;
				int third;
			}
			""";

		string[] alone = await RoslynTestHarness.ApplyEachFixAloneAsync(
			source,
			DiagnosticDescriptors.KeepRunOfMembersTogether,
			Rules([])
		);

		Assert.AreSequenceEqual([fixedSource, fixedSource], alone);
	}

	// A rule that is off hands its runs back, so the members it joined are no run.
	// The declaration rules read the fields of a type too, so they are off beside the member group rule.
	[TestMethod]
	public async Task ARunOfARuleThatIsOffIsLeftAlone()
	{
		const string source = """
			class C
			{
				int first;

				int second;
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = new(Rules([]), StringComparer.Ordinal)
		{
			[DiagnosticDescriptors.SeparateMemberGroups.Id] = ReportDiagnostic.Suppress,
			[DiagnosticDescriptors.SeparateDeclarationRuns.Id] = ReportDiagnostic.Suppress,
			[DiagnosticDescriptors.SeparateSameTypeDeclarationRuns.Id] = ReportDiagnostic.Suppress,
		};

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepRunOfMembersTogetherAnalyzer>(source, rules);

		Assert.IsEmpty(result.Diagnostics);
	}

	// The members below the top-level statements of a file are judged, and the statements are not.
	[TestMethod]
	public async Task TheMembersBelowTheStatementsOfAFileAreJudged()
	{
		const string source = """
			DoThing();

			DoThing();

			class First { }

			class Second { }
			""";

		const string fixedSource = """
			DoThing();

			DoThing();

			class First { }
			class Second { }
			""";

		AnalyzerRunResult result = await RunAsync(source, []);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task APragmaOverTheRuleSilencesIt()
	{
		const string source = """
			class C
			{
			#pragma warning disable ZA0055
				int first;

				int second;
			#pragma warning restore ZA0055
			}
			""";

		AnalyzerRunResult result = await RunAsync(source, []);

		Assert.IsEmpty(result.Diagnostics);
	}

	// The same-type rule asks for the gap between fields of two types, so the fields are two runs while it is on.
	[TestMethod]
	public async Task FieldsOfTwoTypesAreTwoRunsWhereTheSameTypeRuleIsOn()
	{
		const string source = """
			class C
			{
				private int _first;

				private string _second;

				private int _third;
			}
			""";

		DiagnosticDescriptor[] alsoOn =
		[
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.SeparateMemberGroups,
			DiagnosticDescriptors.SeparateModifierGroups,
		];

		AnalyzerRunResult result = await RunAsync(source, alsoOn);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task FieldsOfTwoTypesAreOneRunWhereTheSameTypeRuleIsOff()
	{
		const string source = """
			class C
			{
				private int _first;

				private string _second;
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _first;
				private string _second;
			}
			""";

		// The member rules are off, so the run is the declaration rule's alone.
		Dictionary<string, ReportDiagnostic> rules = new(
			Rules([DiagnosticDescriptors.SeparateDeclarationRuns]),
			StringComparer.Ordinal
		)
		{
			[DiagnosticDescriptors.SeparateSameTypeDeclarationRuns.Id] = ReportDiagnostic.Suppress,
			[DiagnosticDescriptors.SeparateMemberGroups.Id] = ReportDiagnostic.Suppress,
			[DiagnosticDescriptors.SeparateModifierGroups.Id] = ReportDiagnostic.Suppress,
		};

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepRunOfMembersTogetherAnalyzer>(source, rules);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The same-type rule sets a joined run of fields apart from the field of another type, and the fix writes that gap.
	[TestMethod]
	public async Task JoiningFieldsWritesTheBlankLineTheSameTypeRuleAsksFor()
	{
		const string source = """
			class C
			{
				private int _first;

				private int _second;

				private int _third;
				private string _fourth;
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _first;
				private int _second;
				private int _third;

				private string _fourth;
			}
			""";

		AnalyzerRunResult result = await RunAsync(
			source,
			[DiagnosticDescriptors.SeparateSameTypeDeclarationRuns]
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The blank line below the fields stands asked for by the member group rule, so joining the methods below it adds none.
	[TestMethod]
	public async Task ABlankLineAskedForByAnotherRuleIsNotChargedToTheRunBelowIt()
	{
		const string source = """
			class C
			{
				private int _a;
				private int _b;
				public void M3() { }

				public void M4() { }
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _a;
				private int _b;

				public void M3() { }
				public void M4() { }
			}
			""";

		AnalyzerRunResult result = await RunAsync(
			source,
			[DiagnosticDescriptors.SeparateMemberGroups]
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// A lone field beside two sections is absorbed by the nearer one.
	[TestMethod]
	public async Task ALoneFieldBesideTwoSectionsIsAbsorbedAndTheSectionsStay()
	{
		const string source = """
			class C
			{
				int first;

				int second;
				int third;

				int fourth;
				int fifth;
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
			}
			""";

		AnalyzerRunResult result = await RunAsync(source, []);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The gather rule leaves a container to this rule, and a gap this rule declines stays as written.
	// The gather rule already passes a gap beside a tight one, so this holds whether or not it also reads the declined gaps.
	[TestMethod]
	public async Task AGapThisRuleDeclinesIsLeftAsWrittenByTheLooseRunRuleToo()
	{
		const string source = """
			class C
			{
				int first;
				int second;

				int third;

				int fourth;
				int fifth;
			}
			""";

		DiagnosticDescriptor[] alsoOn = [DiagnosticDescriptors.GatherRunOfLooseMembers];

		RunOptions options = new([
			new KeepRunOfMembersTogetherAnalyzer(),
			new GatherRunOfLooseMembersAnalyzer(),
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

	// The modifier rule asks for the gap above the private field and the same-type rule for the gap above the event,
	// and the blank line of one ends the run of the other. So the fix writes the lower gap and the methods' gap, and not the third.
	[TestMethod]
	public async Task TwoRunsThatCrossAreSetApartByTheBlankLinesOfOnlyOneOfThem()
	{
		const string source = """
			class C
			{
				public void First() { }

				public void Second() { }

				public void Third() { }

				public void Fourth() { }
				internal string _name;
				private string _title;
				event System.Action Changed;
			}
			""";

		const string fixedSource = """
			class C
			{
				public void First() { }
				public void Second() { }
				public void Third() { }
				public void Fourth() { }

				internal string _name;

				private string _title;
				event System.Action Changed;
			}
			""";

		DiagnosticDescriptor[] alsoOn =
		[
			DiagnosticDescriptors.SeparateMemberGroups,
			DiagnosticDescriptors.SeparateModifierGroups,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
		];

		AnalyzerRunResult result = await RunAsync(source, alsoOn);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// Half the blocks of the type join to a shorter text, and half only move a blank line, a comment leading each unless the case says not.
	// A read that goes over the whole type for each run grows with the square of the runs, and the time tells it from a read of each run beside itself.
	// The walk times the whole report, so the workspace and the compilation are in it too, which a linear read leaves far under the limit.
	[TestMethod]
	[DoNotParallelize]
	public async Task ManyRunsInOneTypeAreJudgedInUnderFiveSeconds()
	{
		string source = CreateCostType(160, commented: true);

		AnalyzerRunResult result = await ReportCostTypeAsync(source, commentedSiblingRule: true);

		// The three blank lines of each joining block are reported, and the block that only moves one is not.
		Assert.HasCount(240, result.Diagnostics);

		LinearCost.AssertWalkEndsWithin(
			5_000,
			source,
			_ => ReportCostTypeAsync(source, commentedSiblingRule: true).GetAwaiter().GetResult()
		);
	}

	// With no comment between the blocks one chain of tight members runs through them, so a window that follows the chain reaches every run.
	[TestMethod]
	[DoNotParallelize]
	public async Task ManyRunsInOneChainOfTightMembersAreJudgedInUnderFiveSeconds()
	{
		string source = CreateCostType(320, commented: false);

		AnalyzerRunResult result = await ReportCostTypeAsync(source, commentedSiblingRule: false);

		Assert.HasCount(480, result.Diagnostics);

		LinearCost.AssertWalkEndsWithin(
			5_000,
			source,
			_ => ReportCostTypeAsync(source, commentedSiblingRule: false).GetAwaiter().GetResult()
		);
	}

	private static string CreateCostType(int blocks, bool commented)
	{
		StringBuilder body = new();

		for (int index = 0; index < blocks; index++)
		{
			body.Append(
				CultureInfo.InvariantCulture,
				$"{(commented ? $"\t// Block {index}.\n" : string.Empty)}\tprivate int _a{index};\n\n\tprivate int _b{index};\n"
			);

			if (index < blocks / 2)
			{
				body.Append(
					CultureInfo.InvariantCulture,
					$"\n\tprivate int _c{index};\n\n\tprivate int _d{index};\n"
				);
			}

			body.Append(
				CultureInfo.InvariantCulture,
				$"\tpublic string S{index} {{ get; set; }}\n"
			);
		}

		return $"class C\n{{\n{body}}}\n";
	}

	private static Task<AnalyzerRunResult> ReportCostTypeAsync(
		string source,
		bool commentedSiblingRule
	)
	{
		DiagnosticDescriptor[] alsoOn =
		[
			DiagnosticDescriptors.SeparateMemberGroups,
			DiagnosticDescriptors.SeparateModifierGroups,

			.. commentedSiblingRule
				? [DiagnosticDescriptors.SeparateCommentedSibling]
				: Array.Empty<DiagnosticDescriptor>(),
		];

		return RoslynTestHarness.ReportAsync<KeepRunOfMembersTogetherAnalyzer>(
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
			DiagnosticDescriptors.KeepRunOfMembersTogether,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			.. alsoOn,
		];

		return TestRuleSets.Only([.. on.Distinct()]);
	}

	private static Task<AnalyzerRunResult> RunAsync(string source, DiagnosticDescriptor[] alsoOn) =>
		RoslynTestHarness.RunAsync<KeepRunOfMembersTogetherAnalyzer>(source, Rules(alsoOn));
}
