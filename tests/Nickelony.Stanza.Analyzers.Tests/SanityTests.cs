using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Analyzers.Braces;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>
/// Checks odd syntax, cancellation, concurrent analysis and the harness's own failure paths.
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
	public async Task ACheckedBlockIsAnOrdinaryBlock()
	{
		const string source = """
			class C
			{
				void M()
				{
					checked
					{

						DoThing();
					}

					unchecked
					{

					}
				}
			}
			""";

		const string paddingFixed = """
			class C
			{
				void M()
				{
					checked
					{
						DoThing();
					}

					unchecked
					{

					}
				}
			}
			""";

		const string emptyFixed = """
			class C
			{
				void M()
				{
					checked
					{

						DoThing();
					}

					unchecked
					{ }
				}
			}
			""";

		AnalyzerRunResult padding = await RoslynTestHarness.RunAsync<RemoveBracePaddingAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(1, padding.Diagnostics);
		RoslynTestHarness.AssertFixedText(paddingFixed, padding);

		AnalyzerRunResult empty = await RoslynTestHarness.RunAsync<CollapseEmptyBlocksAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(1, empty.Diagnostics);
		RoslynTestHarness.AssertFixedText(emptyFixed, empty);
	}

	[TestMethod]
	public async Task ALabelWithAGotoIsLeftAlone()
	{
		const string source = """
			class C
			{
				void M()
				{
					goto end;

					end:
					DoThing();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,
			TestRuleSets.AllRulesEnabled
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnIterativeRunThatDoesNotSettleFailsWithTheDiagnosticsStillStanding()
	{
		const string source = """
			class C
			{
			}
			""";

		AssertFailedException failure = await Assert.ThrowsExactlyAsync<AssertFailedException>(() =>
			RoslynTestHarness.RunIterativelyAsync(
				source,

				new RunOptions([new AlwaysReportingAnalyzer()])
				{
					Providers = [new AlwaysChangingFixProvider()],
				}
			)
		);

		Assert.Contains("did not converge", failure.Message);
		Assert.Contains(AlwaysReportingAnalyzer.Descriptor.Id, failure.Message);
	}

	[TestMethod]
	public async Task ARunOfARuleThatShipsOffFailsUnlessASeverityNamesIt()
	{
		const string source = """
			class C
			{
			}
			""";

		AssertFailedException failure = await Assert.ThrowsExactlyAsync<AssertFailedException>(() =>
			RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(source)
		);

		Assert.Contains("ships off", failure.Message);

		AssertFailedException byDefault = await Assert.ThrowsExactlyAsync<AssertFailedException>(
			() =>
				RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
					source,

					new Dictionary<string, ReportDiagnostic>
					{
						[DiagnosticDescriptors.RemoveRedundantBraces.Id] = ReportDiagnostic.Default,
					}
				)
		);

		Assert.Contains("ships off", byDefault.Message);

		AnalyzerRunResult disabled =
			await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
				source,

				new Dictionary<string, ReportDiagnostic>
				{
					[DiagnosticDescriptors.RemoveRedundantBraces.Id] = ReportDiagnostic.Suppress,
				}
			);

		Assert.IsEmpty(disabled.Diagnostics);
	}

	// Each row writes the ending the other form of the fixture does not use.
	[TestMethod]
	[DataRow(true)]
	[DataRow(false)]
	public async Task AFixThatWritesTheOtherLineEndingFailsTheRun(bool carriageReturn)
	{
		const string source = """
			class C
			{
			}
			""";

		AssertFailedException once = await Assert.ThrowsExactlyAsync<AssertFailedException>(() =>
			RoslynTestHarness.RunOneAsync(
				source,
				new UnmarkedTreeAnalyzer(),
				providers: [new LineBreakMarkerFixProvider(carriageReturn)]
			)
		);

		AssertFailedException iterative = await Assert.ThrowsExactlyAsync<AssertFailedException>(
			() =>
				RoslynTestHarness.RunIterativelyAsync(
					source,

					new RunOptions([new UnmarkedTreeAnalyzer()])
					{
						Providers = [new LineBreakMarkerFixProvider(carriageReturn)],
					}
				)
		);

		Assert.Contains("line ending", once.Message);
		Assert.Contains("line ending", iterative.Message);
	}

	[TestMethod]
	public async Task AFixThatWritesATokenForATriviaRuleFailsTheRun()
	{
		const string source = """
			class C
			{
			}
			""";

		AssertFailedException failure = await Assert.ThrowsExactlyAsync<AssertFailedException>(() =>
			RoslynTestHarness.RunOneAsync(
				source,
				new UnmarkedTreeAnalyzer(),
				providers: [new MarkerClassFixProvider()]
			)
		);

		Assert.Contains("changed a token", failure.Message);
	}

	[TestMethod]
	public async Task AFixThatLeavesADefaultRuleReportingFailsTheRun()
	{
		const string source = """
			class C
			{
				void A()
				{
					A();
				}

				void B()
				{
					B();
				}
			}
			""";

		AssertFailedException once = await Assert.ThrowsExactlyAsync<AssertFailedException>(() =>
			RoslynTestHarness.RunOneAsync(
				source,
				new UnmarkedTreeAnalyzer(),
				providers: [new GapClosingFixProvider()]
			)
		);

		AssertFailedException iterative = await Assert.ThrowsExactlyAsync<AssertFailedException>(
			() =>
				RoslynTestHarness.RunIterativelyAsync(
					source,

					new RunOptions([new UnmarkedTreeAnalyzer()])
					{
						Providers = [new GapClosingFixProvider()],
					}
				)
		);

		Assert.Contains(DiagnosticDescriptors.SeparateMultiLineConstructs.Id, once.Message);
		Assert.Contains(DiagnosticDescriptors.SeparateMultiLineConstructs.Id, iterative.Message);
	}

	[TestMethod]
	public async Task ASnippetThatDoesNotCompileFailsTheRun()
	{
		const string source = """
			class C
			{
				int
			}
			""";

		AssertFailedException once = await Assert.ThrowsExactlyAsync<AssertFailedException>(() =>
			RoslynTestHarness.RunOneAsync(
				source,
				new UnmarkedTreeAnalyzer(),
				providers: [new LineBreakMarkerFixProvider(carriageReturn: false)]
			)
		);

		AssertFailedException iterative = await Assert.ThrowsExactlyAsync<AssertFailedException>(
			() =>
				RoslynTestHarness.RunIterativelyAsync(
					source,

					new RunOptions([new UnmarkedTreeAnalyzer()])
					{
						Providers = [new LineBreakMarkerFixProvider(carriageReturn: false)],
					}
				)
		);

		Assert.Contains("does not compile", once.Message);
		Assert.Contains("does not compile", iterative.Message);
	}

	[TestMethod]
	public async Task AFixThatBreaksTheSnippetFailsTheRun()
	{
		const string source = """
			class C
			{
			}
			""";

		AssertFailedException once = await Assert.ThrowsExactlyAsync<AssertFailedException>(() =>
			RoslynTestHarness.RunOneAsync(
				source,
				new UnmarkedTreeAnalyzer(),
				providers: [new BrokenSnippetFixProvider()]
			)
		);

		AssertFailedException iterative = await Assert.ThrowsExactlyAsync<AssertFailedException>(
			() =>
				RoslynTestHarness.RunIterativelyAsync(
					source,

					new RunOptions([new UnmarkedTreeAnalyzer()])
					{
						Providers = [new BrokenSnippetFixProvider()],
					}
				)
		);

		Assert.Contains("does not compile", once.Message);
		Assert.Contains("does not compile", iterative.Message);
	}

	[TestMethod]
	public async Task AFixThatWritesOtherTextInTheOtherLineEndingFailsTheRun()
	{
		const string source = """
			class C
			{
			}
			""";

		AssertFailedException once = await Assert.ThrowsExactlyAsync<AssertFailedException>(() =>
			RoslynTestHarness.RunOneAsync(
				source,
				new UnmarkedTreeAnalyzer(),
				providers: [new EndingDependentMarkerFixProvider()]
			)
		);

		AssertFailedException iterative = await Assert.ThrowsExactlyAsync<AssertFailedException>(
			() =>
				RoslynTestHarness.RunIterativelyAsync(
					source,

					new RunOptions([new UnmarkedTreeAnalyzer()])
					{
						Providers = [new EndingDependentMarkerFixProvider()],
					}
				)
		);

		Assert.Contains("differs between the CRLF and the LF form", once.Message);
		Assert.Contains("differs between the CRLF and the LF form", iterative.Message);
	}

	[TestMethod]
	public async Task AFixThatLeavesItsDiagnosticStandingFailsASingleRunOnTheSecondPass()
	{
		const string source = """
			class C
			{
			}
			""";

		AssertFailedException failure = await Assert.ThrowsExactlyAsync<AssertFailedException>(() =>
			RoslynTestHarness.RunOneAsync(
				source,
				new AlwaysReportingAnalyzer(),
				providers: [new AlwaysChangingFixProvider()]
			)
		);

		Assert.Contains("for a second pass", failure.Message);
		Assert.Contains(AlwaysReportingAnalyzer.Descriptor.Id, failure.Message);
	}

	[TestMethod]
	public async Task ADiagnosticThatStandsAtOtherPlacesInTheOtherLineEndingFailsTheRun()
	{
		const string source = """
			class C
			{
			}
			""";

		AssertFailedException failure = await Assert.ThrowsExactlyAsync<AssertFailedException>(() =>
			RoslynTestHarness.RunOneAsync(
				source,
				new FixedOffsetAnalyzer(),
				providers: [new EndingAwareMarkerFixProvider()]
			)
		);

		Assert.Contains("different places", failure.Message);
	}

	[TestMethod]
	public async Task AFixAllPerIdThatNeverSettlesFailsTheRun()
	{
		const string source = """
			class C
			{
			}
			""";

		AssertFailedException failure = await Assert.ThrowsExactlyAsync<AssertFailedException>(() =>
			RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
				expected: source,
				source,

				new RunOptions([new AlwaysReportingAnalyzer()])
				{
					Providers = [new AlwaysChangingFixProvider()],
				}
			)
		);

		Assert.Contains("did not settle", failure.Message);
	}

	[TestMethod]
	public async Task AFixAllPerIdThatSettlesOnOtherTextThanExpectedFailsTheRun()
	{
		const string source = """
			class C
			{
			}
			""";

		AssertFailedException failure = await Assert.ThrowsExactlyAsync<AssertFailedException>(() =>
			RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
				expected: source,
				source,

				new RunOptions([new UnmarkedTreeAnalyzer()])
				{
					Providers = [new EndingAwareMarkerFixProvider()],
				}
			)
		);

		Assert.Contains("settle on different text", failure.Message);
	}

	[TestMethod]
	public async Task AFixAllThatOffersNoActionFailsTheRun()
	{
		const string source = """
			class C
			{
			}
			""";

		AssertFailedException failure = await Assert.ThrowsExactlyAsync<AssertFailedException>(() =>
			RoslynTestHarness.RunFixAllAsync(
				source,

				new RunOptions([new UnmarkedTreeAnalyzer()])
				{
					Providers = [new NoFixAllActionProvider()],
				}
			)
		);

		Assert.Contains("offers no Fix All", failure.Message);
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
	public async Task AnAnalyzerThatThrowsFailsTheRun()
	{
		const string source = """
			class C
			{
			}
			""";

		AnalyzerCrashException crash = await Assert.ThrowsExactlyAsync<AnalyzerCrashException>(() =>
			RoslynTestHarness.RunIterativelyAsync(
				source,
				new RunOptions([new ThrowingAnalyzer()]) { Providers = [] }
			)
		);

		Assert.Contains(nameof(ThrowingAnalyzer), crash.Message);
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

	// Reports at the start of each tree, so every pass finds the diagnostic the last fix left.
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	private sealed class AlwaysReportingAnalyzer : DiagnosticAnalyzer
	{
		public static DiagnosticDescriptor Descriptor { get; } =
			new(
				id: "ZA9999",
				title: "A diagnostic that no fix clears",
				messageFormat: "Clear this",
				category: "Test",
				defaultSeverity: DiagnosticSeverity.Warning,
				isEnabledByDefault: true
			);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
		[Descriptor];

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();

			context.RegisterSyntaxTreeAction(treeContext =>
				treeContext.ReportDiagnostic(
					Diagnostic.Create(
						Descriptor,
						Location.Create(treeContext.Tree, new TextSpan(0, 0))
					)
				)
			);
		}
	}

	// Throws from its first syntax tree action, so the run reports AD0001.
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	private sealed class ThrowingAnalyzer : DiagnosticAnalyzer
	{
		private static readonly DiagnosticDescriptor s_descriptor = new(
			id: "ZA9997",
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

			context.RegisterSyntaxTreeAction(_ =>
				throw new InvalidOperationException("The fake analyzer fails on purpose.")
			);
		}
	}

	// Reports at the start of a tree until its marker comment is there.
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	private sealed class UnmarkedTreeAnalyzer : DiagnosticAnalyzer
	{
		public const string Marker = "// marked";

		public static DiagnosticDescriptor Descriptor { get; } =
			new(
				id: "ZA9996",
				title: "A tree without its marker",
				messageFormat: "Mark this tree",
				category: "Test",
				defaultSeverity: DiagnosticSeverity.Warning,
				isEnabledByDefault: true
			);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
		[Descriptor];

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();

			context.RegisterSyntaxTreeAction(treeContext =>
			{
				SourceText text = treeContext.Tree.GetText(treeContext.CancellationToken);

				if (text.ToString().Contains(Marker))
					return;

				treeContext.ReportDiagnostic(
					Diagnostic.Create(
						Descriptor,
						Location.Create(treeContext.Tree, new TextSpan(0, 0))
					)
				);
			});
		}
	}

	// Writes the marker line with a CRLF or an LF, whatever ending the document uses.
	private sealed class LineBreakMarkerFixProvider(bool carriageReturn) : CodeFixProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds { get; } =
		[UnmarkedTreeAnalyzer.Descriptor.Id];

		public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

		public override Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			context.RegisterCodeFix(
				CodeAction.Create(
					"Mark the tree",

					async cancellationToken =>
					{
						SourceText text = await context.Document.GetTextAsync(cancellationToken);

						return context.Document.WithText(
							SourceText.From(
								$"{UnmarkedTreeAnalyzer.Marker}{(carriageReturn ? "\r\n" : "\n")}{text}"
							)
						);
					},

					equivalenceKey: "MarkTheTree"
				),

				context.Diagnostics
			);

			return Task.CompletedTask;
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

	// Reports at a fixed offset until the marker is there, which falls on another line in the other ending.
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	private sealed class FixedOffsetAnalyzer : DiagnosticAnalyzer
	{
		public static DiagnosticDescriptor Descriptor { get; } =
			new(
				id: "ZA9995",
				title: "A tree without its marker",
				messageFormat: "Mark this tree",
				category: "Test",
				defaultSeverity: DiagnosticSeverity.Warning,
				isEnabledByDefault: true
			);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
		[Descriptor];

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();

			context.RegisterSyntaxTreeAction(treeContext =>
			{
				SourceText text = treeContext.Tree.GetText(treeContext.CancellationToken);

				if (text.ToString().Contains(UnmarkedTreeAnalyzer.Marker))
					return;

				treeContext.ReportDiagnostic(
					Diagnostic.Create(
						Descriptor,
						Location.Create(treeContext.Tree, new TextSpan(10, 0))
					)
				);
			});
		}
	}

	// Writes the marker line with the ending the document uses.
	private sealed class EndingAwareMarkerFixProvider : CodeFixProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds { get; } =
		[UnmarkedTreeAnalyzer.Descriptor.Id, FixedOffsetAnalyzer.Descriptor.Id];

		public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

		public override Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			context.RegisterCodeFix(
				CodeAction.Create(
					"Mark the tree",

					async cancellationToken =>
					{
						SourceText text = await context.Document.GetTextAsync(cancellationToken);
						string ending = text.ToString().Contains("\r\n") ? "\r\n" : "\n";

						return context.Document.WithText(
							SourceText.From($"{UnmarkedTreeAnalyzer.Marker}{ending}{text}")
						);
					},

					equivalenceKey: "MarkTheTreeAsItsEndingGoes"
				),

				context.Diagnostics
			);

			return Task.CompletedTask;
		}
	}

	// Writes a longer marker line in a CRLF document than in an LF one, so the two forms of a fixture settle on other text.
	private sealed class EndingDependentMarkerFixProvider : CodeFixProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds { get; } =
		[UnmarkedTreeAnalyzer.Descriptor.Id];

		public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

		public override Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			context.RegisterCodeFix(
				CodeAction.Create(
					"Mark the tree by its ending",

					async cancellationToken =>
					{
						SourceText text = await context.Document.GetTextAsync(cancellationToken);
						bool crlf = text.ToString().Contains("\r\n");

						return context.Document.WithText(
							SourceText.From(
								crlf
									? $"{UnmarkedTreeAnalyzer.Marker} in crlf\r\n{text}"
									: $"{UnmarkedTreeAnalyzer.Marker}\n{text}"
							)
						);
					},

					equivalenceKey: "MarkTheTreeByItsEnding"
				),

				context.Diagnostics
			);

			return Task.CompletedTask;
		}
	}

	// Writes the marker and a directive that does not compile, which leaves every token as it was.
	private sealed class BrokenSnippetFixProvider : CodeFixProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds { get; } =
		[UnmarkedTreeAnalyzer.Descriptor.Id];

		public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

		public override Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			context.RegisterCodeFix(
				CodeAction.Create(
					"Mark the tree with a broken line",

					async cancellationToken =>
					{
						SourceText text = await context.Document.GetTextAsync(cancellationToken);

						return context.Document.WithText(
							SourceText.From($"{UnmarkedTreeAnalyzer.Marker}\n#error broken\n{text}")
						);
					},

					equivalenceKey: "MarkTheTreeWithABrokenLine"
				),

				context.Diagnostics
			);

			return Task.CompletedTask;
		}
	}

	// Offers a fix whose Fix All provider has no action to give.
	private sealed class NoFixAllActionProvider : CodeFixProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds { get; } =
		[UnmarkedTreeAnalyzer.Descriptor.Id];

		public override FixAllProvider GetFixAllProvider() => new NoActionFixAllProvider();

		public override Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			context.RegisterCodeFix(
				CodeAction.Create(
					"Mark the tree",
					cancellationToken => Task.FromResult(context.Document),
					equivalenceKey: "MarkTheTreeNever"
				),

				context.Diagnostics
			);

			return Task.CompletedTask;
		}
	}

	private sealed class NoActionFixAllProvider : FixAllProvider
	{
		public override Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext) =>
			Task.FromResult<CodeAction?>(null);
	}

	// Writes the marker with a class after it, so the fix adds tokens though its rule is not a token rule.
	private sealed class MarkerClassFixProvider : CodeFixProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds { get; } =
		[UnmarkedTreeAnalyzer.Descriptor.Id];

		public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

		public override Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			context.RegisterCodeFix(
				CodeAction.Create(
					"Mark the tree with a class",

					async cancellationToken =>
					{
						SourceText text = await context.Document.GetTextAsync(cancellationToken);

						return context.Document.WithText(
							SourceText.From(
								$"{UnmarkedTreeAnalyzer.Marker}\nclass Marked {{ }}\n{text}"
							)
						);
					},

					equivalenceKey: "MarkTheTreeWithAClass"
				),

				context.Diagnostics
			);

			return Task.CompletedTask;
		}
	}

	// Writes the marker and removes the blank line between the members, which ZA0004 wants back.
	private sealed partial class GapClosingFixProvider : CodeFixProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds { get; } =
		[UnmarkedTreeAnalyzer.Descriptor.Id];

		public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

		public override Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			context.RegisterCodeFix(
				CodeAction.Create(
					"Close the gap",

					async cancellationToken =>
					{
						SourceText text = await context.Document.GetTextAsync(cancellationToken);

						string closed = GapBeforeB().Replace(text.ToString(), "}$1$2");

						return context.Document.WithText(
							SourceText.From($"{UnmarkedTreeAnalyzer.Marker}\n{closed}")
						);
					},

					equivalenceKey: "CloseTheGap"
				),

				context.Diagnostics
			);

			return Task.CompletedTask;
		}

		[GeneratedRegex(@"\}(\r?\n)\r?\n(\s+void B)", RegexOptions.CultureInvariant)]
		private static partial Regex GapBeforeB();
	}

	// Appends a line on each fix, so the document changes every round and the diagnostic stays.
	private sealed class AlwaysChangingFixProvider : CodeFixProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds { get; } =
		[AlwaysReportingAnalyzer.Descriptor.Id];

		public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

		public override Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			context.RegisterCodeFix(
				CodeAction.Create(
					"Append a line",

					async cancellationToken =>
					{
						SourceText text = await context.Document.GetTextAsync(cancellationToken);
						return context.Document.WithText(SourceText.From($"{text}\n"));
					},

					equivalenceKey: "AppendALine"
				),

				context.Diagnostics
			);

			return Task.CompletedTask;
		}
	}
#pragma warning restore RS1036, RS1038, RS1041, RS2008
}
