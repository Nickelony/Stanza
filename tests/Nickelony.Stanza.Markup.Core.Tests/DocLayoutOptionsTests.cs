using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Core.DocComments;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Markup.Core.Tests;

[TestClass]
public sealed class DocLayoutOptionsTests
{
	private static readonly DocLayoutOptions s_layout = MarkupFormatFixtures.ReprintLayout(
		contentWidth: 80
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
