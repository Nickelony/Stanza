using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Core.DocComments;
using Nickelony.Stanza.Markup.Core.Formatting;
using Nickelony.Stanza.Markup.Core.Options;
using Nickelony.Stanza.Tests.Shared;

using static Nickelony.Stanza.Tests.Shared.MarkupFormatFixtures;

namespace Nickelony.Stanza.Markup.Core.Tests;

[TestClass]
public sealed class MarkupReprintTests
{
	[TestMethod]
	public void AMemberBodyIsWrittenToTheWidth()
	{
		MarkupFormatResult result = FormatReprint(
			"<member name=\"X\">\n  <summary>alpha beta gamma</summary>\n</member>"
		);

		Assert.AreEqual(
			LineFeed(
				"<member name=\"X\">\n  <summary>\n  alpha beta\n  gamma\n  </summary>\n</member>"
			),

			result.Text
		);
	}

	// The body begins where the start tag closes, so it takes the ending of that line, not of the line the tag opens on.
	[TestMethod]
	public void ABodyTakesTheLineEndingOfTheLineItStartsOn()
	{
		MarkupFormatResult result = FormatReprint(
			"<member\nname=\"X\">\r\n  <summary>alpha beta gamma</summary>\r\n</member>"
		);

		Assert.AreEqual(
			"<member\nname=\"X\">\r\n  <summary>\r\n  alpha beta\r\n  gamma\r\n  </summary>\r\n</member>",
			result.Text
		);
	}

	// The first break of the body is the one that ends its start tag's line, so a body that mixes endings takes that one.
	[TestMethod]
	public void ABodyThatMixesLineEndingsIsWrittenWithItsFirstBreak()
	{
		MarkupFormatResult result = FormatReprint(
			"<member name=\"X\">\r\n  <summary>alpha beta gamma</summary>\n</member>"
		);

		Assert.AreEqual(
			"<member name=\"X\">\r\n  <summary>\r\n  alpha beta\r\n  gamma\r\n  </summary>\r\n</member>",
			result.Text
		);
	}

	// CSharpier puts the bracket of a long start tag on the next line, and drops the space a reprint would leave before it.
	[TestMethod]
	public void AStartTagBrokenBeforeItsBracketClosesWithoutASpace()
	{
		MarkupFormatResult result = FormatReprint(
			"<member name=\"X\">\n  <param name=\"a\"\n    >alpha beta gamma</param>\n</member>"
		);

		Assert.AreEqual(
			LineFeed(
				"<member name=\"X\">\n  <param name=\"a\">\n  alpha beta\n  gamma\n  </param>\n</member>"
			),

			result.Text
		);
	}

	[TestMethod]
	public void TheDocumentationBodyOfTheZA0106ExampleIsReprinted()
	{
		string before =
			"<doc>\n  <members>\n    <member name=\"M:Sample.Type.Example\">\n      <summary>\n"
			+ "      Binds the options from the configuration section and validates every value before "
			+ "the host starts, reporting failures early enough to keep the service from running with "
			+ "invalid settings.\n      </summary>\n    </member>\n  </members>\n</doc>";

		string after =
			"<doc>\n  <members>\n    <member name=\"M:Sample.Type.Example\">\n      <summary>\n"
			+ "      Binds the options from the configuration section and validates every value\n"
			+ "      before the host starts, reporting failures early enough to keep the service from running with\n"
			+ "      invalid settings.\n      </summary>\n    </member>\n  </members>\n</doc>";

		MarkupFormatResult result = FormatReprint(
			before,
			contentWidth: MarkupOptionReader.DefaultMaxLineLength
		);

		Assert.AreEqual(LineFeed(after), result.Text);
	}

	[TestMethod]
	public void AMemberBodyIsIndentedWithTabsWhereTheLayoutAsksForThem()
	{
		const string body = "<remarks>\n  <para>Binds the options.</para>\n  </remarks>";

		MarkupFormatResult result = FormatReprint(
			source: Member(body),
			contentWidth: 60,
			indentSize: 4,
			indentWithTabs: true
		);

		Assert.AreEqual(
			LineFeed(
				"<member name=\"X\">\n  <remarks>\n  \t<para>\n  \t\tBinds the options.\n"
					+ "  \t</para>\n  </remarks>\n</member>"
			),

			result.Text
		);
	}

	[TestMethod]
	public void EverythingOutsideTheTwoTagsOfAMemberIsKept()
	{
		string source =
			"<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n"
			+ "<doc>\r\n  <members>\r\n    <member name=\"X\">\r\n"
			+ "      <summary>alpha beta gamma</summary>\r\n"
			+ "    </member>\r\n\r\n    <member name=\"Y\">\r\n"
			+ "      <summary>alpha beta gamma</summary>\r\n"
			+ "    </member>\r\n  </members>\r\n</doc>\r\n";

		string body = "\r\n      <summary>\r\n      alpha beta gamma\r\n      </summary>\r\n    ";

		MarkupFormatResult result = FormatReprint(source, contentWidth: 40);

		Assert.AreEqual(
			source.Replace(
				"\r\n      <summary>alpha beta gamma</summary>\r\n    ",
				body,
				StringComparison.Ordinal
			),

			result.Text
		);
	}

	[TestMethod]
	public void AnElementThirtyTwoLevelsBelowTheMemberIsLeftAlone()
	{
		string source = Nested(DocWalk.MaxElementDepth);
		Assert.AreEqual(source, FormatReprint(source, contentWidth: 400).Text);
	}

	[TestMethod]
	public void AnElementThirtyOneLevelsBelowTheMemberIsWritten()
	{
		string source = Nested(DocWalk.MaxElementDepth - 1);

		// Unknown elements are read as prose, so the reprint runs the tags onto one line,
		// spacing every tag except the first two opening tags and the last closing tag.
		string expected =
			"<member name=\"X\">\n<e><e>"
			+ string.Concat(Enumerable.Repeat(" <e>", 29))
			+ " one two three"
			+ string.Concat(Enumerable.Repeat(" </e>", 30))
			+ "</e>\n</member>";

		Assert.AreEqual(expected, FormatReprint(source, contentWidth: 400).Text);
	}

	// Each tag stands on its own line and the text sits flush, so a rewrite can only come from the layout.
	private static string Nested(int levels)
	{
		string open = string.Join("\n", Enumerable.Repeat("<e>", levels));
		string close = string.Join("\n", Enumerable.Repeat("</e>", levels));

		return $"<member name=\"X\">\n{open}\none two three\n{close}\n</member>";
	}

	[TestMethod]
	[DynamicData(
		nameof(GetBodiesThatAreLeftAlone),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public void ABodyTheEngineCannotReadIsLeftAlone(string name, string source)
	{
		MarkupFormatResult result = FormatReprint(source);
		Assert.AreEqual(LineFeed(source), result.Text, $"{name}: the body was rewritten.");
	}

	public static IEnumerable<object[]> GetBodiesThatAreLeftAlone()
	{
		const string words = "one two three four five six seven eight nine ten eleven twelve";

		return
		[
			["a character reference", Member($"<summary>{words} &amp; more</summary>")],
			["a CDATA section", Member($"<summary><![CDATA[{words}]]></summary>")],
			["a comment", Member($"<summary><!-- note -->{words}</summary>")],

			[
				"a preserved element",

				Member(
					$"<remarks>\n    <custom xml:space=\"preserve\">{words}</custom>\n  </remarks>"
				),
			],

			["an include", Member($"<summary>{words}</summary>\n  <include file=\"a.xml\"/>")],
			["an inheritdoc", Member($"<summary>{words}</summary>\n  <inheritdoc/>")],
			["an opaque element", Member($"<remarks>\n    <code>{words}</code>\n  </remarks>")],
			["a processing instruction", Member($"<summary>{words}<?pi data?></summary>")],

			[
				"an inline element holding a list",

				Member(
					$"<summary><c><list type=\"bullet\"><item><description>{words}</description></item></list></c></summary>"
				),
			],

			[
				"an inline element of the body holding two paragraphs",
				Member($"<c><para>{words}</para><para>{words}</para></c>"),
			],

			[
				"a para holding a list",

				Member(
					$"<remarks>\n    <para>{words}<list type=\"bullet\"><item><description>{words}</description></item></list></para>\n  </remarks>"
				),
			],

			[
				"a blank line inside a nested element",

				Member(
					$"<remarks>\n    <para>{words}<c>{words}\n\n    {words}</c></para>\n  </remarks>"
				),
			],

			[
				"a body that shares the opening line",
				"<member name=\"X\"><summary>one two</summary></member>",
			],

			["an empty body", "<member name=\"X\">\n</member>"],
		];
	}

	[TestMethod]
	public void TheGapRuleDefersInsideAWrittenMember()
	{
		MarkupFormatResult result = FormatReprint(
			"<member name=\"X\">\n  <summary>alpha beta gamma</summary>\n"
				+ "  <returns>delta epsilon zeta</returns>\n</member>"
		);

		Assert.Contains("  </summary>\n  <returns>\n", result.Text);
		Assert.DoesNotContain("\n\n  <returns>", result.Text);
	}

	[TestMethod]
	public void AnAlreadyWrittenBodyIsNoEdit()
	{
		MarkupFormatResult first = FormatReprint(
			"<member name=\"X\">\n  <summary>alpha beta gamma</summary>\n</member>"
		);

		Assert.IsTrue(first.HasEdits);

		MarkupFormatResult second = FormatReprint(first.Text);

		Assert.IsFalse(second.HasEdits);
		Assert.AreEqual(first.Text, second.Text);
	}

	[TestMethod]
	public void AParagraphTheAuthorMarkedIsKept()
	{
		MarkupFormatResult result = FormatReprint(
			"<member name=\"X\">\n  <remarks>\n  <para>alpha beta gamma</para>\n"
				+ "  <para>delta epsilon</para>\n  </remarks>\n</member>"
		);

		Assert.Contains("  <remarks>\n  <para>\n", result.Text);
		Assert.Contains("  </para>\n  <para>\n", result.Text);
		Assert.Contains("  </para>\n  </remarks>\n", result.Text);
	}

	[TestMethod]
	public void AMemberBodyHoldingAListIsWritten()
	{
		const string body =
			"<list type=\"bullet\">\n    <item><description>One.</description></item>\n  </list>";

		MarkupFormatResult result = FormatReprint(Member(body), contentWidth: 60);

		Assert.AreEqual(
			LineFeed(
				"<member name=\"X\">\n  <list type=\"bullet\">\n"
					+ "  <item><description>One.</description></item>\n  </list>\n</member>"
			),

			result.Text
		);
	}

	[TestMethod]
	public void AnEmptyListIsLaidOut()
	{
		MarkupFormatResult result = FormatReprint(
			Member("<list type=\"bullet\"></list>"),
			contentWidth: 60
		);

		Assert.AreEqual(
			LineFeed("<member name=\"X\">\n  <list type=\"bullet\">\n  </list>\n</member>"),
			result.Text
		);
	}

	// A code span is one word of the fill, so it moves whole and keeps the spaces inside it at every width.
	[TestMethod]
	public void ACodeSpanIsFilledAsOneWord()
	{
		string source = Member(
			"<summary>Returns the value of `options.Section  ?? fallback` when the section is missing.</summary>"
		);

		foreach (int width in new[] { 20, 30, 40, 60 })
		{
			Assert.Contains(
				"`options.Section  ?? fallback`",
				FormatReprint(source, contentWidth: width).Text,
				$"The fill split the code span at width {width}."
			);
		}
	}

	// A blank line ends the paragraph, so a span still open there is no span, and the paragraphs stay apart.
	[TestMethod]
	public void ABacktickSpanEndsAtABlankLine()
	{
		string text = FormatReprint(
			Member("<remarks>alpha `beta\n\n  gamma` delta</remarks>"),
			contentWidth: 60
		).Text;

		Assert.AreEqual(2, text.Split("<para>").Length - 1, text);
	}

	// A backtick with no closing one is an ordinary character, so the words after it still wrap one at a time.
	[TestMethod]
	public void AnUnclosedBacktickIsAnOrdinaryCharacter()
	{
		string text = FormatReprint(
			Member("<summary>alpha don`t beta gamma delta epsilon zeta</summary>"),
			contentWidth: 16
		).Text;

		Assert.DoesNotContain("don`t beta gamma delta", text);
	}

	// An item too long to fold reads its text as a paragraph, so an element keeps the words and marks it touches,
	// and the fold is measured from the same atoms, so a second format at any width changes nothing.
	[TestMethod]
	public void AnItemTooLongToFoldKeepsItsTextAndSettles()
	{
		const string body =
			"<list type=\"bullet\">\n    <item><description>Returns what the caller passed to <c>Create</c>, "
			+ "or nothing (<see cref=\"C\"/>), as <see cref=\"Foo\"/>s.</description></item>\n"
			+ "    <item><description><b>Note: </b> the cache is shared.</description></item>\n  </list>";

		for (int width = 20; width <= 120; width++)
		{
			MarkupFormatResult result = FormatReprint(Member(body), contentWidth: width);

			string text = string.Join(
				" ",
				result.Text.Split((char[])[' ', '\n'], StringSplitOptions.RemoveEmptyEntries)
			);

			Assert.Contains("<c>Create</c>,", text, $"width {width}");
			Assert.Contains("(<see cref=\"C\"/>)", text, $"width {width}");
			Assert.Contains("<see cref=\"Foo\"/>s.", text, $"width {width}");
			Assert.Contains("<b>Note: </b> the", text, $"width {width}");

			Assert.IsFalse(
				FormatReprint(result.Text, contentWidth: width).HasEdits,
				$"width {width}: a second format changed the list."
			);
		}
	}

	[TestMethod]
	public void ANestedListIsWrittenOneLevelIn()
	{
		const string body =
			"<list type=\"bullet\">\n    <item>\n      <description>Two.</description>\n"
			+ "      <list type=\"bullet\"><item>Deep.</item></list>\n    </item>\n  </list>";

		MarkupFormatResult result = FormatReprint(Member(body), contentWidth: 60, indentSize: 2);

		Assert.AreEqual(
			LineFeed(
				"<member name=\"X\">\n  <list type=\"bullet\">\n    <item>\n"
					+ "      <description>Two.</description>\n      <list type=\"bullet\">\n"
					+ "        <item>Deep.</item>\n      </list>\n    </item>\n  </list>\n</member>"
			),

			result.Text
		);
	}

	[TestMethod]
	public void APlainParagraphBesideAListStaysBare()
	{
		const string body =
			"<summary>\n    Binds the options.\n    <list type=\"bullet\">\n"
			+ "      <item><description>One.</description></item>\n    </list>\n  </summary>";

		MarkupFormatResult result = FormatReprint(Member(body), contentWidth: 60);

		Assert.AreEqual(
			LineFeed(
				"<member name=\"X\">\n  <summary>\n  Binds the options.\n"
					+ "  <list type=\"bullet\">\n  <item><description>One.</description></item>\n"
					+ "  </list>\n  </summary>\n</member>"
			),

			result.Text
		);
	}

	[TestMethod]
	public void AListBetweenTwoParagraphsIsWrittenBetweenThem()
	{
		const string body =
			"<remarks>\n    The first thought.\n    <list type=\"bullet\">\n"
			+ "      <item>One.</item>\n    </list>\n    The second thought.\n  </remarks>";

		MarkupFormatResult result = FormatReprint(Member(body), contentWidth: 60);

		Assert.AreEqual(
			LineFeed(
				"<member name=\"X\">\n  <remarks>\n  <para>\n  The first thought.\n  </para>\n"
					+ "  <list type=\"bullet\">\n  <item>One.</item>\n  </list>\n  <para>\n"
					+ "  The second thought.\n  </para>\n  </remarks>\n</member>"
			),

			result.Text
		);
	}

	[TestMethod]
	public void ATableInsideAnElementIsWritten()
	{
		const string body =
			"<summary>\n    Binds the options.\n    <table>\n"
			+ "      <listheader><term>Name</term></listheader>\n"
			+ "      <item><description>Value</description></item>\n    </table>\n  </summary>";

		MarkupFormatResult result = FormatReprint(Member(body), contentWidth: 60);

		Assert.AreEqual(
			LineFeed(
				"<member name=\"X\">\n  <summary>\n  Binds the options.\n  <table>\n"
					+ "  <listheader><term>Name</term></listheader>\n"
					+ "  <item><description>Value</description></item>\n  </table>\n"
					+ "  </summary>\n</member>"
			),

			result.Text
		);
	}

	[TestMethod]
	public void ACleanElementBesideAListHoldingOneIsWritten()
	{
		const string body =
			"<summary>Alpha beta gamma.</summary>\n  <remarks>\n    <list type=\"bullet\">\n"
			+ "      <item>One.</item>\n    </list>\n  </remarks>";

		MarkupFormatResult result = FormatReprint(Member(body), contentWidth: 60);

		Assert.AreEqual(
			LineFeed(
				"<member name=\"X\">\n  <summary>Alpha beta gamma.</summary>\n  <remarks>\n"
					+ "  <list type=\"bullet\">\n  <item>One.</item>\n  </list>\n"
					+ "  </remarks>\n</member>"
			),

			result.Text
		);
	}

	[TestMethod]
	public void ANestedListInsideAnElementIsWrittenOneLevelIn()
	{
		const string body =
			"<summary>\n    Alpha.\n    <list type=\"bullet\">\n      <item>\n"
			+ "        <description>Two.</description>\n"
			+ "        <list type=\"bullet\"><item>Deep.</item></list>\n"
			+ "      </item>\n    </list>\n  </summary>";

		MarkupFormatResult result = FormatReprint(Member(body), contentWidth: 60, indentSize: 2);

		Assert.AreEqual(
			LineFeed(
				"<member name=\"X\">\n  <summary>\n    Alpha.\n    <list type=\"bullet\">\n"
					+ "      <item>\n        <description>Two.</description>\n"
					+ "        <list type=\"bullet\">\n          <item>Deep.</item>\n"
					+ "        </list>\n      </item>\n    </list>\n  </summary>\n</member>"
			),

			result.Text
		);
	}

	[TestMethod]
	public void AMemberBodyHoldingATableIsWritten()
	{
		const string body =
			"<table>\n    <listheader><term>Name</term></listheader>\n"
			+ "    <item><description>Value</description></item>\n  </table>";

		MarkupFormatResult result = FormatReprint(Member(body), contentWidth: 60);

		Assert.AreEqual(
			LineFeed(
				"<member name=\"X\">\n  <table>\n  <listheader><term>Name</term></listheader>\n"
					+ "  <item><description>Value</description></item>\n  </table>\n</member>"
			),

			result.Text
		);
	}

	[TestMethod]
	public void AListHoldingCodeIsLeftAlone()
	{
		string source = Member(
			"<list type=\"bullet\">\n  <item><code>var value = Call(1, 2);</code></item>\n  </list>"
		);

		Assert.AreEqual(LineFeed(source), FormatReprint(source, contentWidth: 60).Text);
	}

	[TestMethod]
	public void TheReprintReportsUnderItsOwnCode()
	{
		MarkupFormatResult result = FormatReprint(
			"<member name=\"X\">\n  <summary>alpha beta gamma</summary>\n</member>"
		);

		Assert.HasCount(1, result.Edits);
		Assert.AreEqual(MarkupDiagnostics.ReprintDocumentationMemberBodyId, result.Edits[0].Id);
		Assert.AreEqual(MarkupDiagnostics.ReprintBodyMessage, result.Edits[0].Message);
	}

	[TestMethod]
	public void AParagraphNestedInAParagraphKeepsItsOwnTag()
	{
		MarkupFormatResult result = FormatReprint(
			Member("<remarks><para>Alpha.<para lang=\"en\">Beta.</para></para></remarks>"),
			contentWidth: 80
		);

		Assert.AreEqual(
			Member(
				"<remarks>\n  <para>\n  Alpha.\n  </para>\n  <para lang=\"en\">\n  Beta.\n  </para>\n  </remarks>"
			),

			result.Text
		);
	}

	private static string Member(string body) => $"<member name=\"X\">\n  {body}\n</member>";
}
