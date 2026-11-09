using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using Nickelony.Stanza.Format.Planning;
using Nickelony.Stanza.Format.Running;
using Nickelony.Stanza.Shared;

namespace Nickelony.Stanza.Format.Cli;

/// <summary>Runs a <c>stanza-format</c> command: reads the repository's facts, plans the steps and runs them.</summary>
internal sealed class FormatTool(TextWriter output, TextWriter error, string workingDirectory, CancellationToken cancellationToken)
{
	/// <summary>A run names this many skipped sources, so one line stays short when a tree holds hundreds.</summary>
	private const int MaxNamedSkippedSources = 5;

	private const string ToolName = "stanza-format";
	private const string NothingToFormat = "No changed source, project or markup file to format.";
	private const string FormattingFinished = "Formatting finished. Review the changes and commit them.";
	private const string NotSettled = "https://github.com/Nickelony/Stanza/wiki/Troubleshooting#stanza-format-says-the-run-did-not-settle";

	public static int Run(
		string[] args,
		TextWriter output,
		TextWriter error,
		string workingDirectory,
		CancellationToken cancellationToken = default
	) => ToolHost.RunGuarded(() => new FormatTool(output, error, workingDirectory, cancellationToken).RunReportingFailures(args), error);

	private int RunReportingFailures(string[] args)
	{
		try
		{
			return RunCommand(args);
		}
		catch (FormatRunException exception)
		{
			error.WriteLine($"{ToolHost.ErrorPrefix}{ToolHost.ToReportLine(exception.Message)}");
			return ToolHost.FailedExitCode;
		}
	}

	private int RunCommand(string[] args)
	{
		// An unreadable option is reported before --help or --version, so a typo cannot pass for either.
		if (!FormatCommandLine.TryParse(args, out FormatCommandLine? commandLine, out string? parseError))
			return ToolHost.FailUsage(ToolName, parseError, error);

		bool answered = ToolHost.TryAnswerInformation(
			help: commandLine.Help,
			version: commandLine.Version,
			usage: FormatCommandLine.Usage,
			assembly: typeof(FormatTool).Assembly,
			output
		);

		if (answered)
			return 0;

		FormatOptions options = commandLine.Options;

		string solution = ResolveSolution(options.Path);
		string solutionDirectory = Path.GetDirectoryName(solution) ?? workingDirectory;
		string root = GitRepository.ReadRoot(solutionDirectory, cancellationToken);

		// The manifest decides the layout, so a run refuses a layout it cannot use before it reads the changes.
		var manifest = ToolManifest.Read(solutionDirectory);
		FormatLayout layout = FormatPlanner.ResolveLayout(options, manifest.HasCSharpier);

		if (layout == FormatLayout.CSharpier && !manifest.HasCSharpier)
			throw new FormatRunException("the csharpier layout needs CSharpier in the tool manifest. Add it with 'dotnet tool install csharpier'.");

		if (layout == FormatLayout.CSharpier && manifest.PinsOldCSharpier)
		{
			throw new FormatRunException(
				$"the tool manifest pins CSharpier {manifest.CSharpierVersion}, "
					+ "but stanza-format needs CSharpier 1.1 or later. "
					+ "Update it with 'dotnet tool update csharpier', or pass '--layout whitespace'."
			);
		}

		FormatFacts facts = ReadFacts(options, root, solution: SlashPath.ToRelative(root, solution), manifest);
		ImmutableArray<FormatStep> steps = FormatPlanner.Plan(options, facts, layout);

		string scope = DescribeScope(options, facts.ChangedFiles.Length);

		output.WriteLine(
			$"==> {scope} of {ToolHost.EscapeNonPrintingCharacters(facts.Solution)}, laid out by {layout.ToString().ToLowerInvariant()}"
		);

		if (!facts.SkippedSources.IsEmpty)
			output.WriteLine(DescribeSkippedSources(facts.SkippedSources));

		if (steps.IsEmpty)
		{
			output.WriteLine(NothingToFormat);
			return 0;
		}

		FormatRunner runner = new(root, output, error, cancellationToken);
		ImmutableArray<FormatStep> failed = runner.Run(steps, options.Check);

		// A write run of the marker step alone formatted nothing, so it does not ask for a review of its changes.
		if (failed.IsEmpty && !options.Check && steps.All(static step => step.Kind == FormatStepKind.Markers))
		{
			output.WriteLine(NothingToFormat);
			return 0;
		}

		if (failed.IsEmpty && !options.Check)
			return CheckSettled(runner, FormatPlanner.PlanSettleCheck(options, facts, layout));

		ReportOutcome(failed, options);

		return GetExitCode(failed, options.Check);
	}

	/// <summary>Checks the files a write run formatted, as two passes that undo each other never settle.</summary>
	private int CheckSettled(FormatRunner runner, ImmutableArray<FormatStep> checks)
	{
		output.WriteLine();
		output.WriteLine("Checking the formatted files, so a run that did not settle says so.");

		ImmutableArray<FormatStep> unsettled = runner.Run(checks, check: true);

		output.WriteLine();

		if (unsettled.IsEmpty)
		{
			output.WriteLine(FormattingFinished);
			return 0;
		}

		error.WriteLine($"{ToolHost.ErrorPrefix}the run did not settle, as a check of the files it formatted still finds work in these steps:");

		foreach (FormatStep step in unsettled)
			error.WriteLine($"  - {step.Title}");

		foreach (string fix in unsettled.Select(static step => ToSettleFix(step.Kind)).Distinct(StringComparer.Ordinal))
			error.WriteLine(fix);

		return ToolHost.NeedsFormattingExitCode;
	}

	/// <summary>A write run applied every fix, so a check finds a fix by hand or a pair that disagrees.</summary>
	private static string ToSettleFix(FormatStepKind kind)
	{
		return kind switch
		{
			FormatStepKind.Diagnostics => "Fix by hand each diagnostic the check printed, or set its severity below info; "
				+ $"one the run fixed means a rule and the layout disagree: {NotSettled}",

			FormatStepKind.Markup => "Fix by hand each markup warning or defect the check printed.",
			_ => $"A rule's fix undid the layout, so the rule and the layout disagree: {NotSettled}",
		};
	}

	private static string DescribeScope(FormatOptions options, int changedFiles)
	{
		string verb = options.Check ? "Check" : "Format";

		if (options.Since is not null)
			return $"{verb} {changedFiles} file(s) changed since {ToolHost.EscapeNonPrintingCharacters(options.Since)}";

		if (options.Check || options.All)
			return $"{verb} the whole solution";

		return $"Format {changedFiles} changed file(s)";
	}

	private FormatFacts ReadFacts(FormatOptions options, string root, string solution, ToolManifest manifest)
	{
		ImmutableArray<string> changed = options switch
		{
			{ Since: { } since } => GitRepository.CollectChangedFilesSince(root, since, cancellationToken),
			{ All: true } or { Check: true } => [],
			_ => GitRepository.CollectChangedFiles(root, cancellationToken),
		};

		// A whole check reads the solution whole, and a run with no changed source formats no project, so neither needs the projects' files.
		bool readsProjects = (options.Since is not null || !options.Check) && (options.All || changed.Any(FormatPlanner.IsSource));
		ImmutableArray<CompiledFile> compiled = readsProjects ? CompileItems.Collect(root, solution, cancellationToken) : [];
		ImmutableArray<string> inScope = FormatPlanner.CollectChangedFilesInScope(solution, changed, compiled);

		return new FormatFacts(
			Solution: solution,
			HasMarkup: manifest.HasMarkup,
			ChangedFiles: inScope,
			SkippedSources: FormatPlanner.CollectSkippedSources(changed, inScope),
			CompiledFiles: compiled
		)
		{
			ScopedFiles = options.All || (options.Check && options.Since is null) ? default : changed,
		};
	}

	/// <summary>Names the first few sources a default run leaves out, so a run that formats none says why.</summary>
	private static string DescribeSkippedSources(ImmutableArray<string> skipped)
	{
		string names = string.Join(", ", skipped.Take(MaxNamedSkippedSources).Select(static name => ToolHost.EscapeNonPrintingCharacters(name)));
		string more = skipped.Length > MaxNamedSkippedSources ? $" and {skipped.Length - MaxNamedSkippedSources} more" : string.Empty;

		return $"Skipped {skipped.Length} changed source(s) outside the solution's directory that no project compiles: {names}{more}.";
	}

	/// <summary>
	/// Searches a directory for its one solution, as <c>dotnet format</c> searches the working directory.
	/// </summary>
	private string ResolveSolution(string path)
	{
		string fullPath = Path.GetFullPath(path, workingDirectory);

		if (File.Exists(fullPath))
			return IsSolution(fullPath) ? fullPath : throw new FormatRunException($"'{path}' is not a solution file (.sln or .slnx).");

		if (!Directory.Exists(fullPath))
			throw new FormatRunException($"'{path}' does not exist.");

		string[] found = [.. Directory.EnumerateFiles(fullPath).Where(IsSolution).Order(StringComparer.Ordinal)];
		string names = string.Join(", ", found.Select(Path.GetFileName));

		return found.Length switch
		{
			1 => found[0],
			0 => throw new FormatRunException($"'{path}' holds no solution. Name a solution file or a directory that holds one."),
			_ => throw new FormatRunException($"'{path}' holds {found.Length} solutions ({names}). Name one of them."),
		};
	}

	/// <summary>
	/// Compares the extension whole, as a search for <c>*.sln</c> on Windows also finds a <c>.slnx</c> file.
	/// </summary>
	private static bool IsSolution(string path) => Path.GetExtension(path).ToUpperInvariant() is ".SLN" or ".SLNX";

	private void ReportOutcome(ImmutableArray<FormatStep> failed, FormatOptions options)
	{
		output.WriteLine();

		if (failed.IsEmpty)
		{
			// Only a check passes here, as a write run that ran every step ends with the settle check.
			output.WriteLine("The working tree is formatted.");
			return;
		}

		string reason = options.Check
			? "the working tree is not formatted. These steps would change a file or hold one to fix by hand:"
			: "a step found a file to fix by hand:";

		error.WriteLine($"{ToolHost.ErrorPrefix}{reason}");

		foreach (FormatStep step in failed)
			error.WriteLine($"  - {step.Title}");

		foreach (string fix in failed.Select(step => ToFix(step.Kind, options)).Distinct(StringComparer.Ordinal))
			error.WriteLine(fix);
	}

	/// <summary>
	/// A write run fixes what a formatting step finds, but no line ending, conflict or diagnostic without a fix.
	/// </summary>
	private static string ToFix(FormatStepKind kind, FormatOptions options)
	{
		return kind switch
		{
			FormatStepKind.LineEndings => "Convert each file under Line endings to the ending .gitattributes pins.",
			FormatStepKind.Markers => "Resolve the conflict in each file under Merge markers.",

			FormatStepKind.Diagnostics =>
				$"Run {BuildWriteCommand(options)}, then fix by hand each diagnostic it leaves, or set its severity below info.",

			FormatStepKind.Markup =>
				$"Run {BuildWriteCommand(options)}, then fix by hand each markup warning or defect it leaves, as the Markup files step reports them.",

			_ => $"Run {BuildWriteCommand(options)}, then review the changes and commit them.",
		};
	}

	/// <summary>Writes the run that fixes a check, with the layout and the path of the run that was checked.</summary>
	private static string BuildWriteCommand(FormatOptions options)
	{
		string layout = options.Layout is { } named ? $" --layout {named.ToString().ToLowerInvariant()}" : string.Empty;

		string path =
			options.Path == "." ? string.Empty
			: options.Path.StartsWith('-') ? $" -- {BuildShownArgument(options.Path)}"
			: $" {BuildShownArgument(options.Path)}";

		string scope = options.Since is { } since ? $"--since {BuildShownArgument(since)}" : "--all";

		return $"stanza-format {scope}{layout}{path}";
	}

	private static string BuildShownArgument(string argument) =>
		ToolHost.EscapeNonPrintingCharacters(argument.Contains(' ') ? $"\"{argument}\"" : argument);

	private static int GetExitCode(ImmutableArray<FormatStep> failed, bool check)
	{
		if (failed.IsEmpty)
			return 0;

		return check ? ToolHost.NeedsFormattingExitCode : ToolHost.FailedExitCode;
	}
}
