using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nickelony.Stanza.Shared;

namespace Nickelony.Stanza.Format.Running;

/// <summary>Runs a child process, and copies each stream to a writer as it comes when one is given.</summary>
internal static class ProcessRunner
{
	/// <summary>Bounds the items <see cref="RunEach"/> runs at once, as each may start a host or a request.</summary>
	private const int MaxParallelItems = 8;

	private static readonly UTF8Encoding s_utf8 = new(encoderShouldEmitUTF8Identifier: false);

	/// <summary>Bounds the wait for the streams of an exited child, which a process it left running can hold.</summary>
	internal static TimeSpan DrainTimeout { get; } = TimeSpan.FromSeconds(10);

	/// <summary>Runs the program to its end, and echoes its streams as they come when an echo is given.</summary>
	/// <remarks>
	/// The <paramref name="drainTimeout"/> replaces <see cref="DrainTimeout"/>, and is a test seam.
	/// </remarks>
	public static ProcessResult Run(
		string fileName,
		IEnumerable<string> arguments,
		string workingDirectory,
		ProcessEcho? echo,
		CancellationToken cancellationToken,
		TimeSpan? drainTimeout = null
	)
	{
		// A run that was canceled before the child started never starts it.
		cancellationToken.ThrowIfCancellationRequested();

		ProcessStartInfo start = new(fileName)
		{
			WorkingDirectory = workingDirectory,
			RedirectStandardInput = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			StandardOutputEncoding = s_utf8,
			StandardErrorEncoding = s_utf8,
			UseShellExecute = false,
		};

		foreach (string argument in arguments)
			start.ArgumentList.Add(argument);

		using Process process = Start(start);

		try
		{
			// The child's input ends at once, so a child that reads it, such as a prompt, does not wait on the tool's own input.
			process.StandardInput.Close();

			// A canceled run stops the child too, so a pass does not go on writing files after the tool has ended.
			using CancellationTokenRegistration registration = cancellationToken.Register(() => Stop(process));

			// Both streams drain while the process runs, because a full pipe blocks it.
			StringBuilder outputText = new();
			StringBuilder errorText = new();

			Task output = CopyAsync(process.StandardOutput, echo?.Output, outputText);
			Task error = CopyAsync(process.StandardError, echo?.Error, errorText);

			// A drain that fails, as an echo to a closed console does, ends the run at once:
			// the child would block on the pipe that the failed drain leaves unread, and never exit.
			List<Task> open = [output, error];
			_ = WaitForEach(open, stop: process.WaitForExitAsync(CancellationToken.None));

			process.WaitForExit();

			// A stream ends only when every process that holds it has exited, so one the child left running ends the wait here.
			// What the child wrote is kept, and the drains that stay open end with the pipes.
			if (!WaitForEach(open, stop: Task.Delay(drainTimeout ?? DrainTimeout)))
			{
				foreach (Task drain in open)
					_ = drain.ContinueWith(static task => _ = task.Exception, TaskScheduler.Default);
			}

			cancellationToken.ThrowIfCancellationRequested();

			return new ProcessResult(process.ExitCode, Snapshot(outputText), Snapshot(errorText));
		}
		finally
		{
			// A child that outlives the run, as one does when the run throws, ends with its tree.
			if (!process.HasExited)
				Stop(process);
		}
	}

	/// <summary>Runs each item a few at a time, and returns the results in the items' order.</summary>
	/// <remarks>
	/// The earliest item's run failure is the one thrown, whichever process ended first.
	/// </remarks>
	public static ImmutableArray<TResult> RunEach<TItem, TResult>(
		IEnumerable<TItem> items,
		Func<TItem, TResult> run,
		CancellationToken cancellationToken
	)
	{
		try
		{
			(TResult Result, FormatRunException? Failure)[] outcomes =
			[
				.. items
					.AsParallel()
					.AsOrdered()
					.WithDegreeOfParallelism(Math.Min(Environment.ProcessorCount, MaxParallelItems))
					.WithCancellation(cancellationToken)
					.Select(item => RunCapturingFailure(item, run)),
			];

			foreach ((_, FormatRunException? failure) in outcomes)
			{
				if (failure is not null)
					throw failure;
			}

			return [.. outcomes.Select(static outcome => outcome.Result)];
		}
		catch (AggregateException exception) when (exception.InnerExceptions.Count > 0)
		{
			// A failure of another type than a run failure keeps its own type instead of the aggregate's.
			ExceptionDispatchInfo.Capture(exception.InnerExceptions[0]).Throw();
			throw;
		}
	}

	private static (TResult Result, FormatRunException? Failure) RunCapturingFailure<TItem, TResult>(TItem item, Func<TItem, TResult> run)
	{
		try
		{
			return (run(item), null);
		}
		catch (FormatRunException exception)
		{
			return (default!, exception);
		}
	}

	internal static Process Start(ProcessStartInfo start)
	{
		try
		{
			Process process = Process.Start(start) ?? throw new FormatRunException($"{start.FileName} did not start.");

			ChildJob.Adopt(process);

			return process;
		}
		catch (Win32Exception exception)
		{
			throw new FormatRunException($"{start.FileName} could not be started: {exception.Message}", exception);
		}
	}

	internal static void Stop(Process process)
	{
		try
		{
			process.Kill(entireProcessTree: true);
		}
		catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or AggregateException or NotSupportedException)
		{
			// The process has already exited, or a child of it could not be ended.
			// The handler runs inside Ctrl+C, so a throw here would end the tool before it exits with the canceled code.
		}
	}

	/// <summary>Removes each task as it completes, and says whether the list emptied before the stop did.</summary>
	/// <remarks>
	/// A task that failed throws at once, so a failed drain does not wait for the other.
	/// </remarks>
	private static bool WaitForEach(List<Task> open, Task stop)
	{
		while (open.Count > 0)
		{
			Task[] watched = [.. open, stop];
			Task done = Task.WhenAny(watched).GetAwaiter().GetResult();

			if (done == stop)
				return false;

			done.GetAwaiter().GetResult();
			_ = open.Remove(done);
		}

		return true;
	}

	private static string Snapshot(StringBuilder text)
	{
		lock (text)
			return text.ToString();
	}

	/// <summary>Reads the stream to its end, and echoes each chunk with its non-printing characters escaped.</summary>
	/// <remarks>
	/// A child echoes the names of files it reads, and a name may hold an escape sequence that a terminal obeys.
	/// </remarks>
	private static async Task CopyAsync(StreamReader reader, TextWriter? echo, StringBuilder text)
	{
		char[] buffer = new char[4096];
		string held = string.Empty;
		int read;

		while ((read = await reader.ReadAsync(buffer).ConfigureAwait(false)) > 0)
		{
			lock (text)
				_ = text.Append(buffer, 0, read);

			if (echo is null)
				continue;

			// A pair of surrogates split between two reads is echoed whole with the next one, not as two codes.
			int whole = char.IsHighSurrogate(buffer[read - 1]) ? read - 1 : read;

			WriteEscaped(echo, held + new string(buffer, 0, whole));
			held = new string(buffer, whole, read - whole);
		}

		if (echo is not null && held.Length > 0)
			WriteEscaped(echo, held);
	}

	private static void WriteEscaped(TextWriter echo, string chunk)
	{
		string shown = ToolHost.EscapeNonPrintingCharacters(chunk, keepLineLayout: true);

		lock (echo)
			echo.Write(shown);
	}
}
