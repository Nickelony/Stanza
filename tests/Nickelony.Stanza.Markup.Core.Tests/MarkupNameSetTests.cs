using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Markup.Core.Tests;

[TestClass]
public sealed class MarkupNameSetTests
{
	[TestMethod]
	public void TwoSetsOfTheSameNamesAreEqualAndHashAlikeWhateverTheOrder()
	{
		var first = new MarkupNameSet("see", "para", "c");
		var second = new MarkupNameSet("c", "see", "para");

		Assert.AreEqual(first, second);
		Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
	}
}
