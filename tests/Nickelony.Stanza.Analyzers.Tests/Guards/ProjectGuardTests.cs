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

namespace Nickelony.Stanza.Analyzers.Tests.Guards;

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

	private const string AttachedCondition = "'$(StanzaRunAnalyzers)' != 'false'";

	// What Directory.Build.props attaches, in its order, to each project that does not opt out.
	private static readonly string[] s_attachedProperties =
	[
		"$(StanzaAnalyzerProject)",
		"$(StanzaFixProject)",
	];

	// A run of the whole suite evaluates projects beside the rest of the tests, so a slow machine needs minutes, and the runner's own timeout is five.
	private static readonly TimeSpan s_evaluationLimit = TimeSpan.FromMinutes(4);

	[GeneratedRegex("<Project Path=\"(?<path>[^\"]+)\"", RegexOptions.CultureInvariant)]
	private static partial Regex SolutionProject();

	// The props file is the one place that attaches the pair, so reading it and each project file stands for evaluating every project.
	[TestMethod]
	public void EveryProjectButTheAnalyzersOwnRunsTheAnalyzerAndItsFixes()
	{
		var projects = SolutionProject()
			.Matches(File.ReadAllText(RepositoryPaths.Below("Nickelony.Stanza.slnx")))
			.Select(match => match.Groups["path"].Value)
			.ToDictionary(
				path => Path.GetFileNameWithoutExtension(path),
				path => XDocument.Load(RepositoryPaths.Below(path.Split('/'))),
				StringComparer.Ordinal
			);

		// MSBuild reads only the nearest Directory.Build.props, so a nested one has to import the one above it.
		var nested = RepositoryFiles
			.Under(RepositoryPaths.Root(), "Directory.Build.props")
			.Where(file => Path.GetDirectoryName(file) != RepositoryPaths.Root())
			.ToDictionary(
				file => Path.GetRelativePath(RepositoryPaths.Root(), file).Replace('\\', '/'),
				XDocument.Load,
				StringComparer.Ordinal
			);

		var props = XDocument.Load(RepositoryPaths.Below("Directory.Build.props"));

		GuardAssert.None(
			FindAttachmentProblems(props, nested, projects),
			"The analyzer and its fixes are not attached as Directory.Build.props states"
		);
	}

	[TestMethod]
	public void AProjectThatOptsOutOrInAgainstItsPlaceIsFound()
	{
		Assert.IsEmpty(FindAttachmentProblems(SoundProps(), SoundNested(), SoundProjects()));

		var flipped = new Dictionary<string, XDocument>
		{
			["Nickelony.Stanza.Analyzers"] = XDocument.Parse("<Project />"),
			["Nickelony.Stanza.Markup"] = XDocument.Parse(OptOut),
		};

		Assert.HasCount(2, FindAttachmentProblems(SoundProps(), SoundNested(), flipped));
	}

	[TestMethod]
	public void AnAttachmentThatSendsAReferenceNowhereOrOutOfOrderIsFound()
	{
		var noOutput = XDocument.Parse(
			"""
			<Project>
				<ItemGroup Condition="'$(StanzaRunAnalyzers)' != 'false'">
					<ProjectReference Include="$(StanzaAnalyzerProject)" OutputItemType="Analyzer" />
					<ProjectReference Include="$(StanzaFixProject)" />
				</ItemGroup>
			</Project>
			"""
		);

		var unconditional = XDocument.Parse(
			"""
			<Project>
				<ItemGroup>
					<ProjectReference Include="$(StanzaAnalyzerProject)" OutputItemType="Analyzer" />
					<ProjectReference Include="$(StanzaFixProject)" OutputItemType="Analyzer" />
				</ItemGroup>
			</Project>
			"""
		);

		Assert.ContainsSingle(FindAttachmentProblems(noOutput, SoundNested(), SoundProjects()));

		Assert.ContainsSingle(
			FindAttachmentProblems(unconditional, SoundNested(), SoundProjects())
		);
	}

	[TestMethod]
	public void ANestedPropsFileThatDoesNotImportTheOneAboveIsFound()
	{
		var nested = new Dictionary<string, XDocument>
		{
			["tests/Directory.Build.props"] = XDocument.Parse("<Project />"),
		};

		Assert.ContainsSingle(FindAttachmentProblems(SoundProps(), nested, SoundProjects()));
	}

	[TestMethod]
	public void AProjectThatRemovesOrRedirectsAReferenceOfThePairIsFound()
	{
		var removing = XDocument.Parse(
			"""<Project><ItemGroup><ProjectReference Remove="$(StanzaFixProject)" /></ItemGroup></Project>"""
		);

		var redirecting = XDocument.Parse(
			"""<Project><ItemGroup><ProjectReference Update="$(StanzaFixProject)" OutputItemType="None" /></ItemGroup></Project>"""
		);

		var projects = new Dictionary<string, XDocument>
		{
			["Nickelony.Stanza.Markup"] = removing,
			["Nickelony.Stanza.Format"] = redirecting,
		};

		Assert.HasCount(2, FindAttachmentProblems(SoundProps(), SoundNested(), projects));
	}

	private const string OptOut =
		"<Project><PropertyGroup><StanzaRunAnalyzers>false</StanzaRunAnalyzers></PropertyGroup></Project>";

	private static XDocument SoundProps()
	{
		return XDocument.Parse(
			"""
			<Project>
				<ItemGroup Condition="'$(StanzaRunAnalyzers)' != 'false'">
					<ProjectReference Include="$(StanzaAnalyzerProject)" OutputItemType="Analyzer" />
					<ProjectReference Include="$(StanzaFixProject)" OutputItemType="Analyzer" />
				</ItemGroup>
			</Project>
			"""
		);
	}

	private static Dictionary<string, XDocument> SoundNested()
	{
		return new()
		{
			["tests/Directory.Build.props"] = XDocument.Parse(
				"""<Project><Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '../'))" /></Project>"""
			),
		};
	}

	private static Dictionary<string, XDocument> SoundProjects()
	{
		return new()
		{
			["Nickelony.Stanza.Analyzers"] = XDocument.Parse(OptOut),
			["Nickelony.Stanza.Markup"] = XDocument.Parse("<Project />"),
		};
	}

	// Each project gets the pair from the props file unless it sets StanzaRunAnalyzers to false,
	// and no project file takes a reference of the pair away or changes where it goes.
	private static List<string> FindAttachmentProblems(
		XDocument props,
		IReadOnlyDictionary<string, XDocument> nestedProps,
		IReadOnlyDictionary<string, XDocument> projects
	)
	{
		List<string> problems = [];

		XElement[] attached =
		[
			.. props
				.Descendants("ItemGroup")
				.Where(group => (string?)group.Attribute("Condition") == AttachedCondition)
				.Elements("ProjectReference"),
		];

		if (!attached.Select(GetTarget).SequenceEqual(s_attachedProperties))
		{
			problems.Add(
				$"Directory.Build.props does not attach {string.Join(" then ", s_attachedProperties)} where StanzaRunAnalyzers is not false."
			);
		}

		foreach (XElement reference in attached.Where(reference => !IsAnalyzer(reference)))
			problems.Add($"Directory.Build.props attaches {GetTarget(reference)} as no analyzer.");

		foreach ((string path, XDocument document) in nestedProps)
		{
			if (!document.Descendants("Import").Any(ImportsTheFileAbove))
				problems.Add($"{path} does not import the Directory.Build.props above it.");
		}

		foreach ((string name, XDocument document) in projects)
		{
			bool optsOut = document
				.Descendants("StanzaRunAnalyzers")
				.Any(property => property.Value.Trim() == "false");

			if (optsOut != s_unanalyzedProjects.Contains(name))
			{
				problems.Add(
					$"{name} opts {(optsOut ? "out of" : "into")} the analyzer and its fixes against its place."
				);
			}

			foreach (XElement reference in document.Descendants("ProjectReference"))
			{
				if (reference.Attribute("Remove") is not null)
				{
					problems.Add(
						$"{name} removes a project reference, which could take the pair away."
					);
				}

				if (
					s_attachedProperties.Contains(GetTarget(reference))
					&& reference.Attribute("OutputItemType") is not null
				)
				{
					problems.Add(
						$"{name} changes where the reference {GetTarget(reference)} goes."
					);
				}
			}
		}

		return problems;
	}

	private static bool IsAnalyzer(XElement reference) =>
		(string?)reference.Attribute("OutputItemType") == "Analyzer";

	private static string? GetTarget(XElement reference)
	{
		XAttribute? target = reference.Attribute("Include") ?? reference.Attribute("Update");
		return target?.Value;
	}

	private static bool ImportsTheFileAbove(XElement import)
	{
		string project = (string?)import.Attribute("Project") ?? string.Empty;

		return project.Contains(
			"GetPathOfFileAbove('Directory.Build.props'",
			StringComparison.Ordinal
		);
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
	public async Task EachTestProjectRunsUnderTheSharedTestTimeout()
	{
		string[] projects = [.. RepositoryFiles.Under(RepositoryPaths.Below("tests"), "*.csproj")];

		string shared = Path.GetFullPath(RepositoryPaths.Below("tests", "test.runsettings"));
		int testProjects = 0;

		foreach (string project in projects)
		{
			string name = Path.GetFileNameWithoutExtension(project);
			bool isNamedForTests = name.EndsWith(".Tests", StringComparison.Ordinal);

			using JsonDocument document = await EvaluateAsync(
				project,
				"-getProperty:IsTestProject,RunSettingsFilePath"
			);

			JsonElement properties = document.RootElement.GetProperty("Properties");

			Assert.AreEqual(
				isNamedForTests,
				properties.GetProperty("IsTestProject").GetString() == "true",
				$"{name} is a test project exactly when its name ends in .Tests."
			);

			if (isNamedForTests)
			{
				testProjects++;

				Assert.AreEqual(
					shared,
					Path.GetFullPath(properties.GetProperty("RunSettingsFilePath").GetString()!),
					$"{name} does not run under the shared settings."
				);
			}
		}

		Assert.IsGreaterThan(
			0,
			testProjects,
			"No test project is found, so the check above would pass vacuously."
		);

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

	// The default of five minutes would stop a soak of many seeds, so each soak test states the timeout it runs under.
	[TestMethod]
	public void EachSoakTestStatesItsTimeoutInsteadOfTakingTheDefaultOfFiveMinutes() =>
		Assert.IsEmpty(SoakRun.FindUntimed(typeof(ProjectGuardTests).Assembly));

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

	// An npm tarball and a Python wheel carry the LICENSE at the root of their package.
	[TestMethod]
	public void EachPackageOfAnotherEcosystemCarriesTheLicenseOfTheRepositoryByteForByte()
	{
		byte[] license = File.ReadAllBytes(RepositoryPaths.Below("LICENSE"));

		foreach (string package in new[] { "js", Path.Combine("js", "stylelint-config"), "py" })
		{
			string path = RepositoryPaths.Below(package, "LICENSE");

			Assert.IsTrue(File.Exists(path), $"{package} has no LICENSE.");

			Assert.IsTrue(
				license.AsSpan().SequenceEqual(File.ReadAllBytes(path)),
				$"{package}/LICENSE differs from the LICENSE of the repository."
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
		using JsonDocument document = await EvaluateAsync(project, "-getItem:Compile");

		return
		[
			.. document
				.RootElement.GetProperty("Items")
				.GetProperty("Compile")
				.EnumerateArray()
				.Select(item => item.GetProperty("FullPath").GetString() ?? string.Empty),
		];
	}

	private static async Task<JsonDocument> EvaluateAsync(string project, string selection)
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
		start.ArgumentList.Add(selection);

		RunResult result = await ChildProcess.RunAsync(
			start,
			$"dotnet msbuild {project}",
			s_evaluationLimit
		);

		Assert.AreEqual(0, result.ExitCode, result.Error + result.Output);

		return JsonDocument.Parse(result.Output);
	}
}
