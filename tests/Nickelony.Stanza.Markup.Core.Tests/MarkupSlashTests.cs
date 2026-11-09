using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Core.Formatting;
using Nickelony.Stanza.Markup.Core.Options;
using Nickelony.Stanza.Markup.Core.Xml;

using static Nickelony.Stanza.Tests.Shared.MarkupFormatFixtures;

namespace Nickelony.Stanza.Markup.Core.Tests;

[TestClass]
public sealed class MarkupSlashTests
{
	// The blank lines of a wrapped tag are not what these tests judge.
	private static readonly MarkupOptions s_spaced = new(
		XmlPolicy,
		Rules: Rules(MarkupDiagnostics.SeparateSlashOfSelfClosingMarkupElementId),
		DisabledRules: Rules(MarkupDiagnostics.SeparateMultiLineMarkupElementsId)
	);

	private static readonly MarkupOptions s_tight = new(
		XmlPolicy,
		Rules: Rules(MarkupDiagnostics.KeepSlashOfSelfClosingMarkupElementTightId),
		DisabledRules: Rules(MarkupDiagnostics.SeparateMultiLineMarkupElementsId)
	);

	[TestMethod]
	public void TheGapBeforeTheSlashIsMeasuredOnItsLine()
	{
		MarkupFindings findings = Measure("<root>\n  <a/>\n  <b x=\"1\" />\n  <c  \t/>\n</root>");

		Assert.HasCount(3, findings.SlashGaps);
		Assert.AreEqual(Gap(lineIndex: 1, slashOffset: 4, gapLength: 0), findings.SlashGaps[0]);
		Assert.AreEqual(Gap(lineIndex: 2, slashOffset: 11, gapLength: 1), findings.SlashGaps[1]);
		Assert.AreEqual(Gap(lineIndex: 3, slashOffset: 7, gapLength: 3), findings.SlashGaps[2]);
	}

	[TestMethod]
	public void ATabIsNotTheSingleSpaceTheSpacedShapeAsksFor()
	{
		MarkupSlashGap gap = Measure("<root>\n  <a\t/>\n</root>").SlashGaps.Single();

		Assert.AreEqual(1, gap.GapLength);
		Assert.IsFalse(gap.IsOneSpace);
		Assert.IsTrue(gap.NeedsChange(spaced: true));
		Assert.IsTrue(gap.NeedsChange(spaced: false));
	}

	[TestMethod]
	public void NoGapIsMeasuredUnlessTheFileEnablesAHalfOfThePair()
	{
		MarkupFindings findings = Measure("<root><a/></root>", new MarkupOptions(XmlPolicy));
		Assert.IsEmpty(findings.SlashGaps);
	}

	[TestMethod]
	[DataRow("<root>\n  <a>text</a>\n</root>", "an element with an end tag")]
	[DataRow("<root>\n  <a></a>\n</root>", "an empty element written with two tags")]
	[DataRow("<root>\n  <a\n    x=\"1\"\n    />\n</root>", "a slash on a line of its own")]
	[DataRow("<root>\n  <a x=\"1\"\n  />\n</root>", "a break before the slash")]
	[DataRow("<root>\n  <a x=\"/ \"></a>\n</root>", "a slash inside an attribute")]
	[DataRow("<root xml:space=\"preserve\">\n  <a/>\n</root>", "preserved space")]
	public void NoGapIsMeasuredWhereTheTagIsNotOneLine(string markup, string name) =>
		Assert.IsEmpty(Measure(markup).SlashGaps, name);

	[TestMethod]
	public void AnAttributeValueThatHoldsASlashIsNotTheSlashOfTheTag()
	{
		MarkupSlashGap gap = Measure("<root>\n  <a x=\"b/\" y='c />' />\n</root>")
			.SlashGaps.Single();

		Assert.AreEqual(1, gap.GapLength);
		Assert.IsTrue(gap.IsOneSpace);
	}

	[TestMethod]
	public void ATagInsideAnEmptyElementOfTheRootIsMeasured()
	{
		MarkupFindings findings = Measure("<root />");

		Assert.AreEqual(
			Gap(lineIndex: 0, slashOffset: 6, gapLength: 1),
			Assert.ContainsSingle(findings.SlashGaps)
		);
	}

	[TestMethod]
	public void TheSpacedShapeWritesOneSpaceBeforeEverySlash()
	{
		const string markup =
			"<root>\n  <a/>\n  <b x=\"1\"/>\n  <c x=\"1\" />\n  <d x=\"1\"   />\n  <e\t/>\n</root>\n";

		const string expected =
			"<root>\n  <a />\n  <b x=\"1\" />\n  <c x=\"1\" />\n  <d x=\"1\" />\n  <e />\n</root>\n";

		MarkupFormatResult result = MarkupFormatter.Format(markup, s_spaced);

		Assert.IsNull(result.Failure);
		Assert.AreEqual(expected, result.Text);
		Assert.HasCount(4, result.Edits);

		Assert.IsTrue(
			result.Edits.All(edit =>
				edit.Id == MarkupDiagnostics.SeparateSlashOfSelfClosingMarkupElementId
				&& edit.Message == MarkupDiagnostics.AddSlashSpaceMessage
			)
		);
	}

	[TestMethod]
	public void TheTightShapeRemovesTheWhitespaceBeforeEverySlash()
	{
		const string markup =
			"<root>\n  <a />\n  <b x=\"1\" />\n  <c x=\"1\"/>\n  <d x=\"1\"   />\n  <e\t/>\n</root>\n";

		const string expected =
			"<root>\n  <a/>\n  <b x=\"1\"/>\n  <c x=\"1\"/>\n  <d x=\"1\"/>\n  <e/>\n</root>\n";

		MarkupFormatResult result = MarkupFormatter.Format(markup, s_tight);

		Assert.IsNull(result.Failure);
		Assert.AreEqual(expected, result.Text);

		Assert.IsTrue(
			result.Edits.All(edit =>
				edit.Id == MarkupDiagnostics.KeepSlashOfSelfClosingMarkupElementTightId
				&& edit.Message == MarkupDiagnostics.RemoveSlashSpaceMessage
			)
		);
	}

	[TestMethod]
	public void AFileEnablingBothHalvesGetsTheDefaultHalfWhichIsTheSpacedShape()
	{
		MarkupOptions both = new(
			XmlPolicy,

			Rules: Rules(
				MarkupDiagnostics.SeparateSlashOfSelfClosingMarkupElementId,
				MarkupDiagnostics.KeepSlashOfSelfClosingMarkupElementTightId
			)
		);

		MarkupFormatResult result = MarkupFormatter.Format(
			"<root>\n  <a/>\n  <b />\n</root>\n",
			both
		);

		Assert.IsNull(result.Failure);
		Assert.AreEqual("<root>\n  <a />\n  <b />\n</root>\n", result.Text);

		Assert.IsTrue(
			result.Edits.All(edit =>
				edit.Id == MarkupDiagnostics.SeparateSlashOfSelfClosingMarkupElementId
			)
		);
	}

	[TestMethod]
	public void ASlashOnALineOfItsOwnStaysAsWrittenWhileAWrappedTagTakesTheSpace()
	{
		const string markup =
			"<root>\n  <a\n    x=\"1\"\n  />\n  <b x=\"1\"\n    y=\"2\"/>\n</root>\n";

		Assert.AreEqual(
			markup.Replace("\"2\"/>", "\"2\" />"),
			MarkupFormatter.Format(markup, s_spaced).Text
		);

		Assert.AreEqual(markup, MarkupFormatter.Format(markup, s_tight).Text);
	}

	[TestMethod]
	public void TextCommentsAttributesAndEntitiesSurviveByteForByte()
	{
		const string markup =
			"<root x=\"a &amp; b\">\n  <!-- note -->\n  <a   y='1' z=\"&#10;\"/>\n  text &lt; <b/> more\n</root>\n";

		const string expected =
			"<root x=\"a &amp; b\">\n  <!-- note -->\n  <a   y='1' z=\"&#10;\" />\n  text &lt; <b /> more\n</root>\n";

		MarkupFormatResult result = MarkupFormatter.Format(markup, s_spaced);

		Assert.IsNull(result.Failure);
		Assert.AreEqual(expected, result.Text);
	}

	[TestMethod]
	public void ATagInPreservedSpaceStaysAsWrittenUntilTheSpaceIsDefaultAgain()
	{
		const string markup =
			"<root>\n  <a/>\n  <p xml:space=\"preserve\">\n    <b/>\n    <c xml:space=\"default\">\n      <d/>\n    </c>\n  </p>\n</root>\n";

		const string expected =
			"<root>\n  <a />\n  <p xml:space=\"preserve\">\n    <b/>\n    <c xml:space=\"default\">\n      <d />\n    </c>\n  </p>\n</root>\n";

		MarkupFormatResult result = MarkupFormatter.Format(markup, s_spaced);

		Assert.IsNull(result.Failure);
		Assert.AreEqual(expected, result.Text);
	}

	[TestMethod]
	public void ATagInsideAPropertyElementTheProfileSkipsStaysAsWritten()
	{
		const string markup = "<Grid>\n  <a/>\n  <Grid.Rows>\n    <b/>\n  </Grid.Rows>\n</Grid>\n";

		MarkupOptions skipping = s_spaced with
		{
			Policy = new MarkupPolicy(ContainersOnly: false, SkipsPropertyElements: true),
		};

		MarkupFormatResult result = MarkupFormatter.Format(markup, skipping);

		Assert.AreEqual(
			"<Grid>\n  <a />\n  <Grid.Rows>\n    <b/>\n  </Grid.Rows>\n</Grid>\n",
			result.Text
		);
	}

	[TestMethod]
	public void ATagInsideAMemberTheReprintOwnsStaysAsWritten()
	{
		const string markup =
			"<doc>\n  <a/>\n  <member name=\"M\"><see cref=\"X\"/></member>\n</doc>\n";

		MarkupFormatResult plain = MarkupFormatter.Format(markup, s_spaced);

		Assert.AreEqual(
			"<doc>\n  <a />\n  <member name=\"M\"><see cref=\"X\" /></member>\n</doc>\n",
			plain.Text
		);

		MarkupFormatResult reprinted = FormatReprint(
			markup,
			Rules(MarkupDiagnostics.SeparateSlashOfSelfClosingMarkupElementId)
		);

		Assert.StartsWith("<doc>\n  <a />\n", reprinted.Text);
		Assert.DoesNotContain("<see cref=\"X\" />", reprinted.Text);
	}

	[TestMethod]
	public void TheEditNamesTheSlashItWrites()
	{
		MarkupFormatResult result = MarkupFormatter.Format(
			"<root>\n  <a x=\"1\"/>\n</root>\n",
			s_spaced
		);

		MarkupEdit edit = Assert.ContainsSingle(result.Edits);

		Assert.AreEqual(2, edit.Line);
		Assert.AreEqual(11, edit.Column);
	}

	[TestMethod]
	public void EveryLineEndingIsKept()
	{
		const string markup = "<root>\r\n  <a/>\r\n  <b />\r\n</root>\r\n";

		MarkupFormatResult result = MarkupFormatter.Format(markup, s_tight);

		Assert.AreEqual("<root>\r\n  <a/>\r\n  <b/>\r\n</root>\r\n", result.Text);
	}

	[TestMethod]
	public void NeitherHalfIsAppliedByDefault()
	{
		MarkupFormatResult result = Format("<root>\n  <a/>\n  <b />\n</root>\n");

		Assert.IsNull(result.Failure);
		Assert.IsFalse(result.HasEdits);
	}

	[TestMethod]
	public void ASecondPassFindsNothing()
	{
		foreach (MarkupOptions options in new[] { s_spaced, s_tight })
		{
			MarkupFormatResult first = MarkupFormatter.Format(
				"<root>\n  <a/>\n  <b x=\"1\"  />\n  <c\t/>\n</root>\n",
				options
			);

			Assert.IsFalse(MarkupFormatter.Format(first.Text, options).HasEdits);
		}
	}

	// A single space is the only gap the spaced shape accepts.
	private static MarkupSlashGap Gap(int lineIndex, int slashOffset, int gapLength)
	{
		return new(
			LineIndex: lineIndex,
			SlashOffset: slashOffset,
			GapLength: gapLength,
			IsOneSpace: gapLength == 1
		);
	}

	private static MarkupFindings Measure(string markup, MarkupOptions? options = null)
	{
		return MarkupAnalysis.Measure(
			sourceText: MarkupSourceText.Build(markup),
			options: options ?? s_spaced,
			skipsMembers: false,
			maxDocumentCharacters: 0,
			cancellationToken: CancellationToken.None
		);
	}
}
