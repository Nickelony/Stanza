using System.Collections.Generic;
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
	public void AKeywordIsFoundOnlyWhereItStandsAsAWordOfTheProse(string text, int count)
	{
		Assert.HasCount(
			count,
			DocTextRules.FindKeywords(text, DocTextRules.DefaultKeywords, startsElement: false)
		);
	}

	// A listed true or false is the keyword only where its phrase proves it; the phrase words match in any case.
	[TestMethod]
	[DataRow("True if bound.", true, "true")]
	[DataRow("True if bound.", false, null)]
	[DataRow("alpha. false when missing.", false, "false")]
	[DataRow("alpha\n\nTRUE when missing.", false, "true")]
	[DataRow("alpha true if missing.", false, null)]
	[DataRow("Returns True when bound.", false, "true")]
	[DataRow("RETURNS: (false) when missing.", false, "false")]
	[DataRow("Else, OTHERWISE, false.", false, "false")]
	[DataRow("Else otherwise: false.", false, "false")]
	[DataRow("true, otherwise missing.", false, "true")]
	[DataRow("Is true otherwise.", false, "true")]
	[DataRow("This is true if bound.", false, null)]
	[DataRow("A true value.", false, null)]
	[DataRow("Returns trueish values.", false, null)]
	[DataRow("Returns `true` values.", false, null)]
	[DataRow("Returns true-ish values.", false, null)]
	[DataRow("Returns \"true\" values.", false, null)]
	[DataRow("Returns data/true values.", false, null)]
	[DataRow("Returns default values.", false, null)]
	[DataRow("Returns true if bound.", false, "true")]
	[DataRow("Returns false.", false, "false")]
	[DataRow("Returns true or null.", false, "true")]
	[DataRow("Else otherwise, false.", false, "false")]
	[DataRow("Returns true positives.", false, null)]
	[DataRow("Returns false alarms only.", false, null)]
	[DataRow("Otherwise false positives are dropped.", false, null)]
	[DataRow("Tax returns, true to form.", false, null)]
	[DataRow("Returns the default.", false, null)]
	[DataRow("Otherwise default values apply.", false, null)]
	public void ATrueOrFalseIsFoundOnlyWhereItsPhraseProvesIt(
		string text,
		bool startsElement,
		string? expected
	)
	{
		MarkupNameSet words = new("true", "false", "default");

		List<DocKeyword> found = DocTextRules.FindKeywords(text, words, startsElement);

		Assert.AreEqual(expected is null ? 0 : 1, found.Count);

		if (expected is not null)
			Assert.AreEqual(expected, found[0].Word);
	}

	[TestMethod]
	public void ANullIsFoundOnlyWhenTheListNamesIt()
	{
		Assert.HasCount(
			1,

			DocTextRules.FindKeywords(
				"Returns null.",
				new MarkupNameSet("null"),
				startsElement: false
			)
		);

		Assert.IsEmpty(
			DocTextRules.FindKeywords(
				"Returns null.",
				new MarkupNameSet("true"),
				startsElement: false
			)
		);

		Assert.IsEmpty(
			DocTextRules.FindKeywords(
				"Returns Null.",
				new MarkupNameSet("null"),
				startsElement: false
			)
		);
	}

	[TestMethod]
	public void ANonBreakingSpaceIsTextAndNotWhitespace() =>
		Assert.AreEqual(DocTextEnd.Mark, DocTextRules.ScanTextEnd("value" + (char)0xA0, out _));

	// A word whose case is deliberate, a link, a path, a file name or a listed keyword keeps its first letter.
	[TestMethod]
	[DataRow("alpha beta", 0)]
	[DataRow("\n  alpha beta", 3)]
	[DataRow("alpha<see cref=\"Widget\"/> beta", 0)]
	[DataRow("end. next", 0)]
	[DataRow("null-terminated text", 0)]
	[DataRow("nullable values", 0)]
	[DataRow("Alpha beta", null)]
	[DataRow("iOS apps", null)]
	[DataRow("macOS builds", null)]
	[DataRow("eBay listings", null)]
	[DataRow("jQuery calls", null)]
	[DataRow("maxCount is read", null)]
	[DataRow("https://example.com is read", null)]
	[DataRow("data/settings is read", null)]
	[DataRow("data\\settings is read", null)]
	[DataRow("appsettings.json is read", null)]
	[DataRow("e.g. this", null)]
	[DataRow("null when missing", null)]
	[DataRow("null, when missing", null)]
	[DataRow("2 items", null)]
	[DataRow("\"quoted\" text", null)]
	[DataRow("(bracketed) text", null)]
	[DataRow("&lt;T&gt; values", null)]
	[DataRow("", null)]
	[DataRow(" \n ", null)]
	public void TheCapitalIsTheFirstLetterOfAWordWhoseCaseIsNotDeliberate(
		string text,
		int? offset
	) => Assert.AreEqual(offset, DocTextRules.FindCapital(text, DocTextRules.DefaultKeywords));

	[TestMethod]
	public void AListedKeywordKeepsItsFirstLetter()
	{
		Assert.AreEqual(0, DocTextRules.FindCapital("true if found", DocTextRules.DefaultKeywords));
		Assert.IsNull(DocTextRules.FindCapital("true if found", new MarkupNameSet("null", "true")));
	}

	[TestMethod]
	public void ALetterWithOneUppercaseFormIsCapitalizedInTheInvariantCulture()
	{
		string acute = (char)0xE9 + "lan vital";

		Assert.AreEqual(0, DocTextRules.FindCapital(acute, DocTextRules.DefaultKeywords));
		Assert.AreEqual((char)0xC9 + "lan vital", DocTextRules.WriteCapital(acute, 0));
		Assert.AreEqual("I in the middle", DocTextRules.WriteCapital("i in the middle", 0));
	}

	[TestMethod]
	public void ALetterWithNoSingleUppercaseFormKeepsItsCase() =>
		Assert.IsNull(DocTextRules.FindCapital((char)0xDF + "harp", DocTextRules.DefaultKeywords));
}
