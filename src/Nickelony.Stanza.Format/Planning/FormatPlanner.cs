using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Nickelony.Stanza.Shared;

namespace Nickelony.Stanza.Format.Planning;

/// <summary>Turns the options and the facts into the steps of a run, which a test reads without a process.</summary>
internal static class FormatPlanner
{
	/// <summary>These rules judge a member's line count, so they run after the layout step.</summary>
	/// <seealso href="https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#run-order"/>
	public static readonly ImmutableArray<string> BodyRules = ["ZA0039", "ZA0040", "ZA0041", "ZA0042", "ZA0043", "ZA0044", "ZA0045"];

	/// <summary>
	/// The code <c>dotnet format --verify-no-changes</c> exits with when it would change a file; 1 means it failed.
	/// </summary>
	private const int DotnetFormatChangeExitCode = 2;

	/// <summary>
	/// The code <c>csharpier check</c> exits with when it would change a file, and when it reports an error.
	/// </summary>
	private const int CSharpierChangeExitCode = 1;

	/// <summary>The directory of the repository root, which the commands of <c>dotnet format</c> run in.</summary>
	private const string RootDirectory = ".";

	private const string SourceLayoutTitle = "Source layout (CSharpier, blank lines aside)";

	/// <summary>The extensions of the files CSharpier lays out, as the pinned version's check names them.</summary>
	public static readonly ImmutableHashSet<string> LayoutExtensions = ImmutableHashSet.Create(
		StringComparer.OrdinalIgnoreCase,
		".cs",
		".csx",
		".xaml",
		".axaml",
		".csproj",
		".props",
		".targets",
		".xml",
		".config",
		".slnx"
	);

	public static FormatLayout ResolveLayout(FormatOptions options, bool hasCSharpier) =>
		options.Layout ?? (hasCSharpier ? FormatLayout.CSharpier : FormatLayout.Whitespace);

	/// <summary>Plans the run for the layout the caller resolved from the options and the manifest.</summary>
	public static ImmutableArray<FormatStep> Plan(FormatOptions options, FormatFacts facts, FormatLayout layout) =>
		options.Check ? PlanCheck(facts, layout) : PlanWrite(options, facts, layout);

	private static ImmutableArray<FormatStep> PlanCheck(FormatFacts facts, FormatLayout layout)
	{
		string directory = GetSolutionDirectory(facts.Solution);
		List<FormatStep> steps = [];

		AddLocalTools(steps, facts, layout);

		steps.Add(ForRuleCheck("Analyzers", facts.Solution, "analyzers"));
		steps.Add(ForRuleCheck("IDE style rules", facts.Solution, "style"));

		// The Stanza rules put back blank lines that CSharpier drops, so a source's gaps of one blank line or none are not compared.
		if (layout == FormatLayout.CSharpier)
		{
			FormatStep check = ForCheck(SourceLayoutTitle, FormatStepKind.SourceLayout, "csharpier", "check", ".", "--log-format", "MsBuild");
			steps.Add(check with { ChangeExitCodes = [CSharpierChangeExitCode], WorkingDirectory = directory });
		}
		else if (layout == FormatLayout.Whitespace)
		{
			steps.Add(
				ForVerify(
					"Whitespace",
					FormatStepKind.Commands,
					"format",
					ArgumentPath.ToProtected(facts.Solution),
					"whitespace",
					"--verify-no-changes"
				)
			);
		}

		// A warning of the markup check fails the check too, as `format.ps1` runs it.
		if (facts.HasMarkup)
		{
			FormatStep markup = ForCommand("Markup files", "stanza-markup", "check", ".", "--strict");

			steps.Add(
				markup with
				{
					Kind = FormatStepKind.Markup,
					ChangeExitCodes = [ToolHost.NeedsFormattingExitCode, ToolHost.FailedExitCode],
					WorkingDirectory = directory,
				}
			);
		}

		steps.Add(ForCheck("Line endings", FormatStepKind.LineEndings));
		steps.Add(ForCheck("Merge markers", FormatStepKind.Markers));

		return [.. steps];
	}

	/// <summary>The changed files of the solution's directory, and the changed sources the solution compiles.</summary>
	public static ImmutableArray<string> CollectChangedFilesInScope(
		string solution,
		ImmutableArray<string> changed,
		ImmutableArray<CompiledFile> compiledFiles
	)
	{
		string directory = GetSolutionDirectory(solution);
		HashSet<string> compiled = new(compiledFiles.Select(static file => file.Path), PathComparison.Comparer);

		return [.. changed.Where(path => IsInDirectory(directory, path) || compiled.Contains(path))];
	}

	/// <summary>The changed sources outside the solution's directory that no project compiles.</summary>
	public static ImmutableArray<string> CollectSkippedSources(ImmutableArray<string> changed, ImmutableArray<string> inScope) =>
		[.. changed.Where(IsSource).Except(inScope, PathComparison.Comparer)];

	private static ImmutableArray<FormatStep> PlanWrite(FormatOptions options, FormatFacts facts, FormatLayout layout)
	{
		// A default run formats what the check reads: a source through the projects that compile it, wherever it lies,
		// and the layout and markup files of the solution's directory.
		string directory = GetSolutionDirectory(facts.Solution);
		FormatFacts directoryFacts = facts with { ChangedFiles = [.. facts.ChangedFiles.Where(path => IsInDirectory(directory, path))] };

		ImmutableArray<string> sources = [.. facts.ChangedFiles.Where(IsSource)];
		ImmutableArray<FormatTarget> targets = options.All ? FormatTargets.ForSolution(facts) : FormatTargets.ForChangedSources(facts, sources);

		ImmutableArray<ImmutableArray<string>> layoutCommands = BuildLayoutCommands(options, facts: directoryFacts, layout, targets);
		ImmutableArray<ImmutableArray<string>> markupCommands = BuildMarkupCommands(options, directoryFacts);

		if (targets.IsEmpty && layoutCommands.IsEmpty && markupCommands.IsEmpty)
			return [];

		ImmutableArray<string> watched = options.All
			? [.. facts.CompiledFiles.Select(static file => file.Path).Distinct(PathComparison.Comparer)]
			: sources;

		ImmutableArray<ImmutableArray<string>> allButBodies = BuildFormatCommands(
			targets,
			["analyzers", "--severity", "info", "--exclude-diagnostics", .. BodyRules]
		);

		string layoutTitle = layout == FormatLayout.CSharpier ? "CSharpier" : "Whitespace";
		string layoutDirectory = layout == FormatLayout.CSharpier ? directory : RootDirectory;

		List<FormatStep> steps = [];

		AddLocalTools(steps, facts, layout);

		// The fixes and the IDE style fixes run first, so the layout step lays out the code they write.
		// The body shapes judge the layout's lines, the layout step lays out the bodies they write,
		// and the last pass puts back the blank lines the layout drops.
		// The style pass runs again while it changes a file too, because `dotnet format style` can leave a fix for its next run.
		steps.Add(ForCommands("Analyzers, body shapes aside", allButBodies));
		steps.Add(ForCommands("IDE style fixes", BuildFormatCommands(targets, ["style", "--severity", "info"])) with { Watched = watched });

		// CSharpier fails on the syntax error of a merge marker, so the marker check runs before it and ends the run there.
		if (layout == FormatLayout.CSharpier)
			steps.Add(ForCheck("Merge markers", FormatStepKind.Markers));

		steps.Add(ForCommands(layoutTitle, layoutCommands) with { WorkingDirectory = layoutDirectory });
		steps.Add(ForCommands("Body shapes", BuildFormatCommands(targets, ["analyzers", "--severity", "info", "--diagnostics", .. BodyRules])));

		// The layout step runs again over the bodies the body shapes wrote, so a run that formats no project has none to lay out.
		if (!targets.IsEmpty)
		{
			if (layout == FormatLayout.CSharpier)
				steps.Add(ForCheck("Merge markers", FormatStepKind.Markers));

			steps.Add(ForCommands($"{layoutTitle} again", layoutCommands) with { WorkingDirectory = layoutDirectory });
		}

		steps.Add(ForCommands("Analyzers, body shapes aside, again", allButBodies) with { Watched = watched });
		steps.Add(ForCommands("Markup files", markupCommands) with { WorkingDirectory = directory });
		steps.Add(ForCheck("Merge markers", FormatStepKind.Markers));

		return [.. steps.Where(static step => step.Kind != FormatStepKind.Commands || !step.Commands.IsEmpty)];
	}

	/// <summary>Restores the manifest of the solution's directory, which is the one the tool commands run in.</summary>
	private static void AddLocalTools(List<FormatStep> steps, FormatFacts facts, FormatLayout layout)
	{
		if (layout == FormatLayout.CSharpier || facts.HasMarkup)
			steps.Add(ForCommand("Local tools", "tool", "restore") with { WorkingDirectory = GetSolutionDirectory(facts.Solution) });
	}

	/// <summary>
	/// A whole run lays out the files of the solution's directory, as CSharpier does when it is given that directory.
	/// </summary>
	private static ImmutableArray<ImmutableArray<string>> BuildLayoutCommands(
		FormatOptions options,
		FormatFacts facts,
		FormatLayout layout,
		ImmutableArray<FormatTarget> targets
	)
	{
		if (layout == FormatLayout.None)
			return [];

		if (layout == FormatLayout.Whitespace)
			return BuildFormatCommands(targets, ["whitespace"]);

		if (options.All)
			return ToCommands("csharpier", "format", ".");

		string directory = GetSolutionDirectory(facts.Solution);

		// Each path starts with `./`, as CSharpier reads a name that starts with `@` as a response file.
		return BuildFileCommands(
			["csharpier", "format"],
			facts.ChangedFiles.Where(IsLaidOut).Select(path => ArgumentPath.ToPrefixed(ToDirectoryPath(directory, path)))
		);
	}

	private static ImmutableArray<ImmutableArray<string>> BuildMarkupCommands(FormatOptions options, FormatFacts facts)
	{
		if (!facts.HasMarkup)
			return [];

		if (options.All)
			return ToCommands("stanza-markup", "format", ".");

		string directory = GetSolutionDirectory(facts.Solution);

		IEnumerable<string> markupFiles = facts.ChangedFiles.Where(MarkupFileExtensions.IsMarkup).Select(path => ToDirectoryPath(directory, path));

		return BuildFileCommands(["stanza-markup", "format", "--"], markupFiles);
	}

	/// <summary>Writes a path below the directory relative to it, as a command that runs there reads it.</summary>
	private static string ToDirectoryPath(string directory, string path) => directory == "." ? path : path[(directory.Length + 1)..];

	private static string GetSolutionDirectory(string solution) =>
		RepositoryPath.GetDirectory(solution) is { Length: > 0 } directory ? directory : ".";

	private static bool IsInDirectory(string directory, string path) => directory == "." || RelativePathScope.IsBelow(path, directory);

	private static ImmutableArray<ImmutableArray<string>> BuildFormatCommands(ImmutableArray<FormatTarget> targets, ImmutableArray<string> pass) =>
		[.. targets.Select(target => (ImmutableArray<string>)["format", ArgumentPath.ToProtected(target.Path), .. pass, .. target.Filter])];

	private static ImmutableArray<ImmutableArray<string>> BuildFileCommands(ImmutableArray<string> command, IEnumerable<string> paths) =>
		[.. CommandBatches.Split(paths).Select(batch => (ImmutableArray<string>)[.. command, .. batch])];

	public static bool IsSource(string path) => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

	private static bool IsLaidOut(string path) => LayoutExtensions.Contains(Path.GetExtension(path));

	private static FormatStep ForCommand(string title, params ImmutableArray<string> command) => ForCommands(title, ToCommands(command));

	private static ImmutableArray<ImmutableArray<string>> ToCommands(params ImmutableArray<string> command) => [command];

	private static FormatStep ForRuleCheck(string title, string solution, string pass) =>
		ForVerify(title, FormatStepKind.Diagnostics, "format", ArgumentPath.ToProtected(solution), pass, "--verify-no-changes", "--severity", "info");

	private static FormatStep ForVerify(string title, FormatStepKind kind, params ImmutableArray<string> command)
	{
		return new FormatStep(
			Title: title,
			Kind: kind,
			Commands: [command],
			Watched: [],
			ChangeExitCodes: [DotnetFormatChangeExitCode],
			WorkingDirectory: RootDirectory
		);
	}

	private static FormatStep ForCommands(string title, ImmutableArray<ImmutableArray<string>> commands) =>
		new(Title: title, Kind: FormatStepKind.Commands, Commands: commands, Watched: [], ChangeExitCodes: [], WorkingDirectory: RootDirectory);

	/// <summary>A check that runs a command of its own carries it, so a test reads what the check runs.</summary>
	private static FormatStep ForCheck(string title, FormatStepKind kind, params ImmutableArray<string> command) =>
		new(Title: title, Kind: kind, Commands: command.IsEmpty ? [] : [command], Watched: [], ChangeExitCodes: [], WorkingDirectory: RootDirectory);
}
