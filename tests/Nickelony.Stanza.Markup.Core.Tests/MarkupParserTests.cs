using System.Collections.Generic;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Core.Xml;

namespace Nickelony.Stanza.Markup.Core.Tests;

[TestClass]
public sealed class MarkupParserTests
{
	[TestMethod]
	public void EachElementIsGivenTheTextItWasReadFromWhenItOpens()
	{
		var source = MarkupSourceText.Build(
			"<r>\n  <a>1</a>\n  <b/>\n  <c>\n    <d>2</d>\n  </c>\n</r>\n"
		);

		MarkupElement root = MarkupParser.Parse(source, maxDocumentCharacters: 0).Root!;

		List<MarkupElement> elements = [];
		Collect(root, elements);

		Assert.HasCount(5, elements);

		foreach (MarkupElement element in elements)
			Assert.AreSame(source, element.SourceText, $"<{element.Name}> has another text.");
	}

	// The reprint reads the end tag of an element through its source text, so one never stands without the other.
	[TestMethod]
	public void AnElementWithAnEndTagHasItsSourceTextAndASelfClosedOneHasNoEndTag()
	{
		var source = MarkupSourceText.Build("<r>\n  <a>1</a>\n  <b/>\n</r>\n");

		MarkupElement root = MarkupParser.Parse(source, maxDocumentCharacters: 0).Root!;

		MarkupElement closed = root.ChildElements[0];
		MarkupElement selfClosed = root.ChildElements[1];

		Assert.IsGreaterThan(closed.OpenTagEnd, closed.CloseTagStart);
		Assert.IsNotNull(closed.SourceText);
		Assert.AreEqual("</a>", closed.CloseTag);

		Assert.AreEqual(-1, selfClosed.CloseTagStart);
		Assert.IsNull(selfClosed.CloseTag);
	}

	[TestMethod]
	public void ADocumentAtTheElementCapIsParsedAndOneElementMoreIsRefused()
	{
		var atCap = MarkupSourceText.Build("<r><a/><a/></r>");
		var pastCap = MarkupSourceText.Build("<r><a/><a/><a/></r>");

		Assert.IsNotNull(MarkupParser.Parse(atCap, maxDocumentCharacters: 0, maxElements: 3).Root);

		XmlException refused = Assert.ThrowsExactly<XmlException>(() =>
			MarkupParser.Parse(pastCap, maxDocumentCharacters: 0, maxElements: 3)
		);

		Assert.Contains("more than 3 elements", refused.Message);
	}

	[TestMethod]
	public void TheElementCapRefusalNamesTheLineOfTheElementThatPassedIt()
	{
		var pastCap = MarkupSourceText.Build("<r>\n  <a/>\n  <a/>\n  <a/>\n</r>\n");

		XmlException refused = Assert.ThrowsExactly<XmlException>(() =>
			MarkupParser.Parse(pastCap, maxDocumentCharacters: 0, maxElements: 3)
		);

		// The element that passes the cap is the third `a`, on the fourth line.
		Assert.AreEqual(4, refused.LineNumber);
		Assert.AreEqual(4, MarkupFailure.From(refused).Line);
	}

	private static void Collect(MarkupElement element, List<MarkupElement> elements)
	{
		elements.Add(element);

		foreach (MarkupElement child in element.ChildElements)
			Collect(child, elements);
	}
}
