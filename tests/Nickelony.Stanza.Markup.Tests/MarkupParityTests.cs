extern alias stanza;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Configuration;
using Nickelony.Stanza.Markup.Core;
using Nickelony.Stanza.Markup.Core.Formatting;
using Nickelony.Stanza.Markup.Core.Options;
using Nickelony.Stanza.Tests.Shared;

using static Nickelony.Stanza.Tests.Shared.MarkupFormatFixtures;

using LiteralEdits = stanza::Nickelony.Stanza.Analyzers.MarkupLiterals.MarkupLiteralEdits;

namespace Nickelony.Stanza.Markup.Tests;

[TestClass]
public sealed class MarkupParityTests
{
	[TestMethod]
	public void TheAnalyzerScopeDefaultMatchesTheTool()
	{
		// With no configuration there is no profile to read, so the tool's default applies.
		var defaults = stanza::Nickelony.Stanza.Analyzers.StanzaOptions.From(
			FixedConfigOptions.Empty
		);

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
	[DataRow("<Menu>\n  <a />\n  <Separator />\n  <b />\n</Menu>\n", "Separator", "xml", 2)]
	[DataRow("<Menu>\n  <a />\n  <Separator />\n  <b />\n</Menu>\n", "Separator", "msbuild", 2)]
	[DataRow(
		"<Menu>\n  <a />\n  <Separator />\n  <b />\n</Menu>\n",
		" Separator , Gap ,",
		"xml",
		2
	)]
	[DataRow("<Menu>\n  <Separator />\n  <Separator />\n</Menu>\n", "Separator", "xml", 1)]
	[DataRow(
		"<Menu xmlns:x=\"urn:x\">\n  <a />\n  <x:Separator>1</x:Separator>\n</Menu>\n",
		"Separator",
		"xml",
		1
	)]
	[DataRow("<Menu>\n  <a />\n  <!-- rule -->\n  <Separator />\n</Menu>\n", "Separator", "xml", 1)]
	[DataRow("<Menu>\n  <a /> <Separator /> <b />\n</Menu>\n", "Separator", "xml", 0)]
	[DataRow("<Menu>\n  <a />\n  <Separator />\n  <b />\n</Menu>\n", "separator", "xml", 0)]
	[DataRow("<Menu>\n  <a />\n  <Separator />\n  <b />\n</Menu>\n", "none", "xml", 0)]
	public async Task BothEnginesSeparateTheElementsAFileLists(
		string document,
		string listed,
		string profileName,
		int expectedFindings
	)
	{
		MarkupProfile profile = MarkupProfiles.FindByName(profileName)!;

		MarkupFormatResult result = Format(document, ListedOptions(profile, listed));

		Assert.IsNull(result.Failure);
		Assert.HasCount(expectedFindings, result.Edits);

		List<string> tool =
		[
			.. result.Edits.Select(edit => $"{edit.Id}@{edit.Line}@{edit.Column}"),
		];

		List<string> analyzer = await GetLiteralFindingsAsync(
			document,
			containersOnly: profile.Policy.ContainersOnly,
			maxBlankLines: 1,
			separatedElements: listed
		);

		tool.Sort(StringComparer.Ordinal);
		analyzer.Sort(StringComparer.Ordinal);

		Assert.AreSequenceEqual(analyzer, tool, "the analyzer's findings are not the tool's.");

		List<string> again = await GetLiteralFindingsAsync(
			document: result.Text,
			containersOnly: profile.Policy.ContainersOnly,
			maxBlankLines: 1,
			separatedElements: listed
		);

		Assert.IsEmpty(again, "the analyzer asks for another blank line in what the tool wrote.");
	}

	[TestMethod]
	public async Task BothEnginesGiveTheGapsBesideAListedElementToTheTightRule()
	{
		const string document = "<Menu>\n  <a />\n\n  <Separator />\n\n  <b />\n</Menu>\n";

		MarkupProfile profile = MarkupProfiles.FindByName("xml")!;

		var tightRule = MarkupRuleSet.From([
			MarkupDiagnostics.KeepMultiLineMarkupElementsTogetherId,
		]);

		MarkupFormatResult result = Format(
			document,
			ListedOptions(profile, "Separator", tightRule)
		);

		Assert.IsNull(result.Failure);
		Assert.HasCount(2, result.Edits);

		Assert.IsTrue(
			result.Edits.All(edit =>
				edit.Id == MarkupDiagnostics.KeepMultiLineMarkupElementsTogetherId
			)
		);

		List<string> tool =
		[
			.. result.Edits.Select(edit => $"{edit.Id}@{edit.Line}@{edit.Column}"),
		];

		List<string> analyzer = await GetLiteralFindingsAsync(
			document,
			containersOnly: profile.Policy.ContainersOnly,
			maxBlankLines: 1,

			enabledIds:
			[
				MarkupDiagnostics.SeparateMultiLineMarkupElementsId,
				MarkupDiagnostics.KeepMultiLineMarkupElementsTogetherId,
			],

			separatedElements: "Separator"
		);

		tool.Sort(StringComparer.Ordinal);
		analyzer.Sort(StringComparer.Ordinal);

		Assert.AreSequenceEqual(analyzer, tool, "the analyzer's findings are not the tool's.");
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
			[MarkupDiagnostics.MarkLanguageKeywordsId] = ReportDiagnostic.Warn,
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
			[new stanza::Nickelony.Stanza.Analyzers.Documentation.MarkLanguageKeywordsAnalyzer()],
			new AnalyzerOptions([], new FixedConfigOptionsProvider(FixedConfigOptions.Empty))
		);

		ImmutableArray<Diagnostic> diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync();

		Diagnostic[] keywords =
		[
			.. diagnostics.Where(diagnostic =>
				diagnostic.Id == MarkupDiagnostics.MarkLanguageKeywordsId
			),
		];

		Assert.HasCount(1, keywords);

		bool spaced = stanza::Nickelony.Stanza.Analyzers.FixOptions.Parse(keywords[0]).SpacedSlash;

		string analyzerMarkup =
			stanza::Nickelony.Stanza.Markup.Core.DocComments.DocTextRules.GetKeywordMarkup(
				"null",
				spaced
			);

		const string member =
			"<member name=\"X\">\n  <summary>alpha null beta</summary>\n</member>\n";

		MarkupProfile profile = MarkupProfiles.FindByName("xml")!;

		MarkupFormatResult result = MarkupFormatter.Format(
			source: member,

			options: new MarkupOptions(
				profile.Policy,
				Rules: MarkupRuleSet.From([MarkupDiagnostics.MarkLanguageKeywordsId])
			),

			reprintLayout: MarkupFormatFixtures.ReprintLayout(contentWidth: 60),
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

	[TestMethod]
	public async Task BothEnginesAgreeOnTheCommentedRuleAndWhoOwnsTheGapsAroundIt()
	{
		string document = LineFeed(CommentedSingleLineElements);

		MarkupProfile profile = MarkupProfiles.FindByName("xml")!;

		MarkupFormatResult result = MarkupFormatter.Format(
			document,

			new MarkupOptions(
				profile.Policy,
				Rules: MarkupRuleSet.From([MarkupDiagnostics.SeparateCommentedMarkupElementId])
			),

			cancellationToken: CancellationToken.None
		);

		Assert.IsNull(result.Failure);

		List<string> tool = [];

		foreach (MarkupEdit edit in result.Edits)
			tool.Add($"{edit.Id}@{edit.Line}@{edit.Column}");

		Assert.Contains(
			$"{MarkupDiagnostics.SeparateCommentedMarkupElementId}@3@1",
			tool,
			"the tool does not write the blank line above the comment."
		);

		List<string> enabled =
		[
			MarkupDiagnostics.SeparateCommentedMarkupElementId,
			MarkupDiagnostics.CollapseExtraBlankLinesBetweenMarkupElementsId,
		];

		List<string> analyzer = await GetLiteralFindingsAsync(
			document,
			containersOnly: profile.Policy.ContainersOnly,
			maxBlankLines: 1,
			enabledIds: enabled
		);

		tool.Sort(StringComparer.Ordinal);
		analyzer.Sort(StringComparer.Ordinal);

		Assert.AreSequenceEqual(
			analyzer,
			tool,
			"the analyzer's findings are not the tool's for a file that enables the commented rule."
		);

		List<string> again = await GetLiteralFindingsAsync(
			document: result.Text,
			containersOnly: profile.Policy.ContainersOnly,
			maxBlankLines: 1,
			enabledIds: enabled
		);

		Assert.IsEmpty(again, "the analyzer asks for more in what the tool wrote.");
	}

	[TestMethod]
	public async Task BothEnginesLetTheSingleLineTightRuleWinTheGapAboveAComment()
	{
		string document = LineFeed(CommentedSingleLineElements);

		MarkupProfile profile = MarkupProfiles.FindByName("xml")!;

		MarkupFormatResult result = MarkupFormatter.Format(
			document,

			new MarkupOptions(
				profile.Policy,

				Rules: MarkupRuleSet.From([
					MarkupDiagnostics.SeparateCommentedMarkupElementId,
					MarkupDiagnostics.KeepSingleLineMarkupElementsTogetherId,
				])
			),

			cancellationToken: CancellationToken.None
		);

		Assert.IsNull(result.Failure);

		List<string> tool = [];

		foreach (MarkupEdit edit in result.Edits)
			tool.Add($"{edit.Id}@{edit.Line}@{edit.Column}");

		Assert.IsFalse(
			tool.Any(edit =>
				edit.StartsWith(
					MarkupDiagnostics.SeparateCommentedMarkupElementId,
					StringComparison.Ordinal
				)
			),

			"the tool writes the blank line above the comment beside the tight rule."
		);

		Assert.IsNotEmpty(tool);

		List<string> enabled =
		[
			MarkupDiagnostics.SeparateCommentedMarkupElementId,
			MarkupDiagnostics.KeepSingleLineMarkupElementsTogetherId,
			MarkupDiagnostics.CollapseExtraBlankLinesBetweenMarkupElementsId,
		];

		List<string> analyzer = await GetLiteralFindingsAsync(
			document,
			containersOnly: profile.Policy.ContainersOnly,
			maxBlankLines: 1,
			enabledIds: enabled
		);

		tool.Sort(StringComparer.Ordinal);
		analyzer.Sort(StringComparer.Ordinal);

		Assert.AreSequenceEqual(
			analyzer,
			tool,
			"the analyzer's findings are not the tool's for a file that enables both rules."
		);

		List<string> again = await GetLiteralFindingsAsync(
			document: result.Text,
			containersOnly: profile.Policy.ContainersOnly,
			maxBlankLines: 1,
			enabledIds: enabled
		);

		Assert.IsEmpty(again, "the analyzer asks for more in what the tool wrote.");
		Assert.DoesNotContain("\n\n  <!--", result.Text);
	}

	[TestMethod]
	public async Task NeitherEngineWritesTheCommentedRuleUnlessAFileEnablesIt()
	{
		string document = LineFeed(CommentedSingleLineElements);

		MarkupProfile profile = MarkupProfiles.FindByName("xml")!;

		MarkupFormatResult result = Format(
			document,
			new MarkupOptions(profile.Policy, MaxBlankLines: 1)
		);

		Assert.IsNull(result.Failure);

		Assert.IsFalse(
			result.Edits.Any(edit => edit.Id == MarkupDiagnostics.SeparateCommentedMarkupElementId),
			"the tool writes the commented rule without being asked."
		);

		List<string> analyzer = await GetLiteralFindingsAsync(
			document,
			containersOnly: profile.Policy.ContainersOnly,
			maxBlankLines: 1
		);

		Assert.IsFalse(
			analyzer.Any(finding =>
				finding.StartsWith(
					MarkupDiagnostics.SeparateCommentedMarkupElementId,
					StringComparison.Ordinal
				)
			),

			"the analyzer reports the commented rule while it is off."
		);
	}

	public static IEnumerable<object[]> GetEnabledRuleRows()
	{
		string[] padding = [MarkupDiagnostics.RemoveMarkupElementPaddingId];
		string[] spaced = [MarkupDiagnostics.SeparateSlashOfSelfClosingMarkupElementId];
		string[] tight = [MarkupDiagnostics.KeepSlashOfSelfClosingMarkupElementTightId];

		string[] both =
		[
			MarkupDiagnostics.SeparateSlashOfSelfClosingMarkupElementId,
			MarkupDiagnostics.KeepSlashOfSelfClosingMarkupElementTightId,
		];

		string[] empty = [MarkupDiagnostics.WriteEmptyMarkupElementAsSelfClosingTagId];
		string[] emptyWithSpaced = [.. empty, .. spaced];
		string[] emptyWithTight = [.. empty, .. tight];
		string[] emptyWithBoth = [.. empty, .. both];

		string[] everything =
		[
			MarkupDiagnostics.RemoveMarkupElementPaddingId,
			MarkupDiagnostics.SeparateSlashOfSelfClosingMarkupElementId,
			MarkupDiagnostics.SeparateMultiLineMarkupElementsId,
			MarkupDiagnostics.CollapseExtraBlankLinesBetweenMarkupElementsId,
			MarkupDiagnostics.SeparateMarkupCommentMarkerFromTextId,
		];

		return
		[
			["padding at both tags", LineFeed(PaddedElements), padding],
			["padding beside comments", LineFeed(PaddedComments), padding],
			["padding around a lone comment", LineFeed(PaddedLoneNotes), padding],
			["padding that stays", LineFeed(KeptPadding), padding],
			["padding, CRLF", Crlf(LineFeed(PaddedElements)), padding],
			["the spaced slash", LineFeed(Slashes), spaced],
			["the tight slash", LineFeed(Slashes), tight],
			["both halves of the slash", LineFeed(Slashes), both],
			["the slash, CRLF", Crlf(LineFeed(Slashes)), spaced],
			["empty elements, no slash rule", LineFeed(EmptyElements), empty],
			["empty elements, the spaced slash", LineFeed(EmptyElements), emptyWithSpaced],
			["empty elements, the tight slash", LineFeed(EmptyElements), emptyWithTight],
			["empty elements, both halves of the slash", LineFeed(EmptyElements), emptyWithBoth],
			["empty elements, CRLF", Crlf(LineFeed(EmptyElements)), emptyWithSpaced],
			["every markup rule that the tool applies", LineFeed(PaddedElements), everything],
		];
	}

	[TestMethod]
	[DynamicData(
		nameof(GetEnabledRuleRows),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task BothEnginesAgreeOnTheRulesAFileEnables(
		string name,
		string document,
		string[] enabledIds
	)
	{
		MarkupProfile profile = MarkupProfiles.FindByName("xml")!;

		MarkupFormatResult result = MarkupFormatter.Format(
			document,
			new MarkupOptions(profile.Policy, Rules: MarkupRuleSet.From(enabledIds)),
			cancellationToken: CancellationToken.None
		);

		Assert.IsNull(result.Failure, $"{name}: the row stops parsing: {result.Failure}");

		// The tool applies these three whatever a file enables, so the analyzer enables them to match.
		string[] analyzerIds =
		[
			.. new[]
			{
				MarkupDiagnostics.SeparateMultiLineMarkupElementsId,
				MarkupDiagnostics.CollapseExtraBlankLinesBetweenMarkupElementsId,
				MarkupDiagnostics.SeparateMarkupCommentMarkerFromTextId,
			}
				.Concat(enabledIds)
				.Distinct(),
		];

		List<string> tool = [];

		foreach (MarkupEdit edit in result.Edits)
			tool.Add($"{edit.Id}@{edit.Line}@{edit.Column}");

		List<string> analyzer = await GetLiteralFindingsAsync(
			document,
			containersOnly: profile.Policy.ContainersOnly,
			maxBlankLines: 1,
			enabledIds: analyzerIds
		);

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
			document: result.Text,
			containersOnly: profile.Policy.ContainersOnly,
			maxBlankLines: 1,
			enabledIds: analyzerIds
		);

		Assert.IsEmpty(again, $"{name}: the analyzer asks for more in what the tool wrote.");
	}

	public static IEnumerable<object[]> GetEmptyElementShapeRows()
	{
		const string empty = MarkupDiagnostics.WriteEmptyMarkupElementAsSelfClosingTagId;
		const string spaced = MarkupDiagnostics.SeparateSlashOfSelfClosingMarkupElementId;
		const string tight = MarkupDiagnostics.KeepSlashOfSelfClosingMarkupElementTightId;

		return
		[
			["no slash rule", new[] { empty }],
			["the spaced slash", new[] { empty, spaced }],
			["the tight slash", new[] { empty, tight }],
			["both halves of the slash", new[] { empty, spaced, tight }],
		];
	}

	[TestMethod]
	[DynamicData(
		nameof(GetEmptyElementShapeRows),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task BothEnginesWriteAnEmptyElementInTheSameSlashShape(
		string name,
		string[] enabledIds
	)
	{
		const string document = "<root>\n  <Item a=\"1\"></Item>\n</root>";

		MarkupProfile profile = MarkupProfiles.FindByName("xml")!;

		MarkupFormatResult result = MarkupFormatter.Format(
			document,
			new MarkupOptions(profile.Policy, Rules: MarkupRuleSet.From(enabledIds)),
			cancellationToken: CancellationToken.None
		);

		(ImmutableArray<Diagnostic> diagnostics, _) = await GetLiteralDiagnosticsAsync(
			document,
			containersOnly: profile.Policy.ContainersOnly,
			maxBlankLines: 1,
			enabledIds
		);

		Diagnostic empty = diagnostics.Single(diagnostic =>
			diagnostic.Id == MarkupDiagnostics.WriteEmptyMarkupElementAsSelfClosingTagId
		);

		bool spaced = stanza::Nickelony.Stanza.Analyzers.FixOptions.Parse(empty).SpacedSlash;

		Assert.AreEqual(
			document.Replace("></Item>", spaced ? " />" : "/>", StringComparison.Ordinal),
			result.Text,
			$"{name}: the tool writes another shape than the analyzer's fix."
		);
	}

	public static IEnumerable<object[]> GetShapeTextRows()
	{
		string[] marker = [MarkupDiagnostics.SeparateMarkupCommentMarkerFromTextId];
		string[] spaced = [MarkupDiagnostics.SeparateSlashOfSelfClosingMarkupElementId];
		string[] tight = [MarkupDiagnostics.KeepSlashOfSelfClosingMarkupElementTightId];
		string[] empty = [MarkupDiagnostics.WriteEmptyMarkupElementAsSelfClosingTagId];
		string[] both = [.. spaced, .. tight];
		string[] emptyWithSpaced = [.. empty, .. spaced];
		string[] emptyWithTight = [.. empty, .. tight];
		string[] emptyWithBoth = [.. empty, .. both];

		return
		[
			["comment markers touching their text", LineFeed(TouchingMarkers), marker],
			["comment markers touching their text, CRLF", Crlf(LineFeed(TouchingMarkers)), marker],
			["the spaced slash", LineFeed(SetApartSlashes), spaced],
			["the tight slash", LineFeed(SetApartSlashes), tight],
			["both halves of the slash", LineFeed(SetApartSlashes), both],
			["the slash, CRLF", Crlf(LineFeed(SetApartSlashes)), spaced],
			["empty elements, no slash rule", LineFeed(SetApartEmptyElements), empty],
			["empty elements, the spaced slash", LineFeed(SetApartEmptyElements), emptyWithSpaced],
			["empty elements, the tight slash", LineFeed(SetApartEmptyElements), emptyWithTight],

			[
				"empty elements, both halves of the slash",
				LineFeed(SetApartEmptyElements),
				emptyWithBoth,
			],

			["empty elements, CRLF", Crlf(LineFeed(SetApartEmptyElements)), emptyWithSpaced],
		];
	}

	// The fixes rebuild the changes the tool computes, so the text they write is compared and not only where each side reports.
	[TestMethod]
	[DynamicData(
		nameof(GetShapeTextRows),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task BothEnginesWriteTheSameTextForTheShapeRules(
		string name,
		string document,
		string[] enabledIds
	)
	{
		MarkupProfile profile = MarkupProfiles.FindByName("xml")!;

		MarkupFormatResult result = MarkupFormatter.Format(
			document,
			new MarkupOptions(profile.Policy, Rules: MarkupRuleSet.From(enabledIds)),
			cancellationToken: CancellationToken.None
		);

		Assert.IsNull(result.Failure, $"{name}: the row stops parsing: {result.Failure}");
		Assert.IsTrue(result.HasEdits, $"{name}: the tool found nothing to compare.");

		// The tool applies the blank-line rules whatever a file enables, so a row that holds work for them would not compare the shape fixes alone.
		Assert.IsTrue(
			result.Edits.All(edit => s_shapeIds.Contains(edit.Id)),
			$"{name}: the tool also writes {string.Join(", ", result.Edits.Select(edit => edit.Id).Except(s_shapeIds))}."
		);

		// The tool applies these three whatever a file enables, so the analyzer enables them to match.
		string[] analyzerIds =
		[
			.. enabledIds,
			MarkupDiagnostics.SeparateMultiLineMarkupElementsId,
			MarkupDiagnostics.CollapseExtraBlankLinesBetweenMarkupElementsId,
			MarkupDiagnostics.SeparateMarkupCommentMarkerFromTextId,
		];

		(ImmutableArray<Diagnostic> diagnostics, _) = await GetLiteralDiagnosticsAsync(
			document,
			containersOnly: profile.Policy.ContainersOnly,
			maxBlankLines: 1,
			enabledIds: analyzerIds
		);

		var source = SourceText.From(BuildLiteral(document));
		List<TextChange> changes = [];

		foreach (
			Diagnostic diagnostic in diagnostics.Where(diagnostic =>
				s_shapeIds.Contains(diagnostic.Id)
			)
		)
		{
			TextSpan span = diagnostic.Location.SourceSpan;

			bool spaced = stanza::Nickelony
				.Stanza.Analyzers.FixOptions.Parse(diagnostic)
				.SpacedSlash;

			// This mirrors the rows of MarkupLiteralCodeFixProvider, which the test project cannot reach.
			TextChange? change = diagnostic.Id switch
			{
				MarkupDiagnostics.SeparateMarkupCommentMarkerFromTextId =>
					LiteralEdits.CreateCommentMarkerSpace(source, span),

				MarkupDiagnostics.SeparateSlashOfSelfClosingMarkupElementId =>
					LiteralEdits.CreateSlashGap(source, span, spaced: true),

				MarkupDiagnostics.KeepSlashOfSelfClosingMarkupElementTightId =>
					LiteralEdits.CreateSlashGap(source, span, spaced: false),

				_ => LiteralEdits.CreateEmptyElement(source, span, spaced),
			};

			if (change is { } found && !changes.Contains(found))
				changes.Add(found);
		}

		Assert.IsNotEmpty(changes, $"{name}: the analyzer's fixes found nothing to compare.");

		string fixedSource = source.WithChanges(changes).ToString();
		const string literalStart = "const string markup = \"\"\"\n";

		int start =
			fixedSource.IndexOf(literalStart, StringComparison.Ordinal) + literalStart.Length;

		int end = fixedSource.LastIndexOf("\n\"\"\";", StringComparison.Ordinal);

		Assert.AreEqual(
			result.Text,
			fixedSource[start..end],
			$"{name}: the analyzer's fixes write another text than the tool."
		);
	}

	// Both engines share the analysis, so a row that found nothing would leave the comparison above green.
	[TestMethod]
	public void TheRulesAFileEnablesGiveTheEnginesFindingsToCompare()
	{
		MarkupProfile profile = MarkupProfiles.FindByName("xml")!;

		foreach (object[] row in GetEnabledRuleRows())
		{
			if ((string)row[0] == "padding that stays")
				continue;

			MarkupFormatResult result = MarkupFormatter.Format(
				(string)row[1],
				new MarkupOptions(profile.Policy, Rules: MarkupRuleSet.From((string[])row[2]))
			);

			Assert.IsTrue(result.HasEdits, $"{row[0]}: the tool found nothing to compare.");
		}
	}

	[TestMethod]
	public async Task BothEnginesLeaveTheNewRulesAloneUnlessAFileEnablesThem()
	{
		string[] ids =
		[
			MarkupDiagnostics.RemoveMarkupElementPaddingId,
			MarkupDiagnostics.SeparateSlashOfSelfClosingMarkupElementId,
			MarkupDiagnostics.KeepSlashOfSelfClosingMarkupElementTightId,
			MarkupDiagnostics.WriteEmptyMarkupElementAsSelfClosingTagId,
		];

		MarkupProfile profile = MarkupProfiles.FindByName("xml")!;

		foreach (
			string document in new[]
			{
				LineFeed(PaddedElements),
				LineFeed(Slashes),
				LineFeed(EmptyElements),
			}
		)
		{
			MarkupFormatResult result = Format(document, new MarkupOptions(profile.Policy));

			Assert.IsNull(result.Failure);

			Assert.IsFalse(
				result.Edits.Any(edit => ids.Contains(edit.Id)),
				"the tool writes a rule unasked."
			);

			List<string> analyzer = await GetLiteralFindingsAsync(
				document,
				containersOnly: profile.Policy.ContainersOnly,
				maxBlankLines: 1
			);

			Assert.IsFalse(
				analyzer.Any(finding =>
					ids.Any(id => finding.StartsWith(id, StringComparison.Ordinal))
				),

				"the analyzer reports a rule while it is off."
			);
		}
	}

	private static MarkupOptions ListedOptions(
		MarkupProfile profile,
		string listed,
		MarkupRuleSet rules = default
	)
	{
		MarkupNameSet? names = MarkupOptionReader.ReadSeparatedElements(key =>
			key == OptionKeys.MarkupSeparatedElements ? listed : null
		);

		return new MarkupOptions(Policy: profile.Policy, Rules: rules, SeparatedElements: names);
	}

	private static async Task<List<string>> GetLiteralFindingsAsync(
		string document,
		bool containersOnly,
		int maxBlankLines,
		IReadOnlyList<string>? enabledIds = null,
		string? separatedElements = null
	)
	{
		(ImmutableArray<Diagnostic> diagnostics, int firstValueLine) =
			await GetLiteralDiagnosticsAsync(
				document,
				containersOnly,
				maxBlankLines,
				enabledIds,
				separatedElements
			);

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

	private static async Task<(
		ImmutableArray<Diagnostic> Diagnostics,
		int FirstValueLine
	)> GetLiteralDiagnosticsAsync(
		string document,
		bool containersOnly,
		int maxBlankLines,
		IReadOnlyList<string>? enabledIds = null,
		string? separatedElements = null
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
			new stanza::Nickelony.Stanza.Analyzers.MarkupLiterals.SeparateCommentedMarkupElementAnalyzer(),
			new stanza::Nickelony.Stanza.Analyzers.MarkupLiterals.RemoveMarkupElementPaddingAnalyzer(),
			new stanza::Nickelony.Stanza.Analyzers.MarkupLiterals.SeparateSlashOfSelfClosingMarkupElementAnalyzer(),
			new stanza::Nickelony.Stanza.Analyzers.MarkupLiterals.KeepSlashOfSelfClosingMarkupElementTightAnalyzer(),
			new stanza::Nickelony.Stanza.Analyzers.MarkupLiterals.WriteEmptyMarkupElementAsSelfClosingTagAnalyzer(),
		];

		var options = new Dictionary<string, string>
		{
			["stanza_markup_scope"] = containersOnly ? "containers" : "all",

			["stanza_markup_max_blank_lines"] = maxBlankLines.ToString(
				CultureInfo.InvariantCulture
			),
		};

		if (separatedElements is not null)
			options["stanza_markup_separated_elements"] = separatedElements;

		var withAnalyzers = new CompilationWithAnalyzers(
			compilation,
			analyzers,
			new AnalyzerOptions([], new FixedConfigOptionsProvider(new FixedConfigOptions(options)))
		);

		return (await withAnalyzers.GetAnalyzerDiagnosticsAsync(), firstValueLine);
	}

	private static string BuildLiteral(string document)
	{
		// The comment marks the literal as XML, which the analyzer reads by default.
		return "class C\n{\n\tvoid M()\n\t{\n\t\t// language=xml\n\t\tconst string markup = \"\"\"\n"
			+ document
			+ "\n\"\"\";\n\t}\n}\n";
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

	private const string CommentedSingleLineElements = """
		<root>
		  <a>1</a>
		  <!-- the second element -->
		  <b>2</b>

		  <!-- the third element -->
		  <c>3</c>



		  <!-- the fourth element -->
		  <d>4</d>
		</root>
		""";

	private const string PaddedElements = """
		<root>

		  <a>
		    one
		  </a>
		  <b>
		    two
		  </b>

		</root>
		""";

	private const string PaddedComments = """
		<root>

		  <!-- first -->
		  <a/>
		  <!-- last -->

		</root>
		""";

	private const string PaddedLoneNotes = """
		<root>
		  <a>

		    <!-- only -->
		    <?pi?>

		  </a>
		</root>
		""";

	private const string KeptPadding = """
		<root>
		  <mixed>

		    text <b/>

		  </mixed>
		  <kept xml:space="preserve">

		    <c/>

		  </kept>
		</root>
		""";

	private const string EmptyElements = """
		<root xmlns:x="urn:x">
		  <Item></Item>
		  <Item a="1"  ></Item>
		  <Item
		    a="1"
		    b="2"></Item>
		  <Item
		    a="1"
		    ></Item>
		  <x:Item a="1"></x:Item>
		  <div></div>
		  <note> </note>
		  <note><!--later--></note>
		  <None/>
		  <None />
		</root>
		""";

	private const string Slashes = """
		<root>
		  <a/>
		  <b x="1" />
		  <c x="1"   />
		  <d
		    x="1"
		    />
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

	// The tool sets a multi-line element apart whatever a file enables, so these hold the blank lines that leave it only the shape rules.
	private const string SetApartSlashes = """
		<root>
		  <a/>
		  <b x="1" />
		  <c x="1"   />

		  <d
		    x="1"
		    />
		</root>
		""";

	private const string SetApartEmptyElements = """
		<root xmlns:x="urn:x">
		  <Item></Item>
		  <Item a="1"  ></Item>

		  <Item
		    a="1"
		    b="2"></Item>

		  <Item
		    a="1"
		    ></Item>

		  <x:Item a="1"></x:Item>
		  <div></div>
		  <note> </note>
		  <note><!--later--></note>
		  <None/>
		  <None />
		</root>
		""";

	private const string TouchingMarkers = """
		<root>
		  <!--section-->
		  <a>1</a>
		  <b>2</b><!-- note-->
		  <!--tail -->
		</root>
		""";

	private static readonly ImmutableHashSet<string> s_shapeIds =
	[
		MarkupDiagnostics.SeparateMarkupCommentMarkerFromTextId,
		MarkupDiagnostics.SeparateSlashOfSelfClosingMarkupElementId,
		MarkupDiagnostics.KeepSlashOfSelfClosingMarkupElementTightId,
		MarkupDiagnostics.WriteEmptyMarkupElementAsSelfClosingTagId,
	];

	private static string Crlf(string text) => text.Replace("\n", "\r\n");
}
