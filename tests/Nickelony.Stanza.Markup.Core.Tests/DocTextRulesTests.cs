using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Core.DocComments;

namespace Nickelony.Stanza.Markup.Core.Tests;

[TestClass]
public sealed class DocTextRulesTests
{
	[TestMethod]
	public void ABlankTextHasNoEnd()
	{
		Assert.AreEqual(DocTextEnd.Blank, DocTextRules.ScanTextEnd("", out _));
		Assert.AreEqual(DocTextEnd.Blank, DocTextRules.ScanTextEnd(" \t\r\n", out _));
	}

	[TestMethod]
	public void ATextEndingOnAMarkIsNotAWord() =>
		Assert.AreEqual(DocTextEnd.Mark, DocTextRules.ScanTextEnd("Binds (the value)  ", out _));

	[TestMethod]
	public void ATextEndingOnAWordEndsBeforeTheTrailingWhitespace()
	{
		Assert.AreEqual(
			DocTextEnd.Word,
			DocTextRules.ScanTextEnd("Binds the value \n  ", out int end)
		);

		Assert.AreEqual(15, end);
	}

	// The finder reads a backtick span as the fill does: a blank line ends it, and a backtick it never closes is plain text.
	// A keyword in a link, a path, quotes or a word such as `nullify` or `non-null` is not found.
	[TestMethod]
	[DataRow("returns `null` when missing", 0)]
	[DataRow("a `b` then null", 1)]
	[DataRow("Matches a ` character, or null.", 1)]
	[DataRow("Matches a ` character.\n\nReturns null when missing.", 1)]
	[DataRow("Matches a ` character.\n  \nReturns null when missing.", 1)]
	[DataRow("Joins `a\nnull b` here", 0)]
	[DataRow("see https://example.com/keywords/null for it", 0)]
	[DataRow("reads data/null/settings first", 0)]
	[DataRow("alpha nullify beta", 0)]
	[DataRow("alpha null-terminated beta", 0)]
	[DataRow("alpha non-null beta", 0)]
	[DataRow("alpha non-null-terminated beta", 0)]
	[DataRow("alpha null- or empty beta", 1)]
	[DataRow("alpha null - nothing beta", 1)]
	[DataRow("alpha empty or null- beta", 1)]
	[DataRow("says \"null\" here", 0)]
	[DataRow("says 'null' here", 0)]
	[DataRow("says \"null here", 1)]
	[DataRow("says \"null\" or null", 1)]
	public void AKeywordIsFoundOnlyWhereItStandsAsAWordOfTheProse(string text, int count) =>
		Assert.HasCount(count, DocTextRules.FindNullKeywords(text));

	[TestMethod]
	public void ANonBreakingSpaceIsTextAndNotWhitespace() =>
		Assert.AreEqual(DocTextEnd.Mark, DocTextRules.ScanTextEnd("value" + (char)0xA0, out _));
}
