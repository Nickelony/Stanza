using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Guards;

/// <summary>Packs the solution and reads each package, as a consumer installs it.</summary>
[TestClass]
[TestCategory("Repository")]
public sealed class PackageLayoutGuardTests
{
	private const string Analyzers = "Nickelony.Stanza.Analyzers";
	private const string Markup = "Nickelony.Stanza.Markup";
	private const string Format = "Nickelony.Stanza.Format";
	private const string RepositoryUrl = "https://github.com/Nickelony/Stanza";

	[TestMethod]
	public async Task PackingTheSolutionWritesEachPackageWithItsFilesAndNothingElse()
	{
#if STANZA_NEWEST_ROSLYN
		ExpectedSkip.Skip(
			"This leg builds the analyzer tests alone, so the leg that builds the solution packs it."
		);
#endif

		using TempDirectory directory = new(editorConfig: false);
		string output = directory.Root;

		await PackAsync(output);

		string[] packages =
		[
			.. Directory.GetFiles(output, "*.nupkg").Order(StringComparer.Ordinal),
		];

		string[] ids = ReadPackageIds();

		Assert.AreEqual(
			ids.Length,
			packages.Length,
			$"The pack wrote {string.Join(", ", packages.Select(Path.GetFileName))}."
		);

		Dictionary<string, string[]> expected = ExpectedPayloads();

		Assert.AreSequenceEqual(
			ids,
			[.. expected.Keys.Order(StringComparer.Ordinal)],
			"package-ids.txt and the payloads below name different packages."
		);

		foreach ((string id, string[] files) in expected)
		{
			string package = Assert.ContainsSingle(packages.Where(path => IsPackageOf(path, id)));

			using ZipArchive archive = ZipFile.OpenRead(package);

			string[] payload =
			[
				.. archive
					.Entries.Select(entry => entry.FullName)
					.Where(name => !IsBookkeeping(name))
					.Order(StringComparer.Ordinal),
			];

			Assert.AreSequenceEqual(
				[.. files.Order(StringComparer.Ordinal)],
				payload,
				$"{Path.GetFileName(package)} does not carry exactly the files it promises."
			);

			AssertNuspec(archive, id);
		}

		// The PDBs ship in each package, so a symbol package would repeat them.
		Assert.IsEmpty(Directory.GetFiles(output, "*.snupkg"), "The pack wrote a symbol package.");
	}

	// The tests ran from a build of one configuration, and the pack reads that build's output.
	private static async Task PackAsync(string output)
	{
		string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;

		RunResult result = await ChildBuild.RunAsync(
			workingDirectory: RepositoryPaths.Root(),
			description: "dotnet pack",

			arguments:
			[
				"pack",
				"Nickelony.Stanza.slnx",
				"--no-build",
				"-c",
				configuration,
				"-o",
				output,
			]
		);

		Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
	}

	// The publish step of the release workflow asserts the same file, so the IDs that ship have one home.
	private static string[] ReadPackageIds()
	{
		return
		[
			.. File.ReadLines(RepositoryPaths.Below(".github", "scripts", "package-ids.txt"))
				.Select(line => line.Trim())
				.Where(line => line.Length > 0)
				.Order(StringComparer.Ordinal),
		];
	}

	private static bool IsPackageOf(string path, string id)
	{
		string name = Path.GetFileName(path);

		// The version follows the ID, so `Nickelony.Stanza.Markup.0.9.0.nupkg` is the Markup package alone.
		return name.StartsWith(id + ".", StringComparison.Ordinal)
			&& char.IsDigit(name[id.Length + 1]);
	}

	private static bool IsBookkeeping(string name)
	{
		return name == "[Content_Types].xml"
			|| name.StartsWith("_rels/", StringComparison.Ordinal)
			|| name.StartsWith("package/services/", StringComparison.Ordinal)
			|| name.EndsWith(".nuspec", StringComparison.Ordinal);
	}

	// The exact lists also refuse what a broken pack adds: a lib/ directory means the analyzer was packed like a library,
	// and no package carries Roslyn, because the analyzer suppresses its dependencies and the tools read no C# themselves.
	private static Dictionary<string, string[]> ExpectedPayloads()
	{
		string analyzerDirectory = $"analyzers/dotnet/roslyn{ReadRoslynFloor()}/cs";

		return new Dictionary<string, string[]>(StringComparer.Ordinal)
		{
			// NuGet imports the targets file of a package named by its ID, and the file picks a preset from beside it.
			[Analyzers] =
			[
				"README.md",
				"Stanza.png",
				$"build/{Analyzers}.targets",
				$"{analyzerDirectory}/{Analyzers}.CodeFixes.dll",
				$"{analyzerDirectory}/{Analyzers}.CodeFixes.pdb",
				$"{analyzerDirectory}/{Analyzers}.dll",
				$"{analyzerDirectory}/{Analyzers}.pdb",
				.. PresetFiles.ReadNames().Select(name => $"build/presets/{name}.globalconfig"),
			],

			[Markup] =
			[
				"README.md",
				"Stanza.png",
				"tools/net8.0/any/DotnetToolSettings.xml",
				"tools/net8.0/any/Nickelony.Stanza.Markup.Core.dll",
				"tools/net8.0/any/Nickelony.Stanza.Markup.Core.pdb",
				"tools/net8.0/any/Nickelony.Stanza.Markup.deps.json",
				"tools/net8.0/any/Nickelony.Stanza.Markup.dll",
				"tools/net8.0/any/Nickelony.Stanza.Markup.pdb",
				"tools/net8.0/any/Nickelony.Stanza.Markup.runtimeconfig.json",
			],

			[Format] =
			[
				"README.md",
				"Stanza.png",
				"tools/net8.0/any/DotnetToolSettings.xml",
				"tools/net8.0/any/Nickelony.Stanza.Format.deps.json",
				"tools/net8.0/any/Nickelony.Stanza.Format.dll",
				"tools/net8.0/any/Nickelony.Stanza.Format.pdb",
				"tools/net8.0/any/Nickelony.Stanza.Format.runtimeconfig.json",
			],
		};
	}

	// The analyzers build against the version the unconditioned property names, as its directory in the package says.
	private static string ReadRoslynFloor()
	{
		var packages = XDocument.Load(RepositoryPaths.Below("Directory.Packages.props"));

		string version = packages
			.Descendants("StanzaRoslynVersion")
			.Single(element => element.Parent?.Attribute("Condition") is null)
			.Value.Trim();

		string[] parts = version.Split('.');

		return $"{parts[0]}.{parts[1]}";
	}

	private static void AssertNuspec(ZipArchive archive, string id)
	{
		ZipArchiveEntry entry = Assert.ContainsSingle(
			archive.Entries.Where(candidate =>
				candidate.FullName.EndsWith(".nuspec", StringComparison.Ordinal)
			)
		);

		using Stream stream = entry.Open();

		XElement metadata = XDocument
			.Load(stream)
			.Descendants()
			.First(node => node.Name.LocalName == "metadata");

		string[] dependencies =
		[
			.. metadata
				.Descendants()
				.Where(node => node.Name.LocalName == "dependency")
				.Select(node => node.Attribute("id")?.Value ?? string.Empty),
		];

		// A package that lists a dependency would pull it into the project that installs the analyzer or the tool.
		Assert.IsEmpty(
			dependencies,
			$"{id} lists dependencies: {string.Join(", ", dependencies)}."
		);

		string? development = metadata
			.Elements()
			.FirstOrDefault(node => node.Name.LocalName == "developmentDependency")
			?.Value;

		string[] types =
		[
			.. metadata
				.Descendants()
				.Where(node => node.Name.LocalName == "packageType")
				.Select(node => node.Attribute("name")?.Value ?? string.Empty),
		];

		if (id == Analyzers)
		{
			Assert.AreEqual(
				"true",
				development,
				"The analyzer package is not a development dependency."
			);
		}
		else
		{
			Assert.AreSequenceEqual(["DotnetTool"], types, $"{id} is not a dotnet tool package.");
		}

		AssertMetadata(metadata, id);
	}

	// The version comes from the tag at a release, so only its shape is pinned.
	private static void AssertMetadata(XElement metadata, string id)
	{
		Dictionary<string, string> fields = new()
		{
			["id"] = id,
			["authors"] = "Nickelony",
			["readme"] = "README.md",
			["icon"] = "Stanza.png",
			["projectUrl"] = RepositoryUrl,
			["releaseNotes"] = RepositoryUrl + "/blob/main/CHANGELOG.md",
		};

		foreach ((string name, string expected) in fields)
			Assert.AreEqual(expected, ReadField(metadata, name), $"{id} has another {name}.");

		Assert.MatchesRegex(
			@"^\d+\.\d+\.\d+",
			ReadField(metadata, "version"),
			$"{id} has no version."
		);

		Assert.StartsWith(
			"Copyright (c) ",
			ReadField(metadata, "copyright"),
			$"{id} has no copyright."
		);

		Assert.IsNotEmpty(ReadField(metadata, "description"), $"{id} has no description.");

		XElement license = Assert.ContainsSingle(Fields(metadata, "license"));

		Assert.AreEqual(
			"expression",
			license.Attribute("type")?.Value,
			$"{id} has a license file."
		);

		Assert.AreEqual("MIT", license.Value, $"{id} has another license.");

		XElement repository = Assert.ContainsSingle(Fields(metadata, "repository"));

		Assert.AreEqual(
			"git",
			repository.Attribute("type")?.Value,
			$"{id} has another repository type."
		);

		Assert.AreEqual(
			RepositoryUrl,
			repository.Attribute("url")?.Value,
			$"{id} has another repository."
		);

		string[] tags = ReadField(metadata, "tags")
			.Split(' ', StringSplitOptions.RemoveEmptyEntries);

		Assert.Contains("stanza", tags, $"{id} lacks the tag stanza.");

		Assert.Contains(
			id == Analyzers ? "roslyn-analyzer" : "dotnet-tool",
			tags,
			$"{id} lacks the tag of its kind."
		);

		// The shared targets file sets the oldest NuGet client that reads a tool package.
		if (id != Analyzers)
		{
			Assert.AreEqual(
				"4.1.0",
				metadata.Attribute("minClientVersion")?.Value,
				$"{id} requires another client."
			);
		}
	}

	private static string ReadField(XElement metadata, string name) =>
		Assert.ContainsSingle(Fields(metadata, name)).Value;

	private static IEnumerable<XElement> Fields(XElement metadata, string name) =>
		metadata.Elements().Where(node => node.Name.LocalName == name);
}
