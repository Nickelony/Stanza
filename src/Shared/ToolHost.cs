using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Nickelony.Stanza.Shared;

/// <summary>The console host both tools compile in.</summary>
internal static class ToolHost
{
	internal const string ErrorPrefix = "error: ";

	/// <summary>
	/// Returned when a check fails: a file needs formatting, or a stanza-format check finds a diagnostic.
	/// </summary>
	internal const int NeedsFormattingExitCode = 1;

	/// <summary>
	/// Returned when the run could not do what it was asked, as when a command cannot start or a named path is missing.
	/// </summary>
	internal const int FailedExitCode = 2;

	/// <summary>Returned when the run is canceled, the code a shell reports for SIGINT.</summary>
	internal const int CanceledExitCode = 130;

	/// <summary>Runs a tool's command on the console, and turns a cancel or an I/O failure into an exit code.</summary>
	public static int Run(string[] args, Func<string[], TextWriter, TextWriter, string, CancellationToken, int> command)
	{
		// Ctrl+C and SIGTERM cancel at the next cancellation check; a second one is passed on to the runtime,
		// so a run stuck where the token is never read can still be abandoned.
		using CancellationTokenSource cancellationTokenSource = new();

		bool TryCancel()
		{
			if (cancellationTokenSource.IsCancellationRequested)
				return false;

			cancellationTokenSource.Cancel();
			return true;
		}

		void CancelOnFirstPress(object? sender, ConsoleCancelEventArgs eventArgs) => eventArgs.Cancel = TryCancel();

		Console.CancelKeyPress += CancelOnFirstPress;

		// Disposed before the source, so a signal after the run cannot cancel a disposed source.
		using PosixSignalRegistration? termination = BuildTerminationRegistration(context => context.Cancel = TryCancel());

		TextWriter error = Console.Error;

		try
		{
			(TextWriter output, error) = OpenWriters();
			return command(args, output, error, Directory.GetCurrentDirectory(), cancellationTokenSource.Token);
		}
		catch (OperationCanceledException)
		{
			error.WriteLine($"{ErrorPrefix}canceled.");
			return CanceledExitCode;
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
		{
			return Fail(exception, error);
		}
		finally
		{
			// The handler leaves before the source is disposed, so a press after the run cannot cancel a disposed source.
			Console.CancelKeyPress -= CancelOnFirstPress;
		}
	}

	/// <summary>Registers a SIGTERM handler, or gives <see langword="null"/> where the platform has none.</summary>
	private static PosixSignalRegistration? BuildTerminationRegistration(Action<PosixSignalContext> handler)
	{
		try
		{
			return PosixSignalRegistration.Create(PosixSignal.SIGTERM, handler);
		}
		catch (PlatformNotSupportedException)
		{
			// A run on such a platform stops on Ctrl+C alone.
			return null;
		}
	}

	/// <summary>Pins the console to UTF-8, or writes UTF-8 to the standard handles where no console exists.</summary>
	private static (TextWriter Output, TextWriter Error) OpenWriters()
	{
		// The encoding is pinned so a non-ASCII path is not mangled, without a mark so a redirected stream does not gain one.
		UTF8Encoding utf8 = new(encoderShouldEmitUTF8Identifier: false);

		try
		{
			Console.OutputEncoding = utf8;
			return (Console.Out, Console.Error);
		}
		catch (IOException)
		{
			// A detached process has no console to set, and its standard handles are the ones it was given.
			return (BuildWriter(Console.OpenStandardOutput(), utf8), BuildWriter(Console.OpenStandardError(), utf8));
		}
	}

	private static StreamWriter BuildWriter(Stream stream, Encoding encoding) => new(stream, encoding) { AutoFlush = true };

	/// <summary>Runs a command, and reports an unhandled exception, a cancel excepted, as a failed run.</summary>
	public static int RunGuarded(Func<int> run, TextWriter error)
	{
		try
		{
			return run();
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			return Fail(exception, error);
		}
	}

	private static int Fail(Exception exception, TextWriter error)
	{
		error.WriteLine($"{ErrorPrefix}{exception.GetType().Name}: {ToReportLine(exception.Message)}");
		return FailedExitCode;
	}

	/// <summary>Flattens line breaks and writes each other non-printing character as its code.</summary>
	/// <remarks>
	/// Each line a tool writes is read on its own, and a message can echo a file's text, such as an escape sequence.
	/// </remarks>
	public static string ToReportLine(string? message) => EscapeNonPrintingCharacters(message?.ReplaceLineEndings(" ") ?? string.Empty);

	/// <summary>Writes each control, format or separator character, or lone surrogate, as its code.</summary>
	/// <remarks>
	/// A name may hold a line break or U+202E, which a terminal obeys, or a hidden tag character above U+FFFF.
	/// </remarks>
	public static string EscapeNonPrintingCharacters(string text, bool keepLineLayout = false)
	{
		// Most text holds nothing to escape, so the builder starts only at the first code that needs it.
		StringBuilder? escaped = null;
		int index = 0;

		while (index < text.Length)
		{
			int length = ReadEscapedCode(text, index, keepLineLayout, out int code);

			if (code >= 0)
			{
				escaped ??= new StringBuilder(text.Length + 8).Append(text, 0, index);
				escaped.Append(@"\x").Append(code.ToString("X2", CultureInfo.InvariantCulture));
			}
			else
			{
				escaped?.Append(text, index, length);
			}

			index += length;
		}

		return escaped?.ToString() ?? text;
	}

	/// <summary>Reads the code point at the index, and gives its code when it is escaped, or -1.</summary>
	/// <returns>The number of UTF-16 units the code point takes.</returns>
	private static int ReadEscapedCode(string text, int index, bool keepLineLayout, out int code)
	{
		if (Rune.DecodeFromUtf16(text.AsSpan(index), out Rune rune, out int length) != OperationStatus.Done)
		{
			// A lone surrogate is no character, so it is written as its code too.
			code = text[index];
			return 1;
		}

		code = IsNonPrinting(rune) && !(keepLineLayout && rune.Value is '\t' or '\r' or '\n') ? rune.Value : -1;
		return length;
	}

	private static bool IsNonPrinting(Rune rune)
	{
		return Rune.GetUnicodeCategory(rune)
			is UnicodeCategory.Control
				or UnicodeCategory.Format
				or UnicodeCategory.LineSeparator
				or UnicodeCategory.ParagraphSeparator;
	}

	/// <summary>The version <c>--version</c> prints, without the commit the build appends after a <c>+</c>.</summary>
	public static string GetVersion(Assembly assembly)
	{
		string? informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

		if (informational is null)
			return assembly.GetName().Version?.ToString() ?? "unknown";

		int metadata = informational.IndexOf('+', StringComparison.Ordinal);
		return metadata < 0 ? informational : informational[..metadata];
	}
}
