using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Nickelony.Stanza.Tests.Shared;

/// <summary>Runs a child dotnet build or pack that leaves no build server behind.</summary>
internal static class ChildBuild
{
	// A slow machine gets three minutes, so a build that hangs fails with its own message before the runner's five end the test.
	private static readonly TimeSpan s_exitTimeout = TimeSpan.FromMinutes(3);

	// A node or a VBCSCompiler that outlives its build keeps the pipes of the run open and holds the temp output a test deletes.
	internal static IReadOnlyDictionary<string, string> ServerSwitches { get; } =
		new Dictionary<string, string>
		{
			["MSBUILDDISABLENODEREUSE"] = "1",
			["UseSharedCompilation"] = "false",
		};

	/// <summary>Runs dotnet, and fails the test if it outlasts three minutes.</summary>
	internal static async Task<RunResult> RunAsync(
		string workingDirectory,
		string description,
		IEnumerable<string> arguments,
		IReadOnlyDictionary<string, string>? environment = null
	)
	{
		ProcessStartInfo start = new(DotnetHost.Executable)
		{
			WorkingDirectory = workingDirectory,
			RedirectStandardError = true,
			RedirectStandardOutput = true,
			UseShellExecute = false,
		};

		foreach (string argument in arguments)
			start.ArgumentList.Add(argument);

		start.ArgumentList.Add("--disable-build-servers");

		foreach ((string name, string value) in ServerSwitches)
			start.Environment[name] = value;

		foreach ((string name, string value) in environment ?? new Dictionary<string, string>())
			start.Environment[name] = value;

		start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

		return await ChildProcess.RunAsync(start, description, s_exitTimeout);
	}
}
