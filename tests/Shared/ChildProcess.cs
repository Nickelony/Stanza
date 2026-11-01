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
	private static readonly TimeSpan s_drainTimeout = TimeSpan.FromSeconds(30);

	/// <summary>Runs the process, and fails the test if the program is missing or hangs.</summary>
	internal static async Task<RunResult> RunAsync(
		ProcessStartInfo start,
		string description,
		TimeSpan? exitTimeout = null,
		TimeSpan? drainTimeout = null
	)
	{
		using Process process = Start(start);

		TimeSpan limit = exitTimeout ?? s_exitTimeout;

		// Both streams drain while the process runs, because a full pipe blocks it.
		// The exit wait is bounded because a drain returns only once the process closes its stream,
		// so a drain cannot time out a hung process.
		Task<string> output = process.StandardOutput.ReadToEndAsync();
		Task<string> error = process.StandardError.ReadToEndAsync();

		bool exited = false;
		bool closed;

		try
		{
			exited = process.WaitForExit(limit);

			if (!exited)
				process.Kill(entireProcessTree: true);
		}
		finally
		{
			// A drain also waits for a child the process started and left running, which holds the pipes after the exit.
			// So the drains are awaited for a while only, and a run that outlasts it fails and does not hold its test.
			Task drained = Task.WhenAll(output, error);

			closed =
				await Task.WhenAny(drained, Task.Delay(drainTimeout ?? s_drainTimeout))
					.ConfigureAwait(false) == drained;

			// The reads that stay open end with the pipes, and nothing reads their result.
			if (!closed)
				_ = drained.ContinueWith(static task => _ = task.Exception, TaskScheduler.Default);
		}

		if (!exited)
		{
			throw new AssertFailedException(
				$"The process did not exit within {limit}: {description}"
			);
		}

		if (!closed)
		{
			throw new AssertFailedException(
				$"The process exited, but a process it started still holds its output: {description}"
			);
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
		TimeSpan? exitTimeout = null,
		TimeSpan? drainTimeout = null
	) => RunAsync(start, description, exitTimeout, drainTimeout).GetAwaiter().GetResult();

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
