using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Core.Formatting;
using Nickelony.Stanza.Markup.Core.Options;
using Nickelony.Stanza.Markup.Core.Xml;

using static Nickelony.Stanza.Tests.Shared.MarkupFormatFixtures;

namespace Nickelony.Stanza.Markup.Core.Tests;

[TestClass]
public sealed class MarkupEmptyElementTests
{
	// The blank lines of a wrapped tag are not what these tests judge.
	private static readonly MarkupOptions s_alone = new(
		XmlPolicy,
		Rules: Rules(MarkupDiagnostics.WriteEmptyMarkupElementAsSelfClosingTagId),
		DisabledRules: Rules(MarkupDiagnostics.SeparateMultiLineMarkupElementsId)
	);

	private static readonly MarkupOptions s_withSpaced = s_alone with
	{
		Rules = Rules(
			MarkupDiagnostics.WriteEmptyMarkupElementAsSelfClosingTagId,
			MarkupDiagnostics.SeparateSlashOfSelfClosingMarkupElementId
		),
	};

	private static readonly MarkupOptions s_withTight = s_alone with
	{
		Rules = Rules(
			MarkupDiagnostics.WriteEmptyMarkupElementAsSelfClosingTagId,
			MarkupDiagnostics.KeepSlashOfSelfClosingMarkupElementTightId
		),
	};

	private static readonly MarkupOptions s_withBoth = s_alone with
	{
		Rules = Rules(
			MarkupDiagnostics.WriteEmptyMarkupElementAsSelfClosingTagId,
			MarkupDiagnostics.SeparateSlashOfSelfClosingMarkupElementId,
			MarkupDiagnostics.KeepSlashOfSelfClosingMarkupElementTightId
		),
	};

	[TestMethod]
	public void AnElementOfTwoTagsWithNothingBetweenThemTakesOneSelfClosingTag()
	{
		MarkupFormatResult result = MarkupFormatter.Format(
			"<root>\n  <Item></Item>\n  <Item a=\"1\"></Item>\n</root>\n",
			s_alone
		);

		Assert.IsNull(result.Failure);
		Assert.AreEqual("<root>\n  <Item />\n  <Item a=\"1\" />\n</root>\n", result.Text);
		Assert.HasCount(2, result.Edits);

		Assert.IsTrue(
			result.Edits.All(edit =>
				edit.Id == MarkupDiagnostics.WriteEmptyMarkupElementAsSelfClosingTagId
				&& edit.Message == MarkupDiagnostics.WriteSelfClosingTagMessage
			)
		);
	}

	[TestMethod]
	public void TheTagTakesTheSlashShapeTheSlashRulesAsk()
	{
		const string markup = "<root>\n  <Item a=\"1\"></Item>\n</root>\n";

		Assert.AreEqual(
			"<root>\n  <Item a=\"1\" />\n</root>\n",
			MarkupFormatter.Format(markup, s_alone).Text
		);

		Assert.AreEqual(
			"<root>\n  <Item a=\"1\" />\n</root>\n",
			MarkupFormatter.Format(markup, s_withSpaced).Text
		);

		Assert.AreEqual(
			"<root>\n  <Item a=\"1\"/>\n</root>\n",
			MarkupFormatter.Format(markup, s_withTight).Text
		);

		Assert.AreEqual(
			"<root>\n  <Item a=\"1\" />\n</root>\n",
			MarkupFormatter.Format(markup, s_withBoth).Text
		);
	}

	[TestMethod]
	public void TheWhitespaceBeforeTheBracketOfTheStartTagTakesTheShape()
	{
		const string markup =
			"<root>\n  <Item></Item>\n  <Item a=\"1\"   ></Item>\n  <Item\t></Item >\n</root>\n";

		Assert.AreEqual(
			"<root>\n  <Item />\n  <Item a=\"1\" />\n  <Item />\n</root>\n",
			MarkupFormatter.Format(markup, s_alone).Text
		);

		Assert.AreEqual(
			"<root>\n  <Item/>\n  <Item a=\"1\"/>\n  <Item/>\n</root>\n",
			MarkupFormatter.Format(markup, s_withTight).Text
		);
	}

	[TestMethod]
	public void AttributesAndTheNamespacePrefixSurviveByteForByte()
	{
		const string markup =
			"<root xmlns:x=\"urn:x\">\n  <x:Item   a='1'  b=\"&amp; &#10;\"></x:Item>\n</root>\n";

		const string expected =
			"<root xmlns:x=\"urn:x\">\n  <x:Item   a='1'  b=\"&amp; &#10;\" />\n</root>\n";

		MarkupFormatResult result = MarkupFormatter.Format(markup, s_alone);

		Assert.IsNull(result.Failure);
		Assert.AreEqual(expected, result.Text);
	}

	[TestMethod]
	public void AttributesThatWrapOverLinesStayWhereTheyAre()
	{
		const string markup = "<root>\n  <Item\n    a=\"1\"\n    b=\"2\"></Item>\n</root>\n";

		Assert.AreEqual(
			"<root>\n  <Item\n    a=\"1\"\n    b=\"2\" />\n</root>\n",
			MarkupFormatter.Format(markup, s_alone).Text
		);

		Assert.AreEqual(
			"<root>\n  <Item\n    a=\"1\"\n    b=\"2\"/>\n</root>\n",
			MarkupFormatter.Format(markup, s_withTight).Text
		);
	}

	[TestMethod]
	public void ABracketOnALineOfItsOwnKeepsTheWrappedLayout()
	{
		const string markup = "<root>\n  <Item\n    a=\"1\"\n    ></Item>\n</root>\n";
		const string expected = "<root>\n  <Item\n    a=\"1\"\n    />\n</root>\n";

		Assert.AreEqual(expected, MarkupFormatter.Format(markup, s_withSpaced).Text);
		Assert.AreEqual(expected, MarkupFormatter.Format(markup, s_withTight).Text);
	}

	[TestMethod]
	[DataRow("<root>\n  <Item> </Item>\n</root>\n", "a space")]
	[DataRow("<root>\n  <Item>\n  </Item>\n</root>\n", "a line break")]
	[DataRow("<root>\n  <Item xml:space=\"preserve\">  </Item>\n</root>\n", "preserved whitespace")]
	[DataRow("<root>\n  <Item><!-- note --></Item>\n</root>\n", "a comment")]
	[DataRow("<root>\n  <Item><![CDATA[]]></Item>\n</root>\n", "an empty CDATA section")]
	[DataRow("<root>\n  <Item><![CDATA[text]]></Item>\n</root>\n", "a CDATA section")]
	[DataRow("<root>\n  <Item><?pi data?></Item>\n</root>\n", "a processing instruction")]
	[DataRow("<root>\n  <Item>text</Item>\n</root>\n", "text")]
	[DataRow("<root>\n  <Item>&#32;</Item>\n</root>\n", "a character reference")]
	[DataRow("<root>\n  <Item><Child/></Item>\n</root>\n", "a child element")]
	[DataRow(
		"<!DOCTYPE root [<!ENTITY none \"\">]>\n<root>\n  <Item>&none;</Item>\n</root>\n",
		"an entity that expands to nothing"
	)]
	[DataRow("<root>\n  <Item></Item\n  >\n</root>\n", "an end tag that wraps")]
	public void AnElementWithAnyContentIsNotEmpty(string markup, string name)
	{
		MarkupFormatResult result = MarkupFormatter.Format(markup, s_alone);

		Assert.IsNull(result.Failure, name);
		Assert.AreEqual(markup, result.Text, name);
		Assert.IsEmpty(result.Edits, name);
	}

	[TestMethod]
	[DataRow("div")]
	[DataRow("DIV")]
	[DataRow("Span")]
	[DataRow("script")]
	[DataRow("style")]
	[DataRow("p")]
	[DataRow("a")]
	[DataRow("textarea")]
	[DataRow("title")]
	[DataRow("iframe")]
	[DataRow("li")]
	[DataRow("td")]
	[DataRow("button")]
	[DataRow("select")]
	[DataRow("option")]
	[DataRow("label")]
	[DataRow("form")]
	[DataRow("table")]
	[DataRow("ul")]
	[DataRow("ol")]
	[DataRow("section")]
	[DataRow("nav")]
	[DataRow("header")]
	[DataRow("footer")]
	[DataRow("main")]
	[DataRow("article")]
	[DataRow("canvas")]
	[DataRow("video")]
	[DataRow("audio")]
	public void AnHtmlElementThatNeedsAnEndTagStaysAsWritten(string name)
	{
		string markup = $"<root>\n  <{name}></{name}>\n</root>\n";

		MarkupFormatResult result = MarkupFormatter.Format(markup, s_alone);

		Assert.IsNull(result.Failure);
		Assert.AreEqual(markup, result.Text);
		Assert.IsEmpty(result.Edits);
	}

	[TestMethod]
	[DataRow("br")]
	[DataRow("BR")]
	[DataRow("img")]
	[DataRow("input")]
	[DataRow("hr")]
	[DataRow("link")]
	[DataRow("meta")]
	[DataRow("x:div")]
	[DataRow("Compile")]
	public void AVoidHtmlElementAndAnElementThatIsNotHtmlTakeTheSelfClosingTag(string name)
	{
		MarkupFormatResult result = MarkupFormatter.Format(
			$"<root xmlns:x=\"urn:x\">\n  <{name}></{name}>\n</root>\n",
			s_alone
		);

		Assert.IsNull(result.Failure);
		Assert.AreEqual($"<root xmlns:x=\"urn:x\">\n  <{name} />\n</root>\n", result.Text);
	}

	[TestMethod]
	public void TheListOfHtmlElementsComparesNamesWithoutCase()
	{
		Assert.IsTrue(MarkupHtmlElements.RequiresEndTag("div"));
		Assert.IsTrue(MarkupHtmlElements.RequiresEndTag("DIV"));
		Assert.IsTrue(MarkupHtmlElements.RequiresEndTag("Textarea"));
		Assert.IsFalse(MarkupHtmlElements.RequiresEndTag("br"));
		Assert.IsFalse(MarkupHtmlElements.RequiresEndTag("Item"));
		Assert.IsFalse(MarkupHtmlElements.RequiresEndTag("x:div"));
	}

	[TestMethod]
	public void AnElementUnderPreservedSpaceStillTakesTheTagBecauseItHoldsNoWhitespace()
	{
		const string markup = "<root xml:space=\"preserve\">\n  <Item></Item>\n</root>\n";

		Assert.AreEqual(
			"<root xml:space=\"preserve\">\n  <Item />\n</root>\n",
			MarkupFormatter.Format(markup, s_alone).Text
		);
	}

	[TestMethod]
	public void TheRootElementAndANestedPairBothTakeTheTag()
	{
		Assert.AreEqual("<root />", MarkupFormatter.Format("<root></root>", s_alone).Text);

		Assert.AreEqual(
			"<root>\n  <Group>\n    <Item />\n  </Group>\n</root>\n",

			MarkupFormatter
				.Format("<root>\n  <Group>\n    <Item></Item>\n  </Group>\n</root>\n", s_alone)
				.Text
		);
	}

	[TestMethod]
	public void TheEditNamesTheBracketItReplaces()
	{
		MarkupFormatResult result = MarkupFormatter.Format(
			"<root>\n  <Item a=\"1\"></Item>\n</root>\n",
			s_alone
		);

		MarkupEdit edit = Assert.ContainsSingle(result.Edits);

		Assert.AreEqual(2, edit.Line);
		Assert.AreEqual(14, edit.Column);
	}

	[TestMethod]
	public void NoElementIsWrittenUnlessTheFileEnablesTheRule()
	{
		MarkupFormatResult result = Format("<root>\n  <Item></Item>\n</root>\n");

		Assert.IsNull(result.Failure);
		Assert.IsFalse(result.HasEdits);
	}

	[TestMethod]
	public void AnElementInsideAPropertyElementTheProfileSkipsStaysAsWritten()
	{
		const string markup =
			"<Grid>\n  <Item></Item>\n  <Grid.Rows>\n    <Row></Row>\n  </Grid.Rows>\n</Grid>\n";

		MarkupOptions skipping = s_alone with
		{
			Policy = new MarkupPolicy(ContainersOnly: false, SkipsPropertyElements: true),
		};

		Assert.AreEqual(
			"<Grid>\n  <Item />\n  <Grid.Rows>\n    <Row></Row>\n  </Grid.Rows>\n</Grid>\n",
			MarkupFormatter.Format(markup, skipping).Text
		);
	}

	[TestMethod]
	public void AnElementInsideAMemberTheReprintOwnsStaysAsWritten()
	{
		const string markup =
			"<doc>\n  <Item></Item>\n  <member name=\"M\"><Item></Item></member>\n</doc>\n";

		MarkupFormatResult plain = MarkupFormatter.Format(markup, s_alone);

		Assert.AreEqual(
			"<doc>\n  <Item />\n  <member name=\"M\"><Item /></member>\n</doc>\n",
			plain.Text
		);

		MarkupFormatResult reprinted = FormatReprint(
			markup,
			Rules(MarkupDiagnostics.WriteEmptyMarkupElementAsSelfClosingTagId)
		);

		Assert.StartsWith("<doc>\n  <Item />\n", reprinted.Text);
		Assert.DoesNotContain("<member name=\"M\"><Item />", reprinted.Text);
	}

	[TestMethod]
	public void EveryLineEndingIsKept()
	{
		const string markup =
			"<root>\r\n  <Item></Item>\r\n  <Item\r\n    a=\"1\"\r\n    ></Item>\r\n</root>\r\n";

		const string expected =
			"<root>\r\n  <Item />\r\n  <Item\r\n    a=\"1\"\r\n    />\r\n</root>\r\n";

		Assert.AreEqual(expected, MarkupFormatter.Format(markup, s_alone).Text);
	}

	[TestMethod]
	public void ASecondPassFindsNothingWhicheverSlashRulesAreEnabled()
	{
		foreach (MarkupOptions options in new[] { s_alone, s_withSpaced, s_withTight, s_withBoth })
		{
			MarkupFormatResult first = MarkupFormatter.Format(
				"<root>\n  <Item></Item>\n  <Item a=\"1\"  ></Item>\n  <Item/>\n  <Item x=\"1\"  />\n</root>\n",
				options
			);

			Assert.IsFalse(MarkupFormatter.Format(first.Text, options).HasEdits);
		}
	}

	[TestMethod]
	public void TheSlashRuleAloneSpacesASelfClosingTagAndLeavesAnEmptyPairUntouched()
	{
		MarkupOptions slash = new(
			XmlPolicy,
			Rules: Rules(MarkupDiagnostics.SeparateSlashOfSelfClosingMarkupElementId)
		);

		const string markup = "<root>\n  <Item></Item>\n  <Item/>\n</root>\n";

		Assert.AreEqual(
			"<root>\n  <Item></Item>\n  <Item />\n</root>\n",
			MarkupFormatter.Format(markup, slash).Text
		);
	}

	[TestMethod]
	public void TheFindingHoldsTheBracketAndTheEndTagOnOneLine()
	{
		MarkupFindings findings = MarkupAnalysis.Measure(
			sourceText: MarkupSourceText.Build("<root>\n  <Item a=\"1\"  ></Item>\n</root>"),
			options: s_alone,
			skipsMembers: false,
			maxDocumentCharacters: 0,
			cancellationToken: CancellationToken.None
		);

		MarkupEmptyElement element = Assert.ContainsSingle(findings.EmptyElements);

		Assert.AreEqual(1, element.LineIndex);
		Assert.AreEqual(15, element.BracketOffset);
		Assert.AreEqual(2, element.GapLength);
		Assert.AreEqual(8, element.Length);
		Assert.IsFalse(element.HasWrappedGap);
	}
}
