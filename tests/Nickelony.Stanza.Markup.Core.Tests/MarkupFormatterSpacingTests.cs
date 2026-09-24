using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Core.Formatting;
using Nickelony.Stanza.Markup.Core.Xml;

using static Nickelony.Stanza.Tests.Shared.MarkupFormatFixtures;

namespace Nickelony.Stanza.Markup.Core.Tests;

[TestClass]
public sealed class MarkupFormatterSpacingTests
{
	// The applier copies change by change, so a list that overlaps or leaves the text is the tool's own defect.
	[TestMethod]
	[DataRow(0, 5, 3, 4)]
	[DataRow(0, 5, 0, 2)]
	[DataRow(2, 3, 5, 100)]
	public void EditsThatOverlapOrLeaveTheTextAreRefusedAsADefect(
		int first,
		int firstLength,
		int second,
		int secondLength
	)
	{
		const string source = "<root><a/></root>";

		List<MarkupEdit> edits = [Edit(first, firstLength), Edit(second, secondLength)];

		MarkupFormatResult result = MarkupFormatter.Apply(
			source,
			MarkupSourceText.Build(source),
			edits
		);

		Assert.IsTrue(result.IsDefect);
		Assert.AreEqual("overlapping edits", result.Failure!.Value.Reason);
		Assert.AreEqual(source, result.Text);
		Assert.IsFalse(result.HasEdits);
	}

	[TestMethod]
	public void EditsThatTouchWithoutOverlappingAreApplied()
	{
		const string source = "<root><a/></root>";

		List<MarkupEdit> edits = [Edit(0, 6, "<r>"), Edit(6, 4, "<b/>")];

		MarkupFormatResult result = MarkupFormatter.Apply(
			source,
			MarkupSourceText.Build(source),
			edits
		);

		Assert.IsFalse(result.IsDefect);
		Assert.AreEqual("<r><b/></root>", result.Text);
	}

	[TestMethod]
	public void ACappedRunKeepsTheLineBreakOfTheLineAboveIt()
	{
		const string source = "<root>\n  <a>1</a>\r\n\r\n\r\n\r\n  <b>2</b>\n</root>\n";

		MarkupFormatResult result = Format(source);

		Assert.IsNull(result.Failure);
		Assert.AreEqual("<root>\n  <a>1</a>\r\n\n  <b>2</b>\n</root>\n", result.Text);
	}

	[TestMethod]
	public void ABlankLineInsertedIntoACrLfDocumentEndsInACarriageReturnAndALineFeed()
	{
		// The assert helpers turn every ending into a line feed, so this document is formatted as written.
		MarkupFormatResult result = Format(Source.ReplaceLineEndings("\r\n"));

		Assert.IsNull(result.Failure);
		Assert.Contains("</a>\r\n\r\n  <b>", result.Text);
		Assert.AreEqual(Formatted.ReplaceLineEndings("\r\n"), result.Text);
	}

	[TestMethod]
	public void ARunAboveACapOfTwoOrMoreKeepsTheCapInCarriageReturnPairs()
	{
		for (int cap = 2; cap <= 4; cap++)
		{
			string run = string.Concat(Enumerable.Repeat("\r\n", cap + 3));
			string kept = string.Concat(Enumerable.Repeat("\r\n", cap));

			MarkupFormatResult result = Format(
				$"<root>\r\n  <a>\r\n    one\r\n  </a>\r\n{run}  <b>two</b>\r\n</root>\r\n",
				new MarkupOptions(XmlPolicy, MaxBlankLines: cap)
			);

			Assert.IsNull(result.Failure, $"cap {cap}");

			Assert.AreEqual(
				$"<root>\r\n  <a>\r\n    one\r\n  </a>\r\n{kept}  <b>two</b>\r\n</root>\r\n",
				result.Text,
				$"cap {cap}"
			);
		}
	}

	[TestMethod]
	public void ARunAboveTheCapGoesStraightToNoneWhereTheMultiLineTightRuleIsEnabled()
	{
		MarkupOptions options = new(
			XmlPolicy,
			Rules: Rules(MarkupDiagnostics.KeepMultiLineMarkupElementsTogetherId)
		);

		MarkupFormatResult result = Format(
			"<root>\n  <a>\n    one\n  </a>\n\n\n\n  <b>two</b>\n</root>",
			options
		);

		Assert.AreEqual(
			LineFeed(
				"""
				<root>
				  <a>
				    one
				  </a>
				  <b>two</b>
				</root>
				"""
			),

			result.Text
		);

		Assert.HasCount(1, result.Edits);

		Assert.AreEqual(
			MarkupDiagnostics.KeepMultiLineMarkupElementsTogetherId,
			result.Edits[0].Id
		);

		Assert.AreEqual(MarkupDiagnostics.RemoveBlankLineMessage, result.Edits[0].Message);
		Assert.IsFalse(Format(result.Text, options).HasEdits);
	}

	[TestMethod]
	public void ARunAboveTheCapGoesStraightToNoneWhereTheSingleLineTightRuleIsEnabled()
	{
		MarkupOptions options = new(
			XmlPolicy,
			Rules: Rules(MarkupDiagnostics.KeepSingleLineMarkupElementsTogetherId)
		);

		MarkupFormatResult result = Format(
			"<root>\n  <a>one</a>\n\n\n\n  <b>two</b>\n</root>",
			options
		);

		Assert.AreEqual(
			LineFeed(
				"""
				<root>
				  <a>one</a>
				  <b>two</b>
				</root>
				"""
			),

			result.Text
		);

		Assert.HasCount(1, result.Edits);

		Assert.AreEqual(
			MarkupDiagnostics.KeepSingleLineMarkupElementsTogetherId,
			result.Edits[0].Id
		);

		Assert.IsFalse(Format(result.Text, options).HasEdits);
	}

	[TestMethod]
	public void ARunAboveTheCapKeepsTheCapWhereOnlyTheOtherTightRuleIsEnabled()
	{
		MarkupFormatResult result = Format(
			"<root>\n  <a>one</a>\n\n\n\n  <b>two</b>\n</root>",

			new MarkupOptions(
				XmlPolicy,
				Rules: Rules(MarkupDiagnostics.KeepMultiLineMarkupElementsTogetherId)
			)
		);

		Assert.AreEqual(
			LineFeed(
				"""
				<root>
				  <a>one</a>

				  <b>two</b>
				</root>
				"""
			),

			result.Text
		);
	}

	[TestMethod]
	public void ARunThatHoldsACommentIsLeftAlone()
	{
		AssertChangesNothing(
			"""
			<root>
			  <a>
			    one
			  </a>

			  <!-- section -->

			  <b>
			    two
			  </b>
			</root>
			"""
		);
	}

	[TestMethod]
	public void AFileEnablingTheMultiLineTightRuleHasItsGapTakenTight()
	{
		const string source = "<root>\n  <a>\n    one\n  </a>\n\n  <b>\n    two\n  </b>\n</root>\n";

		const string expected = "<root>\n  <a>\n    one\n  </a>\n  <b>\n    two\n  </b>\n</root>\n";

		MarkupFormatResult result = Format(
			LineFeed(source),

			new MarkupOptions(
				XmlPolicy,
				Rules: Rules(MarkupDiagnostics.KeepMultiLineMarkupElementsTogetherId)
			)
		);

		Assert.IsNull(result.Failure);
		Assert.AreEqual(LineFeed(expected), result.Text);
	}

	[TestMethod]
	public void TheSeparatingRuleDefersToAnEnabledTightOne()
	{
		const string source = "<root>\n  <a>\n    one\n  </a>\n  <b>\n    two\n  </b>\n</root>\n";

		MarkupFormatResult result = Format(
			LineFeed(source),

			new MarkupOptions(
				XmlPolicy,
				Rules: Rules(MarkupDiagnostics.KeepMultiLineMarkupElementsTogetherId)
			)
		);

		Assert.IsNull(result.Failure);
		Assert.IsEmpty(result.Edits);
	}

	[TestMethod]
	public void TheSingleLineTightRuleIsWrittenOnlyWhereAFileEnablesIt()
	{
		const string source = "<root>\n  <a>1</a>\n\n  <b>2</b>\n</root>\n";

		const string expected = "<root>\n  <a>1</a>\n  <b>2</b>\n</root>\n";

		MarkupFormatResult enabled = Format(
			LineFeed(source),

			new MarkupOptions(
				XmlPolicy,
				Rules: Rules(MarkupDiagnostics.KeepSingleLineMarkupElementsTogetherId)
			)
		);

		Assert.IsNull(enabled.Failure);
		Assert.AreEqual(LineFeed(expected), enabled.Text);

		AssertChangesNothing(source);
	}

	[TestMethod]
	public void AFileEnablingBothTightRulesHasBothGapsTakenTight()
	{
		const string source =
			"<root>\n  <a>\n    one\n  </a>\n\n  <b>\n    two\n  </b>\n\n  <c>3</c>\n\n  <d>4</d>\n</root>\n";

		const string expected =
			"<root>\n  <a>\n    one\n  </a>\n  <b>\n    two\n  </b>\n  <c>3</c>\n  <d>4</d>\n</root>\n";

		MarkupFormatResult result = Format(
			LineFeed(source),

			new MarkupOptions(
				XmlPolicy,

				Rules: Rules(
					MarkupDiagnostics.KeepMultiLineMarkupElementsTogetherId,
					MarkupDiagnostics.KeepSingleLineMarkupElementsTogetherId
				)
			)
		);

		Assert.IsNull(result.Failure);
		Assert.AreEqual(LineFeed(expected), result.Text);
	}

	private static MarkupEdit Edit(int start, int length, string newText = "")
	{
		return new MarkupEdit(
			Change: new MarkupChange(
				Span: new MarkupSpan(Start: start, Length: length),
				NewText: newText
			),

			Line: 1,
			Message: "edit",
			Id: MarkupDiagnostics.SeparateMultiLineMarkupElementsId
		);
	}
}
