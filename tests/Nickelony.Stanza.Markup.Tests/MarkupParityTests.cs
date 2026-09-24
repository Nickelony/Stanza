extern alias stanza;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Configuration;
using Nickelony.Stanza.Markup.Core;
using Nickelony.Stanza.Markup.Core.DocComments;
using Nickelony.Stanza.Markup.Core.Formatting;
using Nickelony.Stanza.Markup.Core.Xml;
using Nickelony.Stanza.Tests.Shared;

using static Nickelony.Stanza.Tests.Shared.MarkupFormatFixtures;

namespace Nickelony.Stanza.Markup.Tests;

[TestClass]
public sealed class MarkupParityTests
{
	[TestMethod]
	public void TheAnalyzerScopeDefaultMatchesTheTool()
	{
		// With no configuration there is no profile to read, so the tool's default applies.
		var defaults = stanza::Nickelony.Stanza.Analyzers.StanzaOptions.From(new NoOptions());

		var toolDefaults = new MarkupOptions(MarkupProfiles.Xml.Policy);

		Assert.AreEqual(
			toolDefaults.Policy.ContainersOnly,
			defaults.Markup.Policy.ContainersOnly,
			"stanza_markup_scope defaults differ between the analyzer and the tool."
		);
	}

	public static IEnumerable<object[]> GetCorpusRows()
	{
		List<object[]> rows =
		[
			["a child that spans lines", LineFeed(MultiLineSiblings), "xml", 1],

			[
				"a child that spans lines, containers only",
				LineFeed(MultiLineSiblings),
				"msbuild",
				1,
			],

			["a child that spans lines, a solution", LineFeed(MultiLineSiblings), "slnx", 1],
			["a container holding character data", LineFeed(MixedContent), "xml", 1],
			["a list under a comment", LineFeed(ListUnderAComment), "xml", 1],
			["a preserved container", LineFeed(PreservedContainer), "xml", 1],
			["a CDATA section", LineFeed(CdataContainer), "xml", 1],
			["a comment between two elements", LineFeed(CommentBetweenElements), "xml", 1],
			["a trailing comment on the element before", LineFeed(TrailingComment), "xml", 1],

			[
				"a processing instruction above an element",
				LineFeed(ProcessingInstruction),
				"xml",
				1,
			],

			["a run past the budget", LineFeed(RunPastTheBudget), "xml", 1],
			["a run past the budget, budget off", LineFeed(RunPastTheBudget), "xml", 0],
			["a prolog", LineFeed(Prolog), "xml", 1],
			["single-line siblings", LineFeed(SingleLineSiblings), "xml", 1],
			["a marker touching its text", LineFeed(TouchingMarker), "xml", 1],
			["a child that spans lines, CRLF", Crlf(LineFeed(MultiLineSiblings)), "xml", 1],
			["a run past the budget, CRLF", Crlf(LineFeed(RunPastTheBudget)), "xml", 1],

			[
				"a comment between two elements, CRLF",
				Crlf(LineFeed(CommentBetweenElements)),
				"xml",
				1,
			],
		];

		return rows;
	}

	[TestMethod]
	[DynamicData(
		nameof(GetCorpusRows),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task BothEnginesReportTheSameFindings(
		string name,
		string document,
		string profileName,
		int maxBlankLines
	)
	{
		MarkupProfile profile = MarkupProfiles.FindByName(profileName)!;

		MarkupFormatResult result = Format(
			document,
			new MarkupOptions(profile.Policy, MaxBlankLines: maxBlankLines)
		);

		Assert.IsNull(
			result.Failure,
			$"{name}: the row stops parsing, so nothing was compared: {result.Failure}"
		);

		List<string> analyzer = await GetLiteralFindingsAsync(
			document,
			profile.Policy.ContainersOnly,
			maxBlankLines
		);

		List<string> tool = [];

		foreach (MarkupEdit edit in result.Edits)
			tool.Add($"{edit.Id}@{edit.Line}@{edit.Column}");

		tool.Sort(StringComparer.Ordinal);
		analyzer.Sort(StringComparer.Ordinal);

		Assert.AreSequenceEqual(
			analyzer,
			tool,
			$"{name}: the analyzer's findings are not the tool's."
		);

		if (!result.HasEdits)
			return;

		List<string> again = await GetLiteralFindingsAsync(
			result.Text,
			profile.Policy.ContainersOnly,
			maxBlankLines
		);

		Assert.IsEmpty(
			again,
			$"{name}: the analyzer asks for another blank line in what the tool wrote."
		);
	}

	// Both engines share the analysis, so one that found nothing would leave every parity row green.
	[TestMethod]
	public void TheCorpusGivesTheEnginesFindingsToCompare()
	{
		int edits = 0;

		foreach (object[] row in GetCorpusRows())
		{
			MarkupProfile profile = MarkupProfiles.FindByName((string)row[2])!;
			MarkupOptions options = new(profile.Policy, MaxBlankLines: (int)row[3]);

			edits += Format((string)row[1], options).Edits.Count;
		}

		Assert.IsGreaterThan(
			0,
			edits,
			"The corpus holds no finding, so the parity rows compare nothing."
		);
	}

	[TestMethod]
	public async Task BothEnginesAgreeOnTheTightRule()
	{
		string document = LineFeed(
			"<root>\n  <a>\n    one\n  </a>\n\n  <b>\n    two\n  </b>\n</root>\n"
		);

		MarkupProfile profile = MarkupProfiles.FindByName("xml")!;

		MarkupFormatResult result = MarkupFormatter.Format(
			document,

			new MarkupOptions(
				profile.Policy,
				Rules: MarkupRuleSet.From([MarkupDiagnostics.KeepMultiLineMarkupElementsTogetherId])
			),

			cancellationToken: CancellationToken.None
		);

		Assert.IsNull(result.Failure);

		List<string> analyzer = await GetLiteralFindingsAsync(
			document,
			containersOnly: profile.Policy.ContainersOnly,
			maxBlankLines: 1,

			enabledIds:
			[
				MarkupDiagnostics.SeparateMultiLineMarkupElementsId,
				MarkupDiagnostics.KeepMultiLineMarkupElementsTogetherId,
			]
		);

		List<string> tool = [];

		foreach (MarkupEdit edit in result.Edits)
			tool.Add($"{edit.Id}@{edit.Line}@{edit.Column}");

		tool.Sort(StringComparer.Ordinal);
		analyzer.Sort(StringComparer.Ordinal);

		Assert.AreSequenceEqual(
			analyzer,
			tool,
			"the analyzer's findings are not the tool's for a file that enables the tight rule."
		);
	}

	[TestMethod]
	public async Task BothEnginesWriteTheTightNullKeywordWhenNoSlashRuleIsEnabled()
	{
		const string source = """
			internal static class Sample
			{
				/// <summary>alpha null beta</summary>
				internal static int Bind() => 1;
			}
			""";

		var diagnosticOptions = new Dictionary<string, ReportDiagnostic>
		{
			[MarkupDiagnostics.MarkNullKeywordId] = ReportDiagnostic.Warn,
		};

		var compilation = CSharpCompilation.Create(
			assemblyName: "Parity",

			syntaxTrees:
			[
				CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest)),
			],

			references: [],

			options: new CSharpCompilationOptions(
				OutputKind.DynamicallyLinkedLibrary
			).WithSpecificDiagnosticOptions(diagnosticOptions)
		);

		var withAnalyzers = new CompilationWithAnalyzers(
			compilation,
			[new stanza::Nickelony.Stanza.Analyzers.Documentation.MarkNullKeywordAnalyzer()],

			new AnalyzerOptions(
				[],
				new FixedOptionsProvider(new FixedOptions(new Dictionary<string, string>()))
			)
		);

		ImmutableArray<Diagnostic> diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync();

		Diagnostic[] keywords =
		[
			.. diagnostics.Where(diagnostic =>
				diagnostic.Id == MarkupDiagnostics.MarkNullKeywordId
			),
		];

		Assert.HasCount(1, keywords);

		bool spaced = stanza::Nickelony.Stanza.Analyzers.FixOptions.Parse(keywords[0]).SpacedSlash;

		string analyzerMarkup =
			stanza::Nickelony.Stanza.Markup.Core.DocComments.DocTextRules.GetNullMarkup(spaced);

		const string member =
			"<member name=\"X\">\n  <summary>alpha null beta</summary>\n</member>\n";

		MarkupProfile profile = MarkupProfiles.FindByName("xml")!;

		MarkupFormatResult result = MarkupFormatter.Format(
			source: member,

			options: new MarkupOptions(
				profile.Policy,
				Rules: MarkupRuleSet.From([MarkupDiagnostics.MarkNullKeywordId])
			),

			reprintLayout: new DocLayoutOptions(
				ContentWidth: 60,
				TabColumns: 4,
				MinTailWords: DocLayoutOptions.DefaultMinTailWords,
				MinLineWords: DocLayoutOptions.DefaultMinLineWords,
				MaxOverflowColumns: DocLayoutOptions.DefaultMaxOverflowColumns,
				IndentSize: DocLayoutOptions.DefaultIndentSize,
				IndentText: true,
				Expansion: DocExpansion.None
			),

			cancellationToken: CancellationToken.None
		);

		Assert.IsNull(result.Failure);
		Assert.AreEqual("<see langword=\"null\"/>", analyzerMarkup);

		Assert.Contains(
			analyzerMarkup,
			result.Text,
			"the tool writes another null keyword than the analyzer."
		);
	}

	[TestMethod]
	public async Task AMarkAtTheFrontIsRefusedByBothEngines()
	{
		const string document =
			"\uFEFF<root>\n  <a>\n    one\n  </a>\n  <b>\n    two\n  </b>\n</root>\n";

		MarkupProfile profile = MarkupProfiles.FindByName("xml")!;

		MarkupFormatResult result = Format(
			document,
			new MarkupOptions(profile.Policy, MaxBlankLines: 1)
		);

		Assert.IsNotNull(result.Failure, "the tool refuses a text that starts with a mark.");
		Assert.Contains("U+FEFF", result.Failure.Value.Reason, "the reason names the mark.");
		Assert.IsFalse(result.HasEdits, "a refused text is never laid out.");

		List<string> analyzer = await GetLiteralFindingsAsync(
			document,
			profile.Policy.ContainersOnly,
			maxBlankLines: 1
		);

		Assert.IsEmpty(analyzer, "the analyzer reports nothing for a text that does not parse.");
	}

	[TestMethod]
	public async Task BothEnginesGiveARunAboveTheCapToTheTightRuleThatOwnsIt()
	{
		string document = LineFeed(
			"<root>\n  <a>\n    one\n  </a>\n\n\n\n  <b>\n    two\n  </b>\n</root>\n"
		);

		MarkupProfile profile = MarkupProfiles.FindByName("xml")!;

		MarkupFormatResult result = MarkupFormatter.Format(
			document,

			new MarkupOptions(
				profile.Policy,
				Rules: MarkupRuleSet.From([MarkupDiagnostics.KeepMultiLineMarkupElementsTogetherId])
			),

			cancellationToken: CancellationToken.None
		);

		Assert.IsNull(result.Failure);

		List<string> tool = [];

		foreach (MarkupEdit edit in result.Edits)
			tool.Add($"{edit.Id}@{edit.Line}@{edit.Column}");

		tool.Sort(StringComparer.Ordinal);

		// The cap applies in the tool whatever a file enables, and the analyzer reports it only while it is on.
		// The tight rule owns the run either way, so the two engines name the same rule.
		foreach (bool capIsOn in new[] { false, true })
		{
			List<string> enabled =
			[
				MarkupDiagnostics.SeparateMultiLineMarkupElementsId,
				MarkupDiagnostics.KeepMultiLineMarkupElementsTogetherId,
			];

			if (capIsOn)
				enabled.Add(MarkupDiagnostics.CollapseExtraBlankLinesBetweenMarkupElementsId);

			List<string> analyzer = await GetLiteralFindingsAsync(
				document,
				containersOnly: profile.Policy.ContainersOnly,
				maxBlankLines: 1,
				enabledIds: enabled
			);

			analyzer.Sort(StringComparer.Ordinal);

			Assert.AreSequenceEqual(
				analyzer,
				tool,
				$"the analyzer's findings are not the tool's for a run above the cap (cap on: {capIsOn})."
			);
		}
	}

	private static async Task<List<string>> GetLiteralFindingsAsync(
		string document,
		bool containersOnly,
		int maxBlankLines,
		IReadOnlyList<string>? enabledIds = null
	)
	{
		var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);

		SyntaxTree tree = CSharpSyntaxTree.ParseText(BuildLiteral(document), parseOptions);

		SyntaxToken literal = tree.GetRoot()
			.DescendantTokens()
			.First(token => token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken));

		Assert.AreEqual(
			document,
			literal.ValueText,
			"the literal built for a row holds that row's document"
		);

		int firstValueLine = tree.GetLineSpan(literal.Span).StartLinePosition.Line + 1;

		IReadOnlyList<string> ids =
			enabledIds
			??
			[
				MarkupDiagnostics.SeparateMultiLineMarkupElementsId,
				MarkupDiagnostics.CollapseExtraBlankLinesBetweenMarkupElementsId,
				MarkupDiagnostics.SeparateMarkupCommentMarkerFromTextId,
			];

		var diagnosticOptions = new Dictionary<string, ReportDiagnostic>();

		foreach (string id in ids)
			diagnosticOptions[id] = ReportDiagnostic.Warn;

		CSharpCompilationOptions compilationOptions = new CSharpCompilationOptions(
			OutputKind.DynamicallyLinkedLibrary
		).WithSpecificDiagnosticOptions(diagnosticOptions);

		var compilation = CSharpCompilation.Create(
			assemblyName: "Parity",
			syntaxTrees: [tree],
			references: [],
			options: compilationOptions
		);

		ImmutableArray<DiagnosticAnalyzer> analyzers =
		[
			new stanza::Nickelony.Stanza.Analyzers.MarkupLiterals.SeparateMultiLineMarkupElementsAnalyzer(),
			new stanza::Nickelony.Stanza.Analyzers.MarkupLiterals.KeepMultiLineMarkupElementsTogetherAnalyzer(),
			new stanza::Nickelony.Stanza.Analyzers.MarkupLiterals.KeepSingleLineMarkupElementsTogetherAnalyzer(),
			new stanza::Nickelony.Stanza.Analyzers.MarkupLiterals.CollapseExtraBlankLinesBetweenMarkupElementsAnalyzer(),
			new stanza::Nickelony.Stanza.Analyzers.MarkupLiterals.SeparateMarkupCommentMarkerFromTextAnalyzer(),
		];

		var options = new Dictionary<string, string>
		{
			["stanza_markup_scope"] = containersOnly ? "containers" : "all",

			["stanza_markup_max_blank_lines"] = maxBlankLines.ToString(
				CultureInfo.InvariantCulture
			),
		};

		var withAnalyzers = new CompilationWithAnalyzers(
			compilation,
			analyzers,
			new AnalyzerOptions([], new FixedOptionsProvider(new FixedOptions(options)))
		);

		ImmutableArray<Diagnostic> diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync();

		List<string> findings = [];

		foreach (Diagnostic diagnostic in diagnostics)
		{
			FileLinePositionSpan span = diagnostic.Location.GetLineSpan();

			findings.Add(
				$"{diagnostic.Id}@{span.StartLinePosition.Line - firstValueLine + 1}"
					+ $"@{span.StartLinePosition.Character + 1}"
			);
		}

		return findings;
	}

	private static string BuildLiteral(string document)
	{
		return "class C\n{\n\tvoid M()\n\t{\n\t\tconst string markup = \"\"\"\n"
			+ document
			+ "\n\"\"\";\n\t}\n}\n";
	}

	private sealed class FixedOptions(IReadOnlyDictionary<string, string> values)
		: AnalyzerConfigOptions
	{
		public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
			values.TryGetValue(key, out value);
	}

	private sealed class NoOptions : AnalyzerConfigOptions
	{
		public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
		{
			value = null;
			return false;
		}
	}

	private sealed class FixedOptionsProvider(AnalyzerConfigOptions options)
		: AnalyzerConfigOptionsProvider
	{
		public override AnalyzerConfigOptions GlobalOptions => options;

		public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => options;

		public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => options;
	}

	private const string MixedContent = """
		<root>
		  <a>
		    one
		  </a>
		  <b>
		    text
		    <c/>
		    <d/>
		  </b>
		</root>
		""";

	private const string ListUnderAComment = """
		<root>
		  <!-- the steps -->
		  <list type="number">
		    <item>
		      <description>one</description>
		    </item>
		    <item>
		      <description>two</description>
		    </item>
		  </list>
		</root>
		""";

	private const string PreservedContainer = """
		<root>
		  <a>
		    one
		  </a>
		  <b xml:space="preserve">
		    <c/>
		    <d/>
		  </b>
		</root>
		""";

	private const string CdataContainer = """
		<root>
		  <a>
		    one
		  </a>
		  <b>
		    <![CDATA[raw]]>
		    <c/>
		  </b>
		  <d>
		    four
		  </d>
		</root>
		""";

	private const string CommentBetweenElements = """
		<root>
		  <a>
		    one
		  </a>
		  <!-- section -->
		  <b>
		    two
		  </b>
		</root>
		""";

	private const string RunPastTheBudget =
		"<root>\n  <a>\n    one\n  </a>\n\n\n  <b>\n    two\n  </b>\n</root>";

	private const string TrailingComment = """
		<root>
		  <a>
		    one
		  </a> <!-- tail -->
		  <b>
		    two
		  </b>
		</root>
		""";

	private const string Prolog = """
		<?xml version="1.0" encoding="utf-8"?>
		<root>
		  <a>
		    one
		  </a>
		  <b>
		    two
		  </b>
		</root>
		""";

	private const string ProcessingInstruction = """
		<root>
		  <a>
		    one
		  </a>
		  <?pi target?>
		  <b>
		    two
		  </b>
		</root>
		""";

	private const string SingleLineSiblings = """
		<root>
		  <a>1</a>
		  <b>2</b>
		</root>
		""";

	private const string TouchingMarker = """
		<root>
		  <!--section-->
		  <a>
		    one
		  </a>
		  <b>
		    two
		  </b>
		</root>
		""";

	private static string Crlf(string text) => text.Replace("\n", "\r\n");
}
