using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Core.DocComments;

namespace Nickelony.Stanza.Markup.Core.Tests;

[TestClass]
public sealed class DocNameSetTests
{
	[TestMethod]
	public void TwoSetsOfTheSameNamesAreEqualAndHashAlikeWhateverTheOrder()
	{
		var first = new DocNameSet("see", "para", "c");
		var second = new DocNameSet("c", "see", "para");

		Assert.AreEqual(first, second);
		Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
	}

	[TestMethod]
	public void SetsOfOneSizeWithDifferentNamesSpreadOverManyHashes()
	{
		// One pair of sets may collide by chance, but a hash of the size alone gives every set the same hash.
		int hashes = Enumerable
			.Range(0, 64)
			.Select(index => new DocNameSet($"name{index}").GetHashCode())
			.Distinct()
			.Count();

		Assert.IsGreaterThan(1, hashes);
	}
}
