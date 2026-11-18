using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Configuration;
using Nickelony.Stanza.Markup.Core.Options;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Configuration;

[TestClass]
public sealed class UseKnownStanzaKeyTests
{
	private const string Source = "internal sealed class Sample { }\n";

	[TestMethod]
	public async Task AMisspelledKeyIsReportedWithTheKnownKeyItNearlyNames()
	{
		AnalyzerRunResult result = await ReportAsync(
			new Dictionary<string, string> { ["stanza_max_line_lenght"] = "120" }
		);

		Assert.HasCount(1, result.Diagnostics);

		Assert.AreEqual(
			"Correct or remove the key 'stanza_max_line_lenght', which Stanza does not read; the nearest known key is 'stanza_max_line_length'",
			Message(result.Diagnostics[0])
		);
	}

	[TestMethod]
	public async Task AKeyWithNoKnownKeyNearItIsReportedWithoutASuggestion()
	{
		AnalyzerRunResult result = await ReportAsync(
			new Dictionary<string, string> { ["stanza_unrelated_setting"] = "on" }
		);

		Assert.HasCount(1, result.Diagnostics);

		Assert.AreEqual(
			"Correct or remove the key 'stanza_unrelated_setting', which Stanza does not read",
			Message(result.Diagnostics[0])
		);
	}

	[TestMethod]
	public async Task EachKeyTheReadersNameIsLeftAlone()
	{
		var keys = OptionKeys.Known.ToDictionary(key => key, _ => "1");

		AnalyzerRunResult result = await ReportAsync(keys);

		Assert.Contains(OptionKeys.MarkupProfile, keys.Keys);
		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AKeyOutsideTheStanzaFamilyIsNeverReported()
	{
		AnalyzerRunResult result = await ReportAsync(
			new Dictionary<string, string>
			{
				["max_line_length"] = "100",
				["stanza-max-line-lenght"] = "100",
				["my_stanza_key"] = "1",
				["dotnet_style_stanza_key"] = "1",
			}
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AKeyInAnyCaseIsJudgedAsRoslynReadsIt()
	{
		AnalyzerRunResult known = await ReportAsync(
			new Dictionary<string, string> { ["Stanza_Max_Line_Length"] = "100" }
		);

		AnalyzerRunResult unknown = await ReportAsync(
			new Dictionary<string, string> { ["STANZA_MAX_LINE_LENGHT"] = "100" }
		);

		Assert.IsEmpty(known.Diagnostics);
		Assert.HasCount(1, unknown.Diagnostics);
		Assert.Contains("'stanza_max_line_lenght'", Message(unknown.Diagnostics[0]));
	}

	[TestMethod]
	public async Task EachUnknownKeyIsReportedOnceForTheCompilationInKeyOrderAtTheFirstTokenOfTheFirstFile()
	{
		// The harness compiles the snippet beside a support file, so each key reaches two trees, and the first by path is the snippet.
		AnalyzerRunResult result = await ReportAsync(
			new Dictionary<string, string>
			{
				["stanza_region_paddin"] = "tight",
				["stanza_a_key_nobody_reads"] = "1",
			}
		);

		Assert.HasCount(2, result.Diagnostics);
		Assert.Contains("'stanza_a_key_nobody_reads'", Message(result.Diagnostics[0]));
		Assert.Contains("'stanza_region_paddin'", Message(result.Diagnostics[1]));

		foreach (Diagnostic diagnostic in result.Diagnostics)
		{
			Assert.AreEqual("/Test.cs", diagnostic.Location.GetLineSpan().Path);
			Assert.AreEqual("internal", diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));
		}
	}

	[TestMethod]
	public async Task AKeyOfASectionThatSkipsCSharpFilesIsNotReportedAndAGlobalKeyIs()
	{
		ImmutableArray<Diagnostic> diagnostics = await ReportConfiguredAsync(
			globalConfig: "is_global = true\n\nstanza_global_typo = 1\n",

			editorConfig: """
				root = true

				[*.md]
				stanza_markdown_typo = 1

				[*.cs]
				stanza_cs_typo = 1
				""",

			paths: [("/Sample.cs", Source)]
		);

		Assert.HasCount(2, diagnostics);
		Assert.Contains("'stanza_cs_typo'", Message(diagnostics[0]));
		Assert.Contains("'stanza_global_typo'", Message(diagnostics[1]));
	}

	[TestMethod]
	public async Task AKeyIsReportedInTheFirstNonGeneratedFileItsSectionReaches()
	{
		ImmutableArray<Diagnostic> diagnostics = await ReportConfiguredAsync(
			globalConfig: "is_global = true",

			editorConfig: """
				root = true

				[{A.g.cs,B.cs,C.cs}]
				stanza_typo = 1

				[C.cs]
				stanza_only_in_c = 1
				""",

			paths: [("/C.cs", Source), ("/A.g.cs", Source), ("/B.cs", Source)]
		);

		Assert.HasCount(2, diagnostics);
		Assert.Contains("'stanza_only_in_c'", Message(diagnostics[0]));
		Assert.AreEqual("/C.cs", diagnostics[0].Location.GetLineSpan().Path);
		Assert.Contains("'stanza_typo'", Message(diagnostics[1]));
		Assert.AreEqual("/B.cs", diagnostics[1].Location.GetLineSpan().Path);
	}

	[TestMethod]
	public async Task ASeverityOfNoneInTheEditorConfigTurnsTheReportOff()
	{
		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync(
			Source,

			new RunOptions([new UseKnownStanzaKeyAnalyzer()])
			{
				AnalyzerConfigOptions = new Dictionary<string, string> { ["stanza_typo"] = "1" },

				TreeOptions = new Dictionary<string, ReportDiagnostic>
				{
					[DiagnosticDescriptors.UseKnownStanzaKey.Id] = ReportDiagnostic.Suppress,
				},
			}
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ASeverityOfWarningInTheEditorConfigRaisesTheReport()
	{
		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync(
			Source,

			new RunOptions([new UseKnownStanzaKeyAnalyzer()])
			{
				AnalyzerConfigOptions = new Dictionary<string, string> { ["stanza_typo"] = "1" },

				TreeOptions = new Dictionary<string, ReportDiagnostic>
				{
					[DiagnosticDescriptors.UseKnownStanzaKey.Id] = ReportDiagnostic.Warn,
				},
			}
		);

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual(DiagnosticSeverity.Warning, result.Diagnostics[0].Severity);
	}

	[TestMethod]
	public async Task ASeverityOfNoneInAGlobalConfigTurnsTheReportOff()
	{
		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync(
			Source,

			new RunOptions([new UseKnownStanzaKeyAnalyzer()])
			{
				AnalyzerConfigOptions = new Dictionary<string, string> { ["stanza_typo"] = "1" },

				GlobalOptions = new Dictionary<string, ReportDiagnostic>
				{
					[DiagnosticDescriptors.UseKnownStanzaKey.Id] = ReportDiagnostic.Suppress,
				},
			}
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AFileWhoseTopPragmaHidesTheReportIsPassedOverForOneThatKeepsIt()
	{
		ImmutableArray<Diagnostic> diagnostics = await ReportConfiguredAsync(
			globalConfig: "is_global = true",
			editorConfig: "root = true\n\n[*.cs]\nstanza_typo = 1\n",

			paths:
			[
				("/A.cs", "#pragma warning disable\n" + Source),
				("/B.cs", "#pragma warning disable ZA1101\n" + Source),
				("/C.cs", "#pragma warning disable CS1591\n" + Source),
				("/D.cs", Source),
			]
		);

		Assert.HasCount(1, diagnostics);
		Assert.AreEqual("/C.cs", diagnostics[0].Location.GetLineSpan().Path);
	}

	[TestMethod]
	public async Task APragmaThatIsRestoredBeforeTheFirstTokenDoesNotHideTheReport()
	{
		ImmutableArray<Diagnostic> diagnostics = await ReportConfiguredAsync(
			globalConfig: "is_global = true",
			editorConfig: "root = true\n\n[*.cs]\nstanza_typo = 1\n",

			paths:
			[
				("/A.cs", "#pragma warning disable\n#pragma warning restore ZA1101\n" + Source),
				("/B.cs", "#pragma warning disable\n" + Source),
			]
		);

		Assert.HasCount(1, diagnostics);
		Assert.AreEqual("/A.cs", diagnostics[0].Location.GetLineSpan().Path);
	}

	[TestMethod]
	public async Task AKeyEveryFileHidesWithAPragmaIsLeftHidden()
	{
		ImmutableArray<Diagnostic> diagnostics = await ReportConfiguredAsync(
			globalConfig: "is_global = true",
			editorConfig: "root = true\n\n[*.cs]\nstanza_typo = 1\n",

			paths:
			[
				("/A.cs", "#pragma warning disable\n" + Source),
				("/B.cs", "#pragma warning disable ZA1101\n" + Source),
			]
		);

		Assert.IsEmpty(diagnostics);
	}

	[TestMethod]
	public async Task ABulkFormattingSeverityLeavesTheCheckAloneAndItsOwnCategoryKeyReachesIt()
	{
		AnalyzerRunResult formatting = await ReportWithSeverityAsync(
			"dotnet_analyzer_diagnostic.category-Formatting.severity"
		);

		AnalyzerRunResult configuration = await ReportWithSeverityAsync(
			"dotnet_analyzer_diagnostic.category-Configuration.severity"
		);

		Assert.HasCount(1, formatting.Diagnostics);
		Assert.IsEmpty(configuration.Diagnostics);
		Assert.AreEqual("Configuration", DiagnosticDescriptors.UseKnownStanzaKey.Category);
	}

	[TestMethod]
	public void TheDescriptorTellsTheHostItReportsAtTheEndOfTheCompilation()
	{
		Assert.Contains(
			WellKnownDiagnosticTags.CompilationEnd,
			DiagnosticDescriptors.UseKnownStanzaKey.CustomTags
		);
	}

	private static Task<AnalyzerRunResult> ReportWithSeverityAsync(string bulkKey)
	{
		return RoslynTestHarness.ReportAsync(
			Source,

			new RunOptions([new UseKnownStanzaKeyAnalyzer()])
			{
				AnalyzerConfigOptions = new Dictionary<string, string> { ["stanza_typo"] = "1" },

				TreeOptions = new Dictionary<string, ReportDiagnostic>
				{
					[bulkKey] = ReportDiagnostic.Suppress,
				},
			}
		);
	}

	private static string Message(Diagnostic diagnostic) =>
		diagnostic.GetMessage(CultureInfo.InvariantCulture);

	private static Task<AnalyzerRunResult> ReportAsync(IReadOnlyDictionary<string, string> keys)
	{
		return RoslynTestHarness.ReportAsync(
			Source,
			new RunOptions([new UseKnownStanzaKeyAnalyzer()]) { AnalyzerConfigOptions = keys }
		);
	}

	// Parses the files as a build does, so a section that does not match the path never reaches the analyzer.
	private static async Task<ImmutableArray<Diagnostic>> ReportConfiguredAsync(
		string globalConfig,
		string editorConfig,
		(string Path, string Source)[] paths
	)
	{
		var set = AnalyzerConfigSet.Create(ImmutableArray.Create(
			AnalyzerConfig.Parse(globalConfig, "/.globalconfig"),
			AnalyzerConfig.Parse(editorConfig, "/.editorconfig")
		));

		var compilation = CSharpCompilation.Create(
			"Probe",
			paths.Select(file => CSharpSyntaxTree.ParseText(file.Source, path: file.Path)),
			[MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]
		);

		ConfigSetProvider provider = new(set);

		return await compilation
			.WithAnalyzers([new UseKnownStanzaKeyAnalyzer()], new AnalyzerOptions([], provider))
			.GetAnalyzerDiagnosticsAsync();
	}

	private sealed class ConfigSetProvider(AnalyzerConfigSet set)
		: AnalyzerConfigOptionsProvider
	{
		public override AnalyzerConfigOptions GlobalOptions { get; } =
			new FixedConfigOptions(set.GlobalConfigOptions.AnalyzerOptions);

		public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) =>
			new FixedConfigOptions(set.GetOptionsForSourcePath(tree.FilePath).AnalyzerOptions);

		public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) =>
			FixedConfigOptions.Empty;
	}
}
