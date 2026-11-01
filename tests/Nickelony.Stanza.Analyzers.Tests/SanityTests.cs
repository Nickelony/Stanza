using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>
/// Checks cancellation, concurrent analysis, and the keys and severities a run hands an analyzer.
/// </summary>
[TestClass]
public sealed partial class SanityTests
{
	[TestMethod]
	public async Task ARunCanceledInTheMiddleStopsInsteadOfCompleting()
	{
		const string source = """
			class C
			{
				void M()
				{
				}
				void N()
				{
				}
			}
			""";

		using CancellationTokenSource cancellation = new();

		// The fake cancels from inside its first action, so the token is live when the run starts.
		_ = await Assert.ThrowsAsync<OperationCanceledException>(() =>
			RoslynTestHarness.RunAllWithCancellationAsync(
				source,
				new CancelingAnalyzer(cancellation),
				cancellation.Token
			)
		);
	}

	[TestMethod]
	public async Task ACanceledTokenStopsAFixBeforeItRegistersAndBeforeItApplies()
	{
		const string source = """
			class C
			{
				void M()
				{
					DoThing();
				}

				void N()
				{
					DoThing();
				}
			}
			""";

		ImmutableArray<CodeAction> offered = await RoslynTestHarness.GetActionsAsync(
			source,
			diagnosticOptions: TestRuleSets.AllRulesEnabled
		);

		Assert.IsNotEmpty(offered, "The source offers no fix, so the canceled runs prove nothing.");

		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		bool registered = false;
		bool applied = false;

		// The offered fix writes something while the token is live,
		// so the canceled call below proves the cancellation rather than a fix that writes nothing either way.
		ImmutableArray<CodeActionOperation> live = await offered[0]
			.GetOperationsAsync(CancellationToken.None);

		Assert.IsNotEmpty(
			live.OfType<ApplyChangesOperation>(),
			"The offered fix writes nothing even with a live token, so the canceled run proves nothing."
		);

		// The provider may throw the token's exception or return with nothing,
		// but it must not offer a fix the run has already canceled.
		try
		{
			ImmutableArray<CodeAction> actions =
				await RoslynTestHarness.GetActionsWithCancellationAsync(
					source,
					TestRuleSets.AllRulesEnabled,
					cancellation.Token
				);

			registered = actions.Length > 0;
		}
		catch (OperationCanceledException) { }

		try
		{
			ImmutableArray<CodeActionOperation> operations = await offered[0]
				.GetOperationsAsync(cancellation.Token);

			applied = operations.OfType<ApplyChangesOperation>().Any();
		}
		catch (OperationCanceledException) { }

		Assert.IsFalse(registered, "A canceled registration offered a fix.");
		Assert.IsFalse(applied, "A canceled fix wrote a change.");
	}

	[TestMethod]
	public async Task AConcurrentRunFindsWhatASequentialRunFinds()
	{
		const string first = """
			class First
			{
				void M()
				{
				}
				void N()
				{
				}
			}
			""";

		const string second = """
			class Second
			{
				int Value
				{
					get
					{
						return 1;
					}
				}
				int Other
				{
					get
					{
						return 2;
					}
				}
			}
			""";

		// All the rules are enabled, so the ones that ship off and the caches they fill run in both modes.
		(ImmutableArray<string> concurrent, ImmutableArray<string> sequential) =
			await RoslynTestHarness.RunOverDocumentsAsync(
				[first, second],
				TestRuleSets.AllRulesEnabled
			);

		Assert.IsNotEmpty(sequential);

		Assert.Contains(
			id => sequential.Any(report => report.Contains(id, StringComparison.Ordinal)),
			TestRuleSets.ShipsOff,
			"No rule that ships off reported, so the enabled set added nothing to the run."
		);

		Assert.AreEqual(string.Join("\n", sequential), string.Join("\n", concurrent));
	}

	[TestMethod]
	public async Task AKeyAndASeverityKeyInOneRunReachTheAnalyzerTogether()
	{
		const string source = """
			class C
			{
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,

			new RunOptions([new ConfigReadingAnalyzer()])
			{
				Providers = [],
				AssertFixedPoint = false,
				AnalyzerConfigOptions = new Dictionary<string, string> { ["stanza_probe"] = "7" },

				TreeOptions = new Dictionary<string, ReportDiagnostic>
				{
					["dotnet_analyzer_diagnostic.category-Test.severity"] = ReportDiagnostic.Info,
				},
			}
		);

		// The support document is a tree of the run too, so each tree reports once.
		Assert.IsNotEmpty(result.Diagnostics);

		Assert.IsTrue(
			result.Diagnostics.All(diagnostic =>
				diagnostic.GetMessage(CultureInfo.InvariantCulture)
				== "stanza_probe=7, severity=suggestion"
			)
		);
	}

	[TestMethod]
	public async Task AReportOnlyRuleKeepsItsReportAndThePassStillSettles()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Pick(int value) =>
					value > 2 ? 3 : value > 1 ? 2 : value > 0 ? 1 : 0;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.BreakUpLongTernaryChain)
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(source, result);
	}

	[TestMethod]
	public async Task AReportOnlyRuleOffersNoFix()
	{
		const string source = """
			internal static class Sample
			{
				internal static int Pick(int value) =>
					value > 2 ? 3 : value > 1 ? 2 : value > 0 ? 1 : 0;
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rule = TestRuleSets.Only(
			DiagnosticDescriptors.BreakUpLongTernaryChain
		);

		// The rule has to report, or the empty list below would say nothing.
		AnalyzerRunResult reported = await RoslynTestHarness.ReportAllAsync(source, rule);

		Assert.HasCount(1, reported.Diagnostics);

		ImmutableArray<CodeAction> actions = await RoslynTestHarness.GetActionsAsync(
			source,
			diagnosticOptions: rule
		);

		Assert.IsEmpty(actions);
	}

	// The RS rules guard analyzers that ship, and the fakes below never ship.
#pragma warning disable RS1036, RS1038, RS1041, RS2008

	// Cancels the run from its first syntax tree action.
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	private sealed class CancelingAnalyzer(CancellationTokenSource cancellation)
		: DiagnosticAnalyzer
	{
		private static readonly DiagnosticDescriptor s_descriptor = new(
			id: "ZA9998",
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
			context.RegisterSyntaxTreeAction(_ => cancellation.Cancel());
		}
	}

	// Reports the keys it reads, which the run gave it through one config.
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	private sealed class ConfigReadingAnalyzer : DiagnosticAnalyzer
	{
		private static readonly DiagnosticDescriptor s_descriptor = new(
			id: "ZA9993",
			title: "The keys a run gave the analyzer",
			messageFormat: "{0}",
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

			context.RegisterSyntaxTreeAction(treeContext =>
			{
				AnalyzerConfigOptions options =
					treeContext.Options.AnalyzerConfigOptionsProvider.GetOptions(treeContext.Tree);

				options.TryGetValue("stanza_probe", out string? probe);

				options.TryGetValue(
					"dotnet_analyzer_diagnostic.category-Test.severity",
					out string? severity
				);

				treeContext.ReportDiagnostic(
					Diagnostic.Create(
						s_descriptor,
						Location.None,
						$"stanza_probe={probe}, severity={severity}"
					)
				);
			});
		}
	}
#pragma warning restore RS1036, RS1038, RS1041, RS2008
}
