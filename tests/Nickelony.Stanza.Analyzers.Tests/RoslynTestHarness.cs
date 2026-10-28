using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.CodeFixes;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>Runs snippets through the analyzers in an ad hoc workspace.</summary>
internal static partial class RoslynTestHarness
{
	// How many rounds a run may take before it must have settled:
	// one fix a round, or one Fix All per id.
	private const int MaxFixRounds = 50;

	/// <summary>Every analyzer the product ships, found by reflection.</summary>
	internal static ImmutableArray<DiagnosticAnalyzer> AllAnalyzers { get; } =
		CreateAll<DiagnosticAnalyzer>(typeof(DiagnosticDescriptors).Assembly);

	/// <summary>Applies the first fix of each diagnostic and asserts a fixed point.</summary>
	public static Task<AnalyzerRunResult> RunAsync<TAnalyzer>(
		string source,
		IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions = null,
		IReadOnlyDictionary<string, string>? analyzerConfigOptions = null,
		IReadOnlyDictionary<string, ReportDiagnostic>? treeOptions = null,
		IReadOnlyDictionary<string, ReportDiagnostic>? globalOptions = null
	)
		where TAnalyzer : DiagnosticAnalyzer, new()
	{
		RunOptions options = new([new TAnalyzer()])
		{
			DiagnosticOptions = diagnosticOptions,
			AnalyzerConfigOptions = analyzerConfigOptions,
			TreeOptions = treeOptions,
			GlobalOptions = globalOptions,
		};

		return RunAsync(source, options);
	}

	/// <summary>Runs one analyzer with the given rules enabled and applies its fixes.</summary>
	public static Task<AnalyzerRunResult> RunOneAsync(
		string source,
		DiagnosticAnalyzer analyzer,
		IReadOnlyDictionary<string, ReportDiagnostic> diagnosticOptions,
		IReadOnlyDictionary<string, string>? analyzerConfigOptions = null
	)
	{
		RunOptions options = new([analyzer])
		{
			DiagnosticOptions = diagnosticOptions,
			AnalyzerConfigOptions = analyzerConfigOptions,
		};

		return RunAsync(source, options);
	}

	// The providers are a parameter so a test can run a fake fix through the harness's own checks.
	internal static Task<AnalyzerRunResult> RunOneAsync(
		string source,
		DiagnosticAnalyzer analyzer,
		ImmutableArray<CodeFixProvider> providers
	)
	{
		return RunAsync(source, new RunOptions([analyzer]) { Providers = providers });
	}

	public static Task<AnalyzerRunResult> RunAllAsync(
		string source,
		IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions = null,
		IReadOnlyDictionary<string, string>? analyzerConfigOptions = null
	)
	{
		RunOptions options = new(AllAnalyzers)
		{
			DiagnosticOptions = diagnosticOptions,
			AnalyzerConfigOptions = analyzerConfigOptions,
		};

		return RunAsync(source, options);
	}

	/// <summary>Reports what one analyzer finds and applies no fix.</summary>
	public static Task<AnalyzerRunResult> ReportAsync<TAnalyzer>(
		string source,
		IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions = null,
		IReadOnlyDictionary<string, string>? analyzerConfigOptions = null,
		IReadOnlyDictionary<string, ReportDiagnostic>? treeOptions = null,
		IReadOnlyDictionary<string, ReportDiagnostic>? globalOptions = null
	)
		where TAnalyzer : DiagnosticAnalyzer, new()
	{
		RunOptions options = new([new TAnalyzer()])
		{
			DiagnosticOptions = diagnosticOptions,
			AnalyzerConfigOptions = analyzerConfigOptions,
			TreeOptions = treeOptions,
			GlobalOptions = globalOptions,
		};

		return ReportAsync(source, options);
	}

	/// <summary>Reports what every analyzer finds and applies no fix.</summary>
	public static Task<AnalyzerRunResult> ReportAllAsync(
		string source,
		IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions = null,
		IReadOnlyDictionary<string, string>? analyzerConfigOptions = null
	)
	{
		RunOptions options = new(AllAnalyzers)
		{
			DiagnosticOptions = diagnosticOptions,
			AnalyzerConfigOptions = analyzerConfigOptions,
		};

		return ReportAsync(source, options);
	}

	/// <summary>Reports what the analyzers find in both line endings and applies no fix.</summary>
	internal static async Task<AnalyzerRunResult> ReportAsync(string source, RunOptions options)
	{
		if (options.Analyzers.Length == 1)
		{
			AssertAnalyzerRuns(
				analyzer: options.Analyzers[0],
				diagnosticOptions: options.DiagnosticOptions,
				treeOptions: options.TreeOptions,
				globalOptions: options.GlobalOptions
			);
		}

		AnalyzerRunResult result = await RunOnBothEndingsAsync(
				source,
				run: text => ReportOnceAsync(text, options)
			)
			.ConfigureAwait(false);

		return Recorded(options, source, result);
	}

	private static async Task<AnalyzerRunResult> ReportOnceAsync(string source, RunOptions options)
	{
		using var workspace = new AdhocWorkspace();

		Document document = CreateDocument(workspace, source, options);

		ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(
				document,
				analyzers: options.Analyzers,
				cancellationToken: HarnessBudget.Token,
				requireCompiles: options.RequireCompiles
			)
			.ConfigureAwait(false);

		return CreateUnfixedResult(diagnostics, SourceText.From(source));
	}

	public static async Task<ImmutableArray<Diagnostic>> RunAllOnSupportDocumentAsync(
		IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions = null
	)
	{
		using var workspace = new AdhocWorkspace();

		Document snippet = CreateDocument(workspace, string.Empty, diagnosticOptions);

		Document support = snippet.Project.Documents.Single(document =>
			document.Name == SupportName
		);

		Compilation? compilation = await support
			.Project.GetCompilationAsync(HarnessBudget.Token)
			.ConfigureAwait(false);

		// A null compilation would give the guard nothing to read and let it pass, so it fails the run.
		// The support document always compiles, so this branch means the harness is broken.
		if (compilation is null)
		{
			Assert.Fail("The support document compiled to nothing, so no tree was read.");
			return [];
		}

		SyntaxTree? supportTree = await support
			.GetSyntaxTreeAsync(HarnessBudget.Token)
			.ConfigureAwait(false);

		if (supportTree is null)
		{
			Assert.Fail("The support document has no syntax tree, so no tree was read.");
			return [];
		}

		ImmutableArray<Diagnostic> diagnostics =
		[
			.. await compilation
				.WithAnalyzers(AllAnalyzers)
				.GetAnalyzerDiagnosticsAsync(HarnessBudget.Token)
				.ConfigureAwait(false),
		];

		AssertNoAnalyzerCrash(diagnostics);

		return Recorded(
			AllAnalyzers,
			[.. diagnostics.Where(diagnostic => diagnostic.Location.SourceTree == supportTree)],
			diagnosticOptions
		);
	}

	public static async Task<ImmutableArray<Diagnostic>> RunWithoutCompilingAsync(
		string source,
		IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions = null
	)
	{
		using var workspace = new AdhocWorkspace();

		Document document = CreateDocument(workspace, source, diagnosticOptions);

		Compilation? compilation = await document
			.Project.GetCompilationAsync(HarnessBudget.Token)
			.ConfigureAwait(false);

		if (compilation is null)
			return [];

		SyntaxTree? snippetTree = await document
			.GetSyntaxTreeAsync(HarnessBudget.Token)
			.ConfigureAwait(false);

		ImmutableArray<Diagnostic> diagnostics =
		[
			.. await compilation
				.WithAnalyzers(AllAnalyzers)
				.GetAnalyzerDiagnosticsAsync(HarnessBudget.Token)
				.ConfigureAwait(false),
		];

		AssertNoAnalyzerCrash(diagnostics);

		return Recorded(
			AllAnalyzers,

			[
				.. diagnostics.Where(diagnostic =>
					!HasLocation(diagnostic) || diagnostic.Location.SourceTree == snippetTree
				),
			],

			diagnosticOptions
		);
	}

	// The extra analyzer is a test fake that cancels the token from inside the run.
	public static async Task<ImmutableArray<Diagnostic>> RunAllWithCancellationAsync(
		string source,
		DiagnosticAnalyzer canceler,
		CancellationToken cancellationToken
	)
	{
		using var workspace = new AdhocWorkspace();

		Document document = CreateDocument(workspace, source);

		ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(
				document,
				analyzers: [.. AllAnalyzers, canceler],
				cancellationToken
			)
			.ConfigureAwait(false);

		return Recorded(AllAnalyzers, diagnostics);
	}

	public static async Task<(
		ImmutableArray<string> Concurrent,
		ImmutableArray<string> Sequential
	)> RunOverDocumentsAsync(
		IReadOnlyList<string> sources,
		IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions = null
	)
	{
		using var workspace = new AdhocWorkspace();

		Compilation? compilation = await CreateProject(workspace, sources, diagnosticOptions)
			.GetCompilationAsync(HarnessBudget.Token)
			.ConfigureAwait(false);

		if (compilation is null)
			return ([], []);

		return (
			await GetSortedDiagnosticsAsync(compilation, concurrent: true, diagnosticOptions)
				.ConfigureAwait(false),
			await GetSortedDiagnosticsAsync(compilation, concurrent: false, diagnosticOptions)
				.ConfigureAwait(false)
		);
	}

	private static void AssertNoAnalyzerCrash(ImmutableArray<Diagnostic> diagnostics)
	{
		Diagnostic[] crashes = [.. diagnostics.Where(diagnostic => diagnostic.Id == "AD0001")];

		if (crashes.Length == 0)
			return;

		throw new AnalyzerCrashException(
			"An analyzer threw:"
				+ Environment.NewLine
				+ string.Join(Environment.NewLine, crashes.Select(crash => crash.ToString()))
		);
	}

	public static async Task<ImmutableArray<Diagnostic>> RunNamedAsync<TAnalyzer>(
		string source,
		string fileName
	)
		where TAnalyzer : DiagnosticAnalyzer, new()
	{
		var analyzer = new TAnalyzer();
		AssertAnalyzerRuns(analyzer, diagnosticOptions: null);

		using var workspace = new AdhocWorkspace();

		Document document = CreateDocument(workspace, source, diagnosticOptions: null, fileName);

		ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(
				document,
				analyzers: [analyzer],
				cancellationToken: HarnessBudget.Token
			)
			.ConfigureAwait(false);

		return Recorded([analyzer], diagnostics);
	}

	public static Task<AnalyzerRunResult> RunUnsettledAsync(
		string source,
		IReadOnlyDictionary<string, ReportDiagnostic> diagnosticOptions
	)
	{
		RunOptions options = new(AllAnalyzers)
		{
			DiagnosticOptions = diagnosticOptions,
			AssertFixedPoint = false,
		};

		return RunAsync(source, options);
	}

	/// <summary>Fixes one located diagnostic per round, up to the round limit.</summary>
	public static Task<AnalyzerRunResult> RunIterativelyAsync(
		string source,
		IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions = null,
		IReadOnlyDictionary<string, string>? analyzerConfigOptions = null
	)
	{
		RunOptions options = new(AllAnalyzers)
		{
			DiagnosticOptions = diagnosticOptions,
			AnalyzerConfigOptions = analyzerConfigOptions,
		};

		return RunIterativelyAsync(source, options);
	}

	/// <summary>Fixes as the other overload does, with any analyzers and providers.</summary>
	internal static async Task<AnalyzerRunResult> RunIterativelyAsync(
		string source,
		RunOptions options
	)
	{
		if (options.Analyzers.Length == 1)
		{
			AssertAnalyzerRuns(
				analyzer: options.Analyzers[0],
				diagnosticOptions: options.DiagnosticOptions,
				treeOptions: options.TreeOptions,
				globalOptions: options.GlobalOptions
			);
		}

		AnalyzerRunResult result = await RunOnBothEndingsAsync(
				source,
				run: text => IterateAsync(source: text, options)
			)
			.ConfigureAwait(false);

		return Recorded(options, source, result);
	}

	private static async Task<AnalyzerRunResult> IterateAsync(string source, RunOptions options)
	{
		using var workspace = new AdhocWorkspace();

		Document document = CreateDocument(workspace, source, options);

		ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(
				document,
				analyzers: options.Analyzers,
				cancellationToken: HarnessBudget.Token,
				requireCompiles: options.RequireCompiles
			)
			.ConfigureAwait(false);

		ImmutableArray<Diagnostic> pass = diagnostics;
		Document fixedDocument = document;
		int rounds = 0;

		while (rounds < MaxFixRounds)
		{
			ImmutableArray<Diagnostic> located = GetLocated(pass);

			if (located.Length == 0)
				break;

			fixedDocument = await ApplyFirstFixAsync(
					document: fixedDocument,
					diagnostic: located[0],
					cancellationToken: HarnessBudget.Token,
					providers: options.Providers
				)
				.ConfigureAwait(false);

			pass = await GetDiagnosticsAsync(
					document: fixedDocument,
					analyzers: options.Analyzers,
					cancellationToken: HarnessBudget.Token,
					requireCompiles: options.RequireCompiles
				)
				.ConfigureAwait(false);

			rounds++;
		}

		// The whole pass has to be empty, not only the diagnostics that stand in the document:
		// a report with no location is a diagnostic no lightbulb answers,
		// which an editor would leave standing.
		if (options.AssertFixedPoint)
		{
			Assert.IsEmpty(
				pass,

				$"The fixes did not converge in {MaxFixRounds} rounds: "
					+ string.Join(
						",",
						pass.Select(diagnostic => diagnostic.Id).Distinct().OrderBy(id => id)
					)
			);
		}

		// A run of all the analyzers has the default rules inside it, so only a single-rule run can miss them.
		if (options.AssertFixedPoint && options.Analyzers.Length == 1 && !s_skipsDefaultRules.Value)
		{
			await AssertNoNewDefaultDiagnosticsAsync(
					original: document,
					fixedDocument,
					requireCompiles: options.RequireCompiles
				)
				.ConfigureAwait(false);
		}

		SourceText fixedText = await fixedDocument
			.GetTextAsync(HarnessBudget.Token)
			.ConfigureAwait(false);

		return new AnalyzerRunResult(diagnostics, fixedText.ToString());
	}

	public static async Task<(
		ImmutableArray<Diagnostic> First,
		ImmutableArray<Diagnostic> Second
	)> RunTwiceOnSameTreeAsync<TAnalyzer>(
		string source,
		IReadOnlyDictionary<string, string> firstOptions,
		IReadOnlyDictionary<string, string> secondOptions
	)
		where TAnalyzer : DiagnosticAnalyzer, new()
	{
		AssertAnalyzerRuns(new TAnalyzer(), diagnosticOptions: null);

		using var workspace = new AdhocWorkspace();

		Document document = CreateDocument(workspace, source);

		// Both runs read one tree through two providers, which a generated config cannot give.
		ImmutableArray<Diagnostic> first = await GetDiagnosticsAsync(
				document,
				analyzers: [new TAnalyzer()],
				cancellationToken: HarnessBudget.Token,
				analyzerOptions: ReadFrom(firstOptions)
			)
			.ConfigureAwait(false);

		ImmutableArray<Diagnostic> second = await GetDiagnosticsAsync(
				document,
				analyzers: [new TAnalyzer()],
				cancellationToken: HarnessBudget.Token,
				analyzerOptions: ReadFrom(secondOptions)
			)
			.ConfigureAwait(false);

		_ = Recorded([new TAnalyzer()], first.AddRange(second));

		return (first, second);
	}

	private static async Task<AnalyzerRunResult> RunAsync(string source, RunOptions options)
	{
		if (options.Analyzers.Length == 1)
		{
			AssertAnalyzerRuns(
				analyzer: options.Analyzers[0],
				diagnosticOptions: options.DiagnosticOptions,
				treeOptions: options.TreeOptions,
				globalOptions: options.GlobalOptions
			);
		}

		AnalyzerRunResult result = await RunOnBothEndingsAsync(
				source,
				run: text => RunOnceAsync(source: text, options)
			)
			.ConfigureAwait(false);

		return Recorded(options, source, result);
	}

	private static AnalyzerRunResult Recorded(
		RunOptions options,
		string source,
		AnalyzerRunResult result
	)
	{
		HarnessRecord.Add(options, diagnostics: result.Diagnostics, result, source);
		return result;
	}

	private static ImmutableArray<Diagnostic> Recorded(
		ImmutableArray<DiagnosticAnalyzer> analyzers,
		ImmutableArray<Diagnostic> diagnostics,
		IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions = null
	)
	{
		HarnessRecord.Add(
			new RunOptions(analyzers) { DiagnosticOptions = diagnosticOptions },
			diagnostics
		);

		return diagnostics;
	}

	private static AnalyzerOptions ReadFrom(IReadOnlyDictionary<string, string> keys) =>
		new([], new TestAnalyzerConfigOptionsProvider(keys));

	// A fixture has the line endings of its checkout, so a run on the other ending tests the other.
	// The other run has to write the first run's text in its own ending, and report at the same places.
	private static async Task<AnalyzerRunResult> RunOnBothEndingsAsync(
		string source,
		Func<string, Task<AnalyzerRunResult>> run
	)
	{
		AnalyzerRunResult result = await run(source).ConfigureAwait(false);
		string? otherSource = SwapLineEndings(source);

		AssertKeepsTheEnding(source, result.FixedText);

		if (otherSource is null)
			return result;

		AnalyzerRunResult otherResult = await run(otherSource).ConfigureAwait(false);

		AssertKeepsTheEnding(otherSource, otherResult.FixedText);

		Assert.AreEqual(
			WithEndingOf(otherSource, result.FixedText),
			otherResult.FixedText,
			"The fix differs between the CRLF and the LF form of the source, or writes a line ending the document does not use."
		);

		Assert.AreSequenceEqual(
			DescribePositions(result.Diagnostics),
			DescribePositions(otherResult.Diagnostics),
			"The diagnostics stand at different places in the CRLF and the LF form of the source."
		);

		return result;
	}

	// The comparison above converts each break, so a source that uses one ending is checked for a break of the other kind.
	private static void AssertKeepsTheEnding(string source, string fixedText)
	{
		bool crlf = source.Contains("\r\n", StringComparison.Ordinal);

		if (HasOtherEnding(source, crlf))
			return;

		Assert.IsFalse(
			HasOtherEnding(fixedText, crlf),
			"The fix writes a line ending the document does not use."
		);
	}

	private static bool HasOtherEnding(string text, bool crlf)
	{
		if (!crlf)
			return text.Contains('\r', StringComparison.Ordinal);

		for (int index = 0; index < text.Length; index++)
		{
			bool loneCarriageReturn =
				text[index] == '\r' && (index + 1 == text.Length || text[index + 1] != '\n');

			bool loneLineFeed = text[index] == '\n' && (index == 0 || text[index - 1] != '\r');

			if (loneCarriageReturn || loneLineFeed)
				return true;
		}

		return false;
	}

	private static string WithEndingOf(string source, string text)
	{
		string lineFeeds = ToLineFeed(text);

		return source.Contains("\r\n", StringComparison.Ordinal)
			? lineFeeds.Replace("\n", "\r\n", StringComparison.Ordinal)
			: lineFeeds;
	}

	private static string[] DescribePositions(ImmutableArray<Diagnostic> diagnostics)
	{
		return
		[
			.. diagnostics
				.Select(diagnostic =>
					$"{diagnostic.Id} {(HasLocation(diagnostic) ? diagnostic.Location.GetLineSpan().Span.ToString() : "nowhere")}"
				)
				.Order(StringComparer.Ordinal),
		];
	}

	// A rule that ships off reports nothing unless a severity names it, so a test of it would pass with no report.
	// A severity of none counts, because a test that disables the rule expects no report.
	// A severity of default does not, because it leaves the rule off.
	internal static void AssertAnalyzerRuns(
		DiagnosticAnalyzer analyzer,
		IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions,
		IReadOnlyDictionary<string, ReportDiagnostic>? treeOptions = null,
		IReadOnlyDictionary<string, ReportDiagnostic>? globalOptions = null
	)
	{
		string[] inert =
		[
			.. analyzer
				.SupportedDiagnostics.Where(descriptor =>
					!descriptor.IsEnabledByDefault
					&& !Names(diagnosticOptions, descriptor.Id)
					&& !Names(treeOptions, descriptor.Id)
					&& !Names(globalOptions, descriptor.Id)
				)
				.Select(descriptor => descriptor.Id),
		];

		Assert.IsEmpty(
			inert,
			$"{analyzer.GetType().Name} runs {string.Join(",", inert)}, which ships off, and no severity enables it."
		);
	}

	private static bool Names(IReadOnlyDictionary<string, ReportDiagnostic>? severities, string id)
	{
		return severities?.TryGetValue(id, out ReportDiagnostic severity) == true
			&& severity != ReportDiagnostic.Default;
	}

	// Returns the source in the other line ending, or null when it has no line break.
	private static string? SwapLineEndings(string source)
	{
		if (source.Contains("\r\n", StringComparison.Ordinal))
			return ToLineFeed(source);

		return source.Contains('\n', StringComparison.Ordinal)
			? source.Replace("\n", "\r\n", StringComparison.Ordinal)
			: null;
	}

	private static async Task<AnalyzerRunResult> RunOnceAsync(string source, RunOptions options)
	{
		using var workspace = new AdhocWorkspace();

		Document document = CreateDocument(workspace, source, options);

		ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(
				document,
				analyzers: options.Analyzers,
				cancellationToken: HarnessBudget.Token
			)
			.ConfigureAwait(false);

		Document fixedDocument = document;
		HashSet<(string Id, int? Start, int? End)> fixedRuns = [];

		foreach (
			Diagnostic diagnostic in diagnostics
				.Where(HasLocation)
				.OrderByDescending(diagnostic => diagnostic.Location.SourceSpan.Start)
		)
		{
			// Every gap of a run carries the fix of the whole run, so the first fix applied leaves nothing for the rest.
			var fixOptions = FixOptions.Parse(diagnostic);

			if (
				fixOptions.RunStart is not null
				&& !fixedRuns.Add((diagnostic.Id, fixOptions.RunStart, fixOptions.RunEnd))
			)
			{
				continue;
			}

			fixedDocument = await ApplyFirstFixAsync(
					document: fixedDocument,
					diagnostic,
					cancellationToken: HarnessBudget.Token,
					providers: options.Providers
				)
				.ConfigureAwait(false);
		}

		SourceText fixedText = await fixedDocument
			.GetTextAsync(HarnessBudget.Token)
			.ConfigureAwait(false);

		AnalyzerRunResult result = await CompleteAsync(
				fixedDocument,
				diagnostics,
				analyzers: options.Analyzers,
				fixedText: fixedText.ToString(),
				assertFixedPoint: options.AssertFixedPoint
			)
			.ConfigureAwait(false);

		// A run of all the analyzers has the default rules inside it, so only a single-rule run can miss them.
		if (options.AssertFixedPoint && options.Analyzers.Length == 1 && !s_skipsDefaultRules.Value)
			await AssertNoNewDefaultDiagnosticsAsync(document, fixedDocument).ConfigureAwait(false);

		return result;
	}

	// A fix may not leave a default rule reporting more than the source did, whichever rule it is for.
	// The reports are counted per rule rather than placed, because a fix may rewrite the line a report stands on.
	private static async Task AssertNoNewDefaultDiagnosticsAsync(
		Document original,
		Document fixedDocument,
		bool requireCompiles = true
	)
	{
		ImmutableArray<DiagnosticAnalyzer> defaults =
		[
			.. AllAnalyzers.Where(analyzer =>
				analyzer.SupportedDiagnostics.Any(descriptor =>
					TestRuleSets.DefaultRules.Any(rule => rule.Id == descriptor.Id)
				)
			),
		];

		ImmutableArray<Diagnostic> after = await GetDiagnosticsAsync(
				document: fixedDocument,
				analyzers: defaults,
				cancellationToken: HarnessBudget.Token,
				requireCompiles
			)
			.ConfigureAwait(false);

		if (after.Length == 0)
			return;

		ImmutableArray<Diagnostic> before = await GetDiagnosticsAsync(
				document: original,
				analyzers: defaults,
				cancellationToken: HarnessBudget.Token,
				requireCompiles
			)
			.ConfigureAwait(false);

		var beforeCounts = before
			.GroupBy(diagnostic => diagnostic.Id)
			.ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

		string[] caused =
		[
			.. after
				.GroupBy(diagnostic => diagnostic.Id)
				.Where(group => group.Count() > beforeCounts.GetValueOrDefault(group.Key))
				.Select(group => group.Key)
				.Order(StringComparer.Ordinal),
		];

		Assert.IsEmpty(
			caused,
			$"The fix left more of {string.Join(",", caused)} in the fixed text than the source held."
		);
	}

	private static string ToLineFeed(string text) =>
		text.Replace("\r\n", "\n", StringComparison.Ordinal);

	private static async Task<AnalyzerRunResult> CompleteAsync(
		Document fixedDocument,
		ImmutableArray<Diagnostic> diagnostics,
		ImmutableArray<DiagnosticAnalyzer> analyzers,
		string fixedText,
		bool assertFixedPoint = true
	)
	{
		Compilation? compilation = await fixedDocument
			.Project.GetCompilationAsync(HarnessBudget.Token)
			.ConfigureAwait(false);

		if (compilation is not null)
			AssertSnippetCompiles(compilation, HarnessBudget.Token);

		ImmutableArray<Diagnostic> secondPass = await GetDiagnosticsAsync(
				document: fixedDocument,
				analyzers,
				cancellationToken: HarnessBudget.Token
			)
			.ConfigureAwait(false);

		if (assertFixedPoint)
		{
			Assert.IsEmpty(
				secondPass,

				$"The fix left {string.Join(",", secondPass.Select(diagnostic => diagnostic.Id).Distinct().OrderBy(id => id))} for a second pass: "
					+ string.Join(" | ", secondPass.Select(diagnostic => diagnostic.ToString()))
			);
		}

		return new AnalyzerRunResult(diagnostics, fixedText);
	}

	private static AnalyzerRunResult CreateUnfixedResult(
		ImmutableArray<Diagnostic> diagnostics,
		SourceText sourceText
	) => new(diagnostics, sourceText.ToString());

	private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
		Document document,
		ImmutableArray<DiagnosticAnalyzer> analyzers,
		CancellationToken cancellationToken,
		bool requireCompiles = true,
		AnalyzerOptions? analyzerOptions = null
	)
	{
		Compilation? compilation = await document
			.Project.GetCompilationAsync(cancellationToken)
			.ConfigureAwait(false);

		if (compilation is null)
			return [];

		SyntaxTree? snippetTree = await document
			.GetSyntaxTreeAsync(cancellationToken)
			.ConfigureAwait(false);

		if (requireCompiles)
			AssertSnippetCompiles(compilation, cancellationToken);

		// Without options of its own, a run reads the project's config files, as a build does, so a bulk key reaches Roslyn too.
		AnalyzerOptions options = analyzerOptions ?? document.Project.AnalyzerOptions;

		ImmutableArray<Diagnostic> diagnostics =
		[
			.. await compilation
				.WithAnalyzers(analyzers, options)
				.GetAnalyzerDiagnosticsAsync(cancellationToken)
				.ConfigureAwait(false),
		];

		AssertNoAnalyzerCrash(diagnostics);

		return
		[
			.. diagnostics.Where(diagnostic =>
				!HasLocation(diagnostic) || diagnostic.Location.SourceTree == snippetTree
			),
		];
	}

	private static void AssertSnippetCompiles(
		Compilation compilation,
		CancellationToken cancellationToken
	)
	{
		Diagnostic[] errors =
		[
			.. compilation
				.GetDiagnostics(cancellationToken)
				.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
		];

		string errorText = string.Join(
			Environment.NewLine,
			errors.Select(error => error.ToString())
		);

		Assert.IsEmpty(errors, $"The snippet does not compile:{Environment.NewLine}{errorText}");
	}

	/// <summary>Every code fix provider the product ships, found by reflection.</summary>
	internal static ImmutableArray<CodeFixProvider> AllCodeFixProviders { get; } =
		CreateAll<CodeFixProvider>(typeof(BlankLineCodeFixProvider).Assembly);

	private static ImmutableArray<T> CreateAll<T>(Assembly assembly)
		where T : class
	{
		return
		[
			.. assembly
				.GetTypes()
				.Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(T)))
				.OrderBy(type => type.FullName, StringComparer.Ordinal)
				.Select(type => (T)Activator.CreateInstance(type, nonPublic: true)!),
		];
	}

	private static bool HasLocation(Diagnostic diagnostic) =>
		diagnostic.Location.SourceTree is not null;

	private static ImmutableArray<Diagnostic> GetLocated(ImmutableArray<Diagnostic> diagnostics)
	{
		return
		[
			.. diagnostics
				.Where(HasLocation)
				.OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start),
		];
	}

	public static void AssertFixedText(
		string expected,
		AnalyzerRunResult result,
		string? message = null
	)
	{
		string prefix = message is null ? string.Empty : $"{message} ";

		Assert.AreEqual(
			expected,
			result.FixedText,
			$"{prefix}[actual] {Describe(result.FixedText)} [expected] {Describe(expected)}"
		);
	}

	/// <summary>Asserts one gap for each report, and no gap twice.</summary>
	public static void AssertEachGapIsClaimedOnce(AnalyzerRunResult result)
	{
		Assert.IsNotEmpty(result.Diagnostics);

		Diagnostic[] gaps =
		[
			.. result.Diagnostics.Where(diagnostic =>
				diagnostic.Location.SourceTree is not null
				&& diagnostic.AdditionalLocations.Count == 1
			),
		];

		Assert.HasCount(
			result.Diagnostics.Length,
			gaps,
			"A report names no gap, or more than one."
		);

		// Two reports of one place are one claim only where they are the two comment rules, each writing its own space.
		string commentPair = TestRuleSets.JoinSortedIds([
			DiagnosticDescriptors.SeparateSingleLineCommentMarkerFromText,
			DiagnosticDescriptors.SeparateTrailingCommentFromCode,
		]);

		foreach (
			IGrouping<(int Start, int Neighbor), Diagnostic> claims in gaps.GroupBy(diagnostic =>
				(
					diagnostic.Location.SourceSpan.Start,
					diagnostic.AdditionalLocations[0].SourceSpan.Start
				)
			)
		)
		{
			string ids = string.Join(
				",",
				claims.Select(diagnostic => diagnostic.Id).OrderBy(id => id, StringComparer.Ordinal)
			);

			Assert.IsTrue(
				claims.Count() == 1 || ids == commentPair,
				$"Two reports claim the same gap: {ids}."
			);
		}
	}

	/// <summary>Writes a carriage return as ~ and a tab as &lt; so a failure shows them.</summary>
	public static string Describe(string text)
	{
		return text.Replace("\r", "~", StringComparison.Ordinal)
			.Replace("\t", "<", StringComparison.Ordinal);
	}

	private static readonly AsyncLocal<bool> s_skipsDefaultRules = new();

	/// <summary>Skips re-running the default rules on fixed text until disposed.</summary>
	public static IDisposable SkippingDefaultRules()
	{
		bool previous = s_skipsDefaultRules.Value;
		s_skipsDefaultRules.Value = true;

		return new DefaultRulesScope(previous);
	}

	/// <summary>Parses the runs of the current flow at the version until disposed.</summary>
	public static IDisposable AtLanguageVersion(LanguageVersion languageVersion)
	{
		LanguageVersion? previous = s_languageVersion.Value;
		s_languageVersion.Value = languageVersion;

		return new LanguageVersionScope(previous);
	}

	/// <summary>Parses the runs of the current flow in the mode until disposed.</summary>
	public static IDisposable AtDocumentationMode(DocumentationMode documentationMode)
	{
		DocumentationMode? previous = s_documentationMode.Value;
		s_documentationMode.Value = documentationMode;

		return new DocumentationModeScope(previous);
	}

	private static async Task<ImmutableArray<string>> GetSortedDiagnosticsAsync(
		Compilation compilation,
		bool concurrent,
		IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions
	)
	{
		var options = new CompilationWithAnalyzersOptions(
			options: new AnalyzerOptions([]),
			onAnalyzerException: null,
			concurrentAnalysis: concurrent,
			logAnalyzerExecutionTime: false
		);

		ImmutableArray<Diagnostic> diagnostics =
		[
			.. await compilation
				.WithAnalyzers(AllAnalyzers, options)
				.GetAnalyzerDiagnosticsAsync(HarnessBudget.Token)
				.ConfigureAwait(false),
		];

		AssertNoAnalyzerCrash(diagnostics);

		_ = Recorded(AllAnalyzers, diagnostics, diagnosticOptions);

		return
		[
			.. diagnostics
				.Select(diagnostic => diagnostic.ToString())
				.OrderBy(text => text, StringComparer.Ordinal),
		];
	}

	private sealed class LanguageVersionScope(LanguageVersion? previous) : IDisposable
	{
		public void Dispose() => s_languageVersion.Value = previous;
	}

	private sealed class DocumentationModeScope(DocumentationMode? previous) : IDisposable
	{
		public void Dispose() => s_documentationMode.Value = previous;
	}

	private sealed class DefaultRulesScope(bool previous) : IDisposable
	{
		public void Dispose() => s_skipsDefaultRules.Value = previous;
	}

	/// <summary>Skips a fixture on a Roslyn with no C# 14, and fails it on the newest.</summary>
	public static void RequireRoslynWithCSharp14()
	{
		if (Enum.IsDefined(typeof(LanguageVersion), 1400))
			return;

#if STANZA_NEWEST_ROSLYN
		Assert.Fail(
			"The newest-Roslyn leg runs on a Roslyn that cannot parse C# 14, so its fixtures would never run."
		);
#else
		Nickelony.Stanza.Tests.Shared.ExpectedSkip.Skip(
			"Needs C# 14: test with -p:StanzaNewestRoslyn=true."
		);
#endif
	}
}
