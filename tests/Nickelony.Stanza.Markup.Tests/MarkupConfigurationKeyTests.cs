using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Configuration;
using Nickelony.Stanza.Markup.Core.DocComments;
using Nickelony.Stanza.Markup.Core.Options;
using Nickelony.Stanza.Markup.Notices;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Markup.Tests;

[TestClass]
public sealed class MarkupConfigurationKeyTests
{
	[TestMethod]
	public void AnInvalidValueIsReportedAsAWarning()
	{
		using TempDirectory directory = new();

		string path = directory.Write("sample.xml", "<root>\r\n  <a>1</a>\r\n</root>\r\n");

		_ = directory.Write(
			".editorconfig",

			MarkupFixtures.EditorConfig(
				"[*.xml]\nstanza_markup_scope = nonsense\n"
					+ "stanza_markup_max_blank_lines = soon\n"
			)
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		_ = configuration.GetOptions(path, MarkupProfiles.Xml);

		Assert.HasCount(2, notices.Items);

		string reported = string.Join("\n", notices.Items);

		Assert.Contains("stanza_markup_scope", reported);
		Assert.Contains("stanza_markup_max_blank_lines", reported);
	}

	[TestMethod]
	public void AProfileKeyThatNamesNoProfileIsReportedAndTheExtensionDecides()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.csproj", string.Empty);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.csproj]\nstanza_markup_profile = nope\n")
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		Assert.AreEqual(MarkupProfiles.MsBuild, configuration.FindProfile(path, null));
		Assert.HasCount(1, notices.Items);
		Assert.Contains("not a profile name", notices.Items[0].Message);
	}

	[TestMethod]
	public void AFileIsLaidOutByItsExtensionWithoutAnyConfiguration()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.csproj", string.Empty);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out _
		);

		Assert.IsTrue(configuration.IsEnabled(path));
		Assert.AreEqual(MarkupProfiles.MsBuild, configuration.FindProfile(path, null));

		Assert.IsTrue(configuration.GetOptions(path, MarkupProfiles.MsBuild).Policy.ContainersOnly);
	}

	[TestMethod]
	public void AnInvalidEnabledValueIsReportedAndKeepsTheFile()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\nstanza_markup_enabled = flase\n")
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		Assert.IsTrue(configuration.IsEnabled(path));

		Assert.HasCount(1, notices.Items);
		Assert.Contains(MarkupKeys.Enabled, notices.Items[0].Message);
	}

	[TestMethod]
	public void TheDocumentedEnabledValuesAreHonored()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\nstanza_markup_enabled = FALSE\n")
		);

		MarkupConfiguration disabled = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices disabledNotices
		);

		Assert.IsFalse(disabled.IsEnabled(path));
		Assert.IsEmpty(disabledNotices.Items);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\nstanza_markup_enabled = True\n")
		);

		MarkupConfiguration enabled = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices enabledNotices
		);

		Assert.IsTrue(enabled.IsEnabled(path));
		Assert.IsEmpty(enabledNotices.Items);
	}

	[TestMethod]
	public void ADirectoryCanBeDisabled()
	{
		using TempDirectory directory = new();
		string path = directory.Write("generated/sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[**/generated/**]\nstanza_markup_enabled = false\n")
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out _
		);

		Assert.IsFalse(configuration.IsEnabled(path));
	}

	[TestMethod]
	[DataRow("-1")]
	[DataRow("soon")]
	public void AMaxBlankLinesValueThatMakesNoSenseFallsBackToOne(string value)
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig($"[*.xml]\nstanza_markup_max_blank_lines = {value}\n")
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out _
		);

		Assert.AreEqual(1, configuration.GetOptions(path, MarkupProfiles.Xml).MaxBlankLines);
	}

	[TestMethod]
	public void AnInvalidDocumentationValueIsReportedAsAWarning()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",

			MarkupFixtures.EditorConfig(
				"[*.xml]\nstanza_doc_expand = bogus\nstanza_doc_indent_style = bouncy\n"
					+ "stanza_doc_min_tail_words = many\nstanza_doc_indent_size = two\n"
			)
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		_ = configuration.GetOptions(path, MarkupProfiles.Xml);

		Assert.HasCount(4, notices.Items);

		string reported = string.Join("\n", notices.Items);

		Assert.Contains("stanza_doc_expand", reported);
		Assert.Contains("stanza_doc_indent_style", reported);
		Assert.Contains("stanza_doc_min_tail_words", reported);
		Assert.Contains("stanza_doc_indent_size", reported);
	}

	[TestMethod]
	[DataRow("stanza_markup_max_blank_lines", 1000)]
	[DataRow("stanza_max_line_length", 1000)]
	[DataRow("stanza_doc_max_line_length", 1000)]
	[DataRow("stanza_doc_min_tail_words", 1000)]
	[DataRow("stanza_doc_min_line_words", 1000)]
	[DataRow("stanza_doc_max_overflow_columns", 1000)]
	[DataRow("stanza_tab_width", 64)]
	[DataRow("stanza_doc_indent_size", 64)]
	[DataRow("tab_width", 64)]
	[DataRow("indent_size", 64)]
	public void ACountAboveItsCapIsReportedWithTheRange(string key, int cap)
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig($"[*.xml]\n{key} = {cap}\n")
		);

		MarkupConfiguration atCap = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices atCapNotices
		);

		_ = atCap.GetOptions(path, MarkupProfiles.Xml);

		Assert.IsEmpty(atCapNotices.Items);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig($"[*.xml]\n{key} = {cap + 1}\n")
		);

		MarkupConfiguration aboveCap = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices aboveCapNotices
		);

		_ = aboveCap.GetOptions(path, MarkupProfiles.Xml);

		Assert.HasCount(1, aboveCapNotices.Items);
		Assert.Contains($"from 0 to {cap}", aboveCapNotices.Items[0].Message);
	}

	[TestMethod]
	public void TheIndentationKeysAreCheckedAndTabIsAValueOfIndentSize()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\nindent_size = tab\ntab_width = 8\n")
		);

		MarkupConfiguration valid = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices validNotices
		);

		_ = valid.GetOptions(path, MarkupProfiles.Xml);

		Assert.IsEmpty(validNotices.Items);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\nindent_size = wide\ntab_width = many\n")
		);

		MarkupConfiguration invalid = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices invalidNotices
		);

		_ = invalid.GetOptions(path, MarkupProfiles.Xml);

		Assert.HasCount(2, invalidNotices.Items);

		Assert.Contains(
			"indent_size to 'wide': neither 'tab' nor",
			invalidNotices.Items[0].Message
		);

		Assert.Contains("tab_width to 'many': not a whole number", invalidNotices.Items[1].Message);
	}

	[TestMethod]
	public void AMisspelledDocumentationKeyIsReportedAsAWarning()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\nstanza_doc_expnd = all\n")
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		_ = configuration.GetOptions(path, MarkupProfiles.Xml);

		Assert.HasCount(1, notices.Items);
		Assert.Contains("stanza_doc_expnd", notices.Items[0].Message);
	}

	[TestMethod]
	public void AValidDocumentationValueIsNotReported()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",

			MarkupFixtures.EditorConfig(
				"[*.xml]\nstanza_doc_expand = sections\nstanza_doc_indent_style = hanging\n"
					+ "stanza_doc_indent_size = tab\nstanza_doc_max_line_length = 0\nstanza_doc_weak_tail_words = none\n"
			)
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		_ = configuration.GetOptions(path, MarkupProfiles.Xml);

		Assert.IsEmpty(notices.Items);
	}

	[TestMethod]
	public void AnAnalyzerOnlyKeyIsNotCheckedByTheTool()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",

			MarkupFixtures.EditorConfig(
				"[*.xml]\nstanza_named_argument_count = bogus\nstanza_assertion_roots = Verify\n"
			)
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		_ = configuration.GetOptions(path, MarkupProfiles.Xml);

		Assert.IsEmpty(notices.Items);
	}

	[TestMethod]
	public void AKeyUnderTheToolPrefixThatNothingReadsIsReported()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\nstanza_markup_scop = all\n")
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		_ = configuration.GetOptions(path, MarkupProfiles.Xml);

		Assert.HasCount(1, notices.Items);
		Assert.Contains("stanza_markup_scop", notices.Items[0].Message);
	}

	[TestMethod]
	public void AKeyOfAnotherToolIsLeftAlone()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",

			MarkupFixtures.EditorConfig(
				"[*.xml]\ncsharp_prefer_braces = true\nstanza_markup_max_blank_lines = 2\n"
			)
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		Assert.AreEqual(2, configuration.GetOptions(path, MarkupProfiles.Xml).MaxBlankLines);
		Assert.IsEmpty(notices.Items);
	}

	[TestMethod]
	public void TheReprintKeyIsOffUnlessItNamesMembers()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\nstanza_markup_doc_reprint = off\n")
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		Assert.IsNull(configuration.ResolveReprintLayout(path));
		Assert.IsEmpty(notices.Items);
	}

	[TestMethod]
	public void AForcedReprintRunsWhateverTheFilesSay()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\nstanza_markup_doc_reprint = off\n")
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out _,
			reprintForced: true
		);

		Assert.IsNotNull(configuration.ResolveReprintLayout(path));
	}

	[TestMethod]
	public void AWidthOfZeroLeavesTheMemberBodiesAlone()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",

			MarkupFixtures.EditorConfig(
				"[*.xml]\nstanza_markup_doc_reprint = members\nstanza_max_line_length = 0\n"
			)
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out _
		);

		Assert.IsNull(configuration.ResolveReprintLayout(path));
	}

	[TestMethod]
	public void AnUnreadableReprintValueIsReported()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\nstanza_markup_doc_reprint = yes\n")
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		Assert.IsNull(configuration.ResolveReprintLayout(path));
		Assert.HasCount(1, notices.Items);

		Assert.Contains("neither 'members' nor 'off'", string.Join("\n", notices.Items));
	}

	[TestMethod]
	public void TheDocumentationWidthKeySetsTheReprintWidth()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",

			MarkupFixtures.EditorConfig(
				"[*.xml]\nstanza_markup_doc_reprint = members\n"
					+ "stanza_max_line_length = 200\nstanza_doc_max_line_length = 40\n"
			)
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out _
		);

		DocLayoutOptions? reprint = configuration.ResolveReprintLayout(path);

		Assert.IsNotNull(reprint);
		Assert.AreEqual(40, reprint.Value.ContentWidth);
	}

	[TestMethod]
	public void TheTabIndentKeyReachesTheReprint()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",

			MarkupFixtures.EditorConfig(
				"[*.xml]\nstanza_markup_doc_reprint = members\n"
					+ "stanza_doc_indent_size = tab\ntab_width = 3\n"
			)
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out _
		);

		DocLayoutOptions? reprint = configuration.ResolveReprintLayout(path);

		Assert.IsNotNull(reprint);
		Assert.IsTrue(reprint.Value.IndentWithTabs);
		Assert.AreEqual(3, reprint.Value.TabColumns);
		Assert.AreEqual(3, reprint.Value.IndentSize, "a tab level is as wide as the tab.");
	}

	[TestMethod]
	public void AnAbsentIndentStyleKeepsTheTextNested()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\nstanza_markup_doc_reprint = members\n")
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out _
		);

		DocLayoutOptions? reprint = configuration.ResolveReprintLayout(path);

		Assert.IsNotNull(reprint);
		Assert.IsTrue(reprint.Value.IndentText);
	}
}
