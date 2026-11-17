using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class StanzaTextTests
{
	// The span of a needle, measured as the rules measure a signature or a body that stands in a line.
	private static int ColumnsOf(string source, string needle, int tabColumns = 4)
	{
		var sourceText = SourceText.From(source);
		int start = source.IndexOf(needle, System.StringComparison.Ordinal);

		return StanzaText.GetColumns(sourceText, new TextSpan(start, needle.Length), tabColumns);
	}

	[TestMethod]
	public void ATabInTheLeadingBlanksBeforeTheSpanReachesItsStop()
	{
		// The text before the span is a space and a tab, which end at column 4, so the tab of the span reaches 8.
		Assert.AreEqual(4 + 1, ColumnsOf(" \t\tx", "\tx"));
	}

	[TestMethod]
	public void ATabInsideTheSpanAfterItsFirstCharacterCountsOneColumn()
	{
		Assert.AreEqual(3 + 1 + 2, ColumnsOf("\tint\tx;", "int\tx;"), "The span starts at column 4, so its tab is past `int`.");
	}

	[TestMethod]
	public void ATabInTheLeadingBlanksOfTheLineThatIsInTheSpanReachesItsStop()
	{
		Assert.AreEqual(4 + 4 + 1, ColumnsOf("\t\tx", "\t\tx"));
		Assert.AreEqual(4 + 1, ColumnsOf("\tx", "\tx", tabColumns: 4));
	}

	[TestMethod]
	public void ATabInASpanThatFollowsContentOnItsLineCountsOneColumn()
	{
		// The text before the span holds `a`, so the leading blanks are over and the tab counts one.
		Assert.AreEqual(1 + 1, ColumnsOf("a\tb", "\tb"));
	}

	[TestMethod]
	public void ATabInASpanThatFollowsOnlyBlanksOnItsLineStillReachesItsStop()
	{
		// The text before the span is two spaces, so the tab reaches column 4.
		Assert.AreEqual(2 + 1, ColumnsOf("  \tb", "\tb"));
	}

	[TestMethod]
	public void TheLineOfTheSpanIsTheOnlyOneThatCounts()
	{
		// The line above holds content, and the line of the span does not.
		Assert.AreEqual(4 + 1, ColumnsOf("a\n\tb", "\tb"));
	}

	[TestMethod]
	public void ASpanSplitAtAnyPointOfALineCountsTheSameAsTheLineMeasuredWhole()
	{
		const string line = " \tint\tx\t= \"a\tb\";";

		var sourceText = SourceText.From(line);
		int whole = StanzaText.GetColumns(sourceText, new TextSpan(0, line.Length), tabColumns: 4);

		for (int split = 0; split <= line.Length; split++)
		{
			int first = StanzaText.GetColumns(sourceText, new TextSpan(0, split), tabColumns: 4);
			int second = StanzaText.GetColumns(sourceText, TextSpan.FromBounds(split, line.Length), tabColumns: 4);

			Assert.AreEqual(whole, first + second, $"The split at {split} changed the sum.");
		}
	}
}
