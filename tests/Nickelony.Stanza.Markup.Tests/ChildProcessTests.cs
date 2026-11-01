using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Markup.Tests;

[TestClass]
public sealed class ChildProcessTests
{
	// The host that built the tests runs the tools, so a machine with a second SDK on the path cannot swap it.
	[TestMethod]
	public void TheHostTheEnvironmentNamesIsTheOneAChildProcessRuns()
	{
		using TempDirectory directory = new();
		string host = directory.Write("dotnet-host", string.Empty);

		Assert.AreEqual(host, DotnetHost.Resolve(host));
		Assert.AreEqual("dotnet", DotnetHost.Resolve(Path.Combine(directory.Root, "missing")));
		Assert.AreEqual("dotnet", DotnetHost.Resolve(null));
	}

	// A guard that runs git would pass on a machine without it if the test were skipped, so a missing program fails.
	[TestMethod]
	public void AMissingProgramFailsTheRunInsteadOfSkippingIt()
	{
		var start = new ProcessStartInfo("stanza-no-such-program")
		{
			RedirectStandardOutput = true,
		};

		_ = Assert.ThrowsExactly<AssertFailedException>(() =>
			ChildProcess.Run(start, "a missing program")
		);
	}

	// A process that leaves a child behind holds the pipes open, and a run that waits for them would outlast its test.
	[TestMethod]
	[DoNotParallelize]
	public async Task AChildThatOutlivesTheProcessAndHoldsItsOutputFailsTheRunInsteadOfWaiting()
	{
		var start = new ProcessStartInfo(OperatingSystem.IsWindows() ? "cmd.exe" : "sh")
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		};

		if (OperatingSystem.IsWindows())
		{
			start.ArgumentList.Add("/c");
			start.ArgumentList.Add("start /b ping -n 12 127.0.0.1");
		}
		else
		{
			start.ArgumentList.Add("-c");
			start.ArgumentList.Add("sleep 10 &");
		}

		var clock = Stopwatch.StartNew();

		AssertFailedException failure = await Assert.ThrowsExactlyAsync<AssertFailedException>(() =>
			ChildProcess.RunAsync(
				start,
				"a process that leaves a child",
				drainTimeout: TimeSpan.FromSeconds(1)
			)
		);

		Assert.Contains("still holds", failure.Message);
		Assert.IsLessThan(TimeSpan.FromSeconds(8), clock.Elapsed);
	}
}
