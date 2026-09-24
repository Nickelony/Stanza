using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>Guards that the project files, the solution and the package agree.</summary>
[TestClass]
[TestCategory("Repository")]
public sealed partial class ProjectGuardTests
{
	private static readonly HashSet<string> s_unanalyzedProjects = new(StringComparer.Ordinal)
	{
		"Nickelony.Stanza.Analyzers",
		"Nickelony.Stanza.Analyzers.CodeFixes",
		"Nickelony.Stanza.Analyzers.Package",
	};

	private static readonly string[] s_attachedProjects =
	[
		"Nickelony.Stanza.Analyzers",
		"Nickelony.Stanza.Analyzers.CodeFixes",
	];

	[GeneratedRegex("<Project Path=\"(?<path>[^\"]+)\"", RegexOptions.CultureInvariant)]
	private static partial Regex SolutionProject();

	[TestMethod]
	public async Task EveryProjectButTheAnalyzersOwnRunsTheAnalyzerAndItsFixes()
	{
		string[] projects =
		[
			.. SolutionProject()
				.Matches(File.ReadAllText(RepositoryPaths.Below("Nickelony.Stanza.slnx")))
				.Select(match => match.Groups["path"].Value),
		];

		string[][] attached = await Task.WhenAll(projects.Select(ReadAttachedAnalyzersAsync));

		for (int index = 0; index < projects.Length; index++)
		{
			string name = Path.GetFileNameWithoutExtension(projects[index]);
			string[] expected = s_unanalyzedProjects.Contains(name) ? [] : s_attachedProjects;

			Assert.AreSequenceEqual(
				expected,
				attached[index],
				$"{name} does not attach the analyzer and its fixes as Directory.Build.props does."
			);
		}
	}

	// The self-check runs the analyzers over their own sources, so a source it leaves out is one they never judge.
	[TestMethod]
	public async Task TheSelfCheckCompilesEachSourceOfTheAnalyzersAndTheirFixes()
	{
		string[] compiled = await ReadCompiledFilesAsync(
			Path.Combine(
				"tests",
				"Nickelony.Stanza.Analyzers.SelfCheck",
				"Nickelony.Stanza.Analyzers.SelfCheck.csproj"
			)
		);

		foreach (
			string project in new[]
			{
				"Nickelony.Stanza.Analyzers",
				"Nickelony.Stanza.Analyzers.CodeFixes",
				"Nickelony.Stanza.Markup.Core",
			}
		)
		{
			string directory = RepositoryPaths.Below("src", project);

			// The analyzer compiles the markup engine but the formatter only the tools run, so the self-check does too.
			string formatting = Path.Combine(directory, "Formatting") + Path.DirectorySeparatorChar;

			string[] missing =
			[
				.. RepositoryFiles
					.Under(directory, "*.cs")
					.Where(source =>
						!source.StartsWith(formatting, StringComparison.OrdinalIgnoreCase)
						&& !compiled.Contains(source, StringComparer.OrdinalIgnoreCase)
					),
			];

			Assert.IsNotEmpty(
				compiled,
				"The self-check compiles no file, so the check below would pass vacuously."
			);

			GuardAssert.None(missing, $"The self-check leaves out sources of {project}");
		}
	}

	// A loop that never ends in an analyzer would run to the job's limit, since the token of a harness run is only read.
	[TestMethod]
	public void EachTestProjectRunsUnderTheSharedTestTimeout()
	{
		string[] projects =
		[
			.. RepositoryFiles
				.Under(RepositoryPaths.Below("tests"), "*.csproj")
				.Where(project =>
					XDocument
						.Load(project)
						.Descendants("IsTestProject")
						.Any(element => element.Value.Trim() == "true")
				),
		];

		Assert.IsNotEmpty(
			projects,
			"No test project is found, so the check below would pass vacuously."
		);

		foreach (string project in projects)
		{
			Assert.AreEqual(
				"$(MSBuildProjectDirectory)/../test.runsettings",

				ReadBuildProperty(
					XDocument.Load(project),
					Path.GetFileName(project),
					"RunSettingsFilePath"
				),

				$"{Path.GetFileName(project)} does not run under the shared settings."
			);
		}

		var settings = XDocument.Load(RepositoryPaths.Below("tests", "test.runsettings"));

		bool isMilliseconds = int.TryParse(
			s: settings.Descendants("TestTimeout").Single().Value,
			style: System.Globalization.NumberStyles.None,
			provider: System.Globalization.CultureInfo.InvariantCulture,
			result: out int timeout
		);

		// Ten minutes is under any job limit, and a timeout of zero would stop no test.
		Assert.IsTrue(
			isMilliseconds && timeout is > 0 and <= 600_000,
			$"TestTimeout is {timeout}."
		);
	}

	[TestMethod]
	public void TheVersionIsDeclaredOnceAsALiteral()
	{
		var properties = XDocument.Load(RepositoryPaths.Below("Directory.Build.props"));
		var targets = XDocument.Load(RepositoryPaths.Below("Directory.Build.targets"));

		foreach (string name in new[] { "Version", "AssemblyVersion" })
		{
			Assert.DoesNotContain(
				"$(",
				ReadBuildProperty(properties, "Directory.Build.props", name),
				$"{name} is not a literal version."
			);

			Assert.IsEmpty(
				targets.Descendants(name),
				$"Directory.Build.targets sets {name} a second time."
			);
		}
	}

	[TestMethod]
	public void TheSolutionListsExactlyTheProjectsOfThisRepository()
	{
		string[] projects =
		[
			.. SolutionProject()
				.Matches(File.ReadAllText(RepositoryPaths.Below("Nickelony.Stanza.slnx")))
				.Select(match => match.Groups["path"].Value)
				.OrderBy(path => path, StringComparer.Ordinal),
		];

		string[] files =
		[
			.. RepositoryFiles
				.Under(RepositoryPaths.Root(), "*.csproj")
				.Select(file =>
					Path.GetRelativePath(RepositoryPaths.Root(), file).Replace('\\', '/')
				)
				.OrderBy(path => path, StringComparer.Ordinal),
		];

		Assert.AreSequenceEqual(
			files,
			projects,
			"The solution does not list every project file of this repository."
		);
	}

	[TestMethod]
	public void TheRoslynVersionFloorMatchesWhatTheAnalyzerReadmeAdvertises()
	{
		var packages = XDocument.Load(RepositoryPaths.Below("Directory.Packages.props"));

		// The analyzers build against the version the unconditioned property names; a test leg may raise it.
		string version = packages
			.Descendants("StanzaRoslynVersion")
			.Single(element => element.Parent?.Attribute("Condition") is null)
			.Value.Trim();

		string[] parts = version.Split('.');

		Assert.IsGreaterThanOrEqualTo(2, parts.Length, $"'{version}' is not a version.");

		string floor = $"{parts[0]}.{parts[1]}";

		string readme = RepositoryPaths.Below("src", "Nickelony.Stanza.Analyzers", "README.md");

		Assert.Contains(
			$"Roslyn {floor}",
			File.ReadAllText(readme),
			"The analyzer readme does not advertise the Roslyn floor the package is built for."
		);
	}

	private static string ReadBuildProperty(XDocument document, string file, string name)
	{
		XElement[] elements = [.. document.Descendants(name)];

		Assert.HasCount(1, elements, $"{file} does not declare {name} exactly once.");

		return elements[0].Value.Trim();
	}

	private static async Task<string[]> ReadCompiledFilesAsync(string project)
	{
		using JsonDocument document = await EvaluateAsync(project, "Compile");

		return
		[
			.. document
				.RootElement.GetProperty("Items")
				.GetProperty("Compile")
				.EnumerateArray()
				.Select(item => item.GetProperty("FullPath").GetString() ?? string.Empty),
		];
	}

	private static async Task<JsonDocument> EvaluateAsync(string project, string item)
	{
		ProcessStartInfo start = new(DotnetHost.Executable)
		{
			WorkingDirectory = RepositoryPaths.Root(),
			RedirectStandardError = true,
			RedirectStandardOutput = true,
			UseShellExecute = false,
		};

		start.ArgumentList.Add("msbuild");
		start.ArgumentList.Add(project);
		start.ArgumentList.Add($"-getItem:{item}");

		RunResult result = await ChildProcess.RunAsync(start, $"dotnet msbuild {project}");
		Assert.AreEqual(0, result.ExitCode, result.Error + result.Output);

		return JsonDocument.Parse(result.Output);
	}

	// The evaluated items, because Directory.Build.props attaches the pair and a project can only update it.
	private static async Task<string[]> ReadAttachedAnalyzersAsync(string project)
	{
		using JsonDocument document = await EvaluateAsync(project, "ProjectReference");

		if (
			!document
				.RootElement.GetProperty("Items")
				.TryGetProperty("ProjectReference", out JsonElement references)
		)
		{
			return [];
		}

		return
		[
			.. references
				.EnumerateArray()
				.Where(reference =>
					reference.GetProperty("OutputItemType").GetString() == "Analyzer"
				)
				.Select(reference => reference.GetProperty("Filename").GetString() ?? string.Empty)
				.Order(StringComparer.Ordinal),
		];
	}
}
