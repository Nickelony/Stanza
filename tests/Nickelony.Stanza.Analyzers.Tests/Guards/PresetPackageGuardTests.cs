using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Guards;

/// <summary>Packs the analyzer package and builds a consumer project that picks a level.</summary>
[TestClass]
[TestCategory("Repository")]
public sealed partial class PresetPackageGuardTests
{
	private const string PackageId = "Nickelony.Stanza.Analyzers";
	private const string ErrorLog = "log.sarif";

	// Each class makes probe rules report where a level turns them on:
	// brace padding, two single-line fields apart, a comment above a single-line field,
	// two switch sections tight, and two multi-line arguments tight and two apart.
	private const string Sample = """
		internal sealed class Padded
		{

			private int _value;
		}

		internal sealed class TwoUnits
		{
			private int _first;

			private int _second;
		}

		internal sealed class CommentedSibling
		{
			private int _first;
			private int _second;
			// The third.
			private int _third;
		}

		internal sealed class Switches
		{
			internal int Tight(int value)
			{
				switch (value)
				{
					case 1:
						value++;
						value++;
						return value;
					case 2:
						value--;
						value--;
						return value;
				}

				return 0;
			}
		}

		internal sealed class Arguments
		{
			internal static int Sum(int first, int second)
			{
				return first + second;
			}

			internal int Tight()
			{
				return Sum(
					1
						+ 2,
					3
						+ 4
				);
			}

			internal int Spaced()
			{
				return Sum(
					1
						+ 2,

					3
						+ 4
				);
			}
		}
		""";

	// One rule of each level, the strong rules the csharpier overlay turns off, and the halves it turns on.
	private static readonly string[] s_probes =
	[
		"ZA0203",
		"ZA0111",
		"ZA0051",
		"ZA0012",
		"ZA0151",
		"ZA0152",
	];

	private static readonly Lazy<Task<string>> s_feed = new(PackAsync);

	private static readonly Lazy<TempDirectory> s_feedDirectory = new(() =>
		new TempDirectory(editorConfig: false)
	);

	[GeneratedRegex(@"""version""\s*:\s*""(?<major>\d+)\.", RegexOptions.CultureInvariant)]
	private static partial Regex SdkMajor();

	[ClassCleanup]
	public static void DeleteFeed()
	{
		if (s_feedDirectory.IsValueCreated)
			s_feedDirectory.Value.Dispose();
	}

	[TestMethod]
	public async Task EachLevelReportsItsRulesAtTheSeverityItIsGiven()
	{
		SkipWhereThePackageIsNotBuilt();

		using var consumer = Consumer.Create(await s_feed.Value);

		PresetFiles.LevelTable table = PresetFiles.ReadTable();

		Dictionary<string, HashSet<string>> seen = s_probes.ToDictionary(
			id => id,
			_ => new HashSet<string>(StringComparer.Ordinal),
			StringComparer.Ordinal
		);

		// The target trims a value and ignores its case, so the padded and capitalized row still applies the overlay at strong,
		// and the csharpier formatter at minimal adds none.
		(string? Level, string? Severity, string? Formatter)[] builds =
		[
			(null, null, null),
			("minimal", "warning", "csharpier"),
			("moderate", "error", null),
			("strong", null, null),
			(" Strong ", " WARNING ", " CSharpier "),
			("none", "error", null),
		];

		foreach ((string? level, string? severity, string? formatter) in builds)
		{
			string name =
				$"{level ?? "default"} at {severity ?? "default"} for {formatter ?? "default"}";

			string? applied = severity?.Trim().ToLowerInvariant();

			HashSet<string> enabled = ReadEnabled(
				table,
				level?.Trim().ToLowerInvariant() ?? "moderate",
				formatter?.Trim().ToLowerInvariant()
			);

			BuildResult result = await consumer.BuildAsync(Properties(level, severity, formatter));

			if (applied != "error")
				Assert.AreEqual(0, result.ExitCode, $"{name}\n{result.Output}");

			foreach (string id in s_probes)
			{
				string? expected = enabled.Contains(id) ? applied ?? "suggestion" : null;

				Assert.AreEqual(expected, result.FindLevel(id), $"{name}: {id}\n{result.Output}");
				_ = seen[id].Add(expected ?? "off");
			}
		}

		string[] still = [.. s_probes.Where(id => seen[id].Count < 2)];

		Assert.IsEmpty(
			still,
			$"{string.Join(", ", still)} keep one state in every build, so the sample proves nothing."
		);
	}

	// The overlay sets ZA0152 on where the strong file sets it off, so only a higher overlay reports it;
	// a tie would unset the key and leave the rule off as it ships.
	[TestMethod]
	public async Task TheCSharpierOverlayWinsOverTheLevelAndAProjectFileWinsOverBoth()
	{
		SkipWhereThePackageIsNotBuilt();

		using var consumer = Consumer.Create(await s_feed.Value);

		Dictionary<string, string> properties = Properties(
			level: "strong",
			severity: "warning",
			formatter: "csharpier"
		);

		BuildResult overlay = await consumer.BuildAsync(properties);

		Assert.AreEqual("warning", overlay.FindLevel("ZA0152"), "The overlay lost to the level.");
		Assert.IsNull(overlay.FindLevel("ZA0151"), "The overlay did not turn ZA0151 off.");
		Assert.AreEqual("warning", overlay.FindLevel("ZA0051"), "The level did not apply.");

		Assert.DoesNotContain(
			"MultipleGlobalAnalyzerKeys",
			overlay.Output,
			"The overlay and the level share a rank."
		);

		consumer.Write(
			".editorconfig",

			"root = true\n[*.cs]\n"
				+ "dotnet_diagnostic.ZA0152.severity = none\n"
				+ "dotnet_diagnostic.ZA0051.severity = none\n"
		);

		BuildResult editorConfig = await consumer.BuildAsync(properties);

		Assert.IsNull(
			editorConfig.FindLevel("ZA0152"),
			"An .editorconfig line lost to the overlay."
		);

		Assert.IsNull(editorConfig.FindLevel("ZA0051"), "An .editorconfig line lost to the level.");
		Assert.AreEqual("warning", editorConfig.FindLevel("ZA0111"), "The level did not apply.");

		consumer.Write(".editorconfig", "root = true\n");

		consumer.Write(
			".globalconfig",

			"is_global = true\n"
				+ "dotnet_diagnostic.ZA0152.severity = error\n"
				+ "dotnet_diagnostic.ZA0051.severity = error\n"
		);

		BuildResult globalConfig = await consumer.BuildAsync(properties);

		Assert.AreEqual(
			"error",
			globalConfig.FindLevel("ZA0152"),
			"A .globalconfig lost to the overlay."
		);

		Assert.AreEqual(
			"error",
			globalConfig.FindLevel("ZA0051"),
			"A .globalconfig lost to the level."
		);

		Assert.AreEqual("warning", globalConfig.FindLevel("ZA0111"), "The level did not apply.");
	}

	[TestMethod]
	public async Task ABulkKeyReachesARuleThatShipsOnButNotOneALevelFileStates()
	{
		SkipWhereThePackageIsNotBuilt();

		using var consumer = Consumer.Create(await s_feed.Value);

		consumer.Write(
			".editorconfig",
			"root = true\n[*.cs]\ndotnet_analyzer_diagnostic.category-Formatting.severity = none\n"
		);

		BuildResult shipped = await consumer.BuildAsync([]);

		Assert.IsNull(
			shipped.FindLevel("ZA0111"),
			"The bulk key does not reach a rule that ships on."
		);

		BuildResult stated = await consumer.BuildAsync(Properties(severity: "warning"));

		Assert.AreEqual(
			"warning",
			stated.FindLevel("ZA0111"),
			"The bulk key reached a stated rule."
		);
	}

	[TestMethod]
	[DataRow("StanzaLevel", "minimal, moderate, strong, none")]
	[DataRow("StanzaSeverity", "suggestion, warning, error")]
	[DataRow("StanzaFormatter", "stanza-format, csharpier")]
	public async Task AValueThatNamesNothingFailsTheBuildAndListsTheValues(
		string property,
		string values
	)
	{
		SkipWhereThePackageIsNotBuilt();

		using var consumer = Consumer.Create(await s_feed.Value);

		BuildResult result = await consumer.BuildAsync(new() { [property] = "loose" });

		Assert.AreNotEqual(0, result.ExitCode, $"The build accepted {property} loose.");
		Assert.Contains("STANZA001", result.Output, result.Output);

		Assert.Contains(
			values,
			result.Output,
			$"The error does not list the values of {property}."
		);
	}

	// The build of the SDK that runs the tests has the Roslyn floor at least, so a warning here is a false report.
	// The warning on an older compiler needs a toolset package, which CI restores in the package job.
	[TestMethod]
	public async Task ABuildOnACompilerAtTheRoslynFloorOrNewerDoesNotWarnAboutTheCompiler()
	{
		SkipWhereThePackageIsNotBuilt();

		using var consumer = Consumer.Create(await s_feed.Value);

		BuildResult result = await consumer.BuildAsync(Properties(severity: "warning"));

		Assert.AreEqual(0, result.ExitCode, result.Output);
		Assert.DoesNotContain("STANZA002", result.Output, result.Output);

		Assert.IsNotNull(
			result.FindLevel("ZA0111"),
			"The analyzers did not load, so the compiler check has nothing to say."
		);
	}

	private static Dictionary<string, string> Properties(
		string? level = null,
		string? severity = null,
		string? formatter = null
	)
	{
		Dictionary<string, string> properties = [];

		if (level is not null)
			properties["StanzaLevel"] = level;

		if (severity is not null)
			properties["StanzaSeverity"] = severity;

		if (formatter is not null)
			properties["StanzaFormatter"] = formatter;

		return properties;
	}

	// The rules a build turns on, as the level table states them, the csharpier overlay at strong included.
	private static HashSet<string> ReadEnabled(
		PresetFiles.LevelTable table,
		string level,
		string? formatter
	)
	{
		if (level == PresetFiles.NoRules)
			return [];

		HashSet<string> enabled = [.. table.ReadEnabled(level)];

		if (level == "strong" && formatter == PresetFiles.CSharpier)
		{
			enabled.ExceptWith(table.ReadChanged(PresetFiles.CSharpierOff));
			enabled.UnionWith(table.ReadChanged(PresetFiles.CSharpierOn));
		}

		return enabled;
	}

	private static void SkipWhereThePackageIsNotBuilt()
	{
#if STANZA_NEWEST_ROSLYN
		ExpectedSkip.Skip(
			"This leg builds the analyzer tests alone, so the leg that builds the solution packs the package."
		);
#endif
	}

	// The package project alone is packed from the build the tests ran from, into a feed the consumer restores from.
	private static async Task<string> PackAsync()
	{
		string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
		string feed = s_feedDirectory.Value.Root;

		// The nuspec is written beside the build output by default, where the pack of the whole solution writes it too.
		string nuspec = Path.Combine(feed, "nuspec");

		_ = Directory.CreateDirectory(nuspec);

		string project = RepositoryPaths.Below(
			"src",
			PackageId + ".Package",
			PackageId + ".Package.csproj"
		);

		RunResult result = await ChildBuild.RunAsync(
			workingDirectory: RepositoryPaths.Root(),
			description: "dotnet pack",

			arguments:
			[
				"pack",
				project,
				"--no-build",
				"-c",
				configuration,
				"-o",
				feed,
				$"-p:NuspecOutputPath={nuspec}{Path.DirectorySeparatorChar}",
			]
		);

		Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);

		return feed;
	}

	/// <summary>A consumer build, with the severity its error log gives each rule.</summary>
	private sealed record BuildResult(
		int ExitCode,
		string Output,
		Dictionary<string, string> Levels
	)
	{
		/// <summary>The severity the build reports the rule at, if it reports it.</summary>
		internal string? FindLevel(string id) => Levels.GetValueOrDefault(id);
	}

	private sealed class Consumer(string feed) : IDisposable
	{
		private readonly TempDirectory _directory = new();

		internal static Consumer Create(string feed)
		{
			Consumer consumer = new(feed);

			string package = Directory.GetFiles(feed, PackageId + ".*.nupkg").Single();
			string version = Path.GetFileNameWithoutExtension(package)[(PackageId.Length + 1)..];

			// The target framework is the one the SDK ships a reference pack for, so the restore needs no download.
			string major = SdkMajor()
				.Match(File.ReadAllText(RepositoryPaths.Below("global.json")))
				.Groups["major"]
				.Value;

			consumer.Write(
				"Consumer.csproj",

				$"""
				<Project Sdk="Microsoft.NET.Sdk">
					<PropertyGroup>
						<TargetFramework>net{major}.0</TargetFramework>
					</PropertyGroup>
					<ItemGroup>
						<PackageReference Include="{PackageId}" Version="{version}" PrivateAssets="all" />
					</ItemGroup>
				</Project>
				"""
			);

			consumer.Write("Sample.cs", Sample);

			return consumer;
		}

		internal void Write(string name, string content) => _ = _directory.Write(name, content);

		// The build prints no suggestion, so the levels come from the SARIF error log the compiler writes.
		internal async Task<BuildResult> BuildAsync(Dictionary<string, string> properties)
		{
			string log = Path.Combine(_directory.Root, ErrorLog);

			File.Delete(log);

			List<string> arguments =
			[
				"build",
				"Consumer.csproj",
				"--no-incremental",
				"-nologo",
				"-v:q",
				"-p:NuGetAudit=false",
				$"-p:RestoreSources={feed}",
				$"-p:ErrorLog={log}%2Cversion=2.1",
				.. properties.Select(property => $"-p:{property.Key}={property.Value}"),
			];

			RunResult result = await ChildBuild.RunAsync(
				workingDirectory: _directory.Root,
				description: "dotnet build",
				arguments,

				environment: new Dictionary<string, string>
				{
					["NUGET_PACKAGES"] = Path.Combine(_directory.Root, "packages"),
				}
			);

			return new(result.ExitCode, result.Output + result.Error, ReadLevels(log));
		}

		public void Dispose() => _directory.Dispose();

		// A SARIF result names its level, where note is a suggestion, and a build that stops before the compiler writes none.
		private static Dictionary<string, string> ReadLevels(string log)
		{
			Dictionary<string, string> levels = new(StringComparer.Ordinal);

			if (!File.Exists(log))
				return levels;

			using var document = JsonDocument.Parse(File.ReadAllText(log));

			foreach (
				JsonElement result in document
					.RootElement.GetProperty("runs")[0]
					.GetProperty("results")
					.EnumerateArray()
			)
			{
				string level = result.TryGetProperty("level", out JsonElement value)
					? value.GetString()!
					: "warning";

				_ = levels.TryAdd(
					result.GetProperty("ruleId").GetString()!,
					level == "note" ? "suggestion" : level
				);
			}

			return levels;
		}
	}
}
