using System;
using System.Threading;

namespace Nickelony.Stanza.Analyzers.Tests.Harness;

/// <summary>The time budget of the running test, which cancels a run that hangs.</summary>
internal static class HarnessBudget
{
	// A test whose runs hang is canceled after this long, so it fails on its own instead of holding the CI job.
	private static readonly TimeSpan s_limit = TimeSpan.FromMinutes(2);

	private static readonly AsyncLocal<CancellationTokenSource?> s_source = new();

	/// <summary>The token of the current test, which cancels a run that hangs.</summary>
	public static CancellationToken Token => s_source.Value?.Token ?? CancellationToken.None;

	/// <summary>Starts a test's budget, which its own token can also end.</summary>
	public static void Begin(CancellationToken cancellationToken)
	{
		s_source.Value = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		s_source.Value.CancelAfter(s_limit);
	}

	/// <summary>Gives the test a whole budget again, per soak seed or matrix state.</summary>
	/// <remarks>
	/// A hung state still fails, and the TestTimeout of test.runsettings bounds the whole test.
	/// </remarks>
	public static void Restart() => s_source.Value?.CancelAfter(s_limit);

	public static void End()
	{
		s_source.Value?.Dispose();
		s_source.Value = null;
	}
}
