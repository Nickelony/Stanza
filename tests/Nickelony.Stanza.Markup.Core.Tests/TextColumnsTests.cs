using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Markup.Core.Tests;

[TestClass]
public sealed class TextColumnsTests
{
	[TestMethod]
	public void ATabCountsItsWidthAndAnEmojiCountsTwo()
	{
		// An emoji is wide, so it counts two, and it takes two code units too.
		Assert.AreEqual(4 + 1 + 2, TextColumns.GetColumns("\ta\U0001F600", tabColumns: 4));
	}

	// A code point is written as its number, so no file holds the character itself.
	private static string Text(params int[] codePoints) =>
		string.Concat(codePoints.Select(char.ConvertFromUtf32));

	[TestMethod]
	[DataRow(0x6771, 2, "a CJK ideograph")]
	[DataRow(0x3042, 2, "a hiragana")]
	[DataRow(0xAC00, 2, "a Hangul syllable")]
	[DataRow(0xFF21, 2, "a fullwidth Latin capital")]
	[DataRow(0x20000, 2, "a CJK ideograph outside the basic plane")]
	[DataRow(0x1F600, 2, "an emoji")]
	[DataRow(0xFF61, 1, "a halfwidth form")]
	[DataRow(0x00B1, 1, "an ambiguous sign")]
	[DataRow(0x2460, 1, "an ambiguous circled digit")]
	[DataRow(0x1D400, 1, "a mathematical letter, which is one column in two code units")]
	[DataRow(0x0301, 0, "a combining accent")]
	[DataRow(0x200B, 0, "a zero width space")]
	[DataRow(0x200C, 0, "a zero width non-joiner")]
	[DataRow(0x200D, 0, "a zero width joiner")]
	[DataRow(0xFE0F, 0, "a variation selector")]
	[DataRow(0xE0100, 0, "a variation selector of the supplement")]
	public void ACharacterCountsTheColumnsATerminalShowsIt(int codePoint, int expected, string what) =>
		Assert.AreEqual(expected, TextColumns.GetColumns(Text(codePoint), tabColumns: 4), what);

	[TestMethod]
	public void ACombiningMarkAddsNoColumnToItsBase()
	{
		Assert.AreEqual(1, TextColumns.GetColumns(Text('e', 0x0301), tabColumns: 4));
		Assert.AreEqual(2, TextColumns.GetColumns(Text(0x304B, 0x3099), tabColumns: 4), "A combining kana mark wins over the wide width it also has.");
	}

	[TestMethod]
	public void AnEmojiPresentationSequenceCountsTwoColumns()
	{
		Assert.AreEqual(1, TextColumns.GetColumns(Text(0x2764), tabColumns: 4), "The heart alone is text style.");
		Assert.AreEqual(2, TextColumns.GetColumns(Text(0x2764, 0xFE0F), tabColumns: 4));
		Assert.AreEqual(2, TextColumns.GetColumns(Text(0x00A9, 0xFE0F), tabColumns: 4));
		Assert.AreEqual(2, TextColumns.GetColumns(Text(0x1F600, 0xFE0F), tabColumns: 4), "A wide base stays at two.");
		Assert.AreEqual(1, TextColumns.GetColumns(Text('#', 0xFE0F), tabColumns: 4), "A digit or a sign takes the emoji style only in a keycap.");
		Assert.AreEqual(0, TextColumns.GetColumns(Text(0xFE0F), tabColumns: 4));
	}

	[TestMethod]
	public void AZeroWidthJoinerSequenceCountsItsEmojiAndNoMore()
	{
		// A family of three: each emoji counts two and the joiners none.
		Assert.AreEqual(6, TextColumns.GetColumns(Text(0x1F468, 0x200D, 0x1F469, 0x200D, 0x1F467), tabColumns: 4));
	}

	[TestMethod]
	public void ASurrogatePairIsOneCodePointAndALoneSurrogateOneColumn()
	{
		Assert.AreEqual(2, TextColumns.GetColumns("\U0001F600", tabColumns: 4));
		Assert.AreEqual(1, TextColumns.GetColumns(((char)0xD83D).ToString(), tabColumns: 4));
		Assert.AreEqual(1, TextColumns.GetColumns(((char)0xDE00).ToString(), tabColumns: 4));
		Assert.AreEqual(2, TextColumns.GetColumns(((char)0xDE00).ToString() + ((char)0xD83D), tabColumns: 4));
	}

	[TestMethod]
	public void ATabAfterWideCharactersReachesTheNextTabStopOfTheirColumns()
	{
		// Two ideographs end at column 4, which is a stop, so the tab takes a whole step.
		Assert.AreEqual(4 + 4, TextColumns.GetColumns(Text(0x6771, 0x4EAC, '\t'), tabColumns: 4));

		// One ideograph ends at column 2, so the tab takes 2.
		Assert.AreEqual(2 + 2 + 1, TextColumns.GetColumns(Text(0x6771, '\t', 'a'), tabColumns: 4));
	}

	[TestMethod]
	public void TheOffsetPastAColumnCountsWideCharactersTwo()
	{
		string text = Text(0x6771, 0x4EAC, 0x90FD, 'a');

		Assert.AreEqual(-1, TextColumns.FindOffsetPastColumn(text, tabColumns: 4, maxColumns: 7));
		Assert.AreEqual(3, TextColumns.FindOffsetPastColumn(text, tabColumns: 4, maxColumns: 6));
		Assert.AreEqual(2, TextColumns.FindOffsetPastColumn(text, tabColumns: 4, maxColumns: 5), "The third ideograph ends at column 6.");
	}

	[TestMethod]
	public void ThePairOfASurrogateIsOneCharacterToTheOffsetPastAColumn()
	{
		string text = "ab" + char.ConvertFromUtf32(0x1F600) + "c";

		Assert.AreEqual(2, TextColumns.FindOffsetPastColumn(text, tabColumns: 4, maxColumns: 3), "The offset is that of the high surrogate.");
		Assert.AreEqual(4, TextColumns.FindOffsetPastColumn(text, tabColumns: 4, maxColumns: 4));
	}

	[TestMethod]
	public void AVariationSelectorThatWidensABaseMovesTheOffsetToTheBase()
	{
		string text = "ab" + Text(0x2764, 0xFE0F);

		Assert.AreEqual(2, TextColumns.FindOffsetPastColumn(text, tabColumns: 4, maxColumns: 3), "The widened base ends at column 4.");
		Assert.AreEqual(-1, TextColumns.FindOffsetPastColumn(text, tabColumns: 4, maxColumns: 4));
	}

	[TestMethod]
	public void ASeventyFiveUnitLineOfThirtyIdeographsMeasuresOneHundredFiveColumns()
	{
		// The pinned CSharpier breaks such a call at width 100, because it counts each ideograph as two.
		string line = new string('a', 45) + string.Concat(Enumerable.Repeat(char.ConvertFromUtf32(0x6771), 30));

		Assert.AreEqual(75, line.Length);
		Assert.AreEqual(105, TextColumns.GetColumns(line, tabColumns: 4));
		Assert.AreEqual(72, TextColumns.FindOffsetPastColumn(line, tabColumns: 4, maxColumns: 100));
	}

	[TestMethod]
	public void ATableRangeIsSortedAndDoesNotOverlap()
	{
		foreach (int[] ranges in new[] { TextWidthTable.WideRanges, TextWidthTable.ZeroRanges })
		{
			Assert.AreEqual(0, ranges.Length % 2);

			for (int index = 0; index < ranges.Length; index += 2)
			{
				Assert.IsLessThanOrEqualTo(ranges[index + 1], ranges[index], $"Range {index / 2} ends before it starts.");

				if (index > 0)
					Assert.IsGreaterThan(ranges[index - 1], ranges[index], $"Range {index / 2} is not past the one before it.");
			}
		}
	}

	[TestMethod]
	public void ATabReachesTheNextTabStopOfItsLine()
	{
		Assert.AreEqual(2 + 2 + 1, TextColumns.GetColumns("ab\tc", tabColumns: 4));
		Assert.AreEqual(1 + 1, TextColumns.GetColumns("\tc", tabColumns: 4, startColumn: 3));
	}

	[TestMethod]
	public void TheOffsetPastAColumnIsTheFirstCharacterThatEndsBeyondIt()
	{
		Assert.AreEqual(-1, TextColumns.FindOffsetPastColumn("abcd", tabColumns: 4, maxColumns: 4));

		Assert.AreEqual(4, TextColumns.FindOffsetPastColumn("abcde", tabColumns: 4, maxColumns: 4));

		// The tab reaches column 8 from column 4, so the character after it is the first to end past 8.
		Assert.AreEqual(
			5,
			TextColumns.FindOffsetPastColumn("abcd\tef", tabColumns: 8, maxColumns: 8)
		);

		Assert.AreEqual(
			4,
			TextColumns.FindOffsetPastColumn("abcd\tef", tabColumns: 8, maxColumns: 7)
		);
	}

	// 34 million tabs at the widest tab width the tool reads fit in a document, and their sum passes int.MaxValue.
	[TestMethod]
	public void ASumPastTheCapStopsAtTheCapInsteadOfWrappingNegative() =>
		Assert.AreEqual(TextColumns.MaxColumns, TextColumns.GetColumns("\t\t", int.MaxValue));
}
