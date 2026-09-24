using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Configuration;
using Nickelony.Stanza.Markup.Core.DocComments;
using Nickelony.Stanza.Markup.Core.Formatting;
using Nickelony.Stanza.Markup.Core.Xml;
using Nickelony.Stanza.Markup.Notices;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Markup.Tests;

[TestClass]
public sealed class MarkupConfigurationSeverityTests
{
	[TestMethod]
	public void ASeveritySuffixIsNotPartOfAStanzaValue()
	{
		using TempDirectory directory = new();

		string path = directory.Write("sample.xml", "<root>\r\n  <a>1</a>\r\n</root>\r\n");

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\nstanza_markup_max_blank_lines = 3:suggestion\n")
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		Assert.AreEqual(3, configuration.GetOptions(path, MarkupProfiles.Xml).MaxBlankLines);
		Assert.IsEmpty(notices.Items);
	}

	// A null severity writes no line for the rule.
	[TestMethod]
	[DataRow("warning", true)]
	[DataRow("silent", true)]
	[DataRow("refactoring", true)]
	[DataRow(null, false)]
	[DataRow("none", false)]
	[DataRow("default", false)]
	public void OnlyASeverityLineOtherThanNoneOrDefaultSwitchesTheRuleOn(
		string? severity,
		bool isOn
	)
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		if (severity is not null)
		{
			_ = directory.Write(
				".editorconfig",

				MarkupFixtures.EditorConfig(
					$"[*.xml]\ndotnet_diagnostic.ZA0211.severity = {severity}\n"
				)
			);
		}

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		Assert.AreEqual(
			isOn,
			configuration.GetOptions(path, MarkupProfiles.Xml).Rules.Contains("ZA0211")
		);

		Assert.IsEmpty(notices.Items);
	}

	[TestMethod]
	[DataRow("ZA0101", "<root>\n  <a>\n    1\n  </a>\n  <b>\n    2\n  </b>\n</root>\n")]
	[DataRow("ZA0104", "<root>\n  <a>1</a>\n\n\n  <b>2</b>\n</root>\n")]
	[DataRow("ZA0105", "<root>\n  <!--note-->\n  <a>1</a>\n</root>\n")]
	public void ANoneSeverityDisablesTheRulesThatAlwaysRan(string id, string source)
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", source);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig($"[*.xml]\ndotnet_diagnostic.{id}.severity = none\n")
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out _
		);

		Assert.IsTrue(
			MarkupFormatter.Format(source, new MarkupOptions(MarkupProfiles.Xml.Policy)).HasEdits,
			"the rule has to run when nothing turns it off."
		);

		MarkupFormatResult result = MarkupFormatter.Format(
			source,
			configuration.GetOptions(path, MarkupProfiles.Xml)
		);

		Assert.IsFalse(
			result.HasEdits,
			$"{id} is set to none, so the tool must leave the text alone."
		);
	}

	[TestMethod]
	public void ASeverityLineOfAnotherToolSwitchesNothingOn()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\ndotnet_diagnostic.CA1000.severity = warning\n")
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		MarkupOptions options = configuration.GetOptions(path, MarkupProfiles.Xml);

		Assert.IsFalse(options.Rules.Contains("CA1000"));
		Assert.IsFalse(options.Rules.Contains("ZA0211"));
		Assert.IsEmpty(notices.Items);
	}

	[TestMethod]
	public void ASeverityLineReachesOnlyTheFilesItsSectionMatches()
	{
		using TempDirectory directory = new();

		string markup = directory.Write("sample.xml", string.Empty);
		string project = directory.Write("sample.csproj", string.Empty);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\ndotnet_diagnostic.ZA0211.severity = warning\n")
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out _
		);

		Assert.IsTrue(
			configuration.GetOptions(markup, MarkupProfiles.Xml).Rules.Contains("ZA0211")
		);

		Assert.IsFalse(
			configuration.GetOptions(project, MarkupProfiles.MsBuild).Rules.Contains("ZA0211")
		);
	}

	[TestMethod]
	public void TheMarkupGapSpacingLinesLeaveTheReprintAlone()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",

			MarkupFixtures.EditorConfig(
				"[*.xml]\nstanza_markup_doc_reprint = members\n"
					+ "dotnet_diagnostic.ZA0101.severity = warning\n"
					+ "dotnet_diagnostic.ZA0102.severity = warning\n"
			)
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		DocLayoutOptions? reprint = configuration.ResolveReprintLayout(path);

		Assert.IsNotNull(reprint);
		Assert.IsEmpty(notices.Items);
	}
}
