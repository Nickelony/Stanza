using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Markup.Tests;

[TestClass]
public sealed class SoakRunTests
{
	[TestMethod]
	public void TheLimitOfASoakGrowsWithItsSeedCount()
	{
		Assert.IsGreaterThan(SoakRun.LimitFor(30), SoakRun.LimitFor(300));
		Assert.IsGreaterThan(SoakRun.LimitFor(1), SoakRun.LimitFor(2));
		Assert.IsGreaterThan(TimeSpan.FromMinutes(5), SoakRun.LimitFor(200));
	}

	[TestMethod]
	public async Task ASoakThatOutlastsItsLimitFailsTheTestInsteadOfHoldingIt()
	{
		AssertFailedException failure = await Assert.ThrowsExactlyAsync<AssertFailedException>(() =>
			SoakRun.RunAsync(TimeSpan.FromMilliseconds(100), () => Task.Delay(Timeout.Infinite))
		);

		Assert.Contains("did not end within", failure.Message);
	}

	[TestMethod]
	public async Task ASoakThatEndsInTimeKeepsItsOwnFailure()
	{
		await SoakRun.RunAsync(TimeSpan.FromMinutes(1), () => Task.CompletedTask);

		AssertFailedException failure = await Assert.ThrowsExactlyAsync<AssertFailedException>(() =>
			SoakRun.RunAsync(
				TimeSpan.FromMinutes(1),
				() => throw new AssertFailedException("seed 1000: a fix changed a token.")
			)
		);

		Assert.Contains("seed 1000", failure.Message);
	}

	[TestMethod]
	[DataRow(null, 1000)]
	[DataRow("", 1000)]
	[DataRow("0", 1000)]
	[DataRow("400", 1400)]
	public void TheFirstSeedMovesByTheOffsetAndStaysTheBaseWithoutOne(
		string? offset,
		int expected
	) => Assert.AreEqual(expected, SoakRun.ResolveFirstSeed(1000, offset));

	[TestMethod]
	[DataRow("-1")]
	[DataRow("soon")]
	public void AnOffsetThatIsNoWholeNumberFailsTheRunInsteadOfKeepingTheBase(string offset)
	{
		AssertFailedException failure = Assert.ThrowsExactly<AssertFailedException>(() =>
			SoakRun.ResolveFirstSeed(1000, offset)
		);

		Assert.Contains(SoakRun.OffsetVariable, failure.Message);
	}

	[TestMethod]
	public void ASeedThatThrowsSomethingElseIsNamedByItsReport()
	{
		InvalidOperationException crash = new("broke\nat depth");

		Assert.AreEqual(
			"seed 1234: InvalidOperationException: broke",
			SoakRun.DescribeCrash(1234, crash)
		);
	}

	[TestMethod]
	public void AThrownCrashNamesTheFrameThatThrewIt()
	{
		InvalidOperationException crash;

		try
		{
			throw new InvalidOperationException("broke");
		}
		catch (InvalidOperationException caught)
		{
			crash = caught;
		}

		string report = SoakRun.DescribeCrash(1234, crash);

		Assert.StartsWith("seed 1234: InvalidOperationException: broke at ", report);
		Assert.Contains(nameof(AThrownCrashNamesTheFrameThatThrewIt), report);
	}

	[TestMethod]
	public void EachSoakTestStatesItsTimeoutInsteadOfTakingTheDefaultOfFiveMinutes() =>
		Assert.IsEmpty(SoakRun.FindUntimed(typeof(SoakRunTests).Assembly));
}
