using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Configuration;
using Nickelony.Stanza.Markup.Core;
using Nickelony.Stanza.Markup.Core.DocComments;
using Nickelony.Stanza.Markup.Core.Formatting;
using Nickelony.Stanza.Markup.Core.Options;
using Nickelony.Stanza.Tests.Shared;

using static Nickelony.Stanza.Tests.Shared.MarkupFormatFixtures;

namespace Nickelony.Stanza.Markup.Tests;

/// <summary>Formats seeded documents until nothing changes and keeps their content.</summary>
[TestClass]
public sealed class MarkupConvergenceTests
{
	private const int DefaultSeedCount = 8;
	private const int SoakFirstSeed = 1000;

	// A layout settles in one pass, so the pass after it makes no edit.
	private const int MaxPasses = 2;

	private static readonly string[] s_names = ["alpha", "beta", "gamma", "delta"];

	// Two of the names the generated documents use, so a listed element stands among unlisted ones.
	private static readonly MarkupNameSet s_listed = new("beta", "gamma");

	private static readonly string[][] s_ruleSets =
	[
		[],
		[MarkupDiagnostics.KeepMultiLineMarkupElementsTogetherId],

		[
			MarkupDiagnostics.KeepMultiLineMarkupElementsTogetherId,
			MarkupDiagnostics.KeepSingleLineMarkupElementsTogetherId,
		],
	];

	// The rules of the padding and the slash, which a document of each seed meets beside the gap rules above.
	private static readonly string[][] s_paddingAndSlashRuleSets =
	[
		[MarkupDiagnostics.RemoveMarkupElementPaddingId],
		[MarkupDiagnostics.SeparateSlashOfSelfClosingMarkupElementId],
		[MarkupDiagnostics.KeepSlashOfSelfClosingMarkupElementTightId],

		[
			MarkupDiagnostics.SeparateSlashOfSelfClosingMarkupElementId,
			MarkupDiagnostics.KeepSlashOfSelfClosingMarkupElementTightId,
		],

		[
			MarkupDiagnostics.RemoveMarkupElementPaddingId,
			MarkupDiagnostics.SeparateSlashOfSelfClosingMarkupElementId,
			MarkupDiagnostics.SeparateCommentedMarkupElementId,
		],

		// The empty-element rule beside each choice of the slash pair, which it follows.
		[MarkupDiagnostics.WriteEmptyMarkupElementAsSelfClosingTagId],

		[
			MarkupDiagnostics.WriteEmptyMarkupElementAsSelfClosingTagId,
			MarkupDiagnostics.SeparateSlashOfSelfClosingMarkupElementId,
		],

		[
			MarkupDiagnostics.WriteEmptyMarkupElementAsSelfClosingTagId,
			MarkupDiagnostics.KeepSlashOfSelfClosingMarkupElementTightId,
		],

		[
			MarkupDiagnostics.WriteEmptyMarkupElementAsSelfClosingTagId,
			MarkupDiagnostics.SeparateSlashOfSelfClosingMarkupElementId,
			MarkupDiagnostics.KeepSlashOfSelfClosingMarkupElementTightId,
		],
	];

	private static readonly string[] s_documentationRules =
	[
		MarkupDiagnostics.RemoveDocumentationPaddingId,
		MarkupDiagnostics.CapitalizeDocumentationTextId,
		MarkupDiagnostics.EndDocumentationTextWithPeriodId,
		MarkupDiagnostics.SeparateSlashOfSelfClosingTagId,
		MarkupDiagnostics.WriteEmptyElementAsSelfClosingTagId,
		MarkupDiagnostics.OrderItemsOfDocumentationListId,
		MarkupDiagnostics.OrderElementsOfDocumentationCommentId,
	];

	public static IEnumerable<object[]> Seeds { get; } =
	[.. Enumerable.Range(1, DefaultSeedCount).Select(seed => new object[] { seed })];

	[TestMethod]
	[DynamicData(
		nameof(Seeds),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public void TheLayoutOfAGeneratedDocumentSettlesAndKeepsItsContent(int seed) =>
		AssertLayoutSettles(seed);

	[TestMethod]
	[DynamicData(
		nameof(Seeds),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public void ThePaddingAndSlashRulesSettleAndKeepTheContentOfAGeneratedDocument(int seed)
	{
		foreach (string[] rules in s_paddingAndSlashRuleSets)
			AssertLayoutSettles(seed, extraRules: rules);
	}

	[TestMethod]
	[DynamicData(
		nameof(Seeds),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public void TheLayoutWithListedElementsSettlesAndKeepsTheContentOfAGeneratedDocument(int seed)
	{
		foreach (string[] rules in s_ruleSets)
			AssertLayoutSettles(seed, extraRules: rules, separated: s_listed);
	}

	[TestMethod]
	public void ListingElementsGivesTheGeneratedDocumentsMoreWorkToDo()
	{
		int unlisted = 0;
		int listed = 0;

		foreach (int seed in Enumerable.Range(1, DefaultSeedCount))
		{
			(string source, MarkupOptions options) = CreateLayoutCase(seed);
			unlisted += MarkupFormatter.Format(source, options).Edits.Count;

			(source, options) = CreateLayoutCase(seed, separated: s_listed);
			listed += MarkupFormatter.Format(source, options).Edits.Count;
		}

		Assert.IsGreaterThan(unlisted, listed, "Listing elements wrote no blank line more.");
	}

	[TestMethod]
	public void ThePaddingOfAnElementOfNotesAloneSettlesAndKeepsItsContent()
	{
		const string source = "<root>\n  <a>\n\n    <!--note-->\n    <?pi?>\n\n  </a>\n</root>\n";

		MarkupOptions options = new(
			MarkupProfiles.Xml.Policy,
			Rules: MarkupRuleSet.From([MarkupDiagnostics.RemoveMarkupElementPaddingId])
		);

		(string text, _) = Settle(source, options, seed: 0);

		Assert.AreEqual("<root>\n  <a>\n    <!-- note -->\n    <?pi?>\n  </a>\n</root>\n", text);
		Assert.AreEqual(Normalize(XDocument.Parse(source)), Normalize(XDocument.Parse(text)));
	}

	[TestMethod]
	public void ThePaddingAndSlashRulesGiveTheGeneratedDocumentsWorkToDo()
	{
		foreach (string[] rules in s_paddingAndSlashRuleSets)
		{
			int edits = 0;

			foreach (int seed in Enumerable.Range(1, DefaultSeedCount))
			{
				(string source, MarkupOptions options) = CreateLayoutCase(seed, rules);

				edits += MarkupFormatter
					.Format(source, options)
					.Edits.Count(edit => rules.Contains(edit.Id));
			}

			Assert.IsGreaterThan(0, edits, $"{string.Join(", ", rules)} found nothing to write.");
		}
	}

	[TestMethod]
	[DynamicData(
		nameof(Seeds),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public void ADamagedDocumentationBodySettlesAndKeepsItsWords(int seed)
	{
		// A body that needed no reprint would settle without proving anything.
		Assert.IsGreaterThan(
			0,
			AssertDocumentationSettles(seed),
			$"seed {seed}: the generated bodies needed no reprint."
		);
	}

	// The generated bodies hold neither a closing mark written apart from its word
	// nor a backtick that no span closes before a blank line, so a fixed body holds both, at narrow widths.
	[TestMethod]
	[DataRow(20)]
	[DataRow(30)]
	[DataRow(40)]
	public void ProseWithSpacedMarksAndAnUnclosedBacktickSettlesInOnePass(int width)
	{
		const string source = """
			<doc>
			<members>
			<member name="M:Sample.Member">
			<summary>
			when empty ? returns otherwise the value , and so on ...

			Matches a ` character, or null when missing.

			Reads `a b` then null : done
			</summary>
			</member>
			</members>
			</doc>

			""";

		string[] rules = [.. s_documentationRules, MarkupDiagnostics.MarkLanguageKeywordsId];

		MarkupOptions options = new(MarkupProfiles.Xml.Policy, Rules: MarkupRuleSet.From(rules));

		(_, int edits) = Settle(source, options, seed: width, reprintLayout: ReprintLayout(width));

		Assert.IsGreaterThan(0, edits, $"width {width}: the body needed no reprint.");
	}

	[TestMethod]
	public void TheGeneratedDocumentsMostlyNeedALayoutEdit()
	{
		// Some seeds build a document that is already laid out, so the share is checked rather than each seed.
		int needWork = Enumerable
			.Range(1, DefaultSeedCount)
			.Count(seed =>
			{
				(string source, MarkupOptions options) = CreateLayoutCase(seed);
				return MarkupFormatter.Format(source, options).HasEdits;
			});

		Assert.IsGreaterThanOrEqualTo(
			DefaultSeedCount / 2,
			needWork,
			"Fewer than half of the generated documents needed a layout edit."
		);
	}

	// The test has no runner timeout, since SoakRun holds it to a limit that grows with the seed count.
	[TestMethod]
	[TestCategory("Soak")]
	[Timeout(SoakRun.NoRunnerTimeout)]
	public async Task ALargeSeededSetSettlesTheSameWay()
	{
		int count = SoakRun.ReadCountOrSkip(
			$"Set {SoakRun.Variable} to the number of seeds to run."
		);

		await SoakRun.RunAsync(
			count,

			() =>
			{
				RunSeeds(count);
				return Task.CompletedTask;
			}
		);
	}

	private static void RunSeeds(int count)
	{
		List<string> failures = [];

		int first = SoakRun.ReadFirstSeed(SoakFirstSeed);

		for (int seed = first; seed < first + count; seed++)
		{
			foreach (
				Action<int> run in new Action<int>[]
				{
					seed => AssertLayoutSettles(seed),
					seed => _ = AssertDocumentationSettles(seed),

					seed =>
					{
						foreach (string[] rules in s_paddingAndSlashRuleSets)
							AssertLayoutSettles(seed, extraRules: rules);
					},
				}
			)
			{
				try
				{
					run(seed);
				}
				catch (AssertFailedException exception)
				{
					failures.Add($"seed {seed}: {exception.Message.Split('\n')[0]}");
				}
				catch (Exception exception)
				{
					failures.Add(SoakRun.DescribeCrash(seed, exception));
				}
			}
		}

		Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
	}

	private static void AssertLayoutSettles(
		int seed,
		string[]? extraRules = null,
		MarkupNameSet? separated = null
	)
	{
		(string source, MarkupOptions options) = CreateLayoutCase(seed, extraRules, separated);
		(string text, _) = Settle(source, options, seed);

		Assert.AreEqual(
			Normalize(XDocument.Parse(source)),
			Normalize(XDocument.Parse(text)),
			$"seed {seed}: a layout changed the content of the document."
		);
	}

	private static (string Source, MarkupOptions Options) CreateLayoutCase(
		int seed,
		string[]? extraRules = null,
		MarkupNameSet? separated = null
	)
	{
		MarkupOptions options = new(
			Policy: MarkupProfiles.All[seed % MarkupProfiles.All.Count].Policy,
			MaxBlankLines: 1 + (seed % 3),

			Rules: MarkupRuleSet.From([
				.. s_ruleSets[seed % s_ruleSets.Length],
				.. extraRules ?? [],
			]),

			SeparatedElements: separated
		);

		return (CreateDocument(new Random(seed)), options);
	}

	private static int AssertDocumentationSettles(int seed)
	{
		var random = new Random(seed);

		StringBuilder source = new("<doc>\n<members>\n");

		for (int index = 0; index < 3; index++)
		{
			source.Append(
				CultureInfo.InvariantCulture,
				$"<member name=\"M:Sample.Member{index}\">\n"
			);

			foreach (string line in GeneratedDocBody.Create(random, ["first", "second"]))
				source.Append(line).Append('\n');

			source.Append("</member>\n");
		}

		source.Append("</members>\n</doc>\n");

		int[] widths = [40, 60, 80, 100];

		MarkupOptions options = new(
			MarkupProfiles.Xml.Policy,
			Rules: MarkupRuleSet.From(s_documentationRules)
		);

		(string text, int edits) = Settle(
			source: source.ToString(),
			options,
			seed,
			reprintLayout: ReprintLayout(widths[seed % widths.Length])
		);

		Assert.AreSequenceEqual(
			ReadWords(source.ToString()),
			ReadWords(text),
			$"seed {seed}: a reprint changed the words of a body."
		);

		Assert.IsLessThanOrEqualTo(
			GeneratedDocBody.CountStartTags([source.ToString()]),
			GeneratedDocBody.CountCapitals(ReadTexts(text)),
			$"seed {seed}: a reprint capitalized more than the start of each text."
		);

		return edits;
	}

	// Formats until a pass makes no edit, and fails when that takes more than the allowed passes.
	private static (string Text, int Edits) Settle(
		string source,
		MarkupOptions options,
		int seed,
		DocLayoutOptions? reprintLayout = null
	)
	{
		string text = source;
		int edits = 0;

		for (int pass = 1; pass <= MaxPasses; pass++)
		{
			MarkupFormatResult result = MarkupFormatter.Format(text, options, reprintLayout);

			Assert.IsNull(result.Failure, $"seed {seed}: pass {pass} failed: {result.Failure}");

			if (!result.HasEdits)
				return (text, edits);

			edits += result.Edits.Count;
			text = result.Text;
		}

		Assert.Fail($"seed {seed}: the document still changed after {MaxPasses} passes.");

		return (text, edits);
	}

	private static string[] ReadWords(string xml) =>
		[.. GeneratedDocBody.ReadWords(ReadTexts(xml)).Order(StringComparer.Ordinal)];

	// Each text node stands alone, so words that touch across an element boundary stay two words.
	private static string[] ReadTexts(string xml) =>
		[.. XDocument.Parse(xml).DescendantNodes().OfType<XText>().Select(node => node.Value)];

	// The text of the document with comments trimmed, the one change the comment marker rule makes to a comment,
	// and an element of two tags with nothing between them written as one, the one change the empty-element rule makes.
	private static string Normalize(XDocument document)
	{
		foreach (XComment comment in document.DescendantNodes().OfType<XComment>())
			comment.Value = comment.Value.Trim();

		foreach (
			XElement element in document.Descendants().Where(element => !element.Nodes().Any())
		)
		{
			element.RemoveNodes();
		}

		return document.ToString(SaveOptions.DisableFormatting);
	}

	private static string CreateDocument(Random random)
	{
		StringBuilder builder = new();

		builder.Append("<root>");
		AppendChildren(builder, random, depth: 1);
		builder.Append(Break(random)).Append("</root>\n");

		return builder.ToString();
	}

	private static void AppendChildren(StringBuilder builder, Random random, int depth)
	{
		for (int child = 0; child < random.Next(2, 6); child++)
		{
			string name = s_names[random.Next(s_names.Length)];

			builder.Append(Break(random));

			switch (depth < 3 ? random.Next(5) : random.Next(4))
			{
				case 0:
					// The gap before the slash runs from none to two spaces, so each slash rule has a tag to change.
					// An odd child is written as two tags with nothing between them, which the empty-element rule joins.
					string gap = new(' ', child % 3);
					string end = child % 2 == 1 ? $"></{name}>" : "/>";

					builder.Append(
						CultureInfo.InvariantCulture,
						$"<{name} id=\"{child}\"{gap}{end}"
					);

					break;

				case 1:
					builder.Append(CultureInfo.InvariantCulture, $"<{name}>text {child}</{name}>");
					break;

				case 2:
					builder.Append(CultureInfo.InvariantCulture, $"<!--note {child}-->");
					break;

				case 3:
					builder
						.Append(CultureInfo.InvariantCulture, $"<{name}>")
						.Append(Break(random))
						.Append(CultureInfo.InvariantCulture, $"text {child}")
						.Append(Break(random))
						.Append(CultureInfo.InvariantCulture, $"</{name}>");

					break;

				default:
					builder.Append(CultureInfo.InvariantCulture, $"<{name}>");
					AppendChildren(builder, random, depth + 1);

					builder
						.Append(Break(random))
						.Append(CultureInfo.InvariantCulture, $"</{name}>");

					break;
			}
		}
	}

	// A line break with zero to three blank lines and a random indent after it.
	private static string Break(Random random)
	{
		return "\n"
			+ new string('\n', random.Next(4))
			+ new string(random.Next(2) == 0 ? '\t' : ' ', random.Next(5));
	}
}
