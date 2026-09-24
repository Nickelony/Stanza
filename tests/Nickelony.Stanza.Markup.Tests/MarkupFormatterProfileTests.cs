using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Configuration;
using Nickelony.Stanza.Markup.Core;
using Nickelony.Stanza.Markup.Core.Formatting;
using Nickelony.Stanza.Markup.Core.Xml;
using Nickelony.Stanza.Tests.Shared;

using static Nickelony.Stanza.Markup.Tests.MarkupFixtures;
using static Nickelony.Stanza.Tests.Shared.MarkupFormatFixtures;

namespace Nickelony.Stanza.Markup.Tests;

[TestClass]
public sealed class MarkupFormatterProfileTests
{
	private const string MsBuildDocument = """
		<Project Sdk="Microsoft.NET.Sdk">
		  <PropertyGroup>
		    <TargetFramework>net8.0</TargetFramework>
		  </PropertyGroup>
		  <ItemGroup>
		    <PackageReference Include="a" />
		  </ItemGroup>
		</Project>
		""";

	private const string SlnxDocument = """
		<Solution>
		  <Folder Name="src">
		    <Project Path="src/a.csproj" />
		  </Folder>
		  <Folder Name="tests">
		    <Project Path="tests/a.csproj" />
		  </Folder>
		</Solution>
		""";

	private const string XamlDocument = """
		<Page>
		  <Grid>
		    <Button />
		  </Grid>
		  <StackPanel>
		    <TextBlock />
		  </StackPanel>
		</Page>
		""";

	public static IEnumerable<object[]> GetFixedPointRows()
	{
		(string Profile, string Document)[] documents =
		[
			("xml", MultiLineSiblings),
			("msbuild", MsBuildDocument),
			("slnx", SlnxDocument),
			("xaml", XamlDocument),
		];

		List<object[]> rows = [];

		foreach ((string profile, string document) in documents)
		{
			// The raw literals carry this file's line endings, so each row starts from a line-feed form.
			string lineFeed = LineFeed(document);

			rows.Add([$"{profile}, line feed", profile, lineFeed]);
			rows.Add([$"{profile}, carriage return", profile, lineFeed.Replace("\n", "\r\n")]);
		}

		return rows;
	}

	[TestMethod]
	public void TheEngineFixturesFormatWithThePolicyOfTheXmlProfile() =>
		Assert.AreEqual(MarkupProfiles.Xml.Policy, XmlPolicy);

	[TestMethod]
	[DynamicData(
		nameof(GetFixedPointRows),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public void EveryProfileReachesAFixedPoint(string name, string profileName, string document)
	{
		MarkupProfile profile = MarkupProfiles.FindByName(profileName)!;

		MarkupFormatResult once = Format(document, profile);

		Assert.IsNull(once.Failure, name);
		Assert.IsTrue(once.HasEdits, $"{name}: the document must have work for the layout pass.");

		Assert.AreEqual(
			document.Contains("\r\n"),
			once.Text.Contains("\r\n"),
			$"{name}: the layout keeps the ending the document writes."
		);

		MarkupFormatResult twice = Format(once.Text, profile);

		Assert.IsNull(twice.Failure, name);
		Assert.IsEmpty(twice.Edits, $"{name}: a second pass must find nothing.");
		Assert.AreEqual(once.Text, twice.Text, name);
	}

	[TestMethod]
	public void TheWrappedAttributeLeavesAreSeparatedInTheXmlProfile()
	{
		const string wrapped = """
			<root>
			  <child
			    prop1="1"
			    prop2="2"
			  />
			  <child
			    prop1="1"
			    prop2="2"
			  />
			</root>
			""";

		AssertFormatsTo(
			"""
			<root>
			  <child
			    prop1="1"
			    prop2="2"
			  />

			  <child
			    prop1="1"
			    prop2="2"
			  />
			</root>
			""",

			wrapped,
			MarkupProfiles.Xml
		);

		AssertChangesNothing(wrapped, MarkupProfiles.MsBuild);
	}

	[TestMethod]
	public void TheProjectContainersAreSeparated()
	{
		AssertFormatsTo(
			"""
			<Project Sdk="Microsoft.NET.Sdk">
			  <PropertyGroup>
			    <TargetFramework>net8.0</TargetFramework>
			  </PropertyGroup>

			  <ItemGroup>
			    <PackageReference Include="a" />
			  </ItemGroup>
			</Project>
			""",

			"""
			<Project Sdk="Microsoft.NET.Sdk">
			  <PropertyGroup>
			    <TargetFramework>net8.0</TargetFramework>
			  </PropertyGroup>
			  <ItemGroup>
			    <PackageReference Include="a" />
			  </ItemGroup>
			</Project>
			""",

			MarkupProfiles.MsBuild
		);
	}

	[TestMethod]
	public void ASolutionFileKeepsItsSingleLineProjectEntries()
	{
		AssertChangesNothing(
			"""
			<Solution>
			  <Project Path="a.csproj" />
			  <Project Path="b.csproj" />
			</Solution>
			""",

			MarkupProfiles.Slnx
		);
	}

	[TestMethod]
	public void ASolutionFileIgnoresAWrappedProjectEntry()
	{
		AssertChangesNothing(
			"""
			<Solution>
			  <Project
			    Path="a.csproj" />
			  <Project Path="b.csproj" />
			</Solution>
			""",

			MarkupProfiles.Slnx
		);
	}

	[TestMethod]
	public void TheXamlContainersAreSeparated()
	{
		AssertFormatsTo(
			"""
			<Grid>
			  <StackPanel>
			    <Button />
			  </StackPanel>

			  <StackPanel>
			    <Button />
			  </StackPanel>
			</Grid>
			""",

			"""
			<Grid>
			  <StackPanel>
			    <Button />
			  </StackPanel>
			  <StackPanel>
			    <Button />
			  </StackPanel>
			</Grid>
			""",

			MarkupProfiles.Xaml
		);
	}

	[TestMethod]
	public void AXamlPropertyElementIsLeftAlone()
	{
		AssertChangesNothing(
			"""
			<TextBlock>
			  <TextBlock.Inlines>
			    <Run Text="a" />
			    <Span>
			      <Run Text="b" />
			    </Span>
			    <Run Text="c" />
			  </TextBlock.Inlines>
			</TextBlock>
			""",

			MarkupProfiles.Xaml
		);
	}

	[TestMethod]
	public void ACommentInsideAXamlPropertyElementIsLeftAlone()
	{
		const string source =
			"<TextBlock>\n  <TextBlock.Inlines>\n    <!--a note-->\n  </TextBlock.Inlines>\n</TextBlock>\n";

		AssertChangesNothing(source, MarkupProfiles.Xaml);

		Assert.IsTrue(
			Format(source, MarkupProfiles.Xml).HasEdits,
			"a scope that does not skip the element must still lay the comment out."
		);
	}

	[TestMethod]
	public void AContainerInsideAXamlPropertyElementIsLeftAlone()
	{
		// The siblings are three levels deep, so an incomplete skip would still write into them.
		AssertChangesNothing(
			"""
			<TextBlock>
			  <TextBlock.Inlines>
			    <Span>
			      <Run Text="a" />
			      <Bold>
			        <Run Text="b" />
			        <Italic>
			          <Run Text="c" />
			        </Italic>
			      </Bold>
			    </Span>
			  </TextBlock.Inlines>
			</TextBlock>
			""",

			MarkupProfiles.Xaml
		);
	}

	[TestMethod]
	public void TheProfilesOwnTheirExtensions()
	{
		Assert.AreEqual(MarkupProfiles.Xml, MarkupProfiles.FindByPath("a.xml"));
		Assert.AreEqual(MarkupProfiles.Xml, MarkupProfiles.FindByPath("A.CONFIG"));
		Assert.AreEqual(MarkupProfiles.MsBuild, MarkupProfiles.FindByPath("a.csproj"));
		Assert.AreEqual(MarkupProfiles.MsBuild, MarkupProfiles.FindByPath("dir/a.props"));
		Assert.AreEqual(MarkupProfiles.Slnx, MarkupProfiles.FindByPath("a.slnx"));
		Assert.AreEqual(MarkupProfiles.Xaml, MarkupProfiles.FindByPath("a.axaml"));
		Assert.IsNull(MarkupProfiles.FindByPath("a.txt"));
		Assert.AreEqual(MarkupProfiles.Xml, MarkupProfiles.FindByName("XML"));
		Assert.IsNull(MarkupProfiles.FindByName("nope"));
	}

	[TestMethod]
	[DataRow(".csproj")]
	[DataRow(".vbproj")]
	[DataRow(".fsproj")]
	[DataRow(".vcxproj")]
	[DataRow(".props")]
	[DataRow(".targets")]
	[DataRow(".proj")]
	[DataRow(".projitems")]
	[DataRow(".shproj")]
	public void EveryMsBuildFileTypeIsClaimedByTheMsBuildProfile(string extension) =>
		Assert.AreEqual(MarkupProfiles.MsBuild, MarkupProfiles.FindByPath($"dir/a{extension}"));

	[TestMethod]
	public void AGapBesideAWrappedLeafKeepsItsBlankLineUnderContainers()
	{
		const string wrapped = """
			<ItemGroup>
			  <PackageReference
			    Include="a" />

			  <PackageReference Include="b" />
			</ItemGroup>
			""";

		const string singleLine = """
			<ItemGroup>
			  <PackageReference Include="a" />

			  <PackageReference Include="b" />
			</ItemGroup>
			""";

		MarkupOptions options = new(
			MarkupProfiles.MsBuild.Policy,
			Rules: Rules(MarkupDiagnostics.KeepSingleLineMarkupElementsTogetherId)
		);

		Assert.IsFalse(Format(LineFeed(wrapped), options).HasEdits);

		MarkupFormatResult joined = Format(LineFeed(singleLine), options);

		Assert.AreEqual(
			LineFeed(
				"""
				<ItemGroup>
				  <PackageReference Include="a" />
				  <PackageReference Include="b" />
				</ItemGroup>
				"""
			),

			joined.Text
		);
	}
}
