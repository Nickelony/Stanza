using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Configuration;
using Nickelony.Stanza.Markup.Core;
using Nickelony.Stanza.Markup.Core.DocComments;
using Nickelony.Stanza.Markup.Core.Formatting;
using Nickelony.Stanza.Markup.Core.Xml;
using Nickelony.Stanza.Tests.Shared;

using static Nickelony.Stanza.Tests.Shared.MarkupFormatFixtures;

namespace Nickelony.Stanza.Markup.Tests;

/// <summary>Formats seeded documents until nothing changes and keeps their content.</summary>
[TestClass]
public sealed class MarkupConvergenceTests
{
	private const int DefaultSeedCount = 8;

	private const int SoakFirstSeed = 1000;

	private const string SoakVariable = "STANZA_SOAK_SEEDS";

	// A layout settles in one pass, so the pass after it makes no edit.
	private const int MaxPasses = 2;

	private static readonly string[] s_names = ["alpha", "beta", "gamma", "delta"];

	private static readonly string[][] s_ruleSets =
	[
		[],
		[MarkupDiagnostics.KeepMultiLineMarkupElementsTogetherId],

		[
			MarkupDiagnostics.KeepMultiLineMarkupElementsTogetherId,
			MarkupDiagnostics.KeepSingleLineMarkupElementsTogetherId,
		],
	];

	private static readonly string[] s_documentationRules =
	[
		MarkupDiagnostics.RemoveDocumentationPaddingId,
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

		string[] rules = [.. s_documentationRules, MarkupDiagnostics.MarkNullKeywordId];

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

	[TestMethod]
	[TestCategory("Soak")]
	public void ALargeSeededSetSettlesTheSameWay()
	{
		string? setting = Environment.GetEnvironmentVariable(SoakVariable);

		bool isCount = int.TryParse(
			s: setting,
			style: NumberStyles.None,
			provider: CultureInfo.InvariantCulture,
			result: out int count
		);

		if (!isCount || count < 1)
			ExpectedSkip.Skip($"Set {SoakVariable} to the number of seeds to run.");

		List<string> failures = [];

		for (int seed = SoakFirstSeed; seed < SoakFirstSeed + count; seed++)
		{
			foreach (
				Action<int> run in new Action<int>[]
				{
					AssertLayoutSettles,
					seed => _ = AssertDocumentationSettles(seed),
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
			}
		}

		Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
	}

	private static void AssertLayoutSettles(int seed)
	{
		(string source, MarkupOptions options) = CreateLayoutCase(seed);
		(string text, _) = Settle(source, options, seed);

		Assert.AreEqual(
			Normalize(XDocument.Parse(source)),
			Normalize(XDocument.Parse(text)),
			$"seed {seed}: a layout changed the content of the document."
		);
	}

	private static (string Source, MarkupOptions Options) CreateLayoutCase(int seed)
	{
		MarkupOptions options = new(
			MarkupProfiles.All[seed % MarkupProfiles.All.Count].Policy,
			MaxBlankLines: 1 + (seed % 3),
			Rules: MarkupRuleSet.From(s_ruleSets[seed % s_ruleSets.Length])
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

	private static string[] ReadWords(string xml)
	{
		// Each text node stands alone, so words that touch across an element boundary stay two words.
		string[] texts =
		[
			.. XDocument.Parse(xml).DescendantNodes().OfType<XText>().Select(node => node.Value),
		];

		return [.. GeneratedDocBody.ReadWords(texts).Order(StringComparer.Ordinal)];
	}

	// The text of the document with comments trimmed, the one change the comment marker rule makes to a comment.
	private static string Normalize(XDocument document)
	{
		foreach (XComment comment in document.DescendantNodes().OfType<XComment>())
			comment.Value = comment.Value.Trim();

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
					builder.Append(CultureInfo.InvariantCulture, $"<{name} id=\"{child}\" />");
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
