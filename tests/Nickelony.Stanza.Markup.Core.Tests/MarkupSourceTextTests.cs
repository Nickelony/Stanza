using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Markup.Core.Tests;

[TestClass]
public sealed class MarkupSourceTextTests
{
	[TestMethod]
	public void ALineBreaksAtALineFeedACarriageReturnAndTheTwoTogether()
	{
		var text = MarkupSourceText.Build("a\rb\nc\r\nd");

		Assert.AreEqual(4, text.LineCount);

		Assert.AreEqual(
			new MarkupLine(LineIndex: 0, Start: 0, End: 1, EndIncludingLineBreak: 2),
			text.GetLine(0)
		);

		Assert.AreEqual(
			new MarkupLine(LineIndex: 1, Start: 2, End: 3, EndIncludingLineBreak: 4),
			text.GetLine(1)
		);

		Assert.AreEqual(
			new MarkupLine(LineIndex: 2, Start: 4, End: 5, EndIncludingLineBreak: 7),
			text.GetLine(2)
		);

		Assert.AreEqual(
			new MarkupLine(LineIndex: 3, Start: 7, End: 8, EndIncludingLineBreak: 8),
			text.GetLine(3)
		);
	}

	[TestMethod]
	public void ACharacterThatOnlyLooksLikeALineBreakDoesNotBreakTheLine()
	{
		// The reader of XML counts these as ordinary characters, so the line numbers agree with its own.
		foreach (char separator in new[] { (char)0x0085, (char)0x2028, (char)0x2029 })
		{
			var text = MarkupSourceText.Build($"a{separator}b");
			Assert.AreEqual(1, text.LineCount, $"U+{(int)separator:X4} broke the line.");
		}
	}

	[TestMethod]
	public void AnEmptyTextIsOneLineOfNoCharacters()
	{
		var text = MarkupSourceText.Build(string.Empty);

		Assert.AreEqual(1, text.LineCount);
		Assert.AreEqual(0, text.Length);

		Assert.AreEqual(
			new MarkupLine(LineIndex: 0, Start: 0, End: 0, EndIncludingLineBreak: 0),
			text.GetLine(0)
		);
	}

	[TestMethod]
	public void ALastLineWithoutABreakEndsWithTheText()
	{
		MarkupLine last = MarkupSourceText.Build("a\nb").GetLine(1);

		Assert.IsFalse(last.HasLineBreak);
		Assert.AreEqual(3, last.End);
		Assert.AreEqual(3, last.EndIncludingLineBreak);
	}

	[TestMethod]
	public void ALineBreakIsTheEndingOfItsLine()
	{
		var text = MarkupSourceText.Build("a\r\nb\nc\rd");

		Assert.AreEqual("\r\n", text.GetLineEnding(0));
		Assert.AreEqual("\n", text.GetLineEnding(1));
		Assert.AreEqual("\r", text.GetLineEnding(2));
	}

	[TestMethod]
	public void ALineWithNoBreakTakesTheFirstEndingTheTextUses()
	{
		Assert.AreEqual("\r\n", MarkupSourceText.Build("a\r\nb").GetLineEnding(1));
		Assert.AreEqual("\r", MarkupSourceText.Build("a\rb\nc").GetLineEnding(2));
	}

	[TestMethod]
	public void ATextWithNoBreakAtAllTakesALineFeed() =>
		Assert.AreEqual("\n", MarkupSourceText.Build("ab").GetLineEnding(0));

	[TestMethod]
	public void AnOffsetBelongsToTheLineItStandsIn()
	{
		var text = MarkupSourceText.Build("ab\r\ncd\nef");

		// Each pair is an offset and the line it stands in: the break of a line belongs to that line.
		(int Offset, int Line)[] expected =
		[
			(0, 0),
			(1, 0),
			(2, 0),
			(3, 0),
			(4, 1),
			(6, 1),
			(7, 2),
			(8, 2),
			(9, 2),
		];

		foreach ((int offset, int line) in expected)
			Assert.AreEqual(line, text.GetLineFromOffset(offset).LineIndex, $"offset {offset}");
	}

	[TestMethod]
	public void AnOffsetBeforeTheTextBelongsToTheFirstLine() =>
		Assert.AreEqual(0, MarkupSourceText.Build("a\nb").GetLineFromOffset(-5).LineIndex);

	[TestMethod]
	public void OnlyXmlWhitespaceMakesASpanBlank()
	{
		var text = MarkupSourceText.Build(" \t\r\n x" + (char)0x00A0);

		Assert.IsTrue(text.IsBlank(MarkupSpan.FromBounds(0, 5)));
		Assert.IsTrue(text.IsBlank(new MarkupSpan(2, 0)));
		Assert.IsFalse(text.IsBlank(MarkupSpan.FromBounds(0, 6)));

		// A no-break space is character data in markup, so it is no layout to take away.
		Assert.IsFalse(text.IsBlank(MarkupSpan.FromBounds(6, 7)));
	}

	[TestMethod]
	public void ChangesApplyInOrderAndEachReplacesItsSpan()
	{
		var text = MarkupSourceText.Build("abcd");

		List<MarkupChange> changes =
		[
			new(new MarkupSpan(1, 1), "X"),
			new(new MarkupSpan(3, 0), "Y"),
		];

		Assert.AreEqual("aXcYd", text.Apply(changes));
	}

	[TestMethod]
	public void NoChangeLeavesTheTextAsItWas()
	{
		var text = MarkupSourceText.Build("abcd");
		Assert.AreSame(text.Text, text.Apply([]));
	}
}
