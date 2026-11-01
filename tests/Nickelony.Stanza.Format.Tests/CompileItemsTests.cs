using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Format.Planning;
using Nickelony.Stanza.Format.Running;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Format.Tests;

[TestClass]
public sealed class CompileItemsTests
{
	private static readonly string s_root = Path.GetFullPath(
		Path.Combine(Path.GetTempPath(), "repo")
	);

	[TestMethod]
	public void TheSolutionListingIsReadAfterItsLineOfDashesForItsCSharpAndBasicProjects()
	{
		const string listing =
			"Projekt(e)\n----------\nsrc\\A\\A.csproj\r\ntests\\B\\B.CSPROJ\nweb\\W.esproj\nlib\\V.vbproj\n";

		Assert.AreSequenceEqual(
			[@"src\A\A.csproj", @"tests\B\B.CSPROJ", @"lib\V.vbproj"],
			CompileItems.ReadProjects(listing)
		);
	}

	[TestMethod]
	public void TheEvaluationGivesEachFileTheViewOfItsProject()
	{
		string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "repo"));
		string project = Path.Combine(path1: root, path2: "src", path3: "A", path4: "A.csproj");
		string inside = Path.Combine(path1: root, path2: "src", path3: "A", path4: "A.cs");

		string outside = Path.GetFullPath(
			Path.Combine(Path.GetTempPath(), "elsewhere", "Linked.cs")
		);

		string output =
			"warning : a note before the result\n"
			+ $$"""
				{
				  "Properties": {
				    "TargetFramework": "net8.0",
				    "TargetFrameworks": "",
				    "DefineConstants": "TRACE;DEBUG;NET8_0;DEBUG"
				  },
				  "Items": {
				    "Compile": [
				      { "Identity": "A.cs", "FullPath": {{Quote(inside)}} },
				      { "Identity": "Linked.cs", "FullPath": {{Quote(outside)}} }
				    ]
				  }
				}
				""";

		Assert.AreSequenceEqual(
			[new CompiledFile("src/A/A.cs", "src/A/A.csproj", "net8.0|DEBUG;NET8_0;TRACE|")],
			CompileItems.ReadItems(output, root, project)
		);
	}

	[TestMethod]
	public void TheViewHoldsThePackagesAndAnalyzersAProjectLoadsWhateverTheirOrder()
	{
		string bare = ViewOf("PackageReference");

		string packages = ViewOf(
			"PackageReference",
			Package("Stanza", "1.0.0"),
			Package("Other", "2.0.0")
		);

		string reordered = ViewOf(
			"PackageReference",
			Package("Other", "2.0.0"),
			Package("Stanza", "1.0.0")
		);

		Assert.AreNotEqual(bare, packages);
		Assert.AreEqual(packages, reordered);
	}

	[TestMethod]
	public void AnotherVersionOfAPackageOrAnotherAnalyzerFileIsAnotherView()
	{
		string first = ViewOf("PackageReference", Package("P", "1.0.0"));
		string newer = ViewOf("PackageReference", Package("P", "2.0.0"));
		string overridden = ViewOf("PackageReference", Item("P", ("VersionOverride", "2.0.0")));

		string analyzer = ViewOf(
			"Analyzer",
			Item("y.dll", ("FullPath", Path.Combine(s_root, "y.dll")))
		);

		string[] views = [first, newer, analyzer];

		Assert.HasCount(3, views.Distinct());

		// An override sets the version of the same package.
		Assert.AreEqual(newer, overridden);
	}

	[TestMethod]
	public void OnlyAProjectReferenceThatLoadsAnAnalyzerCountsAsOne()
	{
		string path = Path.Combine(s_root, "B.csproj");
		string bare = ViewOf("ProjectReference");
		string plain = ViewOf("ProjectReference", Item("B.csproj", ("FullPath", path)));

		string analyzer = ViewOf(
			"ProjectReference",
			Item("B.csproj", ("FullPath", path), ("OutputItemType", "Analyzer"))
		);

		Assert.AreEqual(bare, plain);
		Assert.AreNotEqual(bare, analyzer);
	}

	[TestMethod]
	public void TwoProjectsOfOneFrameworkThatLoadDifferentAnalyzersAreTwoViewsOfAFileTheyShare()
	{
		using TempDirectory directory = new();

		_ = directory.Write("shared/Shared.cs", "namespace Shared;\n");

		// The evaluation reads the reference without restoring it, so the package need not exist.
		_ = directory.Write(
			"src/Plain/Plain.csproj",
			BuildProject("<TargetFramework>net8.0</TargetFramework>")
		);

		_ = directory.Write(
			"src/Checked/Checked.csproj",

			BuildProject(
				"<TargetFramework>net8.0</TargetFramework>",
				"<PackageReference Include=\"Nickelony.Stanza.Analyzers\" Version=\"1.0.0\" PrivateAssets=\"all\" />"
			)
		);

		_ = directory.Write(
			"App.slnx",

			"""
			<Solution>
				<Project Path="src/Plain/Plain.csproj" />
				<Project Path="src/Checked/Checked.csproj" />
			</Solution>
			"""
		);

		ImmutableArray<CompiledFile> files = CompileItems.Collect(
			directory.Root,
			"App.slnx",
			CancellationToken.None
		);

		Assert.HasCount(2, files.Select(static file => file.View).Distinct());

		FormatFacts facts = new(
			Solution: "App.slnx",
			HasMarkup: false,
			ChangedFiles: [],
			SkippedSources: [],
			CompiledFiles: files
		);

		// The shared file goes through each project, so the rules only the second one loads still run on it.
		Assert.AreSequenceEqual(
			["App.slnx", "src/Checked/Checked.csproj", "src/Plain/Plain.csproj"],
			[.. FormatTargets.ForSolution(facts).Select(static target => target.Path)]
		);
	}

	[TestMethod]
	public void TwoProjectsThatLoadTheSameAnalyzersAreOneViewOfTheirSharedFile()
	{
		using TempDirectory directory = new();

		_ = directory.Write("shared/Shared.cs", "namespace Shared;\n");

		string reference =
			"<PackageReference Include=\"Nickelony.Stanza.Analyzers\" Version=\"1.0.0\" PrivateAssets=\"all\" />";

		_ = directory.Write(
			"src/A/A.csproj",
			BuildProject("<TargetFramework>net8.0</TargetFramework>", reference)
		);

		_ = directory.Write(
			"src/B/B.csproj",
			BuildProject("<TargetFramework>net8.0</TargetFramework>", reference)
		);

		_ = directory.Write(
			"App.slnx",
			"""<Solution><Project Path="src/A/A.csproj" /><Project Path="src/B/B.csproj" /></Solution>"""
		);

		ImmutableArray<CompiledFile> files = CompileItems.Collect(
			directory.Root,
			"App.slnx",
			CancellationToken.None
		);

		Assert.HasCount(1, files.Select(static file => file.View).Distinct());
	}

	[TestMethod]
	public void AFailedEvaluationReportsTheErrorLinesWithoutTheItemsItRead()
	{
		const string error = "A.csproj : error MSB4019: The imported project was not found.";

		string output = $$"""
			{
			  "Items": { "Compile": [ { "Identity": "A.cs" } ] }
			}
			{{error}}
			""";

		Assert.AreEqual(error, CompileItems.ReadErrors(output));
	}

	[TestMethod]
	public void TheQueryReadsTheFilesAndViewsOfEachProjectOfASolution()
	{
		using TempDirectory directory = new();

		_ = directory.Write("shared/Shared.cs", "namespace Shared;\n");
		_ = directory.Write("src/A/A.cs", "namespace A;\n");

		_ = directory.Write(
			"src/A/A.csproj",

			BuildProject(
				"<TargetFramework>net8.0</TargetFramework><DefineConstants>$(DefineConstants);WPF</DefineConstants>"
			)
		);

		_ = directory.Write(
			"src/B/B.csproj",
			BuildProject("<TargetFrameworks>net8.0;netstandard2.0</TargetFrameworks>")
		);

		// The solution names a project the disk lacks, as one that names a sibling checkout does.
		_ = directory.Write(
			"App.slnx",

			"""
			<Solution>
				<Project Path="src/A/A.csproj" />
				<Project Path="src/B/B.csproj" />
				<Project Path="../Missing/Missing.csproj" />
			</Solution>
			"""
		);

		CompiledFile[] files =
		[
			.. CompileItems.Collect(directory.Root, "App.slnx", CancellationToken.None),
		];

		Assert.AreSequenceEqual(
			[
				"src/A/A.cs src/A/A.csproj",
				"shared/Shared.cs src/A/A.csproj",
				"shared/Shared.cs src/B/B.csproj",
			],

			[.. files.Select(static file => $"{file.Path} {file.Project}")]
		);

		Assert.Contains("WPF", files[1].View);
		Assert.AreNotEqual(files[1].View, files[2].View);
	}

	// The listing reads a solution named with a leading @ as a response file, so the path goes to it with a ./ prefix.
	[TestMethod]
	public void ASolutionWhoseNameStartsWithAnAtSignIsListedNotReadAsAResponseFile()
	{
		using TempDirectory directory = new();

		_ = directory.Write("shared/Shared.cs", "namespace Shared;\n");
		_ = directory.Write("src/A/A.cs", "namespace A;\n");

		_ = directory.Write(
			"src/A/A.csproj",
			BuildProject("<TargetFramework>net8.0</TargetFramework>")
		);

		_ = directory.Write(
			"@App.slnx",
			"""<Solution><Project Path="src/A/A.csproj" /></Solution>"""
		);

		CompiledFile[] files =
		[
			.. CompileItems.Collect(directory.Root, "@App.slnx", CancellationToken.None),
		];

		Assert.AreSequenceEqual(
			["src/A/A.cs", "shared/Shared.cs"],
			[.. files.Select(static file => file.Path)]
		);
	}

	[TestMethod]
	public void AProjectThatSetsTheImportHookOfTheQueryStillGivesTheFilesItsTargetsAdd()
	{
		using TempDirectory directory = new();

		_ = directory.Write("src/A/A.cs", "namespace A;\n");
		_ = directory.Write("src/A/Extra.cs", "namespace A;\n");

		_ = directory.Write(
			"src/A/extra.targets",
			"<Project><ItemGroup><Compile Include=\"Extra.cs\" /></ItemGroup></Project>"
		);

		_ = directory.Write(
			"src/A/A.csproj",

			"""
			<Project Sdk="Microsoft.NET.Sdk">
				<PropertyGroup>
					<TargetFramework>net8.0</TargetFramework>
					<EnableDefaultCompileItems>false</EnableDefaultCompileItems>
					<CustomAfterMicrosoftCommonTargets>$(MSBuildProjectDirectory)/extra.targets</CustomAfterMicrosoftCommonTargets>
				</PropertyGroup>
				<ItemGroup>
					<Compile Include="A.cs" />
				</ItemGroup>
			</Project>
			"""
		);

		_ = directory.Write(
			"App.slnx",
			"""<Solution><Project Path="src/A/A.csproj" /></Solution>"""
		);

		CompiledFile[] files =
		[
			.. CompileItems.Collect(directory.Root, "App.slnx", CancellationToken.None),
		];

		Assert.AreSequenceEqual(
			["src/A/A.cs", "src/A/Extra.cs"],
			[.. files.Select(static file => file.Path)]
		);
	}

	[TestMethod]
	public void AMultiTargetedProjectGivesTheFilesItsFrameworksCompileUnderOneView()
	{
		using TempDirectory directory = new();

		_ = directory.Write("src/M/Foo.cs", "namespace M;\n");
		_ = directory.Write("shared/Net.cs", "namespace M;\n");

		_ = directory.Write(
			"src/M/M.csproj",

			"""
			<Project Sdk="Microsoft.NET.Sdk">
				<PropertyGroup>
					<TargetFrameworks>net8.0;netstandard2.0</TargetFrameworks>
				</PropertyGroup>
				<ItemGroup>
					<Compile Include="../../shared/Net.cs" Condition="'$(TargetFramework)' == 'netstandard2.0'" />
				</ItemGroup>
			</Project>
			"""
		);

		_ = directory.Write(
			"App.slnx",
			"""<Solution><Project Path="src/M/M.csproj" /></Solution>"""
		);

		CompiledFile[] files =
		[
			.. CompileItems.Collect(directory.Root, "App.slnx", CancellationToken.None),
		];

		Assert.AreSequenceEqual(
			["src/M/Foo.cs", "shared/Net.cs"],
			[.. files.Select(static file => file.Path)]
		);

		Assert.HasCount(1, files.Select(static file => file.View).Distinct());
		Assert.Contains("net8.0;netstandard2.0", files[0].View);
	}

	private static string BuildProject(string properties, string items = "")
	{
		return $"""
			<Project Sdk="Microsoft.NET.Sdk">
				<PropertyGroup>{properties}</PropertyGroup>
				<ItemGroup>
					<Compile Include="../../shared/Shared.cs" />
					{items}
				</ItemGroup>
			</Project>
			""";
	}

	// The view of a project that compiles one file and lists one other kind of item.
	private static string ViewOf(string list, params string[] items)
	{
		string compiled = Item("A.cs", ("FullPath", Path.Combine(s_root, "A.cs")));

		string evaluation = $$"""
			{
			  "Properties": { "TargetFramework": "net8.0", "TargetFrameworks": "", "DefineConstants": "" },
			  "Items": {
			    "Compile": [ {{compiled}} ],
			    {{Quote(list)}}: [ {{string.Join(", ", items)}} ]
			  }
			}
			""";

		return CompileItems
			.ReadItems(evaluation, s_root, Path.Combine(s_root, "A.csproj"))
			.First()
			.View;
	}

	private static string Package(string identity, string version) =>
		Item(identity, ("Version", version));

	private static string Item(string identity, params (string Name, string Value)[] metadata)
	{
		Dictionary<string, string> item = new() { ["Identity"] = identity };

		foreach ((string name, string value) in metadata)
			item[name] = value;

		return System.Text.Json.JsonSerializer.Serialize(item);
	}

	private static string Quote(string value) => System.Text.Json.JsonSerializer.Serialize(value);
}
