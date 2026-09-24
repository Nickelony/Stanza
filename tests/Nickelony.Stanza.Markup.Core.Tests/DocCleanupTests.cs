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
			FormatReprint(source, Rules("ZA0203"), contentWidth: 60).Text
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
			FormatReprint(Member("<summary>alpha beta gamma</summary>"), Rules("ZA0210")).Text
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
		MarkupFormatResult once = FormatReprint(Member(element), Rules("ZA0210"), contentWidth: 60);

		Assert.Contains(first, once.Text);
		Assert.Contains(second, once.Text);

		Assert.IsFalse(
			FormatReprint(once.Text, Rules("ZA0210"), contentWidth: 60).HasEdits,
			once.Text
		);
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
		Assert.IsFalse(FormatReprint(source, Rules("ZA0103"), contentWidth: 60).HasEdits);
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
		Assert.AreEqual(source, FormatReprint(source, Rules("ZA0210"), contentWidth: 40).Text);
	}

	[TestMethod]
	[DataRow("see https://example.com/docs")]
	[DataRow("reads data/settings")]
	public void ATextThatEndsOnALinkOrAPathGainsNoPeriod(string text)
	{
		string source = Member($"<summary>{text}</summary>");

		Assert.AreEqual(
			source,
			FormatReprint(source, Rules("ZA0210"), contentWidth: 60).Text,
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
				Rules("ZA0211"),
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
				Rules("ZA0207"),
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
				Rules("ZA0208"),
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
				Rules("ZA0207", "ZA0211"),
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
				Rules("ZA0207", "ZA0208", "ZA0211"),
				contentWidth: 60
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
				Rules("ZA0209"),
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
				Rules("ZA0210", "ZA0211"),
				contentWidth: 80
			).Text
		);
	}

	[TestMethod]
	public void AListAtTheTopLevelLeavesEveryKeywordAlone()
	{
		string text = FormatReprint(
			Member(
				"<summary>alpha is null</summary>\n  <list type=\"bullet\">\n  <item><description>One.</description></item>\n  </list>"
			),

			Rules("ZA0211"),
			contentWidth: 80
		).Text;

		Assert.Contains("alpha is null", text);
	}

	[TestMethod]
	public void ThePeriodClosesASentenceThatEndsOnAKeywordElement()
	{
		Assert.Contains(
			"<see langword=\"null\"/>.",

			FormatReprint(
				Member("<summary>alpha is <see langword=\"null\"/></summary>"),
				Rules("ZA0210"),
				contentWidth: 80
			).Text
		);
	}

	[TestMethod]
	public void TheElementsAreWrittenInTheDocumentedOrder()
	{
		string text = FormatReprint(
			Member("<remarks>Reads twice.</remarks>\n  <summary>Binds.</summary>"),
			Rules("ZA0212"),
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

			Rules("ZA0212"),
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
			Rules("ZA0212"),
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

			Rules("ZA0212"),
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
			FormatReprint(source, Rules("ZA0212"), contentWidth: 60).Text
		);
	}

	[TestMethod]
	public void TheCleanUpsDoNotReachIntoAList()
	{
		string text = FormatReprint(
			Member(
				"<list type=\"bullet\">\n  <item><description>alpha null <c></c> <br /> beta</description></item>\n  </list>"
			),

			Rules("ZA0208", "ZA0209", "ZA0210", "ZA0211"),
			contentWidth: 80
		).Text;

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

			Rules("ZA0213"),
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

			Rules("ZA0213"),
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
			FormatReprint(source, Rules("ZA0213"), contentWidth: 60).Text
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
