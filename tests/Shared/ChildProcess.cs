using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Tests.Shared;

/// <summary>Runs a child process with both streams drained and a bound on its run time.</summary>
internal static class ChildProcess
{
	private static readonly TimeSpan s_exitTimeout = TimeSpan.FromSeconds(30);

	/// <summary>Runs the process, and fails the test if the program is missing or hangs.</summary>
	internal static async Task<RunResult> RunAsync(
		ProcessStartInfo start,
		string description,
		TimeSpan? exitTimeout = null
	)
	{
		using Process process = Start(start);

		TimeSpan limit = exitTimeout ?? s_exitTimeout;

		// Both streams drain while the process runs, because a full pipe blocks it.
		// The exit wait is bounded because a drain returns only once the process closes its stream,
		// so a drain cannot time out a hung process.
		Task<string> output = process.StandardOutput.ReadToEndAsync();
		Task<string> error = process.StandardError.ReadToEndAsync();

		try
		{
			if (!process.WaitForExit(limit))
			{
				process.Kill(entireProcessTree: true);

				throw new AssertFailedException(
					$"The process did not exit within {limit}: {description}"
				);
			}
		}
		finally
		{
			// Both drains are awaited before control leaves,
			// so a killed run does not leave a read on a closed pipe behind.
			_ = await Task.WhenAll(output, error).ConfigureAwait(false);
		}

		return new(
			process.ExitCode,
			await output.ConfigureAwait(false),
			await error.ConfigureAwait(false)
		);
	}

	/// <summary>Runs the process to its end on the calling thread.</summary>
	internal static RunResult Run(
		ProcessStartInfo start,
		string description,
		TimeSpan? exitTimeout = null
	) => RunAsync(start, description, exitTimeout).GetAwaiter().GetResult();

	private static Process Start(ProcessStartInfo start)
	{
		Process? process = null;
		bool found = true;

		try
		{
			process = Process.Start(start);
		}
		catch (Win32Exception)
		{
			found = false;
		}

		// A skipped run would let a guard that needs the program pass on a machine without it.
		// MSTEST0058 expects the assertion outside the catch block.
		if (!found)
		{
			throw new AssertFailedException(
				$"'{start.FileName}' is not on the path, so this process test cannot run."
			);
		}

		return process ?? throw new AssertFailedException("The process did not start.");
	}
}

internal sealed record RunResult(int ExitCode, string Output, string Error);
