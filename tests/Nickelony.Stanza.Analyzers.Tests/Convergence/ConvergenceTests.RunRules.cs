using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Convergence;

public sealed partial class ConvergenceTests
{
	// Each rule that shares a gap beside a base call on or off, so every state writes one text.
	[TestMethod]
	public async Task BaseCallsSettleOnOneTextInEveryStateOfTheRulesThatShareTheirGaps()
	{
		const string source = """
			class C : Stage
			{
				public override void OnStart()
				{
					if (Value == 0) return;
					base.OnStart();
					DoThing();

					Value = 1;

					Value = 2;
					return;
				}

				public override void OnStop()
				{
					DoThing();
					DoThing();
					base.OnStop();
				}

				public override void Clear()
				{
					base.DoThing();
					DoThing();
					Value = 0;
				}

				public override int Fetch()
				{
					if (Value == 0) return 0;
					if (Value == 1) return 1;
					base.Clear();
					return base.Fetch();
				}

				public override async System.Threading.Tasks.Task OnStartAsync()
				{
					await base.OnStartAsync();

					Value = 1;

					Value = 2;

					Value = 3;
				}

				public override void DoThing()
				{
					base.DoThing();
					Value = 1;
				}
			}
			""";

		// The base call rule stays on, since its gaps are what the states contest, and each sharing rule takes both states.
		DiagnosticDescriptor[] sharing =
		[
			DiagnosticDescriptors.SeparateJumpStatement,
			DiagnosticDescriptors.SeparateGuardClauses,
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateInvocationRuns,
		];

		Dictionary<string, string> options = new() { ["stanza_max_loose_run_size"] = "1" };

		await RoslynTestHarness.AssertEveryStateSettlesAsync(
			source,
			toggled: sharing,
			alwaysOn: [DiagnosticDescriptors.SeparateBaseCall],
			analyzerConfigOptions: options,

			inspect: async state =>
			{
				AnalyzerRunResult reported = await RoslynTestHarness.ReportAsync(source, state.Run);

				if (reported.Diagnostics.Length > 0)
					RoslynTestHarness.AssertEachGapIsClaimedOnce(reported);
			}
		);
	}

	// Each run rule on or off, the await rule among them, so every state of the kinds an await can take writes one text.
	[TestMethod]
	public async Task AwaitRunsSettleOnOneTextInEveryStateOfTheRunRules()
	{
		const string source = """
			class C
			{
				int _count;

				async System.Threading.Tasks.Task M()
				{
					DoThing();
					var first = 1;
					await Load(first);
					await Save();
					Assert.That(first);
					Assert.That(first);
					await Load();
					await Load();
					var second = 2;
					_count = second;
					_count = second;
					await Save();
					Events.Changed += DoThing;
					Events.Closed += DoThing;
					_ = await Load();
					await Load(1);
					var third = 3;
					Build(third);
					Build(third);
					await Save();
				}
			}
			""";

		await RoslynTestHarness.AssertEveryStateSettlesAsync(
			source,
			toggled: TestRuleSets.RunRules
		);
	}

	private static readonly Dictionary<string, string> s_splitOptions = new()
	{
		["stanza_assignment_targets"] = "split",
		["stanza_max_loose_run_size"] = "1",
		["stanza_guard_roots"] = "Guard",
	};

	// The run rules alone, so each state of the matrix below stays cheap while the rows below run every analyzer.
	private static readonly ImmutableArray<DiagnosticAnalyzer> s_runRuleAnalyzers =
	[
		new SeparateDeclarationRunsAnalyzer(),
		new SeparateInvocationRunsAnalyzer(),
		new SeparateAssertionRunsAnalyzer(),
		new SeparateAwaitRunsAnalyzer(),
		new SeparateEventSubscriptionRunsAnalyzer(),
		new SeparateAssignmentRunsAnalyzer(),
	];

	// Each run rule on or off, with the assignments split by their targets, so every owner of a gap the split adds writes one text.
	[TestMethod]
	public async Task SplitAssignmentRunsSettleOnOneTextInEveryStateOfTheRunRules()
	{
		const string source = """
			class C
			{
				int _count;
				int[] _items = new int[2];
				System.Text.StringBuilder _start, _end;

				int Count { get; set; }

				async System.Threading.Tasks.Task M(int total)
				{
					DoThing();
					_start = new System.Text.StringBuilder();
					_start.Capacity = 1;
					_end = new System.Text.StringBuilder();
					_end.Capacity = 2;
					_end.Length = 0;
					_count = 1;
					_count++;
					Count = 2;
					Count += 1;
					var first = 1;
					var second = 2;
					_items[0] = first;
					_items[1] = second;
					total = first;
					Events.Changed += DoThing;
					Events.Closed += DoThing;
					Count = total;
					(_count, total) = (1, 2);
					total++;
					total--;
					await Load();
					await Save();
					_count = await Load();
					Count = 3;
					Assert.That(1);
					Assert.That(2);
					_count = 4;
					Events.Changed -= DoThing;
					Count = 5;
					Build(1);
					Build(2);
				}
			}
			""";

		await RoslynTestHarness.AssertEveryStateSettlesAsync(
			source,
			toggled: TestRuleSets.RunRules,
			analyzers: s_runRuleAnalyzers,
			analyzerConfigOptions: s_splitOptions
		);
	}

	// The split adds gaps between runs of assignments, which each neighboring rule must leave or take in one way.
	public static IEnumerable<object[]> SplitAssignmentRows { get; } =
	[
		[
			"a gathered run keeps the gap between its fields and its properties",

			new[]
			{
				DiagnosticDescriptors.GatherRunOfLooseStatements,
				DiagnosticDescriptors.SeparateAssignmentRuns,
			},

			"""
				class C
				{
					int _a;

					int Count { get; set; }

					void M()
					{
						DoThing();

						_a = 1;

						_a = 2;

						Count = 3;

						Count = 4;
					}
				}
				""",

			"""
				class C
				{
					int _a;

					int Count { get; set; }

					void M()
					{
						DoThing();

						_a = 1;
						_a = 2;

						Count = 3;
						Count = 4;
					}
				}
				""",
		],

		[
			"a gathered run keeps a field with the members written through it, and splits it from the next root",

			new[]
			{
				DiagnosticDescriptors.GatherRunOfLooseStatements,
				DiagnosticDescriptors.SeparateAssignmentRuns,
			},

			"""
				class C
				{
					System.Text.StringBuilder _start, _end;

					void M()
					{
						DoThing();

						_start = new System.Text.StringBuilder();

						_start.Capacity = 1;

						_end = new System.Text.StringBuilder();

						_end.Capacity = 2;
					}
				}
				""",

			"""
				class C
				{
					System.Text.StringBuilder _start, _end;

					void M()
					{
						DoThing();

						_start = new System.Text.StringBuilder();
						_start.Capacity = 1;

						_end = new System.Text.StringBuilder();
						_end.Capacity = 2;
					}
				}
				""",
		],

		[
			"top-level statements and the members below them gather around the gap the split asks for",

			new[]
			{
				DiagnosticDescriptors.GatherRunOfLooseStatements,
				DiagnosticDescriptors.GatherRunOfLooseMembers,
				DiagnosticDescriptors.SeparateAssignmentRuns,
				DiagnosticDescriptors.SeparateMemberGroups,
				DiagnosticDescriptors.SeparateMultiLineConstructs,
			},

			"""
				Holder.Field = 1;

				Holder.Field = 2;

				Holder.Property = 3;

				Holder.Property = 4;

				internal static class Holder
				{
					public static int Field;

					public static int Other;

					public static int Third;

					public static int Property { get; set; }
				}
				""",

			"""
				Holder.Field = 1;
				Holder.Field = 2;

				Holder.Property = 3;
				Holder.Property = 4;

				internal static class Holder
				{
					public static int Field;
					public static int Other;
					public static int Third;

					public static int Property { get; set; }
				}
				""",
		],

		[
			"the gap above a jump below a split run is the split's",

			new[]
			{
				DiagnosticDescriptors.SeparateJumpStatement,
				DiagnosticDescriptors.SeparateAssignmentRuns,
			},

			"""
				class C
				{
					int _a;

					int Count { get; set; }

					void M()
					{
						_a = 1;
						_a = 2;
						Count = 3;
						Count = 4;
						return;
					}
				}
				""",

			"""
				class C
				{
					int _a;

					int Count { get; set; }

					void M()
					{
						_a = 1;
						_a = 2;

						Count = 3;
						Count = 4;

						return;
					}
				}
				""",
		],

		[
			"the gap below the guards stays the guard rule's above a split run",

			new[]
			{
				DiagnosticDescriptors.SeparateGuardClauses,
				DiagnosticDescriptors.SeparateAssignmentRuns,
			},

			"""
				class C
				{
					int _a;

					int Count { get; set; }

					void M(object items)
					{
						Guard.NotNull(items);
						_a = 1;
						_a = 2;
						Count = 3;
						Count = 4;
					}
				}
				""",

			"""
				class C
				{
					int _a;

					int Count { get; set; }

					void M(object items)
					{
						Guard.NotNull(items);

						_a = 1;
						_a = 2;

						Count = 3;
						Count = 4;
					}
				}
				""",
		],

		[
			"a commented field assignment is set apart by ZA0012 and the property run by the split",

			new[]
			{
				DiagnosticDescriptors.SeparateCommentedSibling,
				DiagnosticDescriptors.SeparateAssignmentRuns,
			},

			"""
				class C
				{
					int _a;

					int Count { get; set; }

					void M()
					{
						_a = 1;
						// The second.
						_a = 2;
						Count = 3;
						Count = 4;
					}
				}
				""",

			"""
				class C
				{
					int _a;

					int Count { get; set; }

					void M()
					{
						_a = 1;

						// The second.
						_a = 2;

						Count = 3;
						Count = 4;
					}
				}
				""",
		],

		[
			"a run of subscriptions between split runs is ZA0007's at both ends",

			new[]
			{
				DiagnosticDescriptors.SeparateEventSubscriptionRuns,
				DiagnosticDescriptors.SeparateAssignmentRuns,
			},

			"""
				class C
				{
					int _a;

					int Count { get; set; }

					void M()
					{
						_a = 1;
						_a = 2;
						Events.Changed += DoThing;
						Events.Closed += DoThing;
						Count = 3;
						Count = 4;
					}
				}
				""",

			"""
				class C
				{
					int _a;

					int Count { get; set; }

					void M()
					{
						_a = 1;
						_a = 2;

						Events.Changed += DoThing;
						Events.Closed += DoThing;

						Count = 3;
						Count = 4;
					}
				}
				""",
		],

		[
			"a lone subscription has no kind and joins the split runs on both sides",

			new[]
			{
				DiagnosticDescriptors.SeparateEventSubscriptionRuns,
				DiagnosticDescriptors.SeparateAssignmentRuns,
			},

			"""
				class C
				{
					int _a;

					int Count { get; set; }

					void M()
					{
						DoThing();
						_a = 1;
						Events.Changed += DoThing;
						Count = 3;
					}
				}
				""",

			"""
				class C
				{
					int _a;

					int Count { get; set; }

					void M()
					{
						DoThing();

						_a = 1;
						Events.Changed += DoThing;
						Count = 3;
					}
				}
				""",
		],

		[
			"a conditional block above the runs leaves the split gap to both views",

			new[]
			{
				DiagnosticDescriptors.SeparateDeclarationRuns,
				DiagnosticDescriptors.SeparateInvocationRuns,
				DiagnosticDescriptors.SeparateAssignmentRuns,
			},

			"""
				class C
				{
					int _a;

					int Count { get; set; }

					void M()
					{
						DoThing();
				#if VIEW
						DoThing();
				#endif
						_a = 1;
						_a = 2;
						Count = 3;
						Count = 4;
					}
				}
				""",

			"""
				class C
				{
					int _a;

					int Count { get; set; }

					void M()
					{
						DoThing();
				#if VIEW
						DoThing();
				#endif
						_a = 1;
						_a = 2;

						Count = 3;
						Count = 4;
					}
				}
				""",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(SplitAssignmentRows),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ASplitOfAssignmentsSettlesTheSameWayInEveryFixOrderAndView(
		string name,
		DiagnosticDescriptor[] rulesOn,
		string source,
		string fixedSource
	)
	{
		RunOptions options = new(RoslynTestHarness.AllAnalyzers)
		{
			DiagnosticOptions = TestRuleSets.OnSuppressingOthersOf(
				rulesOn,
				s_statementCorpus.ShippedOn
			),

			AnalyzerConfigOptions = s_splitOptions,
		};

		foreach (string view in new[] { string.Empty, "#define VIEW\n" })
		{
			string label = $"{name}, view [{view.Trim()}]";

			await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
				expected: view + fixedSource,
				source: view + source,
				options,
				name: label
			);
		}

		await AssertGatheringLeavesNoWorkAsync(source, s_splitOptions);
	}
}
