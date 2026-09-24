using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
