using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Core.Formatting;
using Nickelony.Stanza.Markup.Core.Options;

using static Nickelony.Stanza.Tests.Shared.MarkupFormatFixtures;

namespace Nickelony.Stanza.Markup.Core.Tests;

[TestClass]
public sealed class MarkupSeparatedElementsTests
{
	private const string Menu =
		"<Menu>\n  <MenuItem Header=\"Open\" />\n  <Separator />\n  <MenuItem Header=\"Exit\" />\n</Menu>\n";

	private const string SeparatedMenu =
		"<Menu>\n  <MenuItem Header=\"Open\" />\n\n  <Separator />\n\n  <MenuItem Header=\"Exit\" />\n</Menu>\n";

	[TestMethod]
	public void AListedSelfClosingElementGetsABlankLineAboveAndBelow() =>
		Assert.AreEqual(SeparatedMenu, Settle(Menu, Listed("Separator")));

	[TestMethod]
	public void AnUnsetListChangesNothing() => AssertChangesNothing(Menu);

	[TestMethod]
	public void AListedElementWithContentGetsABlankLineAboveAndBelow()
	{
		const string source = "<Menu>\n  <a />\n  <Separator>x</Separator>\n  <b />\n</Menu>\n";

		const string expected =
			"<Menu>\n  <a />\n\n  <Separator>x</Separator>\n\n  <b />\n</Menu>\n";

		Assert.AreEqual(expected, Settle(source, Listed("Separator")));
	}

	[TestMethod]
	public void ANameMatchesTheLocalNameOfAPrefixedElement()
	{
		const string source =
			"<Menu xmlns:x=\"urn:x\" xmlns:ui=\"urn:ui\">\n  <a />\n  <x:Separator />\n  <ui:Separator>x</ui:Separator>\n  <b />\n</Menu>\n";

		const string expected =
			"<Menu xmlns:x=\"urn:x\" xmlns:ui=\"urn:ui\">\n  <a />\n\n  <x:Separator />\n\n  <ui:Separator>x</ui:Separator>\n\n  <b />\n</Menu>\n";

		Assert.AreEqual(expected, Settle(source, Listed("Separator")));
	}

	[TestMethod]
	[DataRow("separator")]
	[DataRow("SEPARATOR")]
	[DataRow("Separato")]
	[DataRow("Separators")]
	[DataRow("x:Separator")]
	public void ANameMatchesOrdinallyAndInWholeOnly(string listed) =>
		AssertChangesNothing(Menu, Listed(listed));

	[TestMethod]
	public void TwoListedNeighborsAreSplitFromEachOther()
	{
		const string source = "<Menu>\n  <Separator />\n  <Separator />\n</Menu>\n";
		const string expected = "<Menu>\n  <Separator />\n\n  <Separator />\n</Menu>\n";

		Assert.AreEqual(expected, Settle(source, Listed("Separator")));
	}

	[TestMethod]
	public void TwoListedNeighborsOfDifferentNamesAreSplitFromEachOther()
	{
		const string source = "<Menu>\n  <a />\n  <Separator />\n  <Gap />\n  <b />\n</Menu>\n";

		const string expected =
			"<Menu>\n  <a />\n\n  <Separator />\n\n  <Gap />\n\n  <b />\n</Menu>\n";

		Assert.AreEqual(expected, Settle(source, Listed("Separator, Gap")));
	}

	[TestMethod]
	public void AListedLeafCountsUnderTheContainersScopeToo()
	{
		MarkupOptions options = Listed("Separator") with
		{
			Policy = new MarkupPolicy(ContainersOnly: true),
		};

		Assert.AreEqual(SeparatedMenu, Settle(Menu, options));
	}

	[TestMethod]
	public void ListedElementsSharingALineAreLeftAsWritten() =>
		AssertChangesNothing("<Menu>\n  <a /> <Separator /> <b />\n</Menu>\n", Listed("Separator"));

	[TestMethod]
	public void ABlankLineBesideAListedElementGoesToTheMultiLineTightRule()
	{
		const string source = "<Menu>\n  <a />\n\n  <Separator />\n\n  <b />\n</Menu>\n";
		const string expected = "<Menu>\n  <a />\n  <Separator />\n  <b />\n</Menu>\n";

		MarkupOptions options = Listed("Separator") with
		{
			Rules = Rules(MarkupDiagnostics.KeepMultiLineMarkupElementsTogetherId),
		};

		Assert.AreEqual(expected, Settle(source, options));
	}

	[TestMethod]
	public void TheSingleLineTightRuleLeavesTheGapsBesideAListedElement()
	{
		MarkupOptions options = Listed("Separator") with
		{
			Rules = Rules(MarkupDiagnostics.KeepSingleLineMarkupElementsTogetherId),
		};

		Assert.AreEqual(SeparatedMenu, Settle(Menu, options));
		AssertChangesNothing(SeparatedMenu, options);
	}

	[TestMethod]
	public void AListedElementGetsTheBlankLineAboveItsComment()
	{
		const string source = "<Menu>\n  <a />\n  <!-- rule -->\n  <Separator />\n</Menu>\n";
		const string expected = "<Menu>\n  <a />\n\n  <!-- rule -->\n  <Separator />\n</Menu>\n";

		Assert.AreEqual(expected, Settle(source, Listed("Separator")));
	}

	[TestMethod]
	public void AFileThatSetsTheRuleToNoneStaysAsWritten()
	{
		MarkupOptions options = Listed("Separator") with
		{
			DisabledRules = Rules(MarkupDiagnostics.SeparateMultiLineMarkupElementsId),
		};

		AssertChangesNothing(Menu, options);
	}

	[TestMethod]
	[DataRow("Separator", "Separator")]
	[DataRow("  Separator  ", "Separator")]
	[DataRow("Separator,Gap", "Gap,Separator")]
	[DataRow("Separator , Gap ,", "Gap,Separator")]
	[DataRow(",,Separator,,", "Separator")]
	[DataRow("separator,Separator", "Separator,separator")]
	[DataRow("none,Gap", "Gap,none")]
	[DataRow("None", "None")]
	[DataRow(" None ", "None")]
	[DataRow("NONE", "NONE")]
	public void TheReaderSplitsOnCommasTrimsAndKeepsTheCase(string value, string expected)
	{
		MarkupNameSet? names = Read(value);

		Assert.IsNotNull(names);
		Assert.AreEqual(expected, names.ToString());
	}

	[TestMethod]
	[DataRow("none")]
	[DataRow(" none ")]
	[DataRow("")]
	[DataRow("   ")]
	[DataRow(",")]
	[DataRow(", ,")]
	public void TheReaderReadsNoneAnEmptyValueAndOnlySeparatorsAsNoList(string value) =>
		Assert.IsNull(Read(value));

	[TestMethod]
	public void TheValueNoneInCapitalsNamesTheNoneElementOfAProject()
	{
		const string source =
			"<ItemGroup>\n  <Compile />\n  <None />\n  <Compile />\n</ItemGroup>\n";

		const string expected =
			"<ItemGroup>\n  <Compile />\n\n  <None />\n\n  <Compile />\n</ItemGroup>\n";

		Assert.AreEqual(expected, Settle(source, Listed("None")));
		AssertChangesNothing(source, Listed("none"));
	}

	[TestMethod]
	public void TheReaderReadsNoListFromAnAbsentKey() =>
		Assert.IsNull(MarkupOptionReader.ReadSeparatedElements(static _ => null));

	[TestMethod]
	[DataRow("Separator", null)]
	[DataRow("none", null)]
	[DataRow("", null)]
	[DataRow("Menu Item", "Menu Item")]
	[DataRow("Separator,<Gap>", "<Gap>")]
	[DataRow("x:Separator", "x:Separator")]
	[DataRow("1st", "1st")]
	[DataRow("_a-b.c", null)]
	public void TheCheckFindsTheFirstEntryThatIsNoLocalName(string value, string? expected) =>
		Assert.AreEqual(expected, MarkupOptionReader.FindInvalidElementName(value));

	private static MarkupOptions Listed(string names) =>
		new(XmlPolicy, SeparatedElements: Read(names));

	private static MarkupNameSet? Read(string value)
	{
		return MarkupOptionReader.ReadSeparatedElements(key =>
			key == OptionKeys.MarkupSeparatedElements ? value : null
		);
	}

	private static string Settle(string source, MarkupOptions options)
	{
		MarkupFormatResult result = Format(source, options);

		Assert.IsNull(result.Failure);
		return result.Text;
	}
}
