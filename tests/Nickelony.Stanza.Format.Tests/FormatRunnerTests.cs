using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Format.Planning;
using Nickelony.Stanza.Format.Running;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Format.Tests;

[TestClass]
public sealed class FormatRunnerTests
{
	[TestMethod]
	public void AStepThatKeepsChangingAFileRunsThreeTimesAndSaysTheLastOneStillChangedIt()
	{
		using TempDirectory directory = new(editorConfig: false);
		_ = directory.Write("watched.txt", "start\n");

		_ = directory.Write(
			"grow.proj",

			"""
			<Project>
				<Target Name="Grow">
					<WriteLinesToFile File="watched.txt" Lines="more" Overwrite="false" />
				</Target>
			</Project>
			"""
		);

		FormatStep grow = BuildStep(
			title: "Grow",
			kind: FormatStepKind.Commands,
			command: ["msbuild", "grow.proj", "-t:Grow", "-nologo"],
			watched: ["watched.txt"]
		);

		using StringWriter output = new();
		using StringWriter error = new();

		ImmutableArray<FormatStep> failed = Run(
			root: directory.Root,
			output,
			error,
			steps: [grow],
			check: false
		);

		string text = output.ToString();

		Assert.IsEmpty(failed);
		Assert.Contains("Pass 1 changed a file, so the step runs again.", text);
		Assert.Contains("Pass 2 changed a file, so the step runs again.", text);
		Assert.Contains("Pass 3 still changed a file, so a second run of stanza-format", text);
		Assert.DoesNotContain("Pass 4", text);

		Assert.HasCount(
			4,
			directory.Read("watched.txt").Split('\n', StringSplitOptions.RemoveEmptyEntries)
		);
	}

	[TestMethod]
	public void AWriteRunStopsAtTheFirstStepThatFailsAndACheckGoesOn()
	{
		using TempDirectory directory = new(editorConfig: false);
		_ = directory.Write("conflict.txt", "<<<<<<< ours\n");

		RunGit(directory.Root, "init", "--quiet");

		FormatStep markers = BuildStep("Merge markers", FormatStepKind.Markers);
		FormatStep later = BuildStep("Later", FormatStepKind.Commands, command: ["--version"]);

		using StringWriter writeOutput = new();
		using StringWriter writeError = new();

		ImmutableArray<FormatStep> stopped = Run(
			root: directory.Root,
			output: writeOutput,
			error: writeError,
			steps: [markers, later],
			check: false
		);

		Assert.AreSequenceEqual([markers], stopped);
		Assert.Contains("    conflict.txt", writeOutput.ToString());
		Assert.DoesNotContain("==> Later", writeOutput.ToString());

		using StringWriter checkOutput = new();
		using StringWriter checkError = new();

		ImmutableArray<FormatStep> reported = Run(
			root: directory.Root,
			output: checkOutput,
			error: checkError,
			steps: [markers, later],
			check: true
		);

		Assert.AreSequenceEqual([markers], reported);
		Assert.Contains("==> Later", checkOutput.ToString());
	}

	[TestMethod]
	public void TheErrorStreamOfACommandReachesTheErrorWriterAndTheRunEnds()
	{
		using TempDirectory directory = new(editorConfig: false);

		FormatStep missing = BuildStep(
			"Missing",
			FormatStepKind.Commands,
			["stanza-no-such-command"]
		);

		using StringWriter output = new();
		using StringWriter error = new();

		FormatRunException exception = Assert.ThrowsExactly<FormatRunException>(() =>
			_ = Run(root: directory.Root, output, error, steps: [missing], check: false)
		);

		Assert.StartsWith(
			"Missing could not run, as the command exited with code",
			exception.Message
		);

		Assert.Contains("Could not execute because the specified command", error.ToString());
		Assert.DoesNotContain("Could not execute", output.ToString());
	}

	[TestMethod]
	public void AFormatThatFailsAfterTheCheckListedTheFileEndsTheRunAndIsNoChange()
	{
		using TempDirectory directory = new(editorConfig: false);
		_ = directory.Write("A.cs", "class  A{}\n");

		// The check lists A.cs and exits with the change code, and no tool manifest lets the format command start.
		_ = directory.Write(
			"Check.cs",

			"""
			System.Console.WriteLine(@".\A.cs: error: Was not formatted.");
			System.Console.WriteLine("Checked 1 files in 1ms.");
			return 1;
			"""
		);

		FormatStep layout = BuildStep(
			"Source layout",
			FormatStepKind.SourceLayout,
			command: ["run", "Check.cs"]
		);

		using StringWriter output = new();
		using StringWriter error = new();

		FormatRunException exception = Assert.ThrowsExactly<FormatRunException>(() =>
			_ = Run(
				root: directory.Root,
				output,
				error,
				steps: [layout with { ChangeExitCodes = [1] }],
				check: true
			)
		);

		Assert.StartsWith(
			"Source layout could not run, as the command exited with code 1.",
			exception.Message
		);
	}

	[TestMethod]
	public void AMarkupFindingFailsTheCheckAndTheLaterStepsStillRun()
	{
		using TempDirectory directory = new(editorConfig: false);

		// The markup check exits with 2 for a warning under --strict, and the check reports it as a finding.
		_ = directory.Write(
			"Check.cs",

			"""
			System.Console.WriteLine("a.xml: warning: a key it does not read");
			return 2;
			"""
		);

		FormatStep markup = BuildStep(
			"Markup files",
			FormatStepKind.Markup,
			command: ["run", "Check.cs"]
		) with
		{
			ChangeExitCodes = [1, 2],
		};

		FormatStep later = BuildStep("Later", FormatStepKind.Commands, command: ["--version"]);

		using StringWriter output = new();
		using StringWriter error = new();

		ImmutableArray<FormatStep> failed = Run(
			root: directory.Root,
			output,
			error,
			steps: [markup, later],
			check: true
		);

		Assert.AreSequenceEqual([markup], failed);
		Assert.Contains("a.xml: warning: a key it does not read", output.ToString());
		Assert.Contains("==> Later", output.ToString());
	}

	private static ImmutableArray<FormatStep> Run(
		string root,
		StringWriter output,
		StringWriter error,
		ImmutableArray<FormatStep> steps,
		bool check
	)
	{
		FormatRunner runner = new(root, output, error, cancellationToken: CancellationToken.None);
		return runner.Run(steps, check);
	}

	private static FormatStep BuildStep(
		string title,
		FormatStepKind kind,
		ImmutableArray<string> command = default,
		ImmutableArray<string> watched = default
	)
	{
		return new FormatStep(
			Title: title,
			Kind: kind,
			Commands: command.IsDefaultOrEmpty ? [] : [command],
			Watched: watched.IsDefault ? [] : watched,
			ChangeExitCodes: [],
			WorkingDirectory: "."
		);
	}

	private static void RunGit(string root, params string[] arguments)
	{
		ProcessResult result = ProcessRunner.Run(
			fileName: "git",
			arguments,
			workingDirectory: root,
			echo: null,
			cancellationToken: CancellationToken.None
		);

		Assert.AreEqual(0, result.ExitCode, result.Error);
	}
}
