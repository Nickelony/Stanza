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
	/// <summary>Bounds the processes <see cref="RunEach"/> runs at once, as each one starts a .NET host.</summary>
	private const int MaxParallelProcesses = 8;

	private static readonly UTF8Encoding s_utf8 = new(encoderShouldEmitUTF8Identifier: false);

	public static ProcessResult Run(
		string fileName,
		IEnumerable<string> arguments,
		string workingDirectory,
		ProcessEcho? echo,
		CancellationToken cancellationToken
	)
	{
		// A run that was canceled before the child started never starts it.
		cancellationToken.ThrowIfCancellationRequested();

		ProcessStartInfo start = new(fileName)
		{
			WorkingDirectory = workingDirectory,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			StandardOutputEncoding = s_utf8,
			StandardErrorEncoding = s_utf8,
			UseShellExecute = false,
		};

		foreach (string argument in arguments)
			start.ArgumentList.Add(argument);

		using Process process = Start(start);

		// A canceled run stops the child too, so a pass does not go on writing files after the tool has ended.
		using CancellationTokenRegistration registration = cancellationToken.Register(() => Stop(process));

		// Both streams drain while the process runs, because a full pipe blocks it.
		Task<string> output = CopyAsync(process.StandardOutput, echo?.Output);
		Task<string> error = CopyAsync(process.StandardError, echo?.Error);

		process.WaitForExit();

		string outputText = output.GetAwaiter().GetResult();
		string errorText = error.GetAwaiter().GetResult();

		cancellationToken.ThrowIfCancellationRequested();

		return new ProcessResult(process.ExitCode, outputText, errorText);
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
					.WithDegreeOfParallelism(Math.Min(Environment.ProcessorCount, MaxParallelProcesses))
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

	private static Process Start(ProcessStartInfo start)
	{
		try
		{
			return Process.Start(start) ?? throw new FormatRunException($"{start.FileName} did not start.");
		}
		catch (Win32Exception exception)
		{
			throw new FormatRunException($"{start.FileName} could not be started: {exception.Message}", exception);
		}
	}

	private static void Stop(Process process)
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

	/// <summary>Reads the stream to its end, and echoes each chunk with its non-printing characters escaped.</summary>
	/// <remarks>
	/// A child echoes the names of files it reads, and a name may hold an escape sequence that a terminal obeys.
	/// </remarks>
	private static async Task<string> CopyAsync(StreamReader reader, TextWriter? echo)
	{
		StringBuilder text = new();
		char[] buffer = new char[4096];
		string held = string.Empty;
		int read;

		while ((read = await reader.ReadAsync(buffer).ConfigureAwait(false)) > 0)
		{
			text.Append(buffer, 0, read);

			if (echo is null)
				continue;

			// A pair of surrogates split between two reads is echoed whole with the next one, not as two codes.
			int whole = char.IsHighSurrogate(buffer[read - 1]) ? read - 1 : read;

			WriteEscaped(echo, held + new string(buffer, 0, whole));
			held = new string(buffer, whole, read - whole);
		}

		if (echo is not null && held.Length > 0)
			WriteEscaped(echo, held);

		return text.ToString();
	}

	private static void WriteEscaped(TextWriter echo, string chunk)
	{
		string shown = ToolHost.EscapeNonPrintingCharacters(chunk, keepLineLayout: true);

		lock (echo)
			echo.Write(shown);
	}
}
