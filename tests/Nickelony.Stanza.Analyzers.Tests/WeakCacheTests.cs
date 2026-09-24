using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class WeakCacheTests
{
	[TestMethod]
	public void AKeyIsComputedOnceAndReadBackAfterwards()
	{
		WeakCache<object, int> cache = new();
		object key = new();
		int computations = 0;

		int first = cache.GetOrCompute(key, state: 7, (_, state) => ++computations + state);
		int second = cache.GetOrCompute(key, state: 7, (_, state) => ++computations + state);

		Assert.AreEqual(8, first);
		Assert.AreEqual(8, second);
		Assert.AreEqual(1, computations);
	}

	[TestMethod]
	public void ANullValueIsCachedLikeAnyOther()
	{
		WeakCache<object, string?> cache = new();
		object key = new();
		int computations = 0;

		string? Compute(object _)
		{
			computations++;
			return null;
		}

		cache.GetOrCompute(key, Compute);
		cache.GetOrCompute(key, Compute);

		Assert.AreEqual(1, computations);
	}
}
