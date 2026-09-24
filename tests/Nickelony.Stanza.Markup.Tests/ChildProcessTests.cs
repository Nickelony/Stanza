using System.Diagnostics;
using System.IO;
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
}
