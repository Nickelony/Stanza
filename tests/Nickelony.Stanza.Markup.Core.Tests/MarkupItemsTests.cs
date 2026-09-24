using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Core.Xml;

namespace Nickelony.Stanza.Markup.Core.Tests;

[TestClass]
public sealed class MarkupItemsTests
{
	[TestMethod]
	public void AChildAddedAfterTheSealIsRefused()
	{
		MarkupElement element = new("root", 0);

		element.Seal();

		_ = Assert.ThrowsExactly<InvalidOperationException>(() =>
			element.Add(new MarkupElement("a", 0))
		);
	}

	[TestMethod]
	public void APropertySetAfterTheSealIsRefused()
	{
		MarkupElement element = new("root", 0);

		element.Seal();

		_ = Assert.ThrowsExactly<InvalidOperationException>(() => element.EndLineIndex = 3);
		_ = Assert.ThrowsExactly<InvalidOperationException>(() => element.HasCharacterData = true);
		Assert.AreEqual(0, element.EndLineIndex);
	}

	[TestMethod]
	public void TheParserRecordsTheTagTextAndTheBodyOfAnElement()
	{
		const string document =
			"<member name=\"X\">\n  <summary>Text</summary>\n  <see cref=\"A\"/>\n</member>";

		MarkupElement member = ParseRoot(document);

		Assert.AreEqual("<member name=\"X\">", member.OpenTag);
		Assert.AreEqual("</member>", member.CloseTag);
		Assert.IsFalse(member.IsEmpty);
		Assert.AreEqual(document.IndexOf('>', StringComparison.Ordinal) + 1, member.OpenTagEnd);

		Assert.AreEqual(
			document.IndexOf("</member>", StringComparison.Ordinal),
			member.CloseTagStart
		);

		MarkupText gap = Assert.IsInstanceOfType<MarkupText>(member.Body[0]);
		MarkupElement summary = Assert.IsInstanceOfType<MarkupElement>(member.Body[1]);
		MarkupText between = Assert.IsInstanceOfType<MarkupText>(member.Body[2]);
		MarkupElement see = Assert.IsInstanceOfType<MarkupElement>(member.Body[3]);

		// Whitespace between two children stays in the body,
		// so a word separator is told apart from the layout of a block.
		Assert.HasCount(5, member.Body);
		Assert.AreEqual("\n  ", gap.Text);
		Assert.AreEqual("\n  ", between.Text);

		Assert.AreEqual("<summary>", summary.OpenTag);
		Assert.AreEqual("</summary>", summary.CloseTag);

		Assert.AreEqual(
			document.IndexOf("<summary>", StringComparison.Ordinal) + "<summary>".Length,
			summary.OpenTagEnd
		);

		Assert.AreEqual(
			document.IndexOf("</summary>", StringComparison.Ordinal),
			summary.CloseTagStart
		);

		MarkupText text = Assert.IsInstanceOfType<MarkupText>(summary.Body[0]);

		Assert.AreEqual("Text", text.Text);

		Assert.IsTrue(see.IsEmpty);
		Assert.AreEqual("<see cref=\"A\"/>", see.OpenTag);
		Assert.IsNull(see.CloseTag);
		Assert.AreEqual(-1, see.CloseTagStart);
	}

	[TestMethod]
	public void ATagWrittenAcrossTwoLinesIsCollapsed()
	{
		MarkupElement root = ParseRoot("<a>\n  <see\n    cref=\"A\" />\n</a>");

		MarkupElement see = Assert.IsInstanceOfType<MarkupElement>(root.Body[1]);

		Assert.AreEqual("<see cref=\"A\" />", see.OpenTag);
	}

	[TestMethod]
	public void AReferenceInTheTextIsRecordedExpanded()
	{
		MarkupElement root = ParseRoot("<a>one &amp; two</a>\n");

		MarkupText text = Assert.IsInstanceOfType<MarkupText>(root.Body[0]);

		Assert.AreEqual("one & two", text.Text);
	}

	[TestMethod]
	[DataRow("Grid.Background", true)]
	[DataRow("x:Grid.Background", true)]
	[DataRow("Grid", false)]
	[DataRow("x.Grid:Background", false)]
	public void OnlyADotAfterThePrefixMakesAPropertyElement(string name, bool isPropertyElement) =>
		Assert.AreEqual(isPropertyElement, new MarkupElement(name, 0).IsPropertyElement);

	[TestMethod]
	public void TheTextDirectlyInsideAnElementIsItsCharacterData()
	{
		Assert.IsTrue(ParseRoot("<a>one</a>").HasCharacterData);
		Assert.IsFalse(ParseRoot("<a>\n  <b/>\n</a>").HasCharacterData);
		Assert.IsFalse(ParseRoot("<a><b>one</b></a>").HasCharacterData);
	}

	[TestMethod]
	public void ACDataSectionHoldingOnlySpaceIsCharacterData()
	{
		MarkupElement root = ParseRoot("<a><![CDATA[ ]]></a>");
		Assert.IsTrue(root.HasCharacterData);
	}

	[TestMethod]
	public void ASpacePreservingAttributeReachesTheElementsBelowIt()
	{
		MarkupElement root = ParseRoot("<a xml:space=\"preserve\"><b><c/></b></a>");

		Assert.IsTrue(root.IsSpacePreserved);
		Assert.IsTrue(root.ChildElements[0].IsSpacePreserved);
		Assert.IsTrue(root.ChildElements[0].ChildElements[0].IsSpacePreserved);
	}

	[TestMethod]
	public void ADefaultAttributeEndsThePreservationBelowItsElement()
	{
		MarkupElement root = ParseRoot(
			"<a xml:space=\"preserve\"><b xml:space=\"default\"><c/></b></a>"
		);

		Assert.IsTrue(root.IsSpacePreserved);
		Assert.IsFalse(root.ChildElements[0].IsSpacePreserved);
		Assert.IsFalse(root.ChildElements[0].ChildElements[0].IsSpacePreserved);
	}

	private static MarkupElement ParseRoot(string document)
	{
		var sourceText = MarkupSourceText.Build(document);

		(MarkupElement? root, _) = MarkupParser.Parse(
			sourceText,
			MarkupLimits.DefaultMaxDocumentCharacters
		);

		return root ?? throw new AssertFailedException("The document parsed to no root element.");
	}

	[TestMethod]
	public void AUnicodeSeparatorAtTheEndOfTheDocumentIsNotALineBreak()
	{
		foreach (string separator in new[] { "\u0085", "\u2028", "\u2029" })
		{
			var index = MarkupSourceText.Build($"a{separator}");

			Assert.AreEqual(1, index.LineCount);
			Assert.AreEqual(2, index.GetLine(0).End);
			Assert.IsFalse(index.GetLine(0).HasLineBreak);
		}
	}
}
