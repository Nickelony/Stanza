using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Format.Planning;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Format.Tests;

[TestClass]
public sealed class FormatPlannerTests
{
	private const string Bodies = "ZA0039 ZA0040 ZA0041 ZA0042 ZA0043 ZA0044 ZA0045";

	private static readonly FormatOptions s_changed = new(
		All: false,
		Check: false,
		Path: ".",
		Layout: null
	);

	private static readonly FormatOptions s_all = s_changed with { All = true };
	private static readonly FormatOptions s_check = s_changed with { Check = true };

	[TestMethod]
	public void AWholeRunFollowsTheRunOrder()
	{
		FormatFacts facts = BuildFacts(compiled: [new("src/A/A.cs", "src/A/A.csproj", "net8.0|")]);
		ImmutableArray<FormatStep> steps = Plan(s_all, facts);

		Assert.AreSequenceEqual(
			[
				"Local tools: tool restore",
				$"Analyzers, body shapes aside: format App.slnx analyzers --severity info --exclude-diagnostics {Bodies}",
				"IDE style fixes: format App.slnx style --severity info",
				"Merge markers",
				"CSharpier: csharpier format .",
				$"Body shapes: format App.slnx analyzers --severity info --diagnostics {Bodies}",
				"Merge markers",
				"CSharpier again: csharpier format .",
				$"Analyzers, body shapes aside, again: format App.slnx analyzers --severity info --exclude-diagnostics {Bodies}",
				"Merge markers",
			],

			Show(steps)
		);

		Assert.AreSequenceEqual(["src/A/A.cs"], steps[2].Watched);
		Assert.AreSequenceEqual(["src/A/A.cs"], steps[^2].Watched);
	}

	[TestMethod]
	public void AMergeMarkerIsCheckedBeforeEachCSharpierStepThatFollowsDotnetFormat()
	{
		FormatFacts facts = BuildFacts(compiled: [new("src/A/A.cs", "src/A/A.csproj", "net8.0|")]);
		string[] titles = [.. Plan(s_all, facts).Select(static step => step.Title)];

		// CSharpier fails on the syntax error a marker leaves, so no later step could report the marker.
		for (int index = 1; index < titles.Length; index++)
		{
			if (titles[index].StartsWith("CSharpier", StringComparison.Ordinal))
				Assert.AreEqual("Merge markers", titles[index - 1], titles[index]);
		}

		Assert.AreEqual("Merge markers", titles[^1]);
	}

	[TestMethod]
	[TestCategory("Repository")]
	public void TheBodyRulesAreTheOnesTheRunOrderOfTheFormatterPageNames()
	{
		string page = File.ReadAllText(RepositoryPaths.Below("docs", "Working-with-formatters.md"));

		string named = page.ReplaceLineEndings("\n")
			.Split('\n')
			.First(line => line.StartsWith("dotnet format analyzers", StringComparison.Ordinal))
			.Split("--exclude-diagnostics ")[1];

		Assert.AreEqual(named, string.Join(' ', FormatPlanner.BodyRules));
	}

	[TestMethod]
	[TestCategory("Repository")]
	public void TheChangedFilesLaidOutAreTheOnesThePinnedCSharpierFormats()
	{
		// Each file holds text CSharpier would change, so its check names every extension it formats.
		string[] candidates =
		[
			".cs",
			".csx",
			".xaml",
			".axaml",
			".xml",
			".config",
			".csproj",
			".props",
			".targets",
			".slnx",
			".vbproj",
			".nuspec",
			".resx",
			".json",
		];

		using TempDirectory directory = new(editorConfig: false);

		foreach (string extension in candidates)
		{
			File.WriteAllText(
				Path.Combine(directory.Root, $"f{extension}"),
				extension is ".cs" or ".csx" ? "class   C{}\n" : "<a   b=\"1\"><c/></a>\n"
			);
		}

		ProcessStartInfo start = new(DotnetHost.Executable)
		{
			WorkingDirectory = RepositoryPaths.Root(),
			RedirectStandardError = true,
			RedirectStandardOutput = true,
			UseShellExecute = false,
		};

		foreach (string argument in (string[])["tool", "run", "csharpier", "check", directory.Root])
			start.ArgumentList.Add(argument);

		RunResult result = ChildProcess.Run(start, "dotnet tool run csharpier check");
		string report = result.Output + result.Error;

		Assert.AreSequenceEqual(
			[.. FormatPlanner.LayoutExtensions.Order(StringComparer.Ordinal)],

			[
				.. candidates
					.Where(extension =>
						report.Contains(
							$"f{extension} - Was not formatted",
							StringComparison.Ordinal
						)
					)
					.Order(StringComparer.Ordinal),
			],

			report
		);
	}

	[TestMethod]
	public void WithoutCSharpierTheWhitespacePassOfDotnetFormatLaysOutTheCode()
	{
		FormatFacts facts = BuildFacts();

		Assert.AreEqual(
			FormatLayout.Whitespace,
			FormatPlanner.ResolveLayout(s_all, hasCSharpier: false)
		);

		Assert.AreSequenceEqual(
			[
				$"Analyzers, body shapes aside: format App.slnx analyzers --severity info --exclude-diagnostics {Bodies}",
				"IDE style fixes: format App.slnx style --severity info",
				"Whitespace: format App.slnx whitespace",
				$"Body shapes: format App.slnx analyzers --severity info --diagnostics {Bodies}",
				"Whitespace again: format App.slnx whitespace",
				$"Analyzers, body shapes aside, again: format App.slnx analyzers --severity info --exclude-diagnostics {Bodies}",
				"Merge markers",
			],

			Show(Plan(s_all, facts, hasCSharpier: false))
		);
	}

	[TestMethod]
	public void WithNoLayoutTheRulePassesRunAlone()
	{
		FormatOptions options = s_all with { Layout = FormatLayout.None };

		Assert.AreSequenceEqual(
			[
				"Analyzers, body shapes aside",
				"IDE style fixes",
				"Body shapes",
				"Analyzers, body shapes aside, again",
				"Merge markers",
			],

			[.. Plan(options, BuildFacts()).Select(static step => step.Title)]
		);
	}

	[TestMethod]
	public void ACheckReadsTheWholeSolutionWithoutWriting()
	{
		FormatFacts facts = BuildFacts(hasMarkup: true);

		Assert.AreSequenceEqual(
			[
				"Local tools: tool restore",
				"Analyzers: format App.slnx analyzers --verify-no-changes --severity info",
				"IDE style rules: format App.slnx style --verify-no-changes --severity info",
				"Source layout (CSharpier, blank lines aside): csharpier check . --log-format MsBuild",
				"Markup files: stanza-markup check . --strict",
				"Line endings",
				"Merge markers",
			],

			Show(Plan(s_check, facts))
		);
	}

	[TestMethod]
	public void AWholeRunLaysOutTheDirectoryOfASolutionBelowTheRoot()
	{
		FormatFacts facts = BuildFacts(solution: "src/App.slnx", hasMarkup: true);
		string[] shown = Show(Plan(s_all, facts));

		Assert.Contains("CSharpier: csharpier format .", shown);
		Assert.Contains("Markup files: stanza-markup format .", shown);
	}

	[TestMethod]
	public void AFileOfTwoViewsIsHeldOutOfTheSolutionAndFormattedThroughEachView()
	{
		FormatFacts facts = BuildFacts(
			compiled:
			[
				new("src/Wpf/View.cs", "src/Wpf/Wpf.csproj", "net8.0|WPF"),
				new("shared/Editor.cs", "src/Wpf/Wpf.csproj", "net8.0|WPF"),
				new("shared/Editor.cs", "src/Avalonia/Avalonia.csproj", "net8.0|AVALONIA"),
				new("shared/Editor.cs", "tests/Avalonia/Tests.csproj", "net8.0|AVALONIA"),
			]
		);

		Assert.AreSequenceEqual(
			[
				"format App.slnx style --severity info --exclude ./shared",
				"format src/Wpf/Wpf.csproj style --severity info",
				"format src/Avalonia/Avalonia.csproj style --severity info",
			],

			ShowCommands(Plan(s_all, facts), "IDE style fixes")
		);
	}

	[TestMethod]
	public void ADirectoryThatHoldsAFileOfOneViewIsNotExcludedWhole()
	{
		FormatFacts facts = BuildFacts(
			compiled:
			[
				new("shared/Own.cs", "src/A/A.csproj", "net8.0|A"),
				new("shared/Both.cs", "src/A/A.csproj", "net8.0|A"),
				new("shared/Both.cs", "src/B/B.csproj", "net8.0|B"),
			]
		);

		Assert.AreSequenceEqual(
			[
				"format App.slnx style --severity info --exclude ./shared/Both.cs",
				"format src/A/A.csproj style --severity info",
				"format src/B/B.csproj style --severity info",
			],

			ShowCommands(Plan(s_all, facts), "IDE style fixes")
		);
	}

	[TestMethod]
	public void ProjectsOfOneViewShareTheSolutionPass()
	{
		FormatFacts facts = BuildFacts(
			compiled:
			[
				new("tests/Shared/Paths.cs", "tests/A/A.csproj", "net8.0|"),
				new("tests/Shared/Paths.cs", "tests/B/B.csproj", "net8.0|"),
			]
		);

		Assert.AreSequenceEqual(
			["format App.slnx style --severity info"],
			ShowCommands(Plan(s_all, facts), "IDE style fixes")
		);
	}

	[TestMethod]
	public void AnExclusionListTooLongForOneCommandFormatsEachProjectInTurn()
	{
		string directory = new('f', 200);

		CompiledFile[] compiled =
		[
			.. Enumerable
				.Range(0, 200)
				.SelectMany(index =>
					new CompiledFile[]
					{
						new($"{directory}{index}/Own.cs", "src/A/A.csproj", "net8.0|A"),
						new($"{directory}{index}/Both.cs", "src/A/A.csproj", "net8.0|A"),
						new($"{directory}{index}/Both.cs", "src/B/B.csproj", "net8.0|B"),
					}
				),
		];

		Assert.AreSequenceEqual(
			[
				"format src/A/A.csproj style --severity info",
				"format src/B/B.csproj style --severity info",
			],

			ShowCommands(Plan(s_all, BuildFacts(compiled: compiled)), "IDE style fixes")
		);
	}

	[TestMethod]
	public void AChangedFileOfOneViewGoesThroughTheSolutionAndAFileOfTwoThroughEachView()
	{
		FormatFacts facts = BuildFacts(
			changed: ["shared/Editor.cs", "src/Wpf/View.cs"],
			// The second project of the view stands for one that loads rules the first does not,
			// as a self-check project does.

			compiled:
			[
				new("src/Wpf/View.cs", "src/Wpf/Wpf.csproj", "net8.0|WPF"),
				new("src/Wpf/View.cs", "tests/Wpf/SelfCheck.csproj", "net8.0|WPF"),
				new("shared/Editor.cs", "src/Wpf/Wpf.csproj", "net8.0|WPF"),
				new("shared/Editor.cs", "src/Avalonia/Avalonia.csproj", "net8.0|AVALONIA"),
				new("shared/Editor.cs", "tests/Avalonia/Tests.csproj", "net8.0|AVALONIA"),
			]
		);

		ImmutableArray<FormatStep> steps = Plan(s_changed, facts);

		Assert.AreSequenceEqual(
			[
				"format App.slnx style --severity info --include ./src/Wpf/View.cs",
				"format src/Wpf/Wpf.csproj style --severity info --include ./shared/Editor.cs",
				"format src/Avalonia/Avalonia.csproj style --severity info --include ./shared/Editor.cs",
			],

			ShowCommands(steps, "IDE style fixes")
		);

		Assert.AreSequenceEqual(
			["csharpier format ./shared/Editor.cs ./src/Wpf/View.cs"],
			ShowCommands(steps, "CSharpier")
		);
	}

	[TestMethod]
	public void AChangedSourceNoProjectCompilesIsLaidOutAlone()
	{
		FormatFacts facts = BuildFacts(changed: ["tools/Script.cs", "App.slnx", "README.md"]);

		Assert.AreSequenceEqual(
			[
				"Local tools: tool restore",
				"Merge markers",
				"CSharpier: csharpier format ./tools/Script.cs ./App.slnx",
				"Merge markers",
			],

			Show(Plan(s_changed, facts))
		);
	}

	// The second layout step lays out what the body shapes wrote, and a run that formats no project writes none.
	[TestMethod]
	public void AChangedProjectAndMarkupFileLaysOutOnceBecauseNoBodyShapeRuns()
	{
		FormatFacts facts = BuildFacts(
			hasMarkup: true,
			changed: ["App.csproj", "docs/a.md", "ui/A.xaml"]
		);

		Assert.AreSequenceEqual(
			[
				"Local tools: tool restore",
				"Merge markers",
				"CSharpier: csharpier format ./App.csproj ./ui/A.xaml",
				"Markup files: stanza-markup format -- App.csproj ui/A.xaml",
				"Merge markers",
			],

			Show(Plan(s_changed, facts))
		);
	}

	[TestMethod]
	public void AChangedSourceTheProjectsCompileLaysOutTwice()
	{
		FormatFacts facts = BuildFacts(
			changed: ["src/A/A.cs"],
			compiled: [new("src/A/A.cs", "src/A/A.csproj", "net8.0|")]
		);

		Assert.AreSequenceEqual(
			[
				"Local tools: tool restore",
				$"Analyzers, body shapes aside: format App.slnx analyzers --severity info --exclude-diagnostics {Bodies} --include ./src/A/A.cs",
				"IDE style fixes: format App.slnx style --severity info --include ./src/A/A.cs",
				"Merge markers",
				"CSharpier: csharpier format ./src/A/A.cs",
				$"Body shapes: format App.slnx analyzers --severity info --diagnostics {Bodies} --include ./src/A/A.cs",
				"Merge markers",
				"CSharpier again: csharpier format ./src/A/A.cs",
				$"Analyzers, body shapes aside, again: format App.slnx analyzers --severity info --exclude-diagnostics {Bodies} --include ./src/A/A.cs",
				"Merge markers",
			],

			Show(Plan(s_changed, facts))
		);
	}

	[TestMethod]
	public void AChangedMarkupFileGoesToStanzaMarkup()
	{
		FormatFacts facts = BuildFacts(hasMarkup: true, changed: ["App.csproj", "docs/a.md"]);

		Assert.AreSequenceEqual(
			["stanza-markup format -- App.csproj"],
			ShowCommands(Plan(s_changed, facts), "Markup files")
		);
	}

	[TestMethod]
	public void ADefaultRunLaysOutTheSolutionsDirectoryAndFixesACompiledSourceWhereverItLies()
	{
		FormatFacts facts = BuildFacts(
			solution: "app/App.slnx",
			hasMarkup: true,

			changed:
			[
				"app/Tool.cs",
				"app/data.xml",
				"application/B.cs",
				"other/D.cs",
				"other/data.xml",
			],

			compiled:
			[
				new("app/Tool.cs", "app/App.csproj", "net8.0|"),
				new("other/D.cs", "other/Other.csproj", "net8.0|"),
			]
		);

		ImmutableArray<FormatStep> steps = Plan(s_changed, facts);

		Assert.AreSequenceEqual(
			["format app/App.slnx style --severity info --include ./app/Tool.cs ./other/D.cs"],
			ShowCommands(steps, "IDE style fixes")
		);

		Assert.AreSequenceEqual(
			["csharpier format ./Tool.cs ./data.xml"],
			ShowCommands(steps, "CSharpier")
		);

		Assert.AreSequenceEqual(
			["stanza-markup format -- data.xml"],
			ShowCommands(steps, "Markup files")
		);
	}

	[TestMethod]
	public void TheChangedFilesOfADefaultRunAreTheSolutionsDirectoryAndEachCompiledSource()
	{
		Assert.AreSequenceEqual(
			["app/data.xml", "other/D.cs"],

			FormatPlanner.CollectChangedFilesInScope(
				"app/App.slnx",
				["app/data.xml", "other/D.cs", "other/data.xml", "loose/L.cs", "README.md"],
				[new("other/D.cs", "other/Other.csproj", "net8.0|")]
			)
		);
	}

	[TestMethod]
	public void ARunWithNothingChangedPlansNoStep()
	{
		FormatFacts facts = BuildFacts(changed: ["README.md"]);
		Assert.IsEmpty(Plan(s_changed, facts));
	}

	[TestMethod]
	public void ACheckRunsTheMarkupCheckStrictlyAsFormatPs1Does()
	{
		FormatFacts facts = BuildFacts(hasMarkup: true);

		Assert.AreSequenceEqual(
			["stanza-markup check . --strict"],
			ShowCommands(Plan(s_check, facts), "Markup files")
		);

		FormatStep markup = Plan(s_check, facts)
			.Single(static step => step.Title == "Markup files");

		Assert.AreEqual(FormatStepKind.Markup, markup.Kind);
		Assert.AreSequenceEqual([1, 2], markup.ChangeExitCodes);
	}

	[TestMethod]
	public void ANameThatStartsWithAnAtSignIsPassedToCSharpierWithADotSlash()
	{
		FormatFacts facts = BuildFacts(changed: ["@a.cs", "src/@b.cs", "-c.cs"]);

		Assert.AreSequenceEqual(
			["csharpier format ./@a.cs ./src/@b.cs ./-c.cs"],
			ShowCommands(Plan(s_changed, facts), "CSharpier")
		);
	}

	[TestMethod]
	public void ANameThatStartsWithAnAtSignIsPassedToDotnetFormatWithADotSlash()
	{
		CompiledFile[] compiled =
		[
			new("@a.cs", "src/A/A.csproj", "net8.0|A"),
			new("@shared/b.cs", "src/A/A.csproj", "net8.0|A"),
			new("@shared/b.cs", "src/B/B.csproj", "net8.0|B"),
		];

		FormatFacts changed = BuildFacts(changed: ["@a.cs", "@shared/b.cs"], compiled: compiled);

		Assert.AreSequenceEqual(
			[
				"format App.slnx style --severity info --include ./@a.cs",
				"format src/A/A.csproj style --severity info --include ./@shared/b.cs",
				"format src/B/B.csproj style --severity info --include ./@shared/b.cs",
			],

			ShowCommands(Plan(s_changed, changed), "IDE style fixes")
		);

		Assert.AreSequenceEqual(
			[
				"format App.slnx style --severity info --exclude ./@shared",
				"format src/A/A.csproj style --severity info",
				"format src/B/B.csproj style --severity info",
			],

			ShowCommands(Plan(s_all, BuildFacts(compiled: compiled)), "IDE style fixes")
		);

		// The solution and the projects are arguments too, so a name that starts with an at sign takes the dot slash.
		Assert.AreSequenceEqual(
			["format ./@App.slnx style --severity info"],
			ShowCommands(Plan(s_all, BuildFacts(solution: "@App.slnx")), "IDE style fixes")
		);

		Assert.AreSequenceEqual(
			["format ./@App.slnx analyzers --verify-no-changes --severity info"],
			ShowCommands(Plan(s_check, BuildFacts(solution: "@App.slnx")), "Analyzers")
		);
	}

	[TestMethod]
	public void TheToolCommandsRunInTheSolutionsDirectoryAndDotnetFormatRunsInTheRoot()
	{
		FormatFacts facts = BuildFacts(
			solution: "app/App.slnx",
			hasMarkup: true,
			changed: ["app/Tool.cs", "app/data.xml"],
			compiled: [new("app/Tool.cs", "app/App.csproj", "net8.0|")]
		);

		string[] expected =
		[
			"Local tools=app",
			"Analyzers, body shapes aside=.",
			"IDE style fixes=.",
			"Merge markers=.",
			"CSharpier=app",
			"Body shapes=.",
			"Merge markers=.",
			"CSharpier again=app",
			"Analyzers, body shapes aside, again=.",
			"Markup files=app",
			"Merge markers=.",
		];

		string[] directories =
		[
			.. Plan(s_changed, facts)
				.Select(static step => $"{step.Title}={step.WorkingDirectory}"),
		];

		Assert.AreSequenceEqual(expected, directories);

		Assert.AreSequenceEqual(
			[
				"Local tools=app",
				"Source layout (CSharpier, blank lines aside)=app",
				"Markup files=app",
			],

			[
				.. Plan(s_check, facts)
					.Where(static step => step.WorkingDirectory != ".")
					.Select(static step => $"{step.Title}={step.WorkingDirectory}"),
			]
		);
	}

	[TestMethod]
	public void TheWhitespaceLayoutRunsInTheRootLikeTheOtherDotnetFormatSteps()
	{
		FormatFacts facts = BuildFacts(solution: "app/App.slnx");

		Assert.IsEmpty(
			Plan(s_all, facts, hasCSharpier: false)
				.Where(static step => step.WorkingDirectory != ".")
		);
	}

	[TestMethod]
	public void ASourceOutsideTheSolutionsDirectoryThatNoProjectCompilesIsSkipped()
	{
		ImmutableArray<string> changed = ["app/A.cs", "other/B.cs", "other/C.cs", "other/data.xml"];

		Assert.AreSequenceEqual(
			["other/C.cs"],
			FormatPlanner.CollectSkippedSources(changed, inScope: ["app/A.cs", "other/B.cs"])
		);
	}

	[TestMethod]
	public void ALongListOfChangedFilesIsSplitIntoCommandsThatFitTheBudget()
	{
		string name = new('n', 40);

		string[] changed =
		[
			.. Enumerable.Range(0, 1000).Select(index => $"src/A/{name}{index:D4}.cs"),
		];

		CompiledFile[] compiled =
		[
			.. changed.Select(static path => new CompiledFile(path, "src/A/A.csproj", "net8.0|")),
		];

		FormatFacts facts = BuildFacts(changed: changed, compiled: compiled);

		ImmutableArray<ImmutableArray<string>> commands = Plan(s_changed, facts)
			.Single(static step => step.Title == "IDE style fixes")
			.Commands;

		Assert.IsGreaterThan(1, commands.Length);

		foreach (ImmutableArray<string> command in commands)
		{
			Assert.IsLessThanOrEqualTo(
				CommandBatches.ArgumentBudget + 100,
				string.Join(' ', command).Length
			);
		}

		Assert.AreSequenceEqual(
			[.. changed.Select(static path => $"./{path}")],

			[
				.. commands.SelectMany(static command =>
					command.SkipWhile(static argument => argument != "--include").Skip(1)
				),
			]
		);
	}

	private static ImmutableArray<FormatStep> Plan(
		FormatOptions options,
		FormatFacts facts,
		bool hasCSharpier = true
	) => FormatPlanner.Plan(options, facts, FormatPlanner.ResolveLayout(options, hasCSharpier));

	private static FormatFacts BuildFacts(
		string solution = "App.slnx",
		bool hasMarkup = false,
		string[]? changed = null,
		CompiledFile[]? compiled = null
	)
	{
		return new FormatFacts(
			Solution: solution,
			HasMarkup: hasMarkup,
			ChangedFiles: [.. changed ?? []],
			SkippedSources: [],
			CompiledFiles: [.. compiled ?? []]
		);
	}

	private static string[] Show(ImmutableArray<FormatStep> steps)
	{
		return
		[
			.. steps.SelectMany(static step =>
				step.Commands.IsEmpty
					? [step.Title]
					: step.Commands.Select(command => $"{step.Title}: {string.Join(' ', command)}")
			),
		];
	}

	private static string[] ShowCommands(ImmutableArray<FormatStep> steps, string title)
	{
		return
		[
			.. steps
				.Single(step => step.Title == title)
				.Commands.Select(static command => string.Join(' ', command)),
		];
	}
}
