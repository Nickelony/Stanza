using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Markup.Core.Tests;

[TestClass]
public sealed class TextColumnsTests
{
	[TestMethod]
	public void ATabCountsItsWidthAndEachOtherCodeUnitCountsOne()
	{
		// A surrogate pair is two code units, so it counts two.
		Assert.AreEqual(4 + 1 + 2, TextColumns.GetColumns("\ta\U0001F600", tabColumns: 4));
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
