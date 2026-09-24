using System;
using System.IO;

namespace Nickelony.Stanza.Tests.Shared;

/// <summary>The dotnet host a child process runs under: the one that built the tests.</summary>
internal static class DotnetHost
{
	internal static string Executable { get; } =
		Resolve(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH"));

	/// <summary>The host the environment names if it exists, else the one on the path.</summary>
	internal static string Resolve(string? hostPath) =>
		hostPath is not null && File.Exists(hostPath) ? hostPath : "dotnet";
}
