using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>Packs the solution and reads each package, as a consumer installs it.</summary>
[TestClass]
[TestCategory("Repository")]
public sealed class PackageLayoutTests
{
	private const string Analyzers = "Nickelony.Stanza.Analyzers";
	private const string Markup = "Nickelony.Stanza.Markup";
	private const string Format = "Nickelony.Stanza.Format";

	[TestMethod]
	public async Task PackingTheSolutionWritesEachPackageWithItsFilesAndNothingElse()
	{
#if STANZA_NEWEST_ROSLYN
		ExpectedSkip.Skip(
			"This leg builds the analyzer tests alone, so the leg that builds the solution packs it."
		);
#endif

		string output = Path.Combine(Path.GetTempPath(), $"stanza-pack-{Guid.NewGuid():N}");

		try
		{
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
				string package = Assert.ContainsSingle(
					packages.Where(path => IsPackageOf(path, id))
				);

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
			Assert.IsEmpty(
				Directory.GetFiles(output, "*.snupkg"),
				"The pack wrote a symbol package."
			);
		}
		finally
		{
			if (Directory.Exists(output))
				Directory.Delete(output, recursive: true);
		}
	}

	// The tests ran from a build of one configuration, and the pack reads that build's output.
	private static async Task PackAsync(string output)
	{
		string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;

		ProcessStartInfo start = new(DotnetHost.Executable)
		{
			WorkingDirectory = RepositoryPaths.Root(),
			RedirectStandardError = true,
			RedirectStandardOutput = true,
			UseShellExecute = false,
		};

		foreach (
			string argument in new[]
			{
				"pack",
				"Nickelony.Stanza.slnx",
				"--no-build",
				"-c",
				configuration,
				"-o",
				output,
			}
		)
		{
			start.ArgumentList.Add(argument);
		}

		RunResult result = await ChildProcess.RunAsync(start, "dotnet pack");

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

		// The version follows the ID, so `Nickelony.Stanza.Markup.1.0.0.nupkg` is the Markup package alone.
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
			[Analyzers] =
			[
				"README.md",
				"Stanza.png",
				$"{analyzerDirectory}/{Analyzers}.CodeFixes.dll",
				$"{analyzerDirectory}/{Analyzers}.CodeFixes.pdb",
				$"{analyzerDirectory}/{Analyzers}.dll",
				$"{analyzerDirectory}/{Analyzers}.pdb",
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
	}
}
