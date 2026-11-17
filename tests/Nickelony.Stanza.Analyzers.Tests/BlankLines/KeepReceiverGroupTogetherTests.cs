using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed class KeepReceiverGroupTogetherTests
{
	[TestMethod]
	public async Task DirectSyncAndAwaitedCallsJoinWithoutGlobalKeeping()
	{
		const string source = """
			class C
			{
				async System.Threading.Tasks.Task M(dynamic item, dynamic db, dynamic cache, dynamic logger)
				{
					db.Add();

					await db.Flush();

					await db.Refresh();
				}
				static dynamic Get() => null!;
			}
			""";

		const string fixedSource = """
			class C
			{
				async System.Threading.Tasks.Task M(dynamic item, dynamic db, dynamic cache, dynamic logger)
				{
					db.Add();
					await db.Flush();
					await db.Refresh();
				}
				static dynamic Get() => null!;
			}
			""";

		var options = new Dictionary<string, string>
		{
			["stanza_receiver_roots"] = "db",
			["stanza_receiver_comparison"] = "root",
		};

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepReceiverGroupTogether,
		]);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepReceiverGroupTogetherAnalyzer>(
				source,
				rules,
				options
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task MemberWritesCallsAndUpdatesJoinWithoutFamilyRules()
	{
		const string source = """
			class C
			{
				async System.Threading.Tasks.Task M(dynamic item, dynamic db, dynamic cache, dynamic logger)
				{
					item.name = 1;

					item.Save();

					item.count++;
				}
				static dynamic Get() => null!;
			}
			""";

		const string fixedSource = """
			class C
			{
				async System.Threading.Tasks.Task M(dynamic item, dynamic db, dynamic cache, dynamic logger)
				{
					item.name = 1;
					item.Save();
					item.count++;
				}
				static dynamic Get() => null!;
			}
			""";

		var options = new Dictionary<string, string>
		{
			["stanza_receiver_roots"] = "*",
			["stanza_receiver_comparison"] = "path",
		};

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepReceiverGroupTogether,
		]);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepReceiverGroupTogetherAnalyzer>(
				source,
				rules,
				options
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task UnselectedLoggerGapsStayWritten()
	{
		const string source = """
			class C
			{
				async System.Threading.Tasks.Task M(dynamic item, dynamic db, dynamic cache, dynamic logger)
				{
					logger.Info();

					logger.Info();

					db.Add();

					await db.Save();
				}
				static dynamic Get() => null!;
			}
			""";

		const string fixedSource = """
			class C
			{
				async System.Threading.Tasks.Task M(dynamic item, dynamic db, dynamic cache, dynamic logger)
				{
					logger.Info();

					logger.Info();

					db.Add();
					await db.Save();
				}
				static dynamic Get() => null!;
			}
			""";

		var options = new Dictionary<string, string>
		{
			["stanza_receiver_roots"] = "db",
			["stanza_receiver_comparison"] = "root",
		};

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepReceiverGroupTogether,
		]);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepReceiverGroupTogetherAnalyzer>(
				source,
				rules,
				options
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task WrittenSectionsOfTwoOperationsStayApart()
	{
		const string source = """
			class C
			{
				async System.Threading.Tasks.Task M(dynamic item, dynamic db, dynamic cache, dynamic logger)
				{
					db.A();
					db.B();

					db.C();
					db.D();
				}
				static dynamic Get() => null!;
			}
			""";

		var options = new Dictionary<string, string>
		{
			["stanza_receiver_roots"] = "db",
			["stanza_receiver_comparison"] = "root",
		};

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepReceiverGroupTogether,
		]);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepReceiverGroupTogetherAnalyzer>(
				source,
				rules,
				options
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACommentKeepsTheReceiverGapWritten()
	{
		const string source = """
			class C
			{
				async System.Threading.Tasks.Task M(dynamic item, dynamic db, dynamic cache, dynamic logger)
				{
					db.A();

					// Another phase.
					db.B();
				}
				static dynamic Get() => null!;
			}
			""";

		var options = new Dictionary<string, string>
		{
			["stanza_receiver_roots"] = "db",
			["stanza_receiver_comparison"] = "root",
		};

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepReceiverGroupTogether,
		]);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepReceiverGroupTogetherAnalyzer>(
				source,
				rules,
				options
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task MultilineReceiverCallsKeepTheirGaps()
	{
		const string source = """
			class C
			{
				async System.Threading.Tasks.Task M(dynamic item, dynamic db, dynamic cache, dynamic logger)
				{
					db.A(
					    1);

					db.B();
				}
				static dynamic Get() => null!;
			}
			""";

		var options = new Dictionary<string, string>
		{
			["stanza_receiver_roots"] = "db",
			["stanza_receiver_comparison"] = "root",
		};

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepReceiverGroupTogether,
		]);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepReceiverGroupTogetherAnalyzer>(
				source,
				rules,
				options
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task EveryDiagnosticKeepsTheWholeEligibleGroup()
	{
		const string source = """
			class C
			{
				async System.Threading.Tasks.Task M(dynamic item, dynamic db, dynamic cache, dynamic logger)
				{
					db.A();

					db.B();

					db.C();
					db.D();
				}
				static dynamic Get() => null!;
			}
			""";

		const string fixedSource = """
			class C
			{
				async System.Threading.Tasks.Task M(dynamic item, dynamic db, dynamic cache, dynamic logger)
				{
					db.A();
					db.B();
					db.C();
					db.D();
				}
				static dynamic Get() => null!;
			}
			""";

		var options = new Dictionary<string, string>
		{
			["stanza_receiver_roots"] = "db",
			["stanza_receiver_comparison"] = "root",
		};

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepReceiverGroupTogether,
		]);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepReceiverGroupTogetherAnalyzer>(
				source,
				rules,
				options
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The assignment rule reads the group as joined, so it asks for the blank line above the group before the keeper's fix.
	[TestMethod]
	[CrossCutting]
	public async Task JoiningAMemberRunTransfersSeparationAboveTheReceiverGroup()
	{
		const string source = """
			class C
			{
				async System.Threading.Tasks.Task M(dynamic item, dynamic db, dynamic cache, dynamic logger)
				{
					logger.Info();
					item.name = 1;

					item.count = 2;
					item.Save();
				}
				static dynamic Get() => null!;
			}
			""";

		const string fixedSource = """
			class C
			{
				async System.Threading.Tasks.Task M(dynamic item, dynamic db, dynamic cache, dynamic logger)
				{
					logger.Info();

					item.name = 1;
					item.count = 2;
					item.Save();
				}
				static dynamic Get() => null!;
			}
			""";

		var options = new Dictionary<string, string>
		{
			["stanza_receiver_roots"] = "item",
			["stanza_receiver_comparison"] = "path",
		};

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepReceiverGroupTogether,
			DiagnosticDescriptors.SeparateAssignmentRuns,
		]);

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source, rules, options);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task ClaimingEveryGapOfALongReceiverGroupCostsInStepWithItsLength()
	{
		(int small, int large) = await LinearCost.AssertQueryLinearAsync(
			rule: StanzaRules.KeepReceiverGroupTogether,

			buildSource: units =>
				"class C { void M(dynamic db) {\n"
				+ string.Join("\n", Enumerable.Repeat("db.Save();", units))
				+ "\n} }",

			units: 50,
			rules: TestRuleSets.OnSuppressingAllOthers([DiagnosticDescriptors.KeepReceiverGroupTogether]),
			query: container => LinearCost.CountClaims(container, StanzaRules.KeepReceiverGroupTogether),
			options: new Dictionary<string, string> { ["stanza_receiver_roots"] = "db" }
		);

		// The calls on one receiver are one group, so the rule claims every gap between them.
		Assert.AreEqual(49, small);
		Assert.AreEqual(199, large);
	}

	[TestMethod]
	public async Task EventSubscriptionsAndCallsCanShareTheirUnambiguousOwner()
	{
		const string source = """
			class C
			{
				event System.Action? Changed;
				void M(C db)
				{
					db.Changed += Handle;

					db.Changed -= Handle;

					db.Save();
				}
				void Handle() { }
				void Save() { }
			}
			""";

		const string fixedSource = """
			class C
			{
				event System.Action? Changed;
				void M(C db)
				{
					db.Changed += Handle;
					db.Changed -= Handle;
					db.Save();
				}
				void Handle() { }
				void Save() { }
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepReceiverGroupTogether,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
		]);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepReceiverGroupTogetherAnalyzer>(
				source,
				rules,
				new Dictionary<string, string> { ["stanza_receiver_roots"] = "db" }
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	[DataRow(true, "db", false, true)]
	[DataRow(false, "db", false, false)]
	[DataRow(true, "", false, false)]
	[DataRow(true, "cache", false, false)]
	[DataRow(true, "db", true, false)]
	public async Task OnlyAnActiveSelectedKeeperProtectsWrittenSections(
		bool enabled,
		string roots,
		bool silenced,
		bool protects
	)
	{
		string source =
			(silenced ? "#pragma warning disable ZA0064\n" : string.Empty)
			+ "class C { void M(dynamic db) {\ndb.A();\ndb.B();\n\ndb.C();\ndb.D();\n} }";

		var observations = new List<(int Sections, bool Reads)>();

		var analyzer = new ContainerReadingAnalyzer(
			StanzaRules.SeparateInvocationRuns,

			container =>
			{
				if (container.Siblings.Length == 4)
				{
					observations.Add(
						(
							BlankLineReceiverGroups.GetSections(container).Count,
							container.ReceiverGroups is not null
						)
					);
				}
			}
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunOneAsync(
			source,
			analyzer,

			diagnosticOptions: TestRuleSets.OnSuppressingAllOthers([
				DiagnosticDescriptors.SeparateInvocationRuns,
				.. enabled ? new[] { DiagnosticDescriptors.KeepReceiverGroupTogether } : [],
			]),

			analyzerConfigOptions: new Dictionary<string, string>
			{
				["stanza_receiver_roots"] = roots,
			}
		);

		Assert.IsEmpty(result.Diagnostics);
		Assert.IsNotEmpty(observations);

		foreach ((int sections, bool reads) in observations)
		{
			Assert.AreEqual(protects ? 1 : 0, sections);
			Assert.AreEqual(enabled && roots.Length > 0, reads);
		}
	}

	[TestMethod]
	public async Task ADirectiveSplitsAWholeReceiverFixAndKeepsTheSilencedSectionWritten()
	{
		const string source =
			"class C { void M(dynamic db) {\ndb.A();\n\ndb.B();\n#pragma warning disable ZA0064\n\ndb.C();\n\ndb.D();\n} }";

		const string fixedSource =
			"class C { void M(dynamic db) {\ndb.A();\ndb.B();\n#pragma warning disable ZA0064\n\ndb.C();\n\ndb.D();\n} }";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepReceiverGroupTogetherAnalyzer>(
				source,

				TestRuleSets.OnSuppressingAllOthers([
					DiagnosticDescriptors.KeepReceiverGroupTogether,
				]),

				new Dictionary<string, string> { ["stanza_receiver_roots"] = "db" }
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
