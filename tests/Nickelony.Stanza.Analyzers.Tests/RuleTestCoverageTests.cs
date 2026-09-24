using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Analyzers.Braces;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>
/// Guards that each rule has a fixture of its own, read from what each test's harness runs drove.
/// </summary>
[TestClass]
public sealed class RuleTestCoverageTests
{
	[GlobalTestInitialize]
	public static void RecordTheRunningTest(TestContext context)
	{
		string className = context.FullyQualifiedTestClassName ?? string.Empty;

		HarnessRecord.Begin(
			new RecordedTest(
				Class: className[(className.LastIndexOf('.') + 1)..],
				Method: context.TestName ?? string.Empty
			)
		);

		HarnessBudget.Begin(context.CancellationToken);
	}

	[GlobalTestCleanup]
	public static void EndTheRunningTest(TestContext _) => HarnessBudget.End();

	// A filtered run leaves tests out, so its record could miss a fixture that exists.
	// A test of its own could not see the whole run, since the tests run in parallel and in no set order,
	// so the guard stays an assembly cleanup, which MSTest reports under the test that ended last; its message says so.
	// A condition attribute would keep a test from starting and the guard from running, so T6 bans one in this project.
	[AssemblyCleanup]
	public static void EachRuleHasAFixtureOfItsOwn()
	{
		if (!RanEveryTest())
			return;

		List<string> problems = FindProblems(HarnessRecord.Runs, GetHomes(), GetCrossCutting());

		GuardAssert.None(
			problems,
			"RuleTestCoverageTests found the tests short of what each rule and each fix needs",
			"MSTest reports an assembly cleanup under the test that ended last, which is not the test at fault."
		);
	}

	[TestMethod]
	public async Task TheRecordNamesTheRunningTest()
	{
		const string source = """
			class C
			{
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateMemberGroupsAnalyzer>(
			source
		);

		RecordedRun run = HarnessRecord.Runs.Single(run => run.Result == result);

		Assert.AreEqual(
			new RecordedTest(nameof(RuleTestCoverageTests), nameof(TheRecordNamesTheRunningTest)),
			run.Test
		);

		Assert.AreEqual(DiagnosticDescriptors.SeparateMemberGroups.Id, run.Rules.Single());
		Assert.IsFalse(run.WasRead, "The harness's own reads counted as the test's.");

		_ = result.FixedText;

		Assert.IsTrue(run.WasRead, "A read by the test went unrecorded.");
	}

	[TestMethod]
	public async Task ARunDrivesEachRuleItsOptionsEnableWhetherOrNotItReports()
	{
		const string source = """
			class C
			{

				void M()
				{
					DoThing();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.RemoveBracePadding,
				DiagnosticDescriptors.CollapseEmptyBlocks
			)
		);

		RecordedRun run = HarnessRecord.Runs.Single(run => run.Result == result);

		Assert.Contains(DiagnosticDescriptors.RemoveBracePadding.Id, run.Reported);
		Assert.DoesNotContain(DiagnosticDescriptors.CollapseEmptyBlocks.Id, run.Reported);
		Assert.Contains(DiagnosticDescriptors.CollapseEmptyBlocks.Id, run.Rules);
	}

	[TestMethod]
	public async Task EachHarnessEntryPointRecordsTheRunsItMakes()
	{
		const string source = """
			class C
			{
				void M()
				{
					DoThing();
				}
			}
			""";

		await AssertRecordsAsync(
			nameof(RoslynTestHarness.AssertFixAllPerIdSettlesAsync),
			() => RoslynTestHarness.AssertFixAllPerIdSettlesAsync(source, source)
		);

		await AssertRecordsAsync(
			nameof(RoslynTestHarness.RunAllWithCancellationAsync),

			() =>
				RoslynTestHarness.RunAllWithCancellationAsync(
					source,
					new IdleAnalyzer(),
					CancellationToken.None
				)
		);

		await AssertRecordsAsync(
			nameof(RoslynTestHarness.RunOverDocumentsAsync),
			() => RoslynTestHarness.RunOverDocumentsAsync([source])
		);

		await AssertRecordsAsync(
			nameof(RoslynTestHarness.CountOfferedFixesAsync),

			() =>
				RoslynTestHarness.CountOfferedFixesAsync(
					source,
					DiagnosticDescriptors.SeparateMemberGroups,
					new TextSpan(0, 1)
				)
		);
	}

	[TestMethod]
	public async Task ARunDoesNotDriveARuleASeverityDisables()
	{
		const string source = """
			class C
			{
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateMemberGroupsAnalyzer>(
			source,

			new Dictionary<string, ReportDiagnostic>
			{
				[DiagnosticDescriptors.SeparateMemberGroups.Id] = ReportDiagnostic.Suppress,
			}
		);

		RecordedRun run = HarnessRecord.Runs.Single(run => run.Result == result);

		Assert.IsEmpty(run.Rules);
	}

	[TestMethod]
	public async Task ABulkKeyThatSetsNoneSwitchesOffEachRuleThatShipsOn()
	{
		string categoryKey =
			$"dotnet_analyzer_diagnostic.category-{DiagnosticDescriptors.SeparateMemberGroups.Category}.severity";

		const string bulkKey = "dotnet_analyzer_diagnostic.severity";

		RunOptions[] switchedOff =
		[
			Both() with
			{
				AnalyzerConfigOptions = new Dictionary<string, string> { [bulkKey] = "none" },
			},

			Both() with
			{
				AnalyzerConfigOptions = new Dictionary<string, string> { [categoryKey] = "none" },
			},

			Both() with
			{
				TreeOptions = new Dictionary<string, ReportDiagnostic>
				{
					[bulkKey] = ReportDiagnostic.Suppress,
				},
			},

			Both() with
			{
				GlobalOptions = new Dictionary<string, ReportDiagnostic>
				{
					[categoryKey] = ReportDiagnostic.Suppress,
				},
			},
		];

		foreach (RunOptions options in switchedOff)
			Assert.IsEmpty(await DriveAsync(options), "A bulk key left a rule driven.");
	}

	[TestMethod]
	public async Task ABulkKeyNeverSwitchesOnARuleThatShipsOff()
	{
		ImmutableHashSet<string> driven = await DriveAsync(
			Both() with
			{
				AnalyzerConfigOptions = new Dictionary<string, string>
				{
					["dotnet_analyzer_diagnostic.severity"] = "warning",
				},
			}
		);

		Assert.AreSequenceEqual([DiagnosticDescriptors.SeparateMemberGroups.Id], driven);
	}

	[TestMethod]
	public async Task ARulesOwnKeyBeatsItsCategoryKeyWhichBeatsTheBulkKey()
	{
		string categoryKey =
			$"dotnet_analyzer_diagnostic.category-{DiagnosticDescriptors.SeparateMemberGroups.Category}.severity";

		string ownKey =
			$"dotnet_diagnostic.{DiagnosticDescriptors.SeparateMemberGroups.Id}.severity";

		const string bulkKey = "dotnet_analyzer_diagnostic.severity";

		ImmutableHashSet<string> ownOverBulk = await DriveAsync(
			Both() with
			{
				AnalyzerConfigOptions = new Dictionary<string, string>
				{
					[bulkKey] = "none",
					[ownKey] = "warning",
				},
			}
		);

		ImmutableHashSet<string> categoryOverBulk = await DriveAsync(
			Both() with
			{
				AnalyzerConfigOptions = new Dictionary<string, string>
				{
					[bulkKey] = "none",
					[categoryKey] = "warning",
				},
			}
		);

		ImmutableHashSet<string> categoryUnderBulk = await DriveAsync(
			Both() with
			{
				AnalyzerConfigOptions = new Dictionary<string, string>
				{
					[bulkKey] = "warning",
					[categoryKey] = "none",
				},
			}
		);

		Assert.Contains(DiagnosticDescriptors.SeparateMemberGroups.Id, ownOverBulk);
		Assert.Contains(DiagnosticDescriptors.SeparateMemberGroups.Id, categoryOverBulk);
		Assert.IsEmpty(categoryUnderBulk);
	}

	// A run of two analyzers asks for no severity, so a rule that ships off is in it and stays off.
	private static RunOptions Both() =>
		new([new SeparateMemberGroupsAnalyzer(), new RemoveRedundantBracesAnalyzer()]);

	private static async Task<ImmutableHashSet<string>> DriveAsync(RunOptions options)
	{
		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync("class C\n{\n}\n", options);
		return HarnessRecord.Runs.Single(run => run.Result == result).Rules;
	}

	[TestMethod]
	public void ASingleRuleTestOutsideItsRulesHomeIsFound()
	{
		RecordedRun[] runs =
		[
			Run(className: "SeparateMemberGroupsTests", method: "Reproduces", rule: "ZA0002"),
			Quiet(className: "SeparateMemberGroupsTests", rule: "ZA0002"),
			Run(className: "BraceFamilyTests", method: "Drives", rule: "ZA0002", reported: []),
		];

		List<string> problems = FindProblems(runs, Homes(("ZA0002", "SeparateMemberGroupsTests")));

		Assert.HasCount(1, problems);
		Assert.Contains("BraceFamilyTests.Drives", problems[0]);
	}

	[TestMethod]
	public void ATestMarkedCrossCuttingIsNotSentToItsRulesHome()
	{
		RecordedRun[] runs =
		[
			Run(className: "SeparateMemberGroupsTests", method: "Reproduces", rule: "ZA0002"),
			Quiet(className: "SeparateMemberGroupsTests", rule: "ZA0002"),
			Run(className: "BraceFamilyTests", method: "Vehicle", rule: "ZA0002", reported: []),
			Run(className: "BraceFamilyTests", method: "Drives", rule: "ZA0002", reported: []),
		];

		List<string> problems = FindProblems(
			runs,
			Homes(("ZA0002", "SeparateMemberGroupsTests")),
			crossCutting: ImmutableHashSet.Create(new RecordedTest("BraceFamilyTests", "Vehicle"))
		);

		Assert.HasCount(1, problems);
		Assert.Contains("BraceFamilyTests.Drives", problems[0]);
	}

	[TestMethod]
	public void ATestThatRunsAFixAndNeverReadsItsTextIsFound()
	{
		RecordedRun[] runs =
		[
			Run(className: "SeparateMemberGroupsTests", method: "Reproduces", rule: "ZA0002"),
			Quiet(className: "SeparateMemberGroupsTests", rule: "ZA0002"),

			Run(
				className: "SeparateMemberGroupsTests",
				method: "Counts",
				rule: "ZA0002",
				read: false,
				fixedTheSource: true
			),

			Run(
				className: "SeparateMemberGroupsTests",
				method: "Pins",
				rule: "ZA0002",
				fixedTheSource: true
			),
		];

		List<string> problems = FindProblems(runs, Homes(("ZA0002", "SeparateMemberGroupsTests")));

		Assert.HasCount(1, problems);
		Assert.Contains("SeparateMemberGroupsTests.Counts", problems[0]);
	}

	[TestMethod]
	public void ARuleThatItsHomeNeverLeavesQuietIsFound()
	{
		RecordedRun[] runs =
		[
			Run(className: "SeparateMemberGroupsTests", method: "Reports", rule: "ZA0002"),
			Run(className: "SeparateMultiLineConstructsTests", method: "Reports", rule: "ZA0004"),
			Quiet(className: "SeparateMultiLineConstructsTests", rule: "ZA0004"),
		];

		List<string> problems = FindProblems(
			runs,

			Homes(
				("ZA0002", "SeparateMemberGroupsTests"),
				("ZA0004", "SeparateMultiLineConstructsTests")
			)
		);

		Assert.HasCount(1, problems);
		Assert.Contains("ZA0002", problems[0]);
		Assert.Contains("quiet", problems[0]);
	}

	[TestMethod]
	public void ARuleThatNoReadRunReportsAloneIsFound()
	{
		RecordedRun[] runs =
		[
			Run(
				className: "SeparateMemberGroupsTests",
				method: "Unread",
				rule: "ZA0002",
				read: false
			),

			Run(
				className: "SeparateMemberGroupsTests",
				method: "Mixed",
				rule: "ZA0002",
				reported: ["ZA0002", "ZA0004"]
			),

			Run(
				className: "SeparateMultiLineConstructsTests",
				method: "Reproduces",
				rule: "ZA0004"
			),

			Quiet(className: "SeparateMemberGroupsTests", rule: "ZA0002"),
			Quiet(className: "SeparateMultiLineConstructsTests", rule: "ZA0004"),
		];

		List<string> problems = FindProblems(
			runs,

			Homes(
				("ZA0002", "SeparateMemberGroupsTests"),
				("ZA0004", "SeparateMultiLineConstructsTests")
			)
		);

		Assert.HasCount(1, problems);
		Assert.Contains("ZA0002", problems[0]);
	}

	[TestMethod]
	public void ADataDrivenFamilyTestMayDriveEachRuleOfItsFamily()
	{
		RecordedRun[] runs =
		[
			Run(className: "SiblingRuleSetContractTests", method: "Rows", rule: "ZA0010"),
			Run(className: "SiblingRuleSetContractTests", method: "Rows", rule: "ZA0013"),
			Run(className: "KeepMultiLineAccessorsTogetherTests", method: "Joins", rule: "ZA0010"),

			Run(
				className: "KeepMultiLineSwitchSectionsTogetherTests",
				method: "Joins",
				rule: "ZA0013"
			),

			Quiet(className: "KeepMultiLineAccessorsTogetherTests", rule: "ZA0010"),
			Quiet(className: "KeepMultiLineSwitchSectionsTogetherTests", rule: "ZA0013"),
		];

		ImmutableDictionary<string, string> homes = Homes(
			("ZA0010", "KeepMultiLineAccessorsTogetherTests"),
			("ZA0013", "KeepMultiLineSwitchSectionsTogetherTests")
		);

		Assert.IsEmpty(FindProblems(runs, homes));
	}

	[TestMethod]
	public void ARuleThatOnlyARunOutsideItsHomeReproducesIsFound()
	{
		RecordedRun[] runs =
		[
			Run(className: nameof(RuleSpanTests), method: "Reports", rule: "ZA0002"),
			Run(className: "SiblingRuleSetContractTests", method: "Rows", rule: "ZA0002"),
			Run(className: "SiblingRuleSetContractTests", method: "Rows", rule: "ZA0004"),
			Run(className: "SeparateMultiLineConstructsTests", method: "Separates", rule: "ZA0004"),
			Quiet(className: "SeparateMemberGroupsTests", rule: "ZA0002"),
			Quiet(className: "SeparateMultiLineConstructsTests", rule: "ZA0004"),
		];

		List<string> problems = FindProblems(
			runs,

			Homes(
				("ZA0002", "SeparateMemberGroupsTests"),
				("ZA0004", "SeparateMultiLineConstructsTests")
			),

			crossCutting: ImmutableHashSet.Create(
				new RecordedTest(nameof(RuleSpanTests), "Reports")
			)
		);

		Assert.HasCount(1, problems);
		Assert.Contains("ZA0002", problems[0]);
		Assert.Contains("reproduced", problems[0]);
	}

	// A test drives one rule alone when every run it made drove that rule and no other,
	// and the fixture that reproduces a rule is a run in the rule's home
	// that its test read and that reported that rule alone.
	internal static List<string> FindProblems(
		IEnumerable<RecordedRun> runs,
		IReadOnlyDictionary<string, string> homes,
		IReadOnlySet<RecordedTest>? crossCutting = null
	)
	{
		crossCutting ??= ImmutableHashSet<RecordedTest>.Empty;

		RecordedRun[] judged =
		[
			.. runs.Where(run => run.Test.Class != nameof(RuleTestCoverageTests)),
		];

		List<string> problems = [];

		foreach (IGrouping<RecordedTest, RecordedRun> test in judged.GroupBy(run => run.Test))
		{
			string[] rules =
			[
				.. test.SelectMany(run => run.Rules).Where(homes.ContainsKey).Distinct(),
			];

			if (
				rules.Length == 1
				&& !IsCrossCutting(test.Key, crossCutting)
				&& homes[rules[0]] != test.Key.Class
			)
			{
				problems.Add(
					$"{test.Key.Class}.{test.Key.Method} drives {rules[0]} alone, so it belongs in {homes[rules[0]]}."
				);
			}
		}

		foreach (
			(string rule, string home) in homes.OrderBy(pair => pair.Key, StringComparer.Ordinal)
		)
		{
			bool reproduced = judged.Any(run =>
				run.Test.Class == home
				&& run.WasRead
				&& run.Reported.Contains(rule)
				&& run.Reported.All(id => id == rule || !homes.ContainsKey(id))
			);

			if (!reproduced)
			{
				problems.Add(
					$"{rule} is reproduced by no fixture: no run in {home} that its test read reported it alone."
				);
			}

			// A quiet fixture is the rule's own analyzer run on a source it leaves alone.
			bool quiet = judged.Any(run =>
				run.Test.Class == home
				&& run.WasRead
				&& run.Rules.SetEquals([rule])
				&& !run.Reported.Contains(rule)
			);

			if (!quiet)
			{
				problems.Add(
					$"{rule} has no quiet fixture: no run in {home} that its test read drove it alone and left it silent."
				);
			}
		}

		// A fix that changed its source writes text, and the test that ran it has to pin that text.
		foreach (
			IGrouping<RecordedTest, RecordedRun> test in judged
				.Where(run =>
					run.Result is { FixedTheSource: true, FixedTextWasRead: false }
					&& !IsCrossCutting(run.Test, crossCutting)
				)
				.GroupBy(run => run.Test)
		)
		{
			problems.Add(
				$"{test.Key.Class}.{test.Key.Method} runs a fix that changes its source and never reads the fixed text."
			);
		}

		return problems;
	}

	// A data row's name follows its method's, so the marked method covers each of its rows.
	private static bool IsCrossCutting(RecordedTest test, IReadOnlySet<RecordedTest> crossCutting)
	{
		return crossCutting.Any(marked =>
			marked.Class == test.Class
			&& (
				marked.Method == test.Method
				|| test.Method.StartsWith($"{marked.Method} ", StringComparison.Ordinal)
			)
		);
	}

	private static ImmutableHashSet<RecordedTest> GetCrossCutting()
	{
		return
		[
			.. typeof(RuleTestCoverageTests)
				.Assembly.GetTypes()
				.SelectMany(type =>
					type.GetMethods()
						.Where(method =>
							method.GetCustomAttribute<CrossCuttingAttribute>() is not null
						)
						.Select(method => new RecordedTest(type.Name, method.Name))
				),
		];
	}

	private static ImmutableDictionary<string, string> GetHomes()
	{
		return RoslynTestHarness.AllAnalyzers.ToImmutableDictionary(
			analyzer => analyzer.SupportedDiagnostics.Single().Id,
			analyzer => analyzer.GetType().Name[..^"Analyzer".Length] + "Tests"
		);
	}

	private static bool RanEveryTest()
	{
		IEnumerable<RecordedTest> declared = typeof(RuleTestCoverageTests)
			.Assembly.GetTypes()
			.Where(type =>
				type.GetCustomAttribute<TestClassAttribute>() is not null
				&& type.GetCustomAttribute<IgnoreAttribute>() is null
			)
			.SelectMany(type =>
				type.GetMethods()
					.Where(method =>
						method.GetCustomAttribute<TestMethodAttribute>() is not null
						&& method.GetCustomAttribute<IgnoreAttribute>() is null
					)
					.Select(method => new RecordedTest(type.Name, method.Name))
			);

		return declared.All(HarnessRecord.Started.Contains);
	}

	private static RecordedRun Run(
		string className,
		string method,
		string rule,
		ImmutableHashSet<string>? reported = null,
		bool read = true,
		bool fixedTheSource = false
	)
	{
		AnalyzerRunResult result = new([], fixedTheSource ? "fixed" : "source");
		result.Track("source");

		if (read)
			_ = result.FixedText;

		return new RecordedRun(
			Test: new RecordedTest(Class: className, Method: method),
			Rules: [rule],
			Reported: reported ?? [rule],
			Result: result
		);
	}

	private static async Task AssertRecordsAsync(string entryPoint, Func<Task> run)
	{
		RecordedTest test = new(
			nameof(RuleTestCoverageTests),
			nameof(EachHarnessEntryPointRecordsTheRunsItMakes)
		);

		int before = HarnessRecord.Runs.Count(recorded => recorded.Test == test);

		await run();

		Assert.IsGreaterThan(
			before,
			HarnessRecord.Runs.Count(recorded => recorded.Test == test),
			$"{entryPoint} recorded no run."
		);
	}

	private static RecordedRun Quiet(string className, string rule) =>
		Run(className, method: "Stays", rule, reported: []);

	private static ImmutableDictionary<string, string> Homes(
		params (string Rule, string Home)[] homes
	) => homes.ToImmutableDictionary(home => home.Rule, home => home.Home);

	// The RS rules guard analyzers that ship, and the fake below never ships.
#pragma warning disable RS1036, RS1038, RS1041, RS2008
	// Registers nothing, so the run only has to hold it among the others.
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	private sealed class IdleAnalyzer : DiagnosticAnalyzer
	{
		private static readonly DiagnosticDescriptor s_descriptor = new(
			id: "ZA9994",
			title: "A diagnostic that is never reported",
			messageFormat: "Never reported",
			category: "Test",
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true
		);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
		[s_descriptor];

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();
		}
	}
#pragma warning restore RS1036, RS1038, RS1041, RS2008
}
