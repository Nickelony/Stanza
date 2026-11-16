using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed class ReceiverGroupInterplayTests
{
	// The calls as written: two pairs, a blank line inside each pair and none between them.
	private const string WrittenCalls = "item.A();\n\nitem.B();\nitem.child.A();\n\nitem.child.B();";

	// A blank line between every two calls.
	private const string SeparatedCalls = "item.A();\n\nitem.B();\n\nitem.child.A();\n\nitem.child.B();";

	// One blank line, between the calls on item and the calls on item.child.
	private const string GroupedCalls = "item.A();\nitem.B();\n\nitem.child.A();\nitem.child.B();";

	// No blank line between the calls.
	private const string TightCalls = "item.A();\nitem.B();\nitem.child.A();\nitem.child.B();";

	// The calls each set of rules writes, keyed by the receiver comparison and the enabled rules.
	private static readonly Dictionary<string, string> s_receiverPolicyCalls = new(StringComparer.Ordinal)
	{
		["root: none"] = WrittenCalls,
		["root: ZA0004"] = WrittenCalls,
		["root: ZA0019"] = WrittenCalls,
		["root: ZA0054"] = WrittenCalls,
		["root: ZA0063"] = TightCalls,
		["root: ZA0004,ZA0019"] = WrittenCalls,
		["root: ZA0004,ZA0054"] = TightCalls,
		["root: ZA0004,ZA0063"] = TightCalls,
		["root: ZA0019,ZA0054"] = WrittenCalls,
		["root: ZA0019,ZA0063"] = TightCalls,
		["root: ZA0054,ZA0063"] = TightCalls,
		["root: ZA0004,ZA0019,ZA0054"] = TightCalls,
		["root: ZA0004,ZA0019,ZA0063"] = TightCalls,
		["root: ZA0004,ZA0054,ZA0063"] = TightCalls,
		["root: ZA0019,ZA0054,ZA0063"] = TightCalls,
		["root: ZA0004,ZA0019,ZA0054,ZA0063"] = TightCalls,
		["path: none"] = WrittenCalls,
		["path: ZA0004"] = WrittenCalls,
		["path: ZA0019"] = SeparatedCalls,
		["path: ZA0054"] = WrittenCalls,
		["path: ZA0063"] = TightCalls,
		["path: ZA0004,ZA0019"] = SeparatedCalls,
		["path: ZA0004,ZA0054"] = GroupedCalls,
		["path: ZA0004,ZA0063"] = GroupedCalls,
		["path: ZA0019,ZA0054"] = SeparatedCalls,
		["path: ZA0019,ZA0063"] = GroupedCalls,
		["path: ZA0054,ZA0063"] = TightCalls,
		["path: ZA0004,ZA0019,ZA0054"] = GroupedCalls,
		["path: ZA0004,ZA0019,ZA0063"] = GroupedCalls,
		["path: ZA0004,ZA0054,ZA0063"] = GroupedCalls,
		["path: ZA0019,ZA0054,ZA0063"] = GroupedCalls,
		["path: ZA0004,ZA0019,ZA0054,ZA0063"] = GroupedCalls,
	};

	public static IEnumerable<object[]> ReceiverPolicies { get; } =
	[
		.. new[] { "root", "path" }.SelectMany(comparison =>
			TestRuleSets
				.SubsetRows(
					DiagnosticDescriptors.SeparateInvocationRuns,
					DiagnosticDescriptors.SeparateReceiverGroups,
					DiagnosticDescriptors.KeepRunOfStatementsTogether,
					DiagnosticDescriptors.KeepReceiverGroupTogether
				)
				.Select(row => new object[] { $"{comparison}: {row[0]}", comparison, row[1] })
		),
	];

	[TestMethod]
	[CrossCutting]
	[DynamicData(
		nameof(ReceiverPolicies),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ReceiverPoliciesWriteTheirGroupsInEveryCombinationAndFixOrder(
		string name,
		string comparison,
		DiagnosticDescriptor[] enabled
	)
	{
		string source = WrapCalls(WrittenCalls);
		string fixedSource = WrapCalls(s_receiverPolicyCalls[name]);

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			.. enabled,
		]);

		var options = new Dictionary<string, string>
		{
			["stanza_receiver_roots"] = "item",
			["stanza_receiver_comparison"] = comparison,
		};

		AnalyzerRunResult reported = await RoslynTestHarness.ReportAllAsync(source, rules, options);

		if (!reported.Diagnostics.IsEmpty)
			RoslynTestHarness.AssertEachGapIsClaimedOnce(reported);

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			expected: fixedSource,
			source,

			options: new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = rules,
				AnalyzerConfigOptions = options,
			},

			name
		);
	}

	private static string WrapCalls(string calls) =>
		"class C { void M(dynamic item) {\n" + calls + "\n} }";

	[TestMethod]
	[CrossCutting]
	[DataRow("ZA0019")]
	[DataRow("ZA0004")]
	public async Task APlannedReceiverBoundaryOpensTheLoneJumpBindingInTheFirstAnalysis(
		string separator
	)
	{
		const string source = """
			class C
			{
				int M(C db)
				{
					db.First();
					db.Second();
					int result = Compute();

					return result;
				}
				void First() { }
				void Second() { }
				int Compute() => 1;
			}
			""";

		const string fixedSource = """
			class C
			{
				int M(C db)
				{
					db.First();
					db.Second();

					int result = Compute();
					return result;
				}
				void First() { }
				void Second() { }
				int Compute() => 1;
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			separator == "ZA0019"
				? DiagnosticDescriptors.SeparateReceiverGroups
				: DiagnosticDescriptors.SeparateInvocationRuns,

			DiagnosticDescriptors.KeepJumpWithBinding,
		]);

		var options = new Dictionary<string, string> { ["stanza_receiver_roots"] = "db" };

		AnalyzerRunResult reported = await RoslynTestHarness.ReportAllAsync(source, rules, options);

		Assert.HasCount(2, reported.Diagnostics);

		Assert.HasCount(
			1,

			reported.Diagnostics.Where(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.KeepJumpWithBinding.Id
			)
		);

		RoslynTestHarness.AssertEachGapIsClaimedOnce(reported);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,
			rules,
			options
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			fixedSource,
			source,

			new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = rules,
				AnalyzerConfigOptions = options,
			}
		);
	}

	[TestMethod]
	[CrossCutting]
	[DataRow("ZA0019")]
	[DataRow("ZA0004")]
	public async Task APlannedReceiverBoundaryOpensInitializationInTheFirstAnalysis(
		string separator
	)
	{
		const string source = """
			class C
			{
				void M(C db)
				{
					db.First();
					db.Second();
					var client = new C();

					client.First();
					client.Second();
				}
				void First() { }
				void Second() { }
				int Compute() => 1;
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(C db)
				{
					db.First();
					db.Second();

					var client = new C();
					client.First();
					client.Second();
				}
				void First() { }
				void Second() { }
				int Compute() => 1;
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			separator == "ZA0019"
				? DiagnosticDescriptors.SeparateReceiverGroups
				: DiagnosticDescriptors.SeparateInvocationRuns,

			DiagnosticDescriptors.KeepInitializationWithCalls,
		]);

		var options = new Dictionary<string, string> { ["stanza_receiver_roots"] = "db, client" };

		AnalyzerRunResult reported = await RoslynTestHarness.ReportAllAsync(source, rules, options);

		Assert.HasCount(2, reported.Diagnostics);

		Assert.HasCount(
			1,

			reported.Diagnostics.Where(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.KeepInitializationWithCalls.Id
			)
		);

		RoslynTestHarness.AssertEachGapIsClaimedOnce(reported);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,
			rules,
			options
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			fixedSource,
			source,

			new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = rules,
				AnalyzerConfigOptions = options,
			}
		);
	}

	[TestMethod]
	[CrossCutting]
	[DataRow("guard")]
	[DataRow("loop")]
	[DataRow("assert")]
	public async Task GatheringDoesNotTemporarilyAbsorbAFocusedKeptGroup(string shape)
	{
		const string prefix = "var first = 1;\n\nvar second = 2;\n\nvar third = 3;\n\n";

		(string before, string after, DiagnosticDescriptor keeper) = shape switch
		{
			"guard" => (
				"var value = Get();\n\nif (value is null) return;\nUse(value);",
				"var value = Get();\nif (value is null) return;\nUse(value);",
				DiagnosticDescriptors.KeepGuardWithBinding
			),

			"loop" => (
				"var total = 0;\n\nforeach (var item in xs) { total += item; }\nUse(total);",
				"var total = 0;\nforeach (var item in xs) { total += item; }\nUse(total);",
				DiagnosticDescriptors.KeepAccumulatorWithLoop
			),

			_ => (
				"var value = Get();\n\nSystem.Diagnostics.Debug.Assert(value is not null);\nUse(value);",
				"var value = Get();\nSystem.Diagnostics.Debug.Assert(value is not null);\nUse(value);",
				DiagnosticDescriptors.KeepNarrowingAssertionWithBinding
			),
		};

		const string opening = "class C { void M(int[] xs) {\n";
		const string closing = "\n} object? Get() => null; void Use(object? value) {} }";

		string source = opening + prefix + before + closing;
		string fixedSource = opening + prefix + after + closing;

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			keeper,
		]);

		var options = new Dictionary<string, string> { ["stanza_max_loose_run_size"] = "3" };

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,
			rules,
			options
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			fixedSource,
			source,

			new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = rules,
				AnalyzerConfigOptions = options,
			}
		);
	}

	[TestMethod]
	[CrossCutting]
	public async Task JoinedStatementsDoNotAcquireReceiverGrouping()
	{
		const string source =
			"class C { void M(dynamic db) { db.First(); db.Second(); db.child.First(); db.child.Second(); } }";

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateReceiverGroups,
			DiagnosticDescriptors.KeepReceiverGroupTogether,
		]);

		var options = new Dictionary<string, string>
		{
			["stanza_receiver_roots"] = "*",
			["stanza_receiver_comparison"] = "path",
		};

		AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(source, rules, options);
		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	[CrossCutting]
	public async Task ReceiverKeepingOwnsEveryLooseGapBeforeGenericGathering()
	{
		const string source = """
			class C
			{
				void M(dynamic db)
				{
					db.First();

					db.Second();

					db.Third();

					db.Fourth();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(dynamic db)
				{
					db.First();
					db.Second();
					db.Third();
					db.Fourth();
				}
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.KeepReceiverGroupTogether,
		]);

		var options = new Dictionary<string, string> { ["stanza_receiver_roots"] = "db" };

		AnalyzerRunResult reported = await RoslynTestHarness.ReportAllAsync(source, rules, options);

		Assert.HasCount(3, reported.Diagnostics);

		Assert.IsTrue(
			reported.Diagnostics.All(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.KeepReceiverGroupTogether.Id
			)
		);

		RoslynTestHarness.AssertEachGapIsClaimedOnce(reported);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,
			rules,
			options
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	[DataRow("multi")]
	[DataRow("checked")]
	public async Task FixedNativeSeparatorsOpenRelatedBindingsInTheFirstAnalysis(string shape)
	{
		string prefix = shape == "multi" ? "db.First(\n 1);\n" : "if (x == 0) return 0;\n";

		const string opening =
			"class C {\nint Compute() => 1;\nvoid First(int value) {}\n\nint M(C db, int x) {\n";

		const string closing = "\n}\n}";

		string source = opening + prefix + "int result = Compute();\n\nreturn result;" + closing;

		string fixedSource =
			opening + prefix + "\nint result = Compute();\nreturn result;" + closing;

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepJumpWithBinding,

			shape == "multi"
				? DiagnosticDescriptors.SeparateMultiLineConstructs
				: DiagnosticDescriptors.SeparateCheckedStanza,
		]);

		AnalyzerRunResult reported = await RoslynTestHarness.ReportAllAsync(source, rules);
		Assert.HasCount(2, reported.Diagnostics);
		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source, rules);
		RoslynTestHarness.AssertFixedText(fixedSource, result);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			fixedSource,
			source,
			new RunOptions(RoslynTestHarness.AllAnalyzers) { DiagnosticOptions = rules }
		);
	}

	[TestMethod]
	[CrossCutting]
	public async Task ReceiverPathEdgesEndTheSharedSymbolAssignmentException()
	{
		const string source = """
			class C
			{
				int Field;
				int Property { get; set; }
				C child = null!;
				void M(C item)
				{
					item.Field = 1;
					item.Property = 2;
					item.child.Field = 3;
					item.child.Property = 4;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				int Field;
				int Property { get; set; }
				C child = null!;
				void M(C item)
				{
					item.Field = 1;
					item.Property = 2;

					item.child.Field = 3;
					item.child.Property = 4;
				}
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.SeparateAssignmentRuns,
			DiagnosticDescriptors.SeparateReceiverGroups,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
		]);

		var options = new Dictionary<string, string>
		{
			["stanza_receiver_roots"] = "item",
			["stanza_receiver_comparison"] = "path",
			["stanza_assignment_targets"] = "split",
		};

		AnalyzerRunResult reported = await RoslynTestHarness.ReportAllAsync(source, rules, options);

		Assert.HasCount(1, reported.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.SeparateAssignmentRuns.Id,
			reported.Diagnostics[0].Id
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,
			rules,
			options
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			fixedSource,
			source,

			new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = rules,
				AnalyzerConfigOptions = options,
			}
		);
	}

	[TestMethod]
	[CrossCutting]
	public async Task ATopLevelGuardKeeperLeavesTheMultiLineRuleOnlyTheGapBelowTheGuard()
	{
		const string source = """
			object? value = new object();
			if (value is null)
				return;
			System.GC.KeepAlive(value);
			""";

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.KeepGuardWithBinding,
			DiagnosticDescriptors.KeepNarrowingAssertionWithBinding,
		]);

		AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(source, rules);
		Assert.HasCount(1, result.Diagnostics);

		Assert.AreEqual(DiagnosticDescriptors.SeparateMultiLineConstructs.Id, result.Diagnostics[0].Id);

		Assert.AreEqual(
			source.IndexOf("System.GC", System.StringComparison.Ordinal),
			result.Diagnostics[0].Location.SourceSpan.Start
		);
	}

	[TestMethod]
	[CrossCutting]
	public async Task SilencingReceiverInvocationGroupingRestoresAwaitOwnership()
	{
		const string source = """
			class C
			{
				async System.Threading.Tasks.Task M(C db)
				{
			#pragma warning disable ZA0004
					Start();
					await db.Flush();
					await db.Refresh();
					Finish();
			#pragma warning restore ZA0004
				}
				void Start() { }
				void Finish() { }
				System.Threading.Tasks.Task Flush() => System.Threading.Tasks.Task.CompletedTask;
				System.Threading.Tasks.Task Refresh() => System.Threading.Tasks.Task.CompletedTask;
			}
			""";

		const string fixedSource = """
			class C
			{
				async System.Threading.Tasks.Task M(C db)
				{
			#pragma warning disable ZA0004
					Start();

					await db.Flush();
					await db.Refresh();

					Finish();
			#pragma warning restore ZA0004
				}
				void Start() { }
				void Finish() { }
				System.Threading.Tasks.Task Flush() => System.Threading.Tasks.Task.CompletedTask;
				System.Threading.Tasks.Task Refresh() => System.Threading.Tasks.Task.CompletedTask;
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
		]);

		var options = new Dictionary<string, string> { ["stanza_receiver_roots"] = "db" };

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,
			rules,
			options
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	[DataRow("root", false, false)]
	[DataRow("root", false, true)]
	[DataRow("root", true, false)]
	[DataRow("root", true, true)]
	[DataRow("path", false, false)]
	[DataRow("path", false, true)]
	[DataRow("path", true, false)]
	[DataRow("path", true, true)]
	public async Task AuthoredReceiverSectionsSurviveGenericRemovalBeforeAnyFix(
		string comparison,
		bool mixed,
		bool separates
	)
	{
		string source =
			"class C { void M(dynamic db) {\n"
			+ (
				mixed
					? "db.member = 1;\ndb.A();\n\ndb.B();\ndb.member = 2;"
					: "db.A();\ndb.B();\n\ndb.C();\ndb.D();"
			)
			+ "\n} }";

		string fixedSource = source;

		DiagnosticDescriptor[] ordinary =
		[
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
		];

		for (int mask = 0; mask < 16; mask++)
		{
			Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
				DiagnosticDescriptors.KeepReceiverGroupTogether,
				.. separates ? new[] { DiagnosticDescriptors.SeparateReceiverGroups } : [],
				.. ordinary.Where((_, index) => (mask & (1 << index)) != 0),
			]);

			var options = new Dictionary<string, string>
			{
				["stanza_receiver_roots"] = "db",
				["stanza_receiver_comparison"] = comparison,
				["stanza_max_loose_run_size"] = "1",
			};

			AnalyzerRunResult reported = await RoslynTestHarness.ReportAllAsync(
				source,
				rules,
				options
			);

			Assert.IsEmpty(reported.Diagnostics, $"mask {mask}");
			AnalyzerRunResult batch = await RoslynTestHarness.RunAllAsync(source, rules, options);
			RoslynTestHarness.AssertFixedText(fixedSource, batch);
		}
	}

	[TestMethod]
	[CrossCutting]
	[DataRow("root")]
	[DataRow("path")]
	public async Task ReceiverJoinsSettleWithoutInventingSectionsAtATightUpperGap(string comparison)
	{
		const string source =
			"class C { void M(dynamic db) {\nvar x = 1;\n\nvar y = 2;\n\nvar z = 3;\ndb.A();\ndb.B();\ndb.C();\n} }";

		const string fixedSource =
			"class C { void M(dynamic db) {\nvar x = 1;\nvar y = 2;\nvar z = 3;\n\ndb.A();\ndb.B();\ndb.C();\n} }";

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepReceiverGroupTogether,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
		]);

		var options = new Dictionary<string, string>
		{
			["stanza_receiver_roots"] = "db",
			["stanza_receiver_comparison"] = comparison,
		};

		AnalyzerRunResult reported = await RoslynTestHarness.ReportAllAsync(source, rules, options);
		RoslynTestHarness.AssertEachGapIsClaimedOnce(reported);
		AnalyzerRunResult batch = await RoslynTestHarness.RunAllAsync(source, rules, options);
		RoslynTestHarness.AssertFixedText(fixedSource, batch);

		AnalyzerRunResult first = await RoslynTestHarness.RunIterativelyAsync(
			source,

			new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = rules,
				AnalyzerConfigOptions = options,
			}
		);

		RoslynTestHarness.AssertFixedText(fixedSource, first);

		AnalyzerRunResult last = await RoslynTestHarness.RunIterativelyAsync(
			source,

			new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = rules,
				AnalyzerConfigOptions = options,
				FixesBottomFirst = true,
			}
		);

		RoslynTestHarness.AssertFixedText(fixedSource, last);
	}

	[TestMethod]
	[CrossCutting]
	[DataRow("root")]
	[DataRow("path")]
	public async Task EveryFixOrderKeepsReceiverSectionsWhileJoiningAdjacentSingletons(
		string comparison
	)
	{
		const string source =
			"class C { void M(dynamic db) {\ndb.A();\n\ndb.B();\n\ndb.C();\ndb.D();\n\ndb.E();\ndb.F();\n} }";

		const string fixedSource =
			"class C { void M(dynamic db) {\ndb.A();\ndb.B();\ndb.C();\ndb.D();\n\ndb.E();\ndb.F();\n} }";

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepReceiverGroupTogether,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
		]);

		var options = new Dictionary<string, string>
		{
			["stanza_receiver_roots"] = "db",
			["stanza_receiver_comparison"] = comparison,
			["stanza_max_loose_run_size"] = "1",
		};

		AnalyzerRunResult reported = await RoslynTestHarness.ReportAllAsync(source, rules, options);
		RoslynTestHarness.AssertEachGapIsClaimedOnce(reported);
		AnalyzerRunResult batch = await RoslynTestHarness.RunAllAsync(source, rules, options);
		RoslynTestHarness.AssertFixedText(fixedSource, batch);

		AnalyzerRunResult first = await RoslynTestHarness.RunIterativelyAsync(
			source,

			new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = rules,
				AnalyzerConfigOptions = options,
			}
		);

		RoslynTestHarness.AssertFixedText(fixedSource, first);

		AnalyzerRunResult last = await RoslynTestHarness.RunIterativelyAsync(
			source,

			new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = rules,
				AnalyzerConfigOptions = options,
				FixesBottomFirst = true,
			}
		);

		RoslynTestHarness.AssertFixedText(fixedSource, last);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			fixedSource,
			source,

			new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = rules,
				AnalyzerConfigOptions = options,
			}
		);
	}
}
