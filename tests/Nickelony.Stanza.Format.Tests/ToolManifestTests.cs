using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Format.Running;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Format.Tests;

[TestClass]
public sealed class ToolManifestTests
{
	[TestMethod]
	public void TheManifestNamesCSharpierWithItsVersionAndStanzaMarkupWhateverTheirCase()
	{
		const string json = """
			{
			  // A comment and a trailing comma are read as dotnet reads them.
			  "version": 1,
			  "tools": {
			    "CSharpier": { "version": "1.3.0", "commands": ["csharpier"] },
			    "nickelony.stanza.markup": { "version": "1.0.0", "commands": ["stanza-markup"] },
			  }
			}
			""";

		Assert.AreEqual(
			new ToolManifest(CSharpierVersion: "1.3.0", HasMarkup: true),
			ToolManifest.FromJson(json)
		);

		Assert.AreEqual(
			new ToolManifest(CSharpierVersion: null, HasMarkup: false),
			ToolManifest.FromJson("{}")
		);
	}

	[TestMethod]
	public void OnlyACSharpierBeforeOnePointZeroIsOld()
	{
		Assert.IsTrue(PinsOldCSharpier("0.30.6"));
		Assert.IsFalse(PinsOldCSharpier("1.0.0"));
		Assert.IsFalse(PinsOldCSharpier("10.0.0"));
		Assert.IsFalse(PinsOldCSharpier(null));
	}

	[TestMethod]
	public void BothManifestsOfADirectoryListTheirToolsAndAnIsRootEndsTheWalkAfterThem()
	{
		using TempDirectory directory = new(editorConfig: false);

		_ = directory.Write(
			".config/dotnet-tools.json",
			"""{ "tools": { "dotnet-ef": { "version": "8.0.0", "commands": ["dotnet-ef"] } } }"""
		);

		_ = directory.Write(
			"dotnet-tools.json",

			"""
			{ "isRoot": true, "tools": { "csharpier": { "version": "1.3.0", "commands": ["csharpier"] } } }
			"""
		);

		Assert.AreEqual(
			new ToolManifest(CSharpierVersion: "1.3.0", HasMarkup: false),
			ToolManifest.Read(directory.Root)
		);
	}

	[TestMethod]
	public void TheManifestOfTheSolutionsDirectoryAndItsParentsListTheToolsAsDotnetReadsThem()
	{
		using TempDirectory directory = new(editorConfig: false);

		// The parent ends the walk, so no manifest above the temporary directory is read.
		_ = directory.Write(
			".config/dotnet-tools.json",

			"""
			{ "isRoot": true, "tools": { "csharpier": { "version": "0.30.6", "commands": ["csharpier"] } } }
			"""
		);

		_ = directory.Write(
			"sub/dotnet-tools.json",

			"""
			{ "tools": { "Nickelony.Stanza.Markup": { "version": "1.0.0", "commands": ["stanza-markup"] } } }
			"""
		);

		Assert.AreEqual(
			new ToolManifest(CSharpierVersion: "0.30.6", HasMarkup: true),
			ToolManifest.Read(Path.Combine(directory.Root, "sub"))
		);

		_ = directory.Write(
			"sub/deeper/.config/dotnet-tools.json",

			"""
			{ "tools": { "csharpier": { "version": "1.3.0", "commands": ["csharpier"] } } }
			"""
		);

		Assert.AreEqual(
			new ToolManifest(CSharpierVersion: "1.3.0", HasMarkup: true),
			ToolManifest.Read(Path.Combine(directory.Root, "sub", "deeper"))
		);
	}

	[TestMethod]
	public void AManifestThatIsRootEndsTheWalkForTheTools()
	{
		using TempDirectory directory = new(editorConfig: false);

		_ = directory.Write(
			".config/dotnet-tools.json",

			"""
			{ "isRoot": true, "tools": { "csharpier": { "version": "1.3.0", "commands": ["csharpier"] } } }
			"""
		);

		_ = directory.Write("sub/dotnet-tools.json", """{ "isRoot": true, "tools": { } }""");

		Assert.AreEqual(
			new ToolManifest(CSharpierVersion: null, HasMarkup: false),
			ToolManifest.Read(Path.Combine(directory.Root, "sub"))
		);
	}

	[TestMethod]
	public void AManifestThatIsNotJsonIsARunFailureThatNamesTheFile()
	{
		using TempDirectory directory = new(editorConfig: false);

		foreach (string text in (string[])["{ \"tools\": ", "[]", ""])
		{
			_ = directory.Write(".config/dotnet-tools.json", text);

			FormatRunException exception = Assert.ThrowsExactly<FormatRunException>(() =>
				_ = ToolManifest.Read(directory.Root)
			);

			Assert.Contains("dotnet-tools.json' is not valid JSON", exception.Message);
			Assert.IsInstanceOfType<JsonException>(exception.InnerException);
		}
	}

	private static bool PinsOldCSharpier(string? version) =>
		new ToolManifest(CSharpierVersion: version, HasMarkup: false).PinsOldCSharpier;
}
