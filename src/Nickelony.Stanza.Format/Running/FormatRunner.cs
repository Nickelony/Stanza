using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Nickelony.Stanza.Format.Planning;
using Nickelony.Stanza.Shared;

namespace Nickelony.Stanza.Format.Running;

/// <summary>Runs the steps of a plan in order, and writes each command and what it printed.</summary>
internal sealed class FormatRunner(string root, TextWriter output, TextWriter error, CancellationToken cancellationToken)
{
	/// <summary>
	/// A command line is cut at this length where it is shown, as a batch of paths can run to thousands of characters.
	/// </summary>
	private const int ShownCommandLength = 400;

	/// <summary>The most times a step runs while it changes a file, as a fix can open work for another rule.</summary>
	private const int MaxPasses = 3;

	private readonly ProcessEcho _echo = new(output, error);

	/// <summary>
	/// A check reports each step that would change a file, and a write run stops at the first step that fails,
	/// so no later pass builds on a half-made one; a step that could not run ends a check too.
	/// </summary>
	public ImmutableArray<FormatStep> Run(ImmutableArray<FormatStep> steps, bool check)
	{
		List<FormatStep> failed = [];

		foreach (FormatStep step in steps)
		{
			output.WriteLine();
			output.WriteLine($"==> {step.Title}");

			if (TryRunStep(step))
				continue;

			failed.Add(step);

			if (!check)
				break;
		}

		return [.. failed];
	}

	private bool TryRunStep(FormatStep step)
	{
		if (step.Kind is FormatStepKind.Commands or FormatStepKind.Diagnostics or FormatStepKind.Markup)
			return TryRunCommands(step);

		ImmutableArray<string> problems = step.Kind switch
		{
			FormatStepKind.SourceLayout => CollectSourceLayoutDrift(step),
			FormatStepKind.LineEndings => FormatChecks.CollectWrongLineEndings(GitRepository.ReadLineEndings(root, cancellationToken)),
			_ => FormatChecks.CollectMarkedFiles(GitRepository.ReadMarkedFiles(root, cancellationToken)),
		};

		foreach (string problem in problems)
			output.WriteLine($"    {ToolHost.EscapeNonPrintingCharacters(problem)}");

		return problems.IsEmpty;
	}

	private bool TryRunCommands(FormatStep step)
	{
		for (int pass = 1; ; pass++)
		{
			string before = step.Watched.IsEmpty ? string.Empty : ReadFingerprint(step.Watched);

			foreach (ImmutableArray<string> command in step.Commands)
			{
				output.WriteLine($"    {ToolHost.EscapeNonPrintingCharacters(BuildShownCommand(command))}");

				ProcessResult result = RunStepCommand(step, command, echo: _echo);

				if (result.ExitCode != 0)
				{
					output.WriteLine($"    The command exited with code {result.ExitCode}.");
					return false;
				}
			}

			if (step.Watched.IsEmpty || ReadFingerprint(step.Watched) == before)
				return true;

			if (pass == MaxPasses)
			{
				output.WriteLine($"    Pass {pass} still changed a file, so a second run of stanza-format may change more.");
				return true;
			}

			output.WriteLine($"    Pass {pass} changed a file, so the step runs again.");
		}
	}

	/// <summary>
	/// A source passes when it matches CSharpier but for its line endings and each gap of one blank line or none.
	/// </summary>
	private ImmutableArray<string> CollectSourceLayoutDrift(FormatStep step)
	{
		ProcessResult check = RunStepCommand(step, step.Commands[0], echo: null);

		CSharpierReport report = FormatChecks.ReadCSharpierReport(check.Output + "\n" + check.Error);
		// CSharpier exits with the change code for a failure too, so a failure line ends the run.
		ImmutableArray<string> failures = report.Failures;

		if (check.ExitCode != 0 && !failures.IsEmpty)
		{
			string more = failures.Length > 1 ? $" (and {failures.Length - 1} more line(s))" : string.Empty;
			throw new FormatRunException($"{step.Title} could not run, as CSharpier reported: {failures[0]}{more}");
		}

		List<string> drift = [.. report.Unknown];
		string workingDirectory = Path.GetFullPath(step.WorkingDirectory, root);

		// A project file is compared exactly, as no rule pass writes one.
		// A source starts a process of its own, and a formatted repository can have hundreds that differ only in blank lines.
		ImmutableArray<bool> drifts = ProcessRunner.RunEach(
			report.Unformatted,
			path => !FormatPlanner.IsSource(path) || DriftsFromCSharpier(step, path, workingDirectory),
			cancellationToken
		);

		for (int index = 0; index < drifts.Length; index++)
		{
			if (drifts[index])
				drift.Add(RepositoryPath.ToRelative(root, Path.GetFullPath(report.Unformatted[index], workingDirectory)));
		}

		// A check of no file is not a check.
		if (report.CheckedFiles <= 0)
			drift.Add($"CSharpier checked no file (exit code {check.ExitCode}).");

		return [.. drift];
	}

	/// <summary>Whether the source differs from CSharpier's layout in more than blank lines and line endings.</summary>
	private bool DriftsFromCSharpier(FormatStep step, string path, string workingDirectory)
	{
		// CSharpier prints the path after `./`, and it is protected in case a version does not.
		ImmutableArray<string> command = ["csharpier", "format", ArgumentPath.ToProtected(path), "--write-stdout"];
		ProcessResult layout = RunCommand(step, command, echo: null, allowedExitCodes: []);

		return !FormatChecks.MatchesApartFromBlankLines(layout.Output, File.ReadAllText(Path.GetFullPath(path, workingDirectory)));
	}

	/// <summary>
	/// Ends the run when the command exits with neither 0 nor one of the step's change codes, as it then failed.
	/// </summary>
	private ProcessResult RunStepCommand(FormatStep step, ImmutableArray<string> command, ProcessEcho? echo) =>
		RunCommand(step, command, echo, allowedExitCodes: step.ChangeExitCodes);

	private ProcessResult RunCommand(FormatStep step, ImmutableArray<string> command, ProcessEcho? echo, ImmutableArray<int> allowedExitCodes)
	{
		string workingDirectory = Path.GetFullPath(step.WorkingDirectory, root);
		ProcessResult result = ProcessRunner.Run(fileName: "dotnet", arguments: command, workingDirectory, echo, cancellationToken);

		if (result.ExitCode != 0 && !allowedExitCodes.Contains(result.ExitCode))
			throw new FormatRunException($"{step.Title} could not run, as the command exited with code {result.ExitCode}.");

		return result;
	}

	private string ReadFingerprint(ImmutableArray<string> paths)
	{
		using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

		foreach (string path in paths)
		{
			string fullPath = Path.Combine(root, path);

			hash.AppendData(File.Exists(fullPath) ? File.ReadAllBytes(fullPath) : []);
			hash.AppendData([0]);
		}

		return Convert.ToHexString(hash.GetHashAndReset());
	}

	private static string BuildShownCommand(ImmutableArray<string> command)
	{
		string shown = "dotnet " + string.Join(' ', command.Select(static argument => argument.Contains(' ') ? $"\"{argument}\"" : argument));
		return shown.Length <= ShownCommandLength ? shown : $"{shown[..ShownCommandLength]}... ({command.Length} arguments)";
	}
}
