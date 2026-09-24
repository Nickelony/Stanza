using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Core.Xml;

namespace Nickelony.Stanza.Markup.Core.Tests;

[TestClass]
public sealed class MarkupElementTests
{
	[TestMethod]
	[DataRow("Grid.RowDefinitions", true)]
	[DataRow("x:Grid.Row", true)]
	[DataRow("x:Grid", false)]
	[DataRow("Grid", false)]
	[DataRow("a.b:Grid", false)]
	public void AnElementIsAPropertyElementWhenTheNameAfterItsPrefixHoldsADot(
		string name,
		bool expected
	)
	{
		Assert.AreEqual(expected, new MarkupElement(name, startLineIndex: 0).IsPropertyElement);
	}

	[TestMethod]
	public void AnElementStartsAndEndsOnItsFirstLineUntilTheParserSaysOtherwise()
	{
		var element = new MarkupElement("a", startLineIndex: 4);

		Assert.AreEqual(4, element.StartLineIndex);
		Assert.AreEqual(4, element.EndLineIndex);
		Assert.AreEqual(-1, element.CloseTagStart);
	}

	[TestMethod]
	public void SealingSortsTheChildrenIntoTheBodyTheItemsAndTheChildElements()
	{
		var outer = new MarkupElement("outer", startLineIndex: 0);
		var inner = new MarkupElement("inner", startLineIndex: 1);
		var text = new MarkupText("words");

		var comment = new MarkupComment(
			startLineIndex: 2,
			endLineIndex: 2,
			start: 10,
			closeStart: 14,
			isSpacePreserved: false,
			isInPropertyElement: false
		);

		var instruction = new MarkupProcessingInstruction(startLineIndex: 3, endLineIndex: 3);

		outer.Add(text);
		outer.Add(inner);
		outer.Add(comment);
		outer.Add(instruction);

		outer.Seal();

		// The body holds the text and the elements, and the items the elements, comments and instructions.
		Assert.AreSequenceEqual([text, inner], new List<IMarkupBodyNode>(outer.Body));
		Assert.AreSequenceEqual([inner, comment, instruction], new List<IMarkupItem>(outer.Items));
		Assert.AreSequenceEqual([inner], new List<MarkupElement>(outer.ChildElements));

		Assert.IsTrue(outer.HasElementChildren);
	}

	[TestMethod]
	public void ASealedElementWithNoChildHoldsNothing()
	{
		var element = new MarkupElement("a", startLineIndex: 0);

		element.Seal();

		Assert.IsEmpty(element.Body);
		Assert.IsEmpty(element.Items);
		Assert.IsEmpty(element.ChildElements);
		Assert.IsFalse(element.HasElementChildren);
	}

	[TestMethod]
	public void ASealedListRefusesAWriteThroughACast()
	{
		var outer = new MarkupElement("outer", startLineIndex: 0);
		var inner = new MarkupElement("inner", startLineIndex: 1);

		outer.Add(inner);
		outer.Seal();

		var other = new MarkupElement("other", startLineIndex: 2);

		Assert.ThrowsExactly<NotSupportedException>(() =>
			((IList<IMarkupBodyNode>)outer.Body)[0] = other
		);

		Assert.ThrowsExactly<NotSupportedException>(() =>
			((IList<IMarkupItem>)outer.Items)[0] = other
		);

		Assert.ThrowsExactly<NotSupportedException>(() =>
			((IList<MarkupElement>)outer.ChildElements)[0] = other
		);
	}

	[TestMethod]
	public void AnElementHoldsNoChildListUntilItIsSealed()
	{
		var element = new MarkupElement("a", startLineIndex: 0);

		element.Add(new MarkupElement("b", startLineIndex: 1));

		Assert.IsEmpty(element.ChildElements);
		Assert.IsFalse(element.HasElementChildren);
	}

	[TestMethod]
	public void ASealedElementHandsOutListsThatACastCannotGrow()
	{
		var element = new MarkupElement("a", startLineIndex: 0);

		element.Add(new MarkupElement("b", startLineIndex: 1));
		element.Seal();

		// A list handed out as a List<T> would let a cast add to a tree that other walks have cached.
		Assert.ThrowsExactly<NotSupportedException>(() =>
			((IList<MarkupElement>)element.ChildElements).Add(new MarkupElement("c", 2))
		);

		Assert.ThrowsExactly<NotSupportedException>(() =>
			((IList<IMarkupItem>)element.Items).Add(new MarkupProcessingInstruction(2, 2))
		);

		Assert.ThrowsExactly<NotSupportedException>(() =>
			((IList<IMarkupBodyNode>)element.Body).Add(new MarkupText("x"))
		);
	}

	[TestMethod]
	public void ASealedElementRefusesEachLaterChange()
	{
		var element = new MarkupElement("a", startLineIndex: 0);

		element.Seal();

		Assert.ThrowsExactly<InvalidOperationException>(() => element.Add(new MarkupText("x")));
		Assert.ThrowsExactly<InvalidOperationException>(() => element.EndLineIndex = 3);
		Assert.ThrowsExactly<InvalidOperationException>(() => element.OpenTagStart = 1);
		Assert.ThrowsExactly<InvalidOperationException>(() => element.OpenTagEnd = 1);
		Assert.ThrowsExactly<InvalidOperationException>(() => element.CloseTagStart = 1);
		Assert.ThrowsExactly<InvalidOperationException>(() => element.CloseTagEnd = 1);
		Assert.ThrowsExactly<InvalidOperationException>(() => element.IsEmpty = true);
		Assert.ThrowsExactly<InvalidOperationException>(() => element.HasCharacterData = true);
		Assert.ThrowsExactly<InvalidOperationException>(() => element.IsSpacePreserved = true);
		Assert.ThrowsExactly<InvalidOperationException>(() => element.SourceText = null);
	}

	[TestMethod]
	public void TheOpenTagIsReadCollapsedFromTheSourceText()
	{
		var source = MarkupSourceText.Build("<a   b=\"1  2\"\n   c='3' >x</a>");

		var element = new MarkupElement("a", startLineIndex: 0)
		{
			SourceText = source,
			OpenTagStart = 0,
			OpenTagEnd = 24,
		};

		// Whitespace between the attributes collapses, and the spaces inside a quoted value stay.
		Assert.AreEqual("<a b=\"1  2\" c='3'>", element.OpenTag);
	}

	[TestMethod]
	public void AnElementWithNoSourceTextHasAnEmptyOpenTag() =>
		Assert.AreEqual(string.Empty, new MarkupElement("a", startLineIndex: 0).OpenTag);

	[TestMethod]
	public void TheEndTagIsReadCollapsedAndAnElementWithNoneHasNull()
	{
		var source = MarkupSourceText.Build("<a>x</  a >");

		var closed = new MarkupElement("a", startLineIndex: 0)
		{
			SourceText = source,
			CloseTagStart = 4,
			CloseTagEnd = 11,
		};

		var selfClosed = new MarkupElement("a", startLineIndex: 0) { SourceText = source };

		Assert.AreEqual("</ a>", closed.CloseTag);
		Assert.IsNull(selfClosed.CloseTag);
	}
}
