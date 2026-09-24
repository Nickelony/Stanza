using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Core.DocComments;
using Nickelony.Stanza.Markup.Core.Formatting;
using Nickelony.Stanza.Markup.Core.Xml;

namespace Nickelony.Stanza.Markup.Core.Tests;

[TestClass]
public sealed class MarkupRecursionTests
{
	private const int PastTheCap = DocWalk.MaxElementDepth + 8;
	private const int WithinTheCap = 5;
	private const int AtTheCap = DocWalk.MaxElementDepth;
	private const int OneLevelMore = DocWalk.MaxElementDepth + 1;

	[TestMethod]
	public void TheListOrderStopsAtTheDepthCap()
	{
		var cleanup = DocCleanupOptions.From(
			MarkupRuleSet.From([MarkupDiagnostics.OrderItemsOfDocumentationListId])
		);

		DocNode[] misordered = [Element("description"), Element("term")];

		Assert.AreEqual(
			"term",
			GetFirstChildName(Apply(Chain("item", WithinTheCap, misordered), cleanup))
		);

		Assert.AreEqual(
			"description",
			GetFirstChildName(Apply(Chain("item", PastTheCap, misordered), cleanup))
		);
	}

	[TestMethod]
	public void TheElementTextStopsAtTheDepthCap()
	{
		string within = MarkupReprint.GetElementText(Parse(WithinTheCap), depth: 0);
		string past = MarkupReprint.GetElementText(Parse(PastTheCap), depth: 0);

		Assert.AreEqual(WithinTheCap, CountOccurrences(within, "<e>"));
		Assert.AreEqual(DocWalk.MaxElementDepth, CountOccurrences(past, "<e>"));
	}

	// Each cap admits a nest of exactly MaxElementDepth levels and refuses one level more.
	[TestMethod]
	public void EachCapHoldsAtItsBound()
	{
		Assert.IsNotNull(MarkupReprint.BuildElement(Parse(AtTheCap), depth: 0));
		Assert.IsNull(MarkupReprint.BuildElement(Parse(OneLevelMore), depth: 0));

		Assert.IsFalse(DocWalk.HoldsAList(Chain("e", AtTheCap, new DocText("x")), depth: 0));
		Assert.IsTrue(DocWalk.HoldsAList(Chain("e", OneLevelMore, new DocText("x")), depth: 0));

		var cleanup = DocCleanupOptions.From(
			MarkupRuleSet.From([MarkupDiagnostics.EndDocumentationTextWithPeriodId])
		);

		Assert.AreEqual(
			"x.",
			GetInnermostText(Apply(Chain("e", AtTheCap, new DocText("x")), cleanup))
		);

		Assert.AreEqual(
			"x",
			GetInnermostText(Apply(Chain("e", OneLevelMore, new DocText("x")), cleanup))
		);
	}

	private static MarkupElement Parse(int levels)
	{
		string source =
			string.Concat(Enumerable.Repeat("<e>", levels))
			+ "x"
			+ string.Concat(Enumerable.Repeat("</e>", levels));

		return MarkupParser.Parse(MarkupSourceText.Build(source), maxDocumentCharacters: 0).Root!;
	}

	private static int CountOccurrences(string text, string value) =>
		text.Split([value], System.StringSplitOptions.None).Length - 1;

	private static List<DocNode> Apply(DocElement root, DocCleanupOptions cleanup) =>
		DocCleanup.Apply([root], cleanup);

	private static DocElement Chain(string name, int levels, params DocNode[] innermost)
	{
		DocElement element = Element(name, innermost);

		for (int level = 1; level < levels; level++)
			element = Element(name, element);

		return name == "item" ? Element("list", element) : element;
	}

	private static DocElement Element(string name, params DocNode[] body)
	{
		string openTag = $"<{name}>";
		string endTag = $"</{name}>";

		string phrase =
			openTag
			+ string.Concat(
				body.Select(static node =>
					node is DocElement element ? element.Phrase : ((DocText)node).Text
				)
			)
			+ endTag;

		return new DocElement(name, openTag, closeTag: endTag, isEmpty: false, body, phrase);
	}

	private static string GetInnermostText(List<DocNode> body)
	{
		DocNode node = body[0];

		while (node is DocElement element)
			node = element.Body[0];

		return ((DocText)node).Text;
	}

	private static string GetFirstChildName(List<DocNode> body)
	{
		var element = (DocElement)body[0];

		while (element.Body[0] is DocElement child && child.Name is not ("term" or "description"))
			element = child;

		return ((DocElement)element.Body[0]).Name;
	}
}
