using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed class GatherRunOfLooseMembersTests
{
	private const string LimitKey = "stanza_max_loose_run_size";

	// Each row is four members set apart by blank lines, which the rule gathers at the default limit of three.
	public static IEnumerable<object[]> LoosePlaces { get; } =
	[
		[
			"fields of a class",

			"""
				class C
				{
					int first;

					int second;

					int third;

					int fourth;
				}
				""",

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
			"members of an interface",

			"""
				interface I
				{
					void First();

					void Second();

					void Third();

					void Fourth();
				}
				""",

			"""
				interface I
				{
					void First();
					void Second();
					void Third();
					void Fourth();
				}
				""",
		],

		[
			"fields of a struct",

			"""
				struct S
				{
					public int First;

					public int Second;

					public int Third;

					public int Fourth;
				}
				""",

			"""
				struct S
				{
					public int First;
					public int Second;
					public int Third;
					public int Fourth;
				}
				""",
		],

		[
			"members of a record",

			"""
				record R
				{
					int first;

					int second;

					int third;

					int fourth;
				}
				""",

			"""
				record R
				{
					int first;
					int second;
					int third;
					int fourth;
				}
				""",
		],

		[
			"members of an enum",

			"""
				enum E
				{
					First,

					Second,

					Third,

					Fourth,
				}
				""",

			"""
				enum E
				{
					First,
					Second,
					Third,
					Fourth,
				}
				""",
		],

		[
			"members of an enum with no comma after the last",

			"""
				enum E
				{
					First = 1,

					Second = 2,

					Third = 3,

					Fourth = 4
				}
				""",

			"""
				enum E
				{
					First = 1,
					Second = 2,
					Third = 3,
					Fourth = 4
				}
				""",
		],

		[
			"members of a namespace",

			"""
				namespace N
				{
					delegate void First();

					delegate void Second();

					delegate void Third();

					delegate void Fourth();
				}
				""",

			"""
				namespace N
				{
					delegate void First();
					delegate void Second();
					delegate void Third();
					delegate void Fourth();
				}
				""",
		],

		[
			"members of a file-scoped namespace",

			"""
				namespace N;

				delegate void First();

				delegate void Second();

				delegate void Third();

				delegate void Fourth();
				""",

			"""
				namespace N;

				delegate void First();
				delegate void Second();
				delegate void Third();
				delegate void Fourth();
				""",
		],

		[
			"members of a file",

			"""
				delegate void First();

				delegate void Second();

				delegate void Third();

				delegate void Fourth();
				""",

			"""
				delegate void First();
				delegate void Second();
				delegate void Third();
				delegate void Fourth();
				""",
		],

		[
			"members of a file below its imports",

			"""
				using System;

				delegate void First();

				delegate void Second();

				delegate void Third();

				delegate void Fourth();
				""",

			"""
				using System;

				delegate void First();
				delegate void Second();
				delegate void Third();
				delegate void Fourth();
				""",
		],

		[
			"fields that carry an attribute on their line",

			"""
				class C
				{
					[Obsolete] int first;

					[Obsolete] int second;

					int third;

					[Obsolete] int fourth;
				}
				""",

			"""
				class C
				{
					[Obsolete] int first;
					[Obsolete] int second;
					int third;
					[Obsolete] int fourth;
				}
				""",
		],

		[
			"members whose neighbors are a field run and a method",

			"""
				class C
				{
					int first;

					int second;

					int third;

					int fourth;

					void Run()
					{
						DoThing();
					}
				}
				""",

			"""
				class C
				{
					int first;
					int second;
					int third;
					int fourth;

					void Run()
					{
						DoThing();
					}
				}
				""",
		],
	];

	// Each row is a container the rule leaves alone at the default limit of three.
	public static IEnumerable<object[]> QuietPlaces { get; } =
	[
		[
			"three loose members",

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
			"a multi-line member in the middle",

			"""
				class C
				{
					int first;

					int second;

					void Run()
					{
						DoThing();
					}

					int third;

					int fourth;
				}
				""",
		],

		[
			"a comment above a member",

			"""
				class C
				{
					int first;

					int second;

					// The third field.
					int third;

					int fourth;
				}
				""",
		],

		[
			"a documentation comment above a member",

			"""
				class C
				{
					int first;

					int second;

					/// <summary>The third field.</summary>
					int third;

					int fourth;
				}
				""",
		],

		[
			"a documentation comment set apart by a blank line from its member",

			"""
				class C
				{
					int first;

					int second;

					/// <summary>The third field.</summary>

					int third;

					int fourth;
				}
				""",
		],

		[
			"a comment that trails a member",

			"""
				class C
				{
					int first;

					int second; // The second field.

					int third;

					int fourth;
				}
				""",
		],

		[
			"a block comment in a gap",

			"""
				class C
				{
					int first;

					int second;
					/* The third field. */

					int third;

					int fourth;
				}
				""",
		],

		[
			"an attribute on its own line, which counts toward the member's lines",

			"""
				class C
				{
					int first;

					int second;

					[Obsolete]
					int third;

					int fourth;
				}
				""",
		],

		[
			"a region marker in a gap",

			"""
				class C
				{
					int first;

					int second;

				#region Rest
					int third;

					int fourth;
				#endregion
				}
				""",
		],

		[
			"a conditional directive among the members",

			"""
				class C
				{
					int first;

					int second;

				#if DEBUG
					int third;
				#endif

					int fourth;

					int fifth;
				}
				""",
		],

		[
			"enum members with a comment that trails a comma",

			"""
				enum E
				{
					First,

					Second, // The second member.

					Third,

					Fourth,
				}
				""",
		],

		[
			"enum members with a comment above a member",

			"""
				enum E
				{
					First,

					Second,

					// The third member.
					Third,

					Fourth,
				}
				""",
		],

		[
			"an enum member with an attribute on its own line",

			"""
				enum E
				{
					First,

					Second,

					[Obsolete]
					Third,

					Fourth,
				}
				""",
		],

		[
			"enum members with a documentation comment",

			"""
				enum E
				{
					First,

					Second,

					/// <summary>The third member.</summary>
					Third,

					Fourth,
				}
				""",
		],

		[
			"top-level statements, which the statement rule judges",

			"""
				DoThing();

				DoThing();

				DoThing();

				DoThing();
				""",
		],

		[
			"imports, which are no units",

			"""
				using System;

				using System.Linq;

				using System.Text;

				using System.Threading;
				""",
		],

		[
			"an import and members, with the import set apart",

			"""
				using System;

				delegate void First();

				delegate void Second();
				""",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(LoosePlaces),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task FourLooseMembersAreGatheredAtTheDefaultLimit(
		string name,
		string source,
		string fixedSource
	)
	{
		AnalyzerRunResult result = await RunAsync(source);

		Assert.HasCount(3, result.Diagnostics, name);
		RoslynTestHarness.AssertFixedText(fixedSource, result, name);
	}

	[TestMethod]
	[DynamicData(
		nameof(QuietPlaces),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AGapThatEndsARunLeavesTheMembersAsWritten(string name, string source)
	{
		AnalyzerRunResult result = await RunAsync(source);
		Assert.IsEmpty(result.Diagnostics, name);
	}

	// Both gaps of the run hold the bounds of the run, so the fix of any gap gathers it whole.
	[TestMethod]
	public async Task TheFixOfAnyGapRemovesTheBlankLinesOfTheWholeRun()
	{
		const string source = """
			class C
			{
				int first;

				int second;

				int third;

				int fourth;

				void Run()
				{
					DoThing();
				}

				int fifth;

				int sixth;
			}
			""";

		const string fixedSource = """
			class C
			{
				int first;
				int second;
				int third;
				int fourth;

				void Run()
				{
					DoThing();
				}

				int fifth;

				int sixth;
			}
			""";

		string[] alone = await RoslynTestHarness.ApplyEachFixAloneAsync(
			source,
			DiagnosticDescriptors.GatherRunOfLooseMembers,
			Rules()
		);

		Assert.AreSequenceEqual([fixedSource, fixedSource, fixedSource], alone);

		AnalyzerRunResult all = await RoslynTestHarness.RunFixAllAsync(source, Rules());

		RoslynTestHarness.AssertFixedText(fixedSource, all);
	}

	// The bounds are indexes, so only the container tells the runs of two types apart.
	[TestMethod]
	public async Task TwoTypesThatHoldRunsOfTheSameBoundsAreBothGathered()
	{
		const string source = """
			class One
			{
				int first;

				int second;

				int third;

				int fourth;
			}

			class Two
			{
				int first;

				int second;

				int third;

				int fourth;
			}
			""";

		const string fixedSource = """
			class One
			{
				int first;
				int second;
				int third;
				int fourth;
			}

			class Two
			{
				int first;
				int second;
				int third;
				int fourth;
			}
			""";

		AnalyzerRunResult result = await RunAsync(source);

		Assert.HasCount(6, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("4", 0)]
	[DataRow("3", 3)]
	[DataRow("2", 3)]
	[DataRow("1", 3)]
	[DataRow("0", 0)]
	public async Task TheLimitDecidesWhichRunsAreGathered(string value, int expected)
	{
		const string source = """
			class C
			{
				int first;

				int second;

				int third;

				int fourth;
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source, LimitOf(value));

		Assert.HasCount(expected, result.Diagnostics, value);
	}

	// A smaller limit gathers a shorter run.
	[TestMethod]
	public async Task ARunOfTwoIsGatheredAtALimitOfOne()
	{
		const string source = """
			class C
			{
				int first;

				int second;

				void Run()
				{
					DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				int first;
				int second;

				void Run()
				{
					DoThing();
				}
			}
			""";

		AnalyzerRunResult result = await RunAsync(
			source,
			new Dictionary<string, string> { [LimitKey] = "1" }
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The two-unit rule counts every sibling of the container, so imports and statements make it a larger one.
	[TestMethod]
	public async Task ATwoUnitContainerBelongsToTheTwoUnitRuleWhereItIsOn()
	{
		const string source = """
			class C
			{
				int first;

				int second;
			}
			""";

		AnalyzerRunResult off = await ReportAsync(source, LimitOf("1"));

		AnalyzerRunResult on = await RoslynTestHarness.ReportAsync<GatherRunOfLooseMembersAnalyzer>(
			source,
			Rules(DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether),
			LimitOf("1")
		);

		Assert.HasCount(1, off.Diagnostics, "The two-unit rule is off.");
		Assert.IsEmpty(on.Diagnostics, "The two-unit rule is on.");
	}

	[TestMethod]
	public async Task AContainerWhollyInsideAConditionalIsGatheredInTheViewThatHoldsIt()
	{
		const string type = """
			class C
			{
			#if B
				int first;

				int second;

				int third;
			#endif
			}
			""";

		const string typeFixed = """
			class C
			{
			#if B
				int first;
				int second;
				int third;
			#endif
			}
			""";

		const string file = """
			#if B
			delegate void First();

			delegate void Second();

			delegate void Third();
			#endif
			""";

		const string fileFixed = """
			#if B
			delegate void First();
			delegate void Second();
			delegate void Third();
			#endif
			""";

		foreach (
			(string source, string fixedSource) in new[] { (type, typeFixed), (file, fileFixed) }
		)
		{
			AnalyzerRunResult held = await RunAsync("#define B\n" + source, LimitOf("1"));

			Assert.IsGreaterThan(0, held.Diagnostics.Length, "view with B");
			RoslynTestHarness.AssertFixedText("#define B\n" + fixedSource, held, "view with B");

			AnalyzerRunResult hidden = await RunAsync(source, LimitOf("1"));

			Assert.IsEmpty(hidden.Diagnostics, "view without B");
			RoslynTestHarness.AssertFixedText(source, hidden, "view without B");

			foreach (string view in new[] { "", "#define B\n" })
			{
				AnalyzerRunResult settled = await RunAsync(view + fixedSource, LimitOf("1"));
				Assert.IsEmpty(settled.Diagnostics, $"the fixed text in the view [{view.Trim()}]");
			}
		}
	}

	// The gathering and the grouping rules settle on one text from the top and from the bottom, in the view that holds the conditional.
	[TestMethod]
	public async Task AContainerWhollyInsideAConditionalSettlesInEveryFixOrder()
	{
		const string type = """
			#define B
			class C
			{
			#if B
				int first;

				int second;

				int third;
			#endif
			}
			""";

		const string typeFixed = """
			#define B
			class C
			{
			#if B
				int first;
				int second;
				int third;
			#endif
			}
			""";

		const string file = """
			#define B
			#if B
			delegate void First();

			delegate void Second();

			delegate void Third();
			#endif
			""";

		const string fileFixed = """
			#define B
			#if B
			delegate void First();
			delegate void Second();
			delegate void Third();
			#endif
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rules = Rules(
			DiagnosticDescriptors.SeparateMemberGroups,
			DiagnosticDescriptors.SeparateModifierGroups,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether
		);

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			expected: typeFixed,
			type,
			WithLimit(rules, limit: "1")
		);

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			expected: fileFixed,
			file,
			WithLimit(rules, limit: "1")
		);
	}

	[TestMethod]
	public async Task AFileOfImportsAndTwoMembersIsNoTwoUnitContainer()
	{
		const string source = """
			using System;

			delegate void First();

			delegate void Second();
			""";

		const string fixedSource = """
			using System;

			delegate void First();
			delegate void Second();
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<GatherRunOfLooseMembersAnalyzer>(
				source,
				Rules(DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether),
				new Dictionary<string, string> { [LimitKey] = "1" }
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The two-unit rule and this one settle on one text in either fix order.
	[TestMethod]
	public async Task TheTwoUnitRuleAndTheMemberRuleSettleInEitherFixOrder()
	{
		const string source = """
			class C
			{
				int first;

				int second;
			}
			""";

		const string fixedSource = """
			class C
			{
				int first;
				int second;
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = new(
			Rules(DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether)
		);

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			expected: fixedSource,
			source,
			WithLimit(rules, limit: "1")
		);
	}

	// The gap below the imports belongs to the import section rule, and the members start below it.
	[TestMethod]
	public async Task TheGapBelowTheImportsIsNoGapOfARun()
	{
		const string source = """
			using System;

			delegate void First();

			delegate void Second();

			delegate void Third();
			""";

		const string fixedSource = """
			using System;

			delegate void First();
			delegate void Second();
			delegate void Third();
			""";

		Dictionary<string, ReportDiagnostic> rules = new(Rules())
		{
			[DiagnosticDescriptors.SeparateImportSection.Id] = ReportDiagnostic.Suppress,
		};

		AnalyzerRunResult result = await RunAsync(source, rules, LimitOf("1"));

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheImportSectionRuleAndTheMemberRuleSettleInEitherFixOrder()
	{
		const string source = """
			using System;
			delegate void First();

			delegate void Second();

			delegate void Third();
			""";

		const string fixedSource = """
			using System;

			delegate void First();
			delegate void Second();
			delegate void Third();
			""";

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			expected: fixedSource,
			source,
			WithLimit(Rules(), limit: "1")
		);
	}

	[TestMethod]
	public async Task TheFileScopedNamespaceRuleAndTheMemberRuleSettleInEitherFixOrder()
	{
		const string source = """
			namespace N;
			delegate void First();

			delegate void Second();

			delegate void Third();
			""";

		const string fixedSource = """
			namespace N;

			delegate void First();
			delegate void Second();
			delegate void Third();
			""";

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			expected: fixedSource,
			source,
			WithLimit(Rules(), limit: "1")
		);
	}

	// A global statement is a statement: ZA0052 judges it, and this rule reads the types below it.
	[TestMethod]
	public async Task ATopLevelStatementIsNoMember()
	{
		const string source = """
			DoThing();

			DoThing();

			DoThing();

			DoThing();

			delegate void First();

			delegate void Second();

			delegate void Third();

			delegate void Fourth();
			""";

		const string fixedSource = """
			DoThing();

			DoThing();

			DoThing();

			DoThing();

			delegate void First();
			delegate void Second();
			delegate void Third();
			delegate void Fourth();
			""";

		AnalyzerRunResult result = await RunAsync(source);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The statement rule and the member rule share the limit, and each reads its own part of the file.
	public static IEnumerable<object[]> StatementsAndTypes { get; } =
	[
		[
			"3",

			"""
				using System;

				DoThing();

				DoThing();

				DoThing();

				DoThing();

				delegate void First();

				delegate void Second();

				delegate void Third();

				delegate void Fourth();
				""",

			"""
				using System;

				DoThing();
				DoThing();
				DoThing();
				DoThing();

				delegate void First();
				delegate void Second();
				delegate void Third();
				delegate void Fourth();
				""",
		],

		[
			"2",

			"""
				using System;

				DoThing();

				DoThing();

				DoThing();

				delegate void First();

				delegate void Second();

				delegate void Third();
				""",

			"""
				using System;

				DoThing();
				DoThing();
				DoThing();

				delegate void First();
				delegate void Second();
				delegate void Third();
				""",
		],

		[
			"1",

			"""
				using System;

				DoThing();

				DoThing();

				delegate void First();

				delegate void Second();
				""",

			"""
				using System;

				DoThing();
				DoThing();

				delegate void First();
				delegate void Second();
				""",
		],

		[
			"0",

			"""
				using System;

				DoThing();

				DoThing();

				DoThing();

				DoThing();

				delegate void First();

				delegate void Second();

				delegate void Third();

				delegate void Fourth();
				""",

			"""
				using System;

				DoThing();

				DoThing();

				DoThing();

				DoThing();

				delegate void First();

				delegate void Second();

				delegate void Third();

				delegate void Fourth();
				""",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(StatementsAndTypes),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task BothLooseRunRulesSettleOnAFileOfStatementsAndTypes(
		string limit,
		string source,
		string fixedSource
	)
	{
		Dictionary<string, ReportDiagnostic> rules = new(
			Rules(DiagnosticDescriptors.GatherRunOfLooseStatements)
		);

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			expected: fixedSource,
			source,
			options: WithLimit(rules, limit),
			name: $"Limit {limit}"
		);
	}

	// Each rule alone leaves the other kind of unit as written.
	[TestMethod]
	public async Task EachLooseRunRuleGathersOnlyItsOwnPartOfAFile()
	{
		const string source = """
			DoThing();

			DoThing();

			DoThing();

			DoThing();

			delegate void First();

			delegate void Second();

			delegate void Third();

			delegate void Fourth();
			""";

		const string statementsGathered = """
			DoThing();
			DoThing();
			DoThing();
			DoThing();

			delegate void First();

			delegate void Second();

			delegate void Third();

			delegate void Fourth();
			""";

		const string membersGathered = """
			DoThing();

			DoThing();

			DoThing();

			DoThing();

			delegate void First();
			delegate void Second();
			delegate void Third();
			delegate void Fourth();
			""";

		AnalyzerRunResult statements =
			await RoslynTestHarness.RunAsync<GatherRunOfLooseStatementsAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.GatherRunOfLooseStatements)
			);

		AnalyzerRunResult members = await RunAsync(source);

		Assert.HasCount(3, statements.Diagnostics);
		RoslynTestHarness.AssertFixedText(statementsGathered, statements);

		Assert.HasCount(3, members.Diagnostics);
		RoslynTestHarness.AssertFixedText(membersGathered, members);
	}

	// A run that crosses no rule's border stays whole, whatever the members around it hold.
	[TestMethod]
	public async Task AFieldRunAndAMethodRunAreEachGatheredAndKeepTheGapBetweenThem()
	{
		const string source = """
			interface I
			{
				int First { get; }

				int Second { get; }

				void Third();

				void Fourth();

				void Fifth();

				void Sixth();
			}
			""";

		const string fixedSource = """
			interface I
			{
				int First { get; }

				int Second { get; }

				void Third();
				void Fourth();
				void Fifth();
				void Sixth();
			}
			""";

		AnalyzerRunResult result = await RunAsync(source);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// Gathering the run would make the member group rule ask for a blank line that is not there now.
	[TestMethod]
	public async Task AGapThatGatheringWouldHaveTheMemberGroupRuleAskForStaysBlank()
	{
		const string source = """
			interface I
			{
				int First { get; }
				void Second();

				void Third();

				void Fourth();

				void Fifth();
			}
			""";

		AnalyzerRunResult result = await RunAsync(source);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AMemberTightAgainstItsNeighborStaysOutOfTheRun()
	{
		const string source = """
			class C
			{
				void Run()
				{
					DoThing();
				}

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
				void Run()
				{
					DoThing();
				}

				int first;
				int second;

				int third;
				int fourth;
				int fifth;
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
	public async Task AMultiLineMemberKeepsItsGaps()
	{
		const string source = """
			class C
			{
				int first;

				int second;

				int third;

				int fourth;

				void Run()
				{
					DoThing();
				}

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

				void Run()
				{
					DoThing();
				}

				int fifth;
			}
			""";

		AnalyzerRunResult result = await RunAsync(source);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// A documentation comment belongs to its member, so the blank line above it ends the run above it.
	[TestMethod]
	public async Task ADocumentedMemberEndsTheRunAboveIt()
	{
		const string source = """
			class C
			{
				int first;

				int second;

				int third;

				/// <summary>The fourth field.</summary>
				int fourth;

				int fifth;

				int sixth;

				int seventh;

				int eighth;
			}
			""";

		const string fixedSource = """
			class C
			{
				int first;
				int second;
				int third;

				/// <summary>The fourth field.</summary>
				int fourth;
				int fifth;
				int sixth;
				int seventh;
				int eighth;
			}
			""";

		AnalyzerRunResult result = await RunAsync(
			source,
			new Dictionary<string, string> { [LimitKey] = "2" }
		);

		Assert.HasCount(6, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The same-type rule asks for a blank line around a run of one type, so the documented fields keep their gaps.
	[TestMethod]
	public async Task TheDeclarationGroupRulesKeepTheGapsOfDocumentedMembers()
	{
		const string source = """
			class C
			{
				/// <summary>The first field.</summary>
				int first;

				/// <summary>The second field.</summary>
				int second;

				/// <summary>The third field.</summary>
				string third;

				/// <summary>The fourth field.</summary>
				string fourth;
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = new(
			Rules(
				DiagnosticDescriptors.SeparateMemberGroups,
				DiagnosticDescriptors.SeparateModifierGroups,
				DiagnosticDescriptors.SeparateSameTypeDeclarationRuns
			)
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,
			rules,
			LimitOf("1")
		);

		RoslynTestHarness.AssertFixedText(source, result);
	}

	// A modifier group rule that is on asks for a blank line between the static and the instance fields.
	[TestMethod]
	public async Task AGapBetweenModifierGroupsStaysBlankWhereTheModifierRuleIsOn()
	{
		const string source = """
			class C
			{
				static int first;

				static int second;

				int third;

				int fourth;
			}
			""";

		const string grouped = """
			class C
			{
				static int first;
				static int second;

				int third;
				int fourth;
			}
			""";

		const string gathered = """
			class C
			{
				static int first;
				static int second;
				int third;
				int fourth;
			}
			""";

		Dictionary<string, ReportDiagnostic> modifiers = new(
			Rules(DiagnosticDescriptors.SeparateModifierGroups)
		);

		AnalyzerRunResult on = await RoslynTestHarness.RunIterativelyAsync(
			source,
			modifiers,
			LimitOf("1")
		);

		RoslynTestHarness.AssertFixedText(grouped, on, "The modifier rule is on.");

		AnalyzerRunResult off = await RoslynTestHarness.RunIterativelyAsync(
			source,
			Rules(),
			LimitOf("1")
		);

		RoslynTestHarness.AssertFixedText(gathered, off, "The modifier rule is off.");
	}

	// A pragma over the members silences the rule.
	[TestMethod]
	public async Task APragmaOverTheRuleSilencesIt()
	{
		const string source = """
			class C
			{
			#pragma warning disable ZA0053
				int first;

				int second;

				int third;

				int fourth;
			#pragma warning restore ZA0053
			}
			""";

		AnalyzerRunResult result = await RunAsync(source);

		Assert.IsEmpty(result.Diagnostics);
	}

	// The jump rule judges statements only, so a rule that judges members reads no jump.
	[TestMethod]
	public async Task TheJumpRuleBeingOnChangesNothingForMembers()
	{
		const string source = """
			class C
			{
				int first;

				int second;

				int third;

				int fourth;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<GatherRunOfLooseMembersAnalyzer>(
				source,
				Rules(DiagnosticDescriptors.SeparateJumpStatement)
			);

		Assert.HasCount(3, result.Diagnostics);
	}

	// A member among the statements is a compile error that the rule must still read without a crash.
	[TestMethod]
	public async Task StatementsAmongTheMembersAreReadWithoutACrash()
	{
		const string source = """
			delegate void First();

			delegate void Second();

			delegate void Third();

			delegate void Fourth();

			DoThing();

			delegate void Fifth();

			delegate void Sixth();
			""";

		ImmutableArray<Diagnostic> diagnostics = await RoslynTestHarness.RunWithoutCompilingAsync(
			source,
			Rules()
		);

		string id = DiagnosticDescriptors.GatherRunOfLooseMembers.Id;

		Assert.HasCount(3, diagnostics.Where(diagnostic => diagnostic.Id == id));
	}

	private static Dictionary<string, string> LimitOf(string value) => new() { [LimitKey] = value };

	private static IReadOnlyDictionary<string, ReportDiagnostic> Rules(
		params DiagnosticDescriptor[] alsoOn
	)
	{
		return TestRuleSets.Only([DiagnosticDescriptors.GatherRunOfLooseMembers, .. alsoOn]);
	}

	private static RunOptions WithLimit(
		IReadOnlyDictionary<string, ReportDiagnostic> rules,
		string limit
	)
	{
		return new RunOptions(RoslynTestHarness.AllAnalyzers)
		{
			DiagnosticOptions = rules,
			AnalyzerConfigOptions = LimitOf(limit),
		};
	}

	private static Task<AnalyzerRunResult> RunAsync(
		string source,
		IReadOnlyDictionary<string, string>? options = null
	)
	{
		return RunAsync(source, Rules(), options);
	}

	private static Task<AnalyzerRunResult> ReportAsync(
		string source,
		IReadOnlyDictionary<string, string> options
	)
	{
		return RoslynTestHarness.ReportAsync<GatherRunOfLooseMembersAnalyzer>(
			source,
			Rules(),
			analyzerConfigOptions: options
		);
	}

	private static Task<AnalyzerRunResult> RunAsync(
		string source,
		IReadOnlyDictionary<string, ReportDiagnostic> rules,
		IReadOnlyDictionary<string, string>? options
	)
	{
		return RoslynTestHarness.RunAsync<GatherRunOfLooseMembersAnalyzer>(
			source,
			rules,
			analyzerConfigOptions: options
		);
	}
}
