using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

using static Nickelony.Stanza.Tests.Shared.MarkupFormatFixtures;

namespace Nickelony.Stanza.Markup.Tests;

/// <summary>Checks the tool's exit codes and streams in a child process.</summary>
[TestClass]
public sealed class MarkupToolProcessTests
{
	[TestMethod]
	public async Task TheProcessExitsWithTheCommandCodeAndOnlyARefusedRunWritesAnError()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.xml", LineFeed(Source));

		RunResult needsFormatting = await RunAsync(directory, "check", "sample.xml");

		Assert.AreEqual(1, needsFormatting.ExitCode);
		Assert.AreEqual(string.Empty, needsFormatting.Error);
		Assert.AreEqual(0, (await RunAsync(directory, "format", "sample.xml")).ExitCode);
		Assert.AreEqual(0, (await RunAsync(directory, "check", "sample.xml")).ExitCode);

		RunResult refused = await RunAsync(directory, "check", "missing.xml");

		Assert.AreEqual(2, refused.ExitCode);
		Assert.Contains("error: 'missing.xml' does not exist.", refused.Error);
	}

	[TestMethod]
	public async Task ARunWritesUtf8ForAPathOutsideAscii()
	{
		using TempDirectory directory = new();
		_ = directory.Write("café.xml", LineFeed(Source));

		RunResult run = await RunAsync(directory, "check", "café.xml");

		Assert.AreEqual(1, run.ExitCode);
		Assert.Contains("café.xml", run.Output);
		Assert.AreEqual(string.Empty, run.Error);
	}

	// A process with no console cannot set the console encoding, so it writes UTF-8 to the handles it was given.
	[TestMethod]
	[OSCondition(OperatingSystems.Windows)]
	[SupportedOSPlatform("windows")]
	public void AProcessWithNoConsoleStillRunsAndWritesUtf8()
	{
		using TempDirectory directory = new();
		_ = directory.Write("café.xml", LineFeed(Source));

		string tool = Path.Combine(AppContext.BaseDirectory, "Nickelony.Stanza.Markup.dll");
		RunResult run = DetachedProcess.Run($"dotnet \"{tool}\" check café.xml", directory.Root);

		Assert.AreEqual(1, run.ExitCode, run.Error);
		Assert.AreEqual(string.Empty, run.Error);
		Assert.Contains("needs formatting: café.xml", run.Output);
	}

	// SIGINT is what Ctrl+C sends on Unix, and SIGTERM is what a service manager sends to stop a process.
	[TestMethod]
	[DataRow("INT")]
	[DataRow("TERM")]
	[OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
	public async Task ASignalCancelsARunBetweenFilesAndExitsWithTheCanceledCode(string signal)
	{
		// The reports of the files outgrow what a pipe holds, so a run nobody reads from stalls well before its last file.
		const int fileCount = 6000;

		using TempDirectory directory = new();

		for (int index = 0; index < fileCount; index++)
			_ = directory.Write($"d{index % 20}/f{index}.xml", LineFeed(Source));

		ProcessStartInfo start = CreateStartInfo(directory, "check", ".");

		using Process process =
			Process.Start(start) ?? throw new AssertFailedException("No process.");

		Task<string> error = process.StandardError.ReadToEndAsync();

		// The first report shows the run has begun to process files.
		string? first = await process
			.StandardOutput.ReadLineAsync()
			.WaitAsync(TimeSpan.FromSeconds(60));

		Assert.IsNotNull(first, "The run reported nothing.");

		// Nobody reads on, so the run fills the pipe and waits inside a write, between two files and far from the last.
		await Task.Delay(TimeSpan.FromSeconds(1));
		SendSignal(signal, process.Id);

		Task<string> rest = process.StandardOutput.ReadToEndAsync();

		if (!process.WaitForExit(TimeSpan.FromSeconds(60)))
		{
			process.Kill(entireProcessTree: true);
			Assert.Fail($"The run did not stop on SIG{signal}.");
		}

		process.WaitForExit();

		int reports = 1 + (await rest).Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

		Assert.AreEqual(130, process.ExitCode);
		Assert.Contains("error: canceled.", await error);
		Assert.IsLessThan(fileCount, reports);
	}

	private static void SendSignal(string signal, int processId)
	{
		ProcessStartInfo start = new("kill")
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		};

		start.ArgumentList.Add($"-{signal}");

		start.ArgumentList.Add(
			processId.ToString(System.Globalization.CultureInfo.InvariantCulture)
		);

		RunResult sent = ChildProcess.Run(start, "kill");

		Assert.AreEqual(0, sent.ExitCode, sent.Error);
	}

	private static ProcessStartInfo CreateStartInfo(TempDirectory directory, params string[] args)
	{
		var startInfo = new ProcessStartInfo(DotnetHost.Executable)
		{
			WorkingDirectory = directory.Root,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			StandardOutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
			StandardErrorEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
		};

		startInfo.ArgumentList.Add(
			Path.Combine(AppContext.BaseDirectory, "Nickelony.Stanza.Markup.dll")
		);

		foreach (string argument in args)
			startInfo.ArgumentList.Add(argument);

		return startInfo;
	}

	private static async Task<RunResult> RunAsync(TempDirectory directory, params string[] args) =>
		await ChildProcess.RunAsync(CreateStartInfo(directory, args), string.Join(' ', args));
}
