using System;
using System.Linq;
using System.Threading;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Core.Formatting;
using Nickelony.Stanza.Markup.Core.Xml;

using static Nickelony.Stanza.Tests.Shared.MarkupFormatFixtures;

namespace Nickelony.Stanza.Markup.Core.Tests;

[TestClass]
public sealed class MarkupFormatterLayoutTests
{
	[TestMethod]
	public void AnEmptyDocumentIsANoOp()
	{
		MarkupFormatResult result = Format(string.Empty);

		Assert.IsNull(result.Failure);
		Assert.IsEmpty(result.Edits);
	}

	[TestMethod]
	public void ADocumentWithoutARootElementIsANoOp()
	{
		const string source = "<?xml version=\"1.0\"?>\n<!-- nothing to lay out -->\n";

		MarkupFormatResult result = Format(source);

		Assert.IsNull(result.Failure);
		Assert.IsEmpty(result.Edits);
	}

	// The opener's own dashes start a closer, so the search for the closer starts after the opener.
	[TestMethod]
	public void ACommentThatOpensWithAnAngleBracketIsADocumentWithoutARoot()
	{
		MarkupFormatResult result = Format("<!-->x-->\n");

		Assert.IsNull(result.Failure);
		Assert.AreEqual("<!-- >x -->\n", result.Text);
	}

	[TestMethod]
	public void ACommentOfADocumentWithoutARootElementIsLaidOut()
	{
		const string source = "<?xml version=\"1.0\"?>\n<!--a note-->\n";

		MarkupFormatResult result = Format(source);

		Assert.IsNull(result.Failure);
		Assert.AreEqual("<?xml version=\"1.0\"?>\n<!-- a note -->\n", result.Text);
	}

	[TestMethod]
	public void ADocumentNestedPastTheStackLimitIsHandled()
	{
		// The change walk is iterative, so deep nesting cannot exhaust the stack.
		// The sibling pair at the bottom proves the walk reached it.
		const int depth = 20000;

		string source =
			string.Concat(Enumerable.Repeat("<a>", depth))
			+ "\n<b>\n1\n</b>\n<c>\n2\n</c>\n"
			+ string.Concat(Enumerable.Repeat("</a>", depth));

		MarkupFormatResult result = Format(source);

		Assert.IsNull(result.Failure);
		Assert.HasCount(1, result.Edits);
		Assert.Contains("</b>\n\n<c>", result.Text);
	}

	// A line feed written after a bare carriage return would join it into one CRLF break and lose the blank line.
	[TestMethod]
	[DataRow("\r", "\r\r")]
	[DataRow("\r\r\r", "\r\r")]
	public void AGapAfterABareCarriageReturnKeepsItsBlankLine(string gap, string written)
	{
		string source =
			$"<root>\n  <child>\n    text\n  </child>{gap}  <child>\n    text\n  </child>\n</root>\n";

		MarkupFormatResult result = Format(source);

		Assert.Contains($"</child>{written}  <child>", result.Text);
		Assert.IsFalse(Format(result.Text).HasEdits, "a second format changed the document.");
	}

	[TestMethod]
	public void TheSingleLineSiblingsStayTight()
	{
		AssertChangesNothing(
			"""
			<root>
			  <a>1</a>
			  <b>2</b>
			</root>
			"""
		);
	}

	[TestMethod]
	public void TheFirstChildNeverGetsABlankLineBeforeIt()
	{
		AssertChangesNothing(
			"""
			<root><a>
			  one
			</a></root>
			"""
		);
	}

	[TestMethod]
	public void TheElementsThatShareALineAreLeftAlone()
	{
		AssertChangesNothing(
			"""
			<root>
			  <a>
			    one
			  </a><b>two</b>
			</root>
			"""
		);
	}

	[TestMethod]
	public void AnElementWithMixedContentIsLeftAlone()
	{
		AssertChangesNothing(
			"""
			<root>
			  mixed <b>one</b>
			  <c>
			    two
			  </c>
			</root>
			"""
		);
	}

	[TestMethod]
	public void ACDataSectionBetweenTwoElementsIsLeftAlone()
	{
		AssertChangesNothing(
			"""
			<root>
			  <a>
			    one
			  </a>
			  <![CDATA[text]]>
			  <b>
			    two
			  </b>
			</root>
			"""
		);

		AssertChangesNothing(
			"""
			<root>
			  <a>
			    one
			  </a>
			  <![CDATA[
			  ]]>
			  <b>
			    two
			  </b>
			</root>
			"""
		);
	}

	[TestMethod]
	public void AWhitespaceOnlyCDataOnTheLineOfTheNextElementIsLeftAlone()
	{
		const string source = """
			<root>
			  <a>
			    <b/>
			  </a><![CDATA[
			  ]]><c/>
			</root>
			""";

		MarkupFormatResult result = Format(source);

		Assert.IsNull(result.Failure);
		Assert.IsEmpty(result.Edits);
	}

	[TestMethod]
	public void ACommentInsidePreservedSpaceIsLeftAlone()
	{
		AssertChangesNothing(
			"""
			<root xml:space="preserve">
			  <!--note-->
			  <a>x</a>
			</root>
			"""
		);
	}

	[TestMethod]
	public void AnElementThatPreservesSpaceIsLeftAlone()
	{
		AssertChangesNothing(
			"""
			<root>
			  <text xml:space="preserve">
			    <t>
			      one
			    </t>
			    <t>
			      two
			    </t>
			  </text>
			</root>
			"""
		);
	}

	[TestMethod]
	public void AnInnerElementCanBringBackTheDefaultSpaceHandling()
	{
		AssertFormatsTo(
			"""
			<root>
			  <text xml:space="preserve">
			    <inner xml:space="default">
			      <a>
			        one
			      </a>

			      <b>
			        two
			      </b>
			    </inner>
			  </text>
			</root>
			""",

			"""
			<root>
			  <text xml:space="preserve">
			    <inner xml:space="default">
			      <a>
			        one
			      </a>
			      <b>
			        two
			      </b>
			    </inner>
			  </text>
			</root>
			"""
		);
	}

	[TestMethod]
	public void TheNestedContainersAreSeparatedRecursively()
	{
		AssertFormatsTo(
			"""
			<root>
			  <outer>
			    <inner>
			      one
			    </inner>

			    <inner>
			      two
			    </inner>
			  </outer>

			  <other>
			    three
			  </other>
			</root>
			""",

			"""
			<root>
			  <outer>
			    <inner>
			      one
			    </inner>
			    <inner>
			      two
			    </inner>
			  </outer>
			  <other>
			    three
			  </other>
			</root>
			"""
		);
	}

	[TestMethod]
	public void AnEmptyElementIsMeasuredByTheEndOfItsTag()
	{
		AssertFormatsTo(
			"""
			<root>
			  <b name="x > y"
			     other="z" />

			  <c>three</c>
			</root>
			""",

			"""
			<root>
			  <b name="x > y"
			     other="z" />
			  <c>three</c>
			</root>
			"""
		);
	}

	[TestMethod]
	public void ANotWellFormedDocumentIsReported()
	{
		MarkupFormatResult result = Format("<root>\n  <child>\n</root>\n");

		Assert.IsNotNull(result.Failure);
		Assert.IsEmpty(result.Edits);
	}

	[TestMethod]
	public void ADocumentWithAnInternalEntityIsLaidOut()
	{
		MarkupFormatResult result = Format(
			"<!DOCTYPE root [<!ENTITY e \"value\">]>\n<root>\n  <a>\n    &e;\n  </a>\n  <b>\n    &e;\n  </b>\n</root>\n"
		);

		Assert.IsNull(result.Failure);
		Assert.HasCount(1, result.Edits, "the two multi-line children are separated.");
	}

	[TestMethod]
	[DataRow("")]
	[DataRow("<?pi it's a pi?>")]
	public void AnApostropheInAnInstructionOfTheSubsetDoesNotHideTheRoot(string instruction)
	{
		MarkupFormatResult result = Format(
			$"<!DOCTYPE a [{instruction}<!ENTITY e \"x\">]>\n<a>\n<b>\n</b>\n<c/>\n</a>\n"
		);

		Assert.IsNull(result.Failure);
		Assert.HasCount(1, result.Edits, "the multi-line child and its sibling are separated.");
	}

	[TestMethod]
	[DataRow("&#32;")]
	[DataRow("&#10;")]
	[DataRow("&sp;")]
	public void ACharacterOrEntityReferenceMakesTheContentMixed(string reference)
	{
		MarkupFormatResult result = Format(
			$"<!DOCTYPE root [<!ENTITY sp \" \">]>\n<root>\n  <b>\n  </b>{reference}\n  <c/>\n</root>\n"
		);

		Assert.IsNull(result.Failure);
		Assert.IsEmpty(result.Edits, result.Text);
	}

	[TestMethod]
	public void TheMarkupInsideAnEntityIsNotReportedAtItsDeclaration()
	{
		MarkupFormatResult result = Format(
			"<!DOCTYPE root [<!ENTITY e \"<a><!--x--></a>\">]>\n<root>\n  &e;\n  &e;\n</root>\n"
		);

		Assert.IsNull(result.Failure);
		Assert.IsEmpty(result.Edits, result.Text);
	}

	// The text inside an entity's expansion belongs to the entity, so it gives the container around it no character data.
	[TestMethod]
	public void TextInsideAnEntityLeavesItsContainerFreeToCap()
	{
		MarkupFormatResult result = Format(
			"<!DOCTYPE r [<!ENTITY e \"<b>hi</b>\">]>\n<r>\n  <a />\n\n\n\n  <c />&e;\n</r>\n"
		);

		Assert.IsNull(result.Failure);
		Assert.AreEqual("ZA0104", result.Edits.Single().Id, result.Text);
	}

	[TestMethod]
	public void AnOutsideDeclarationIsLeftAlone()
	{
		MarkupFormatResult result = Format("<!DOCTYPE root SYSTEM \"other.dtd\">\n<root />\n");

		Assert.IsNull(result.Failure);
		Assert.IsEmpty(result.Edits);
	}

	[TestMethod]
	public void AnEntityFromAnOutsideDeclarationIsRefused()
	{
		MarkupFormatResult result = Format(
			"<!DOCTYPE root SYSTEM \"other.dtd\">\n<root>\n  &e;\n</root>\n"
		);

		Assert.IsNotNull(result.Failure);
		Assert.IsEmpty(result.Edits);
	}

	[TestMethod]
	public void AnEntityThatOverflowsInAnAttributeIsRefused()
	{
		const string declarations =
			"<!ENTITY a \"aaaaaaaaaa\">"
			+ "<!ENTITY b \"&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;\">"
			+ "<!ENTITY c \"&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;\">"
			+ "<!ENTITY d \"&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;\">"
			+ "<!ENTITY e \"&d;&d;&d;&d;&d;&d;&d;&d;&d;&d;\">"
			+ "<!ENTITY f \"&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;\">"
			+ "<!ENTITY g \"&f;&f;&f;&f;&f;&f;&f;&f;&f;&f;\">";

		MarkupFormatResult result = Format(
			$"<!DOCTYPE root [{declarations}]>\n<root x=\"&g;\">\n  <a>\n    one\n  </a>\n</root>\n"
		);

		Assert.IsNotNull(result.Failure);
	}

	[TestMethod]
	public void ADocumentThatEndsInsideATagIsRefused()
	{
		Assert.IsNotNull(Format("<root>\n  <a>\n    one\n  </a>\n  <b").Failure);
		Assert.IsNotNull(Format("<root").Failure);

		Assert.IsNull(
			Format("<!-- a note -->\n<?target data?>\n  \n").Failure,
			"nothing but comments and instructions"
		);
	}

	[TestMethod]
	public void AFailureReasonDropsThePositionWhateverTheLanguageOfTheRuntime()
	{
		Assert.AreEqual(
			"Bad thing.",

			MarkupFailure.Describe(
				new XmlException(
					message: "Bad thing.",
					innerException: null,
					lineNumber: 1,
					linePosition: 1
				)
			)
		);

		Assert.AreEqual(
			"Bad thing.",

			MarkupFailure.Describe(
				new LocalizedXmlException("Bad thing. Zeile 12, Position 3.", 12, 3)
			)
		);

		Assert.AreEqual(
			"Bad thing.",

			MarkupFailure.Describe(
				new LocalizedXmlException("Bad thing. Ligne 3, colonne 3.", 3, 3)
			)
		);

		Assert.AreEqual(
			"Bad 12 thing.",

			MarkupFailure.Describe(
				new LocalizedXmlException("Bad 12 thing. Zeile 12, Position 3.", 12, 3)
			)
		);

		Assert.AreEqual("Bad thing.", MarkupFailure.Describe(new XmlException("Bad thing.")));
	}

	private sealed class LocalizedXmlException : XmlException
	{
		private readonly string _localizedMessage;

		public LocalizedXmlException(string localizedMessage, int lineNumber, int linePosition)
			: base(message: localizedMessage, innerException: null, lineNumber, linePosition) =>
			_localizedMessage = localizedMessage;

		public override string Message => _localizedMessage;
	}

	[TestMethod]
	public void ACanceledRunStopsTheLayout()
	{
		using CancellationTokenSource cancellation = new();

		cancellation.Cancel();

		_ = Assert.ThrowsExactly<OperationCanceledException>(() =>
			MarkupFormatter.Format(
				"<root>\n  <a>\n    one\n  </a>\n</root>\n",
				new MarkupOptions(XmlPolicy),
				cancellationToken: cancellation.Token
			)
		);
	}

	[TestMethod]
	public void ADocumentOverTheSizeBoundIsRefusedCleanly()
	{
		const string source = "<root>\n  <child>text</child>\n</root>\n";

		MarkupFormatResult result = Format(
			source,
			new MarkupOptions(XmlPolicy),
			maxDocumentCharacters: 8
		);

		Assert.IsNotNull(result.Failure);
		Assert.IsFalse(result.IsDefect, "the refusal is a parse failure");
		Assert.IsGreaterThanOrEqualTo(1, result.Failure!.Value.Line, "a failure names a line");
		Assert.IsGreaterThanOrEqualTo(1, result.Failure!.Value.Column, "a failure names a column");
		Assert.IsEmpty(result.Edits);

		Assert.IsNull(Format(source).Failure);
	}

	[TestMethod]
	public void AMalformedPrologIsReported()
	{
		Assert.IsNotNull(Format("hello<a/>").Failure, "stray text before the root");
		Assert.IsNotNull(Format("<root attr=>").Failure, "a broken first tag");
		Assert.IsNotNull(Format("<a/><b/>").Failure, "a second root element");

		Assert.IsNotNull(
			Format("<root><!-- unterminated </root>").Failure,
			"an unterminated comment"
		);
	}

	[TestMethod]
	public void AFindingCarriesTheLineAndTheMessageOfAGap()
	{
		MarkupFormatResult added = Format(LineFeed(Source));

		Assert.HasCount(1, added.Edits);
		Assert.AreEqual(5, added.Edits[0].Line);
		Assert.AreEqual(MarkupDiagnostics.SeparateMultiLineMarkupElementsId, added.Edits[0].Id);
		Assert.AreEqual(MarkupDiagnostics.AddBlankLineMessage, added.Edits[0].Message);

		MarkupFormatResult removed = Format(
			LineFeed(
				"""
				<root>
				  <a>1</a>



				  <b>2</b>
				</root>
				"""
			)
		);

		Assert.HasCount(1, removed.Edits);
		Assert.AreEqual(6, removed.Edits[0].Line);

		Assert.AreEqual(
			MarkupDiagnostics.CollapseExtraBlankLinesBetweenMarkupElementsId,
			removed.Edits[0].Id
		);

		Assert.AreEqual(MarkupDiagnostics.RemoveBlankLinesMessage, removed.Edits[0].Message);
	}

	[TestMethod]
	public void AUnicodeSeparatorIsNotALineBreakForTheLayout()
	{
		foreach (string separator in new[] { "\u0085", "\u2028", "\u2029" })
		{
			string source =
				$"<root>\n  <a>{separator}\n    <x/>\n  </a>\n  <b>\n    <y/>\n  </b>\n</root>\n";

			string expected =
				$"<root>\n  <a>{separator}\n    <x/>\n  </a>\n\n  <b>\n    <y/>\n  </b>\n</root>\n";

			AssertFormatsTo(expected, source);
		}
	}

	[TestMethod]
	public void AClosingTagWrittenAcrossLinesEndsOnItsTerminatorLine()
	{
		const string source = "<root>\n  <a>\n    one</a\n  >\n  <b/>\n</root>\n";

		const string expected = "<root>\n  <a>\n    one</a\n  >\n\n  <b/>\n</root>\n";

		AssertFormatsTo(expected, source);
	}
}
