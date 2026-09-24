using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Core.DocComments;

namespace Nickelony.Stanza.Markup.Core.Tests;

[TestClass]
public sealed class DocLayoutOptionsTests
{
	private static readonly DocLayoutOptions s_layout = new(
		ContentWidth: 80,
		TabColumns: 4,
		MinTailWords: 2,
		MinLineWords: 2,
		MaxOverflowColumns: 4,
		IndentSize: 0,
		IndentText: true,
		Expansion: DocExpansion.None
	);

	[TestMethod]
	public void AWidthOfZeroOrLessLeavesTheLayoutAsItIs()
	{
		Assert.AreEqual(s_layout, s_layout.NarrowBy(0));
		Assert.AreEqual(s_layout, s_layout.NarrowBy(-3));
	}

	[TestMethod]
	public void AReductionNarrowsOnlyTheContentWidth()
	{
		DocLayoutOptions reduced = s_layout.NarrowBy(10);

		Assert.AreEqual(70, reduced.ContentWidth);
		Assert.AreEqual(s_layout with { ContentWidth = 70 }, reduced);
	}
}
