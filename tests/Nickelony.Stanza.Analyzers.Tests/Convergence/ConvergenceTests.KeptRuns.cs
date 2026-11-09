using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Convergence;

public sealed partial class ConvergenceTests
{
	// Both views of a file, since a conditional in a block gives each view runs of its own.
	public static IEnumerable<object[]> Views { get; } =
	[
		["a file without the conditional view", ""],
		["a file with the conditional view", "#define VIEW\n"],
	];

	// The statements read assignments as one kind or by their target, which splits the runs of fields and properties.
	public static IEnumerable<object[]> ViewsAndTargets { get; } =
	[
		["a file without the conditional view, assignments of one kind", "", "mixed"],
		["a file without the conditional view, assignments by target", "", "split"],
		["a file with the conditional view, assignments of one kind", "#define VIEW\n", "mixed"],
		["a file with the conditional view, assignments by target", "#define VIEW\n", "split"],
	];

	// The statements that the keep rule joins, beside the lone and tight statements that the joined run regroups.
	// `_start` and the property written through it share a root, which joins them where assignments split by target.
	// The first block sits whole in a conditional, the second holds a conditional statement, and neither is judged for all views.
	private const string KeptStatementsSource = """
		class C
		{
			int _count;
			int Total { get; set; }
			System.Text.StringBuilder _start, _end;

			async System.Threading.Tasks.Task M(int value)
			{
				int width = 0;

				int height = 0;
				string name = "";

				string title = "";

				DoThing();
				DoThing();

				Build(1);


				Build(2);
				Build(3);

				var first = 1;

				var second = 2;
				Assert.AreEqual(1, first);

				Assert.AreEqual(2, second);
				await Load();

				await Save();
				_count = 1;

				_count = 2;
				Total = 3;

				Total = 4;
				_start = new System.Text.StringBuilder();

				_start.Capacity = 1;
				_end = new System.Text.StringBuilder();
				_end.Capacity = 2;
				Events.Changed += DoThing;

				Events.Closed += DoThing;
				return;
			}

			void Whole()
			{
		#if VIEW
				int a = 0;

				int b = 0;
				string c = "";
		#endif
			}

			void Mixed()
			{
		#if VIEW
				DoThing();
		#endif
				int d = 0;

				int e = 0;
				string f = "";
			}

			void Crossing()
			{
				DoThing();

				DoThing();
				using IDisposable first = Open();
				IDisposable second = Open();
				string third = "";
				Build(1);
			}
		}
		""";

	// The modifier rule and the same-type rule read runs of fields that cross, so a blank line of one ends the run of the other.
	private const string KeptCrossingMembersSource = """
		class E
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

	// The crossing members repeated, each group with its own names, so the crossings of one group sit beside those of the next.
	private static string BuildKeptCrossingMembersSource(int groups)
	{
		string members = string.Concat(
			Enumerable
				.Range(0, groups)
				.Select(index =>
					$"\tpublic void First{index}() {{ }}\n\n\tpublic void Second{index}() {{ }}\n\n\tpublic void Third{index}() {{ }}\n\n\tpublic void Fourth{index}() {{ }}\n\tinternal string _name{index};\n\tprivate string _title{index};\n\tevent System.Action Changed{index};\n"
				)
		);

		return "class E\n{\n" + members + "}\n";
	}

	// Joined members regroup for the member, modifier, declaration and same-type rules alike.
	private const string KeptMembersSource = """
		class C
		{
			private int _first;

			private int _second;
			public int Third;

			public int Fourth;
			public string Name { get; set; }

			public string Title { get; set; }
			public int Count { get; set; }

			public static int Shared;

			public static int Other;
			const int Limit = 1;

			const int Cap = 2;
			public string Label;

			public int Size;
			public string Tag;
			public void Run() { }

			public void Stop() { }
			private void Reset() { }
		}

		class D
		{
		#if VIEW
			int _a;

			int _b;
			string _c;
		#endif
		}
		""";

	// A statement rule reads the runs of its own kind and the neighbors beside them, so the rules sit in two groups of the kinds that meet.
	// The declaration and call rules meet the assertion and await ones, and the assertion, await, assignment and subscription rules meet each other.
	private static readonly DiagnosticDescriptor[] s_keptDeclarationAndCallReaders =
	[
		DiagnosticDescriptors.SeparateDeclarationRuns,
		DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
		DiagnosticDescriptors.SeparateInvocationRuns,
		DiagnosticDescriptors.SeparateAssertionRuns,
		DiagnosticDescriptors.SeparateAwaitRuns,
	];

	private static readonly DiagnosticDescriptor[] s_keptAssertionAndAssignmentReaders =
	[
		DiagnosticDescriptors.SeparateAssertionRuns,
		DiagnosticDescriptors.SeparateAwaitRuns,
		DiagnosticDescriptors.SeparateEventSubscriptionRuns,
		DiagnosticDescriptors.SeparateAssignmentRuns,
	];

	// The member rules the keep rule reads, with the declaration rules that ask for the gaps of fields.
	private static readonly DiagnosticDescriptor[] s_keptMemberReaders =
	[
		DiagnosticDescriptors.SeparateMemberGroups,
		DiagnosticDescriptors.SeparateModifierGroups,
		DiagnosticDescriptors.SeparateDeclarationRuns,
		DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
	];

	// The gather rules beside the keep rule, and the cap of blank lines in a row, which all remove the same gaps.
	private static readonly DiagnosticDescriptor[] s_keptStatementReadersBesideGather =
	[
		DiagnosticDescriptors.SeparateDeclarationRuns,
		DiagnosticDescriptors.SeparateInvocationRuns,
		DiagnosticDescriptors.SeparateAssignmentRuns,
		DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
	];

	// The kinds the gather matrix leaves out, which an await or a subscription makes: their states cost as much again.
	private static readonly DiagnosticDescriptor[] s_keptAwaitAndSubscriptionReadersBesideGather =
	[
		DiagnosticDescriptors.SeparateDeclarationRuns,
		DiagnosticDescriptors.SeparateAwaitRuns,
		DiagnosticDescriptors.SeparateEventSubscriptionRuns,
		DiagnosticDescriptors.SeparateAssignmentRuns,
	];

	// The rules that end a stanza or keep a gap, which a joined run can leave to another rule.
	private static readonly DiagnosticDescriptor[] s_keptGuardJumpAndBaseCallReaders =
	[
		DiagnosticDescriptors.SeparateInvocationRuns,
		DiagnosticDescriptors.SeparateJumpStatement,
		DiagnosticDescriptors.SeparateBaseCall,
		DiagnosticDescriptors.SeparateGuardClauses,
	];

	private static readonly DiagnosticDescriptor[] s_keptBesideStatements =
	[
		DiagnosticDescriptors.GatherRunOfLooseStatements,
		DiagnosticDescriptors.CollapseExtraBlankLines,
	];

	private static readonly DiagnosticDescriptor[] s_keptBesideMembers =
	[
		DiagnosticDescriptors.GatherRunOfLooseMembers,
		DiagnosticDescriptors.CollapseExtraBlankLines,
	];

	// The guard, the base call and the jump each end a stanza at a gap that the joined run can ask for as well.
	private const string KeptGuardsSource = """
		class B
		{
			public virtual void M() { }
		}

		class C : B
		{
			public override void M()
			{
				base.M();
				Do(1);

				Do(2);
				Do(3);
			}

			void Guard(string text)
			{
				if (text == null) return;
				Do(1);

				Do(2);
			}

			int Jump()
			{
				Do(1);

				Do(2);
				return 1;
			}

			void Do(int value) { }
		}
		""";

	// Each rule the keep rule reads on or off, with the keep rule, so every state writes one text in every fix order.
	// The rules sit in two groups, because the states of both together would outlast the test timeout on a slow runner.
	// No assignment rule is among these, so the setting that splits assignments changes nothing.
	[TestMethod]
	[DynamicData(
		nameof(Views),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public Task KeptRunsOfStatementsSettleOnOneTextInEveryStateOfTheDeclarationAndCallRules(
		string name,
		string view
	)
	{
		return AssertKeptRunsSettleAsync(
			source: view + KeptStatementsSource,
			readers: s_keptDeclarationAndCallReaders,
			keep: DiagnosticDescriptors.KeepRunOfStatementsTogether,
			beside: [],
			targets: "mixed",
			name
		);
	}

	[TestMethod]
	[DynamicData(
		nameof(ViewsAndTargets),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public Task KeptRunsOfStatementsSettleOnOneTextInEveryStateOfTheAssertionAwaitAndAssignmentRules(
		string name,
		string view,
		string targets
	)
	{
		return AssertKeptRunsSettleAsync(
			source: view + KeptStatementsSource,
			readers: s_keptAssertionAndAssignmentReaders,
			keep: DiagnosticDescriptors.KeepRunOfStatementsTogether,
			beside: [],
			targets,
			name
		);
	}

	// The guard, jump and base call rules ask for the gap beside a stanza, which a joined run changes.
	[TestMethod]
	public Task KeptRunsOfStatementsSettleOnOneTextInEveryStateOfTheGuardJumpAndBaseCallRules()
	{
		return AssertKeptRunsSettleAsync(
			source: KeptGuardsSource,
			readers: s_keptGuardJumpAndBaseCallReaders,
			keep: DiagnosticDescriptors.KeepRunOfStatementsTogether,
			beside: [],
			targets: "mixed"
		);
	}

	// The same states, with the gather rules and the cap on beside the keep rule at a limit that has them gather every run.
	[TestMethod]
	[DynamicData(
		nameof(ViewsAndTargets),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public Task KeptRunsOfStatementsSettleBesideTheGatherRules(
		string name,
		string view,
		string targets
	)
	{
		return AssertKeptRunsSettleAsync(
			source: view + KeptStatementsSource,
			readers: s_keptStatementReadersBesideGather,
			keep: DiagnosticDescriptors.KeepRunOfStatementsTogether,
			beside: s_keptBesideStatements,
			targets,
			name
		);
	}

	[TestMethod]
	[DynamicData(
		nameof(ViewsAndTargets),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public Task KeptRunsOfStatementsSettleBesideTheGatherRulesWithTheAwaitAndSubscriptionRules(
		string name,
		string view,
		string targets
	)
	{
		return AssertKeptRunsSettleAsync(
			source: view + KeptStatementsSource,
			readers: s_keptAwaitAndSubscriptionReadersBesideGather,
			keep: DiagnosticDescriptors.KeepRunOfStatementsTogether,
			beside: s_keptBesideStatements,
			targets,
			name
		);
	}

	[TestMethod]
	[DynamicData(
		nameof(Views),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public Task KeptRunsOfMembersSettleOnOneTextInEveryStateOfTheRulesTheyRead(
		string name,
		string view
	)
	{
		return AssertKeptRunsSettleAsync(
			source: view + KeptMembersSource,
			readers: s_keptMemberReaders,
			keep: DiagnosticDescriptors.KeepRunOfMembersTogether,
			beside: [],
			targets: "mixed",
			name
		);
	}

	[TestMethod]
	public Task KeptRunsOfMembersSettleOnOneTextWhereTheRunsOfTwoRulesCross()
	{
		return AssertKeptRunsSettleAsync(
			source: KeptCrossingMembersSource,
			readers: s_keptMemberReaders,
			keep: DiagnosticDescriptors.KeepRunOfMembersTogether,
			beside: [],
			targets: "mixed"
		);
	}

	[TestMethod]
	[DataRow(2)]
	[DataRow(10)]
	public Task KeptRunsOfMembersSettleOnOneTextWhereManyRunsOfTwoRulesCross(int groups)
	{
		return AssertKeptRunsSettleAsync(
			source: BuildKeptCrossingMembersSource(groups),
			readers: s_keptMemberReaders,
			keep: DiagnosticDescriptors.KeepRunOfMembersTogether,
			beside: [],
			targets: "mixed"
		);
	}

	[TestMethod]
	[DynamicData(
		nameof(Views),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public Task KeptRunsOfMembersSettleBesideTheGatherRules(string name, string view)
	{
		return AssertKeptRunsSettleAsync(
			source: view + KeptMembersSource,
			readers: s_keptMemberReaders,
			keep: DiagnosticDescriptors.KeepRunOfMembersTogether,
			beside: s_keptBesideMembers,
			targets: "mixed",
			name
		);
	}

	private static async Task AssertKeptRunsSettleAsync(
		string source,
		DiagnosticDescriptor[] readers,
		DiagnosticDescriptor keep,
		DiagnosticDescriptor[] beside,
		string targets,
		string? name = null
	)
	{
		DiagnosticDescriptor[] toggled = [.. readers, keep];

		DiagnosticAnalyzer keepAnalyzer = RoslynTestHarness.AllAnalyzers.Single(analyzer =>
			analyzer.SupportedDiagnostics.Single().Id == keep.Id
		);

		Dictionary<string, string> options = new()
		{
			["stanza_max_loose_run_size"] = "1",
			["stanza_assignment_targets"] = targets,
		};

		int reporting = 0;

		await RoslynTestHarness.AssertEveryStateSettlesAsync(
			source,
			toggled,
			alwaysOn: beside,
			analyzerConfigOptions: options,

			inspect: async state =>
			{
				AnalyzerRunResult reported = await RoslynTestHarness.ReportAsync(source, state.Run);

				// The gather rules and the cap report a span of blank lines, which is no one gap, so only the toggled rules are judged.
				AnalyzerRunResult gaps = new(
					[
						.. reported.Diagnostics.Where(diagnostic =>
							toggled.Any(rule => rule.Id == diagnostic.Id)
						),
					],

					fixedText: string.Empty
				);

				if (gaps.Diagnostics.Length > 0)
					RoslynTestHarness.AssertEachGapIsClaimedOnce(gaps);

				if (reported.Diagnostics.Any(diagnostic => diagnostic.Id == keep.Id))
				{
					reporting++;

					// The fix of the keep rule alone leaves no gap for another rule that the source did not already leave.
					AnalyzerRunResult kept = await RoslynTestHarness.RunIterativelyAsync(
						source,

						new RunOptions([keepAnalyzer])
						{
							DiagnosticOptions = state.Rules,
							AnalyzerConfigOptions = options,
							BothEndings = state.Run.BothEndings,
						}
					);

					AnalyzerRunResult left = await RoslynTestHarness.ReportAsync(
						kept.FixedText,
						state.Run
					);

					AssertNothingNewIsLeft(
						before: reported,
						left,
						ignored: [keep, .. beside],
						label: state.Label
					);
				}
			}
		);

		Assert.IsGreaterThan(
			0,
			reporting,

			"No state reported the keep rule, so the matrix proves nothing."
				+ (name is null ? string.Empty : $" ({name})")
		);
	}

	// The gather rules and the cap are left out, because they answer any blank line a rule writes, whichever rule it is.
	// A report is placed by its token among the tokens of the file, which a blank line does not move,
	// and a gap that another rule takes over after the join is the same place, so only a gap that was not asked for fails.
	private static void AssertNothingNewIsLeft(
		AnalyzerRunResult before,
		AnalyzerRunResult left,
		DiagnosticDescriptor[] ignored,
		string label
	)
	{
		HashSet<int> standing = [.. ReadReportedPlaces(before, ignored)];

		foreach (int place in ReadReportedPlaces(left, ignored))
		{
			Assert.IsTrue(
				standing.Contains(place),
				$"{label}: the fix of {ignored[0].Id} left a report at token {place} for a second pass."
			);
		}
	}

	private static IEnumerable<int> ReadReportedPlaces(
		AnalyzerRunResult result,
		DiagnosticDescriptor[] ignored
	)
	{
		foreach (
			Diagnostic diagnostic in result.Diagnostics.Where(diagnostic =>
				ignored.All(rule => rule.Id != diagnostic.Id)
			)
		)
		{
			if (diagnostic.Location.SourceTree is not { } tree)
				continue;

			int start = diagnostic.Location.SourceSpan.Start;

			yield return tree.GetRoot().DescendantTokens().Count(token => token.SpanStart < start);
		}
	}
}
