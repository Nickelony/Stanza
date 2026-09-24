using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Markup.Core.Tests;

[TestClass]
public sealed class MarkupColumnsTests
{
	[TestMethod]
	public void ATabCountsItsWidthAndEachOtherCodeUnitCountsOne()
	{
		// A surrogate pair is two code units, so it counts two.
		Assert.AreEqual(4 + 1 + 2, MarkupColumns.GetColumns("\ta\U0001F600", tabColumns: 4));
	}

	[TestMethod]
	public void ATabReachesTheNextTabStopOfItsLine()
	{
		Assert.AreEqual(2 + 2 + 1, MarkupColumns.GetColumns("ab\tc", tabColumns: 4));
		Assert.AreEqual(1 + 1, MarkupColumns.GetColumns("\tc", tabColumns: 4, startColumn: 3));
	}

	// 34 million tabs at the widest tab width the tool reads fit in a document, and their sum passes int.MaxValue.
	[TestMethod]
	public void ASumPastTheCapStopsAtTheCapInsteadOfWrappingNegative() =>
		Assert.AreEqual(MarkupColumns.MaxColumns, MarkupColumns.GetColumns("\t\t", int.MaxValue));
}
