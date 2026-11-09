using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Core.Formatting;

using static Nickelony.Stanza.Tests.Shared.MarkupFormatFixtures;

namespace Nickelony.Stanza.Markup.Core.Tests;

[TestClass]
public sealed class DocCleanupTests
{
	[TestMethod]
	public void ThePaddingIsTakenOutWhenTheRuleIsEnabled()
	{
		string source = Member("<summary>Binds.</summary>\n\n  <returns>Delta.</returns>");

		Assert.AreEqual(
			Member("<summary>Binds.</summary>\n  <returns>Delta.</returns>"),
			FormatReprint(source, Rules("ZA0903"), contentWidth: 60).Text
		);
	}

	[TestMethod]
	public void ThePaddingStaysWhenTheRuleIsOff()
	{
		string source = Member("<summary>Binds.</summary>\n\n  <returns>Delta.</returns>");
		Assert.AreEqual(source, FormatReprint(source, Rules(), contentWidth: 60).Text);
	}

	[TestMethod]
	public void ThePeriodClosesTheOwnTextOfAnElement()
	{
		Assert.AreEqual(
			Member("<summary>\n  alpha beta\n  gamma.\n  </summary>"),
			FormatReprint(Member("<summary>alpha beta gamma</summary>"), Rules("ZA0911")).Text
		);
	}

	[TestMethod]
	[DataRow(
		"<remarks>First paragraph\n\n  Second paragraph</remarks>",
		"First paragraph.",
		"Second paragraph."
	)]
	[DataRow("<remarks>Intro <para>One</para></remarks>", "Intro.", "One.")]
	public void EachParagraphGainsItsPeriodInOnePass(string element, string first, string second)
	{
		MarkupFormatResult once = FormatReprint(Member(element), Rules("ZA0911"), contentWidth: 60);

		Assert.Contains(first, once.Text);
		Assert.Contains(second, once.Text);

		Assert.IsFalse(
			FormatReprint(once.Text, Rules("ZA0911"), contentWidth: 60).HasEdits,
			once.Text
		);
	}

	[TestMethod]
	public void TheCapitalOpensTheOwnTextOfAnElement()
	{
		Assert.AreEqual(
			Member("<summary>\n  Alpha beta\n  gamma\n  </summary>"),
			FormatReprint(Member("<summary>alpha beta gamma</summary>"), Rules("ZA0910")).Text
		);
	}

	[TestMethod]
	[DataRow(
		"<remarks>first paragraph\n\n  second paragraph</remarks>",
		"First paragraph",
		"Second paragraph"
	)]
	[DataRow("<remarks>intro <para>one</para></remarks>", "Intro", "One")]
	[DataRow("<remarks><para>one</para> two</remarks>", "One", "Two")]
	[DataRow(
		"<remarks>reads.\n  <list type=\"bullet\">\n  <item>x</item>\n  </list>\n  then writes</remarks>",
		"Reads.",
		"Then writes"
	)]
	public void EachParagraphGainsItsCapitalInOnePass(string element, string first, string second)
	{
		MarkupFormatResult once = FormatReprint(Member(element), Rules("ZA0910"), contentWidth: 60);

		Assert.Contains(first, once.Text);
		Assert.Contains(second, once.Text);

		Assert.IsFalse(
			FormatReprint(once.Text, Rules("ZA0910"), contentWidth: 60).HasEdits,
			once.Text
		);
	}

	[TestMethod]
	public void TheCapitalAndThePeriodSettleEachParagraphInOnePass()
	{
		MarkupFormatResult once = FormatReprint(
			Member("<remarks>first paragraph\n\n  second paragraph</remarks>"),
			Rules("ZA0910", "ZA0911"),
			contentWidth: 60
		);

		Assert.Contains("First paragraph.", once.Text);
		Assert.Contains("Second paragraph.", once.Text);

		Assert.IsFalse(
			FormatReprint(once.Text, Rules("ZA0910", "ZA0911"), contentWidth: 60).HasEdits,
			once.Text
		);
	}

	[TestMethod]
	[DataRow("<see langword=\"true\"/> if found")]
	[DataRow("<paramref name=\"value\"/> is read")]
	[DataRow("<typeparamref name=\"T\"/> is read")]
	[DataRow("<see cref=\"T:X\"/> is read")]
	[DataRow("<c>x</c> is read")]
	[DataRow("iOS apps")]
	[DataRow("appsettings.json is read")]
	[DataRow("https://example.com is read")]
	[DataRow("null when missing")]
	[DataRow("2 items")]
	public void ATextWhoseStartKeepsItsCaseIsLeftAlone(string text)
	{
		string source = Member($"<summary>{text}</summary>");

		Assert.AreEqual(
			source,
			FormatReprint(source, Rules("ZA0910"), contentWidth: 60).Text,
			text
		);
	}

	[TestMethod]
	public void AListedKeywordAtTheStartSettlesWithTheKeywordRuleInOnePass()
	{
		MarkupNameSet keywords = new("null", "true");
		MarkupRuleSet rules = Rules("ZA0908", "ZA0910", "ZA0912");

		MarkupFormatResult once = FormatReprint(
			source: Member("<summary>true if found</summary>"),
			rules,
			contentWidth: 60,
			keywords: keywords
		);

		Assert.Contains("<summary><see langword=\"true\"/> if found</summary>", once.Text);

		Assert.IsFalse(
			FormatReprint(source: once.Text, rules, contentWidth: 60, keywords: keywords).HasEdits,
			once.Text
		);
	}

	[TestMethod]
	public void TextOutsideEveryElementKeepsItsCase()
	{
		string source = Member("alpha beta");
		Assert.AreEqual(source, FormatReprint(source, Rules("ZA0910"), contentWidth: 60).Text);
	}

	[TestMethod]
	public void AFoldedListItemKeepsANonBreakingSpaceAtEitherEnd()
	{
		const char space = (char)0xA0;
		string item = $"{space}alpha{space}";

		string source = Member(
			$"<summary>Lists.\n  <list type=\"bullet\">\n  <item>\n  {item}\n  </item>\n  </list></summary>"
		);

		Assert.Contains(
			$"<item>{item}</item>",
			FormatReprint(source, Rules(), contentWidth: 60).Text
		);
	}

	[TestMethod]
	public void TheGapRulesLeaveAMemberTheReprintDeclinesAsWritten()
	{
		string source = Member("<summary>A <b><c>x</c>\n\n  <c>y</c></b> z</summary>");
		Assert.IsFalse(FormatReprint(source, Rules("ZA2003"), contentWidth: 60).HasEdits);
	}

	[TestMethod]
	public void AMemberWithBareCarriageReturnsIsLeftAsWritten()
	{
		string source = Member("<summary>alpha beta gamma</summary>").Replace('\n', '\r');
		Assert.IsFalse(FormatReprint(source, Rules()).HasEdits);
	}

	// A bare carriage return among line feeds would join the base indent, and every written line would repeat it.
	[TestMethod]
	public void AMemberWithOneBareCarriageReturnIsLeftAsWritten()
	{
		string source =
			"<member name=\"X\">\n\r  <summary>alpha beta gamma delta</summary>\n</member>";

		Assert.IsFalse(FormatReprint(source, Rules()).HasEdits);
	}

	[TestMethod]
	public void AnElementThatEndsOnAnElementGainsNoPeriod()
	{
		string source = Member("<summary>alpha <c>beta</c></summary>");
		Assert.AreEqual(source, FormatReprint(source, Rules("ZA0911"), contentWidth: 40).Text);
	}

	[TestMethod]
	[DataRow("see https://example.com/docs")]
	[DataRow("reads data/settings")]
	public void ATextThatEndsOnALinkOrAPathGainsNoPeriod(string text)
	{
		string source = Member($"<summary>{text}</summary>");

		Assert.AreEqual(
			source,
			FormatReprint(source, Rules("ZA0911"), contentWidth: 60).Text,
			text
		);
	}

	[TestMethod]
	public void AKeywordInsideAWordIsNotMarkedUp()
	{
		Assert.DoesNotContain(
			"<see langword",

			FormatReprint(
				Member("<summary>alpha nullify beta</summary>"),
				Rules("ZA0912"),
				contentWidth: 60
			).Text
		);
	}

	[TestMethod]
	public void TheSpacedRuleWritesOneSpaceBeforeTheSlash()
	{
		Assert.Contains(
			"<see cref=\"T:X\" />",

			FormatReprint(
				Member("<summary>alpha <see cref=\"T:X\"/> beta</summary>"),
				Rules("ZA0907"),
				contentWidth: 60
			).Text
		);
	}

	[TestMethod]
	public void TheTightRuleTakesTheSpaceAway()
	{
		Assert.Contains(
			"<see cref=\"T:X\"/>",

			FormatReprint(
				Member("<summary>alpha <see cref=\"T:X\" /> beta</summary>"),
				Rules("ZA0908"),
				contentWidth: 60
			).Text
		);
	}

	[TestMethod]
	public void ASpacedRuleWritesTheSpacedNullKeyword()
	{
		Assert.Contains(
			"<see langword=\"null\" />",

			FormatReprint(
				Member("<summary>alpha null beta</summary>"),
				Rules("ZA0907", "ZA0912"),
				contentWidth: 60
			).Text
		);
	}

	[TestMethod]
	public void BothSlashRulesWriteTheTightNullKeyword()
	{
		Assert.Contains(
			"<see langword=\"null\"/>",

			FormatReprint(
				Member("<summary>alpha null beta</summary>"),
				Rules("ZA0907", "ZA0908", "ZA0912"),
				contentWidth: 60
			).Text
		);
	}

	[TestMethod]
	public void TheListedKeywordsAreMarkedWhereTheirPhraseProvesThem()
	{
		MarkupFormatResult result = FormatReprint(
			source: Member(
				"<summary>True if bound; otherwise, false. Is <c>default</c> or <c>true</c>.</summary>"
			),

			rules: Rules("ZA0908", "ZA0912"),
			contentWidth: 200,
			keywords: new MarkupNameSet("null", "true", "false", "default")
		);

		Assert.Contains(
			"<summary><see langword=\"true\"/> if bound; otherwise, <see langword=\"false\"/>. Is <see langword=\"default\"/> or <see langword=\"true\"/>.</summary>",

			result
				.Text.Replace("\n", string.Empty, StringComparison.Ordinal)
				.Replace("  ", " ", StringComparison.Ordinal)
		);
	}

	[TestMethod]
	public void AKeywordTheFileDoesNotListIsLeftAlone()
	{
		string source = Member("<summary>True if bound; otherwise, false, or null.</summary>");

		MarkupFormatResult unlisted = FormatReprint(
			source,
			rules: Rules("ZA0912"),
			contentWidth: 200,
			keywords: new MarkupNameSet("null")
		);

		Assert.DoesNotContain("langword=\"true\"", unlisted.Text);
		Assert.DoesNotContain("langword=\"false\"", unlisted.Text);
		Assert.Contains("langword=\"null\"", unlisted.Text);

		Assert.DoesNotContain(
			"langword=\"null\"",

			FormatReprint(
				source,
				rules: Rules("ZA0912"),
				contentWidth: 200,
				keywords: new MarkupNameSet("true")
			).Text
		);
	}

	[TestMethod]
	public void AnElementThatHoldsNothingClosesItself()
	{
		Assert.Contains(
			"<c/>",

			FormatReprint(
				Member("<summary>alpha <c></c> beta</summary>"),
				Rules("ZA0909"),
				contentWidth: 60
			).Text
		);
	}

	[TestMethod]
	public void ThePeriodClosesASentenceThatEndsOnTheKeyword()
	{
		Assert.Contains(
			"<see langword=\"null\"/>.",

			FormatReprint(
				Member("<summary>alpha is null</summary>"),
				Rules("ZA0911", "ZA0912"),
				contentWidth: 80
			).Text
		);
	}

	[TestMethod]
	public void AListAtTheTopLevelLeavesOnlyItsOwnKeywordsAlone()
	{
		string text = FormatReprint(
			Member(
				"<summary>alpha is null</summary>\n  <list type=\"bullet\">\n  <item><description>One null.</description></item>\n  </list>"
			),

			Rules("ZA0912"),
			contentWidth: 80
		).Text;

		Assert.Contains("alpha is <see langword=\"null\"/>", text);
		Assert.Contains("One null.", text);
	}

	[TestMethod]
	public void ThePeriodClosesASentenceThatEndsOnAKeywordElement()
	{
		Assert.Contains(
			"<see langword=\"null\"/>.",

			FormatReprint(
				Member("<summary>alpha is <see langword=\"null\"/></summary>"),
				Rules("ZA0911"),
				contentWidth: 80
			).Text
		);
	}

	[TestMethod]
	public void ThePeriodAndTheShortTagLandInOnePassOnAKeywordElementWithNoContent()
	{
		MarkupFormatResult once = FormatReprint(
			Member("<summary>Is <see langword=\"true\"></see></summary>"),
			Rules("ZA0909", "ZA0911"),
			contentWidth: 80
		);

		Assert.Contains("<see langword=\"true\"/>.", once.Text);

		Assert.IsFalse(
			FormatReprint(once.Text, Rules("ZA0909", "ZA0911"), contentWidth: 80).HasEdits,
			once.Text
		);
	}

	[TestMethod]
	public void ThePeriodClosesASentenceThatEndsOnAKeywordElementWithNoContent()
	{
		Assert.Contains(
			"<see langword=\"true\"></see>.",

			FormatReprint(
				Member("<summary>Is <see langword=\"true\"></see></summary>"),
				Rules("ZA0911"),
				contentWidth: 80
			).Text
		);
	}

	[TestMethod]
	public void TheElementsAreWrittenInTheDocumentedOrder()
	{
		string text = FormatReprint(
			Member("<remarks>Reads twice.</remarks>\n  <summary>Binds.</summary>"),
			Rules("ZA0913"),
			contentWidth: 60
		).Text;

		AssertStandsBefore(text, "<summary>", "<remarks>");
	}

	[TestMethod]
	public void AnElementTheOrderDoesNotNameKeepsItsPlace()
	{
		string text = FormatReprint(
			Member(
				"<remarks>M.</remarks>\n  <returns>R.</returns>\n  <note>N.</note>\n"
					+ "  <param name=\"x\">P.</param>\n  <summary>S.</summary>"
			),

			Rules("ZA0913"),
			contentWidth: 60
		).Text;

		AssertStandsBefore(text, "<returns>", "<remarks>");
		AssertStandsBefore(text, "<remarks>", "<note>");
		AssertStandsBefore(text, "<note>", "<summary>");
		AssertStandsBefore(text, "<summary>", "<param");
	}

	[TestMethod]
	public void ARunOfProseBreaksTheOrder()
	{
		string text = FormatReprint(
			Member("<remarks>M.</remarks>\n  Reads.\n  <summary>S.</summary>"),
			Rules("ZA0913"),
			contentWidth: 60
		).Text;

		AssertStandsBefore(text, "<remarks>", "<summary>");
		AssertStandsBefore(text, "<remarks>", "Reads.");
	}

	[TestMethod]
	public void ANamedElementKeepsTheOrderItStandsIn()
	{
		string text = FormatReprint(
			Member(
				"<param name=\"b\">B.</param>\n  <param name=\"a\">A.</param>\n"
					+ "  <summary>S.</summary>"
			),

			Rules("ZA0913"),
			contentWidth: 60
		).Text;

		AssertStandsBefore(text, "<summary>", "<param name=\"b\"");
		AssertStandsBefore(text, "<param name=\"b\"", "<param name=\"a\"");
	}

	[TestMethod]
	public void AnOrderedBodyIsWrittenAsItStands()
	{
		string source = Member("<summary>S.</summary>\n  <remarks>M.</remarks>");

		Assert.AreEqual(
			FormatReprint(source, Rules(), contentWidth: 60).Text,
			FormatReprint(source, Rules("ZA0913"), contentWidth: 60).Text
		);
	}

	[TestMethod]
	public void TheCleanUpsDoNotReachIntoAList()
	{
		string text = FormatReprint(
			Member(
				"<list type=\"bullet\">\n  <item><description>alpha null <c></c> <br /> beta</description></item>\n  </list>"
			),

			Rules("ZA0908", "ZA0909", "ZA0910", "ZA0911", "ZA0912"),
			contentWidth: 80
		).Text;

		Assert.Contains("<description>alpha", text);
		Assert.Contains("<c></c>", text);
		Assert.Contains("<br />", text);
		Assert.DoesNotContain("<c/>", text);
		Assert.DoesNotContain("<br/>", text);
		Assert.DoesNotContain("beta.</description>", text);
		Assert.DoesNotContain("langword", text);
	}

	[TestMethod]
	public void TheItemsOfAListAreWrittenInTheDocumentedOrder()
	{
		string text = FormatReprint(
			Member(
				"<list type=\"table\">\n  <item>\n    <description>Explains it.</description>\n    <term>Widget</term>\n  </item>\n  </list>"
			),

			Rules("ZA0914"),
			contentWidth: 40
		).Text;

		AssertStandsBefore(text, "<term>", "<description>");
	}

	[TestMethod]
	public void TheHeaderOfAListStandsBeforeItsItems()
	{
		string text = FormatReprint(
			Member(
				"<list type=\"table\">\n  <item><term>a</term><description>b</description></item>\n  <listheader><term>h</term><description>d</description></listheader>\n  </list>"
			),

			Rules("ZA0914"),
			contentWidth: 20
		).Text;

		AssertStandsBefore(text, "<listheader>", "<item>");
	}

	[TestMethod]
	public void TheItemsOfOneKindKeepTheOrderTheyStandIn()
	{
		string source = Member(
			"<list type=\"bullet\">\n  <item>beta</item>\n  <item>alpha</item>\n  </list>"
		);

		Assert.AreEqual(
			FormatReprint(source, Rules(), contentWidth: 60).Text,
			FormatReprint(source, Rules("ZA0914"), contentWidth: 60).Text
		);
	}

	[TestMethod]
	public void TheItemOrderIsOffUntilTheSeverityLineNamesIt()
	{
		string text = FormatReprint(
			Member(
				"<list type=\"table\">\n  <item>\n    <description>Explains it.</description>\n    <term>Widget</term>\n  </item>\n  </list>"
			),

			Rules(),
			contentWidth: 40
		).Text;

		AssertStandsBefore(text, "<description>", "<term>");
	}

	private static void AssertStandsBefore(string text, string first, string second)
	{
		int firstOffset = text.IndexOf(first, StringComparison.Ordinal);
		int secondOffset = text.IndexOf(second, StringComparison.Ordinal);

		Assert.IsGreaterThanOrEqualTo(0, firstOffset, $"'{first}' is missing from the output.");
		Assert.IsGreaterThanOrEqualTo(0, secondOffset, $"'{second}' is missing from the output.");

		Assert.IsLessThan(
			secondOffset,
			firstOffset,
			$"'{first}' does not stand before '{second}'."
		);
	}
}
