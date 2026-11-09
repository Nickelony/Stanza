using System;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32.SafeHandles;
using Nickelony.Stanza.Format.Running;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Format.Tests;

[TestClass]
public sealed class ProcessRunnerTests
{
	[TestMethod]
	public void ACanceledTokenStartsNoProcess()
	{
		using TempDirectory directory = new(editorConfig: false);
		using CancellationTokenSource canceled = new();

		canceled.Cancel();

		string repository = Path.Combine(directory.Root, "repo");

		_ = Assert.ThrowsExactly<OperationCanceledException>(() =>
			_ = ProcessRunner.Run(
				fileName: "git",
				arguments: ["init", "--quiet", repository],
				workingDirectory: directory.Root,
				echo: null,
				cancellationToken: canceled.Token
			)
		);

		Assert.IsFalse(Directory.Exists(repository));
	}

	[TestMethod]
	public void AProgramThatIsMissingIsARunFailureThatNamesIt()
	{
		using TempDirectory directory = new(editorConfig: false);

		FormatRunException exception = Assert.ThrowsExactly<FormatRunException>(() =>
			_ = ProcessRunner.Run(
				fileName: "stanza-missing-program",
				arguments: [],
				workingDirectory: directory.Root,
				echo: null,
				cancellationToken: CancellationToken.None
			)
		);

		Assert.StartsWith("stanza-missing-program could not be started", exception.Message);
		Assert.IsInstanceOfType<Win32Exception>(exception.InnerException);
	}

	[TestMethod]
	[DoNotParallelize]
	public void AChildThatReadsItsInputGetsAnEmptyOneWhileTheToolsInputStaysOpen()
	{
		using TempDirectory directory = new(editorConfig: false);

		// The tool's input is a pipe that nothing writes to and nothing closes, as a terminal or a CI agent can hold it.
		using AnonymousPipeServerStream writer = new(
			PipeDirection.Out,
			HandleInheritability.Inheritable
		);

		using CancellationTokenSource bound = new(TimeSpan.FromSeconds(30));

		ProcessResult result;

		using (StandardInput.Swap(writer.ClientSafePipeHandle))
		{
			// Git hashes what it reads until its input ends, so a child that inherited the tool's input would wait on it.
			result = ProcessRunner.Run(
				fileName: "git",
				arguments: ["hash-object", "--stdin"],
				workingDirectory: directory.Root,
				echo: null,
				cancellationToken: bound.Token
			);
		}

		Assert.AreEqual(0, result.ExitCode);
		Assert.AreEqual("e69de29bb2d1d6434b8b29ae775ad8c2e48c5391", result.Output.Trim());
	}

	[TestMethod]
	public void AnEchoThatFailsEndsTheRunInsteadOfLeavingTheChildBlockedOnAFullPipe()
	{
		using TempDirectory directory = new(editorConfig: false);

		TestGit.Init(directory.Root);

		// The blob is far larger than a pipe holds, so git blocks on its output once nothing reads it.
		string large = Path.Combine(directory.Root, "large.txt");
		File.WriteAllText(large, new string('x', 4 * 1024 * 1024));

		string blob = TestGit.Read(directory.Root, "hash-object", "-w", "large.txt");

		using FailingWriter output = new();
		using StringWriter error = new();
		using CancellationTokenSource bound = new(TimeSpan.FromSeconds(30));

		_ = Assert.ThrowsExactly<IOException>(() =>
			_ = ProcessRunner.Run(
				fileName: "git",
				arguments: ["cat-file", "blob", blob],
				workingDirectory: directory.Root,
				echo: new ProcessEcho(output, error),
				cancellationToken: bound.Token
			)
		);

		Assert.IsFalse(
			bound.IsCancellationRequested,
			"The run ended only when its bound stopped the child."
		);
	}

	/// <summary>Stands for a console whose reader has quit.</summary>
	private sealed class FailingWriter : StringWriter
	{
		public override void Write(string? value) =>
			throw new IOException("The pipe is being closed.");
	}

	/// <summary>Sets the input a child inherits, until disposed.</summary>
	private sealed class StandardInput : IDisposable
	{
		private const int StandardInputHandle = -10;

		private readonly IntPtr _previous;

		private StandardInput(IntPtr previous) => _previous = previous;

		public static StandardInput Swap(SafePipeHandle handle)
		{
			IntPtr replacement = handle.DangerousGetHandle();

			if (OperatingSystem.IsWindows())
			{
				IntPtr previous = GetStdHandle(StandardInputHandle);
				_ = SetStdHandle(StandardInputHandle, replacement);

				return new StandardInput(previous);
			}

			IntPtr saved = new(Dup(0));
			_ = Dup2(replacement.ToInt32(), 0);

			return new StandardInput(saved);
		}

		public void Dispose()
		{
			if (OperatingSystem.IsWindows())
			{
				_ = SetStdHandle(StandardInputHandle, _previous);
				return;
			}

			_ = Dup2(_previous.ToInt32(), 0);
			_ = Close(_previous.ToInt32());
		}

		[DllImport("kernel32.dll")]
		private static extern IntPtr GetStdHandle(int kind);

		[DllImport("kernel32.dll")]
		private static extern bool SetStdHandle(int kind, IntPtr handle);

		[DllImport("libc", EntryPoint = "dup")]
		private static extern int Dup(int descriptor);

		[DllImport("libc", EntryPoint = "dup2")]
		private static extern int Dup2(int descriptor, int target);

		[DllImport("libc", EntryPoint = "close")]
		private static extern int Close(int descriptor);
	}

	[TestMethod]
	public void TheErrorStreamOfAChildGoesToTheErrorWriterWithItsNonPrintingCharactersEscaped()
	{
		using TempDirectory directory = new(editorConfig: false);

		using StringWriter output = new();
		using StringWriter error = new();

		// Git names an unknown command in its message without escaping it, so the ESC reaches the stream.
		string command = "x" + (char)0x1B + "[31my";

		ProcessResult result = ProcessRunner.Run(
			fileName: "git",
			arguments: [command],
			workingDirectory: directory.Root,
			echo: new ProcessEcho(output, error),
			cancellationToken: CancellationToken.None
		);

		Assert.AreNotEqual(0, result.ExitCode);
		Assert.AreEqual(string.Empty, output.ToString());
		Assert.Contains(@"\x1B[31my", error.ToString());
		Assert.DoesNotContain(((char)0x1B).ToString(), error.ToString());
		Assert.Contains("\n", error.ToString());
	}

	[TestMethod]
	public void TheOutputStreamOfAChildGoesToTheOutputWriterWithItsLinesKept()
	{
		using TempDirectory directory = new(editorConfig: false);

		using StringWriter output = new();
		using StringWriter error = new();

		ProcessResult result = ProcessRunner.Run(
			fileName: "git",
			arguments: ["--version"],
			workingDirectory: directory.Root,
			echo: new ProcessEcho(output, error),
			cancellationToken: CancellationToken.None
		);

		Assert.AreEqual(0, result.ExitCode);
		Assert.StartsWith("git version", output.ToString());
		Assert.EndsWith("\n", output.ToString());
		Assert.AreEqual(string.Empty, error.ToString());
	}
}
