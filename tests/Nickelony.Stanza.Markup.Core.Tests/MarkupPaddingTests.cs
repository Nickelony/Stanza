using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Core.Formatting;
using Nickelony.Stanza.Markup.Core.Options;
using Nickelony.Stanza.Markup.Core.Xml;

using static Nickelony.Stanza.Tests.Shared.MarkupFormatFixtures;

namespace Nickelony.Stanza.Markup.Core.Tests;

[TestClass]
public sealed class MarkupPaddingTests
{
	private static readonly MarkupOptions s_padding = new(
		XmlPolicy,
		Rules: Rules(MarkupDiagnostics.RemoveMarkupElementPaddingId)
	);

	[TestMethod]
	public void ABlankLineAfterTheStartTagAndBeforeTheEndTagIsMeasuredAsPadding()
	{
		MarkupRemoval[] paddings = Measure("<root>\n\n  <a/>\n\n</root>");

		Assert.HasCount(2, paddings);
		Assert.IsTrue(paddings.All(removal => removal.KeptBlankLines == 0));
		Assert.AreEqual(new MarkupGap(0, 2), paddings[0].Gap);
		Assert.AreEqual(new MarkupGap(2, 4), paddings[1].Gap);
	}

	[TestMethod]
	public void NoPaddingIsMeasuredUnlessTheFileEnablesTheRule()
	{
		const string markup = "<root>\n\n  <a/>\n\n</root>";
		Assert.IsEmpty(Measure(markup, new MarkupOptions(XmlPolicy)));
	}

	[TestMethod]
	public void ARunOfBlankLinesIsOneRemoval()
	{
		MarkupRemoval removal = Assert.ContainsSingle(Measure("<root>\n\n\n  <a/>\n</root>"));
		Assert.AreEqual(new MarkupGap(0, 3), removal.Gap);
	}

	[TestMethod]
	[DataRow("<root>\n  <a/>\n</root>", "no blank line")]
	[DataRow("<root><a/>\n\n<b/></root>", "a gap between children")]
	[DataRow("<root>\n  <a/>\n\n  <b/>\n</root>", "a blank line between children")]
	[DataRow("<root><a/></root>", "one line")]
	[DataRow("<root>\n\n</root>", "no child")]
	[DataRow("<root>\n\n  text\n  <a/>\n\n</root>", "text beside a child")]
	[DataRow("<root>\n\n  <a/>text\n\n</root>", "text after a child")]
	[DataRow("<root>\n\n  <![CDATA[x]]>\n  <a/>\n\n</root>", "a CDATA section")]
	[DataRow("<root>\n\n  &#10;<a/>\n\n</root>", "a reference to a line break")]
	[DataRow("<root xml:space=\"preserve\">\n\n  <a/>\n\n</root>", "preserved space")]
	public void NoPaddingIsMeasuredWhereTheWhitespaceIsContentOrAbsent(
		string markup,
		string name
	) => Assert.IsEmpty(Measure(markup), name);

	[TestMethod]
	[DataRow(
		"<root>\n  <a>\n\n    <!-- only -->\n\n  </a>\n</root>\n",
		"<root>\n  <a>\n    <!-- only -->\n  </a>\n</root>\n",
		"a lone comment"
	)]
	[DataRow(
		"<root>\n  <a>\n\n    <?pi?>\n\n  </a>\n</root>\n",
		"<root>\n  <a>\n    <?pi?>\n  </a>\n</root>\n",
		"a lone processing instruction"
	)]
	public void AnElementOfNotesAloneLosesItsPadding(string markup, string expected, string name)
	{
		MarkupFormatResult result = MarkupFormatter.Format(markup, s_padding);

		Assert.IsNull(result.Failure, name);
		Assert.AreEqual(expected, result.Text, name);
	}

	[TestMethod]
	public void AnElementWithTextAndNoChildElementKeepsItsBlankLines()
	{
		const string markup = "<root>\n  <a>\n\n    text\n    <!-- note -->\n\n  </a>\n</root>\n";

		MarkupFormatResult result = MarkupFormatter.Format(markup, s_padding);

		Assert.IsNull(result.Failure);
		Assert.IsFalse(result.HasEdits);
	}

	[TestMethod]
	public void AnElementInsideMixedContentIsLaidOutByItsOwnContent()
	{
		MarkupRemoval[] paddings = Measure("<p>text <b>\n\n  <a/>\n\n</b> more</p>");
		Assert.HasCount(2, paddings);
	}

	[TestMethod]
	public void MixedContentAndPreservedSpaceKeepEveryBlankLineWhileTheirNeighborsAreLaidOut()
	{
		const string markup =
			"<root>\n\n  <mixed>\n\n    text <b/>\n\n  </mixed>\n\n  <kept xml:space=\"preserve\">\n\n    <a/>\n\n  </kept>\n\n  <reopened xml:space=\"preserve\">\n\n    <inner xml:space=\"default\">\n\n      <a/>\n\n    </inner>\n\n  </reopened>\n\n</root>";

		const string expected =
			"<root>\n  <mixed>\n\n    text <b/>\n\n  </mixed>\n\n  <kept xml:space=\"preserve\">\n\n    <a/>\n\n  </kept>\n\n  <reopened xml:space=\"preserve\">\n\n    <inner xml:space=\"default\">\n      <a/>\n    </inner>\n\n  </reopened>\n</root>";

		MarkupFormatResult result = MarkupFormatter.Format(markup, s_padding);

		Assert.IsNull(result.Failure);
		Assert.AreEqual(expected, result.Text);
	}

	[TestMethod]
	public void ACommentKeepsItsPlaceWhenThePaddingAroundItGoes()
	{
		const string markup = "<root>\n\n  <!-- first -->\n  <a/>\n  <!-- last -->\n\n</root>\n";
		const string expected = "<root>\n  <!-- first -->\n  <a/>\n  <!-- last -->\n</root>\n";

		MarkupFormatResult result = MarkupFormatter.Format(markup, s_padding);

		Assert.IsNull(result.Failure);
		Assert.AreEqual(expected, result.Text);
	}

	[TestMethod]
	public void ACommentOnTheLineOfTheTagsBelongsToThatTag()
	{
		const string markup = "<root> <!-- open -->\n\n  <a/>\n\n  <!-- close --></root>\n";
		const string expected = "<root> <!-- open -->\n  <a/>\n  <!-- close --></root>\n";

		MarkupFormatResult result = MarkupFormatter.Format(markup, s_padding);

		Assert.IsNull(result.Failure);
		Assert.AreEqual(expected, result.Text);
	}

	[TestMethod]
	[DataRow(
		"<root>\n  <a> <!-- open -->\n\n  </a>\n</root>\n",
		"<root>\n  <a> <!-- open -->\n  </a>\n</root>\n",
		"a note on the start tag line"
	)]
	[DataRow(
		"<root>\n  <a><!-- open -->\n\n  <!-- close --></a>\n</root>\n",
		"<root>\n  <a><!-- open -->\n  <!-- close --></a>\n</root>\n",
		"a note on each tag line"
	)]
	public void AnElementWhoseNotesAllStandOnTheTagLinesLosesTheBlankLinesBetweenThem(
		string markup,
		string expected,
		string name
	)
	{
		MarkupFormatResult result = MarkupFormatter.Format(markup, s_padding);

		Assert.IsNull(result.Failure, name);
		Assert.AreEqual(expected, result.Text, name);
	}

	[TestMethod]
	public void AProcessingInstructionIsAnItemLikeAComment()
	{
		const string markup = "<root>\n\n  <?pi target?>\n  <a/>\n</root>\n";

		MarkupFormatResult result = MarkupFormatter.Format(markup, s_padding);

		Assert.IsNull(result.Failure);
		Assert.AreEqual("<root>\n  <?pi target?>\n  <a/>\n</root>\n", result.Text);
	}

	[TestMethod]
	public void AStartTagThatWrapsItsAttributesIsMeasuredFromItsLastLine()
	{
		const string markup = "<root\n  id=\"1\"\n  name=\"x\">\n\n  <a/>\n</root>\n";

		MarkupFormatResult result = MarkupFormatter.Format(markup, s_padding);

		Assert.IsNull(result.Failure);
		Assert.AreEqual("<root\n  id=\"1\"\n  name=\"x\">\n  <a/>\n</root>\n", result.Text);
	}

	[TestMethod]
	public void ABlankLineWithWhitespaceOnItIsPaddingToo()
	{
		const string markup = "<root>\n \t \n  <a/>\n  \n</root>\n";

		MarkupFormatResult result = MarkupFormatter.Format(markup, s_padding);

		Assert.IsNull(result.Failure);
		Assert.AreEqual("<root>\n  <a/>\n</root>\n", result.Text);
	}

	[TestMethod]
	public void EveryLineEndingIsKept()
	{
		const string markup = "<root>\r\n\r\n  <a/>\r\n\n</root>\r\n";

		MarkupFormatResult result = MarkupFormatter.Format(markup, s_padding);

		Assert.IsNull(result.Failure);
		Assert.AreEqual("<root>\r\n  <a/>\r\n</root>\r\n", result.Text);
	}

	[TestMethod]
	public void NestedElementsAreEachLaidOutWithoutOverlappingEdits()
	{
		const string markup = "<a>\n\n  <b>\n\n    <c/>\n\n  </b>\n\n</a>\n";

		MarkupFormatResult result = MarkupFormatter.Format(markup, s_padding);

		Assert.IsNull(result.Failure);
		Assert.AreEqual("<a>\n  <b>\n    <c/>\n  </b>\n</a>\n", result.Text);
		Assert.HasCount(4, result.Edits);

		Assert.IsTrue(
			result.Edits.All(edit =>
				edit.Id == MarkupDiagnostics.RemoveMarkupElementPaddingId
				&& edit.Message == MarkupDiagnostics.RemovePaddingMessage
			)
		);

		// A second pass finds nothing.
		Assert.IsFalse(MarkupFormatter.Format(result.Text, s_padding).HasEdits);
	}

	[TestMethod]
	public void TheEditNamesTheLineThatFollowsTheBlankLines()
	{
		MarkupFormatResult result = MarkupFormatter.Format(
			"<root>\n\n  <a/>\n\n</root>\n",
			s_padding
		);

		Assert.AreEqual(3, result.Edits[0].Line);
		Assert.AreEqual(5, result.Edits[1].Line);
	}

	[TestMethod]
	public void AGapRuleAndThePaddingRuleEditTheirOwnLines()
	{
		const string markup = "<root>\n\n  <a>1</a>\n\n  <b>2</b>\n\n\n  <c>3</c>\n\n</root>\n";

		MarkupOptions options = new(
			XmlPolicy,

			Rules: Rules(
				MarkupDiagnostics.RemoveMarkupElementPaddingId,
				MarkupDiagnostics.KeepSingleLineMarkupElementsTogetherId
			)
		);

		MarkupFormatResult result = MarkupFormatter.Format(markup, options);

		Assert.IsNull(result.Failure);
		Assert.AreEqual("<root>\n  <a>1</a>\n  <b>2</b>\n  <c>3</c>\n</root>\n", result.Text);
	}

	[TestMethod]
	public void APropertyElementTheProfileSkipsKeepsItsPadding()
	{
		const string markup = "<Grid>\n\n  <Grid.Rows>\n\n    <a/>\n\n  </Grid.Rows>\n\n</Grid>\n";

		MarkupOptions skipping = s_padding with
		{
			Policy = new MarkupPolicy(ContainersOnly: false, SkipsPropertyElements: true),
		};

		MarkupFormatResult result = MarkupFormatter.Format(markup, skipping);

		Assert.IsNull(result.Failure);

		Assert.AreEqual(
			"<Grid>\n  <Grid.Rows>\n\n    <a/>\n\n  </Grid.Rows>\n</Grid>\n",
			result.Text
		);
	}

	[TestMethod]
	public void AMemberTheReprintOwnsKeepsItsPaddingAndTheRestIsLaidOut()
	{
		const string markup =
			"<doc>\n\n  <member name=\"M\">\n\n    <a/>\n\n  </member>\n\n</doc>\n";

		MarkupFormatResult result = FormatReprint(
			markup,
			Rules(MarkupDiagnostics.RemoveMarkupElementPaddingId)
		);

		Assert.IsNull(result.Failure);

		Assert.AreEqual(
			"<doc>\n  <member name=\"M\">\n\n    <a/>\n\n  </member>\n</doc>\n",
			result.Text
		);
	}

	[TestMethod]
	public void TheRuleIsNotAppliedByDefault()
	{
		const string markup = "<root>\n\n  <a/>\n\n</root>\n";

		MarkupFormatResult result = Format(markup);

		Assert.IsNull(result.Failure);
		Assert.IsFalse(result.HasEdits);
	}

	// The gap rules record removals of their own, which the formatter filters by the rules a file enables.
	private static MarkupRemoval[] Measure(string markup, MarkupOptions? options = null)
	{
		MarkupFindings findings = MarkupAnalysis.Measure(
			sourceText: MarkupSourceText.Build(markup),
			options: options ?? s_padding,
			skipsMembers: false,
			maxDocumentCharacters: 0,
			cancellationToken: CancellationToken.None
		);

		return
		[
			.. findings.Removals.Where(removal =>
				removal.Id == MarkupDiagnostics.RemoveMarkupElementPaddingId
			),
		];
	}
}
