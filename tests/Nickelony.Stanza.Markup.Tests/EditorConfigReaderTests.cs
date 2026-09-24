using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Configuration;
using Nickelony.Stanza.Markup.Core.Xml;
using Nickelony.Stanza.Markup.Notices;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Markup.Tests;

[TestClass]
public sealed class EditorConfigReaderTests
{
	[TestMethod]
	public void ALaterSectionOverridesAnEarlierOneInTheSameFile()
	{
		using TempDirectory directory = new();

		string path = directory.Write("sample.xml", "<root>\r\n  <a>1</a>\r\n</root>\r\n");

		_ = directory.Write(
			".editorconfig",

			MarkupFixtures.EditorConfig(
				"[*.xml]\nstanza_markup_max_blank_lines = 0\n"
					+ "[*.{xml,config}]\nstanza_markup_max_blank_lines = 2\n"
			)
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out _
		);

		Assert.AreEqual(2, configuration.GetOptions(path, MarkupProfiles.Xml).MaxBlankLines);
	}

	[TestMethod]
	[DataRow("a#b.xml", "[a\\#b.xml]")]
	[DataRow("a;b.xml", "[a\\;b.xml]")]
	public void AnEscapedCommentCharacterInASectionHeaderMatchesTheFileName(
		string name,
		string header
	)
	{
		using TempDirectory directory = new();

		string path = directory.Write(name, "<root>\r\n  <a>1</a>\r\n</root>\r\n");

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig($"{header}\nstanza_markup_max_blank_lines = 3\n")
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out _
		);

		Assert.AreEqual(3, configuration.GetOptions(path, MarkupProfiles.Xml).MaxBlankLines);
	}

	[TestMethod]
	public void APreSectionKeyIsOverriddenByANamedSection()
	{
		using TempDirectory directory = new();

		string path = directory.Write("sample.xml", "<root>\r\n  <a>1</a>\r\n</root>\r\n");
		string other = directory.Write("sample.config", "<root>\r\n  <a>1</a>\r\n</root>\r\n");

		_ = directory.Write(
			".editorconfig",

			MarkupFixtures.EditorConfig(
				"stanza_markup_max_blank_lines = 0\n[*.xml]\n"
					+ "stanza_markup_max_blank_lines = 3\n"
			)
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out _
		);

		Assert.AreEqual(3, configuration.GetOptions(path, MarkupProfiles.Xml).MaxBlankLines);

		// A file the section does not match keeps the key written before it.
		Assert.AreEqual(0, configuration.GetOptions(other, MarkupProfiles.Xml).MaxBlankLines);
	}

	[TestMethod]
	public void AnInlineCommentIsNotPartOfAValueOrASectionHeader()
	{
		using TempDirectory directory = new();

		string path = directory.Write("sample.xml", "<root>\r\n  <a>1</a>\r\n</root>\r\n");

		_ = directory.Write(
			".editorconfig",

			MarkupFixtures.EditorConfig(
				"[*.xml] # the xml files\nstanza_markup_max_blank_lines = 2 # tight\n"
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
	public void TheBuildOutputGlobMatchesANestedDirectory()
	{
		using TempDirectory directory = new();

		string generated = directory.Write("src/project/obj/sample.props", string.Empty);
		string source = directory.Write("src/project/sample.props", string.Empty);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[**/{bin,obj}/**]\nstanza_markup_enabled = false\n")
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out _
		);

		Assert.IsFalse(configuration.IsEnabled(generated));
		Assert.IsTrue(configuration.IsEnabled(source));
	}

	[TestMethod]
	public void ABraceSectionMatchesEachAlternative()
	{
		using TempDirectory directory = new();

		string project = directory.Write("a.csproj", string.Empty);
		string plain = directory.Write("b.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.{csproj,props}]\nstanza_markup_max_blank_lines = 3\n")
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out _
		);

		Assert.AreEqual(3, configuration.GetOptions(project, MarkupProfiles.MsBuild).MaxBlankLines);

		Assert.AreEqual(1, configuration.GetOptions(plain, MarkupProfiles.Xml).MaxBlankLines);
	}

	[TestMethod]
	public void AProblemInASharedEditorConfigIsReportedOnce()
	{
		using TempDirectory directory = new();

		string first = directory.Write("a/sample.xml", string.Empty);
		string second = directory.Write("b/sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[file[z-a].xml]\nstanza_markup_max_blank_lines = 2\n")
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		_ = configuration.GetOptions(first, MarkupProfiles.Xml);
		_ = configuration.GetOptions(second, MarkupProfiles.Xml);

		Assert.HasCount(1, notices.Items);
	}

	[TestMethod]
	public void AValueProblemInASharedEditorConfigIsReportedOnce()
	{
		using TempDirectory directory = new();

		string first = directory.Write("a/sample.xml", string.Empty);
		string second = directory.Write("b/sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\nstanza_markup_scope = nonsense\n")
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		_ = configuration.GetOptions(first, MarkupProfiles.Xml);
		_ = configuration.GetOptions(second, MarkupProfiles.Xml);

		Assert.HasCount(1, notices.Items);

		Assert.Contains(
			".editorconfig",
			notices.Items[0].Message,
			"The warning names the file that set the key."
		);
	}

	[TestMethod]
	public void ACachedChainStillLetsTheClosestFileWin()
	{
		using TempDirectory directory = new();

		string inside = directory.Write("nested/sample.xml", string.Empty);
		string outside = directory.Write("other/sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",

			MarkupFixtures.EditorConfig(
				"[*.xml]\nstanza_markup_max_blank_lines = 3\nstanza_markup_scope = containers\n"
			)
		);

		_ = directory.Write("nested/.editorconfig", "[*.xml]\nstanza_markup_max_blank_lines = 5\n");

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out _
		);

		MarkupOptions nested = configuration.GetOptions(inside, MarkupProfiles.Xml);
		MarkupOptions sibling = configuration.GetOptions(outside, MarkupProfiles.Xml);

		Assert.AreEqual(5, nested.MaxBlankLines);
		Assert.IsTrue(nested.Policy.ContainersOnly);
		Assert.AreEqual(3, sibling.MaxBlankLines);
		Assert.IsTrue(sibling.Policy.ContainersOnly);
	}

	[TestMethod]
	public void TheRootKeyStopsTheSearchUp()
	{
		using TempDirectory directory = new();
		string path = directory.Write("nested/sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\nstanza_markup_enabled = false\n")
		);

		_ = directory.Write("nested/.editorconfig", MarkupFixtures.EditorConfig(string.Empty));

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out _
		);

		Assert.IsTrue(configuration.IsEnabled(path));
	}

	[TestMethod]
	public void AFileNameThatStartsWithTwoDotsStillMatches()
	{
		using TempDirectory directory = new();
		string path = directory.Write("..sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\nstanza_markup_max_blank_lines = 2\n")
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out _
		);

		Assert.AreEqual(2, configuration.GetOptions(path, MarkupProfiles.Xml).MaxBlankLines);
	}

	[TestMethod]
	public void AColonSeparatesAKeyFromItsValue()
	{
		using TempDirectory directory = new();

		string path = directory.Write(
			"nested/deep/sample.xml",
			"<root>\r\n  <a>1</a>\r\n</root>\r\n"
		);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\nstanza_markup_max_blank_lines = 7\n")
		);

		_ = directory.Write("nested/.editorconfig", "root: true\n");

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out _
		);

		Assert.AreEqual(
			new MarkupOptions(MarkupProfiles.Xml.Policy).MaxBlankLines,
			configuration.GetOptions(path, MarkupProfiles.Xml).MaxBlankLines
		);
	}

	[TestMethod]
	public void AnUnsetValueRemovesTheOneAnOuterFileSet()
	{
		using TempDirectory directory = new();

		string path = directory.Write("nested/sample.xml", "<root>\r\n  <a>1</a>\r\n</root>\r\n");
		string outer = MarkupFixtures.EditorConfig("[*.xml]\nstanza_markup_max_blank_lines = 7\n");
		string inner = "[*.xml]\nstanza_markup_max_blank_lines = unset\n";

		_ = directory.Write(".editorconfig", outer);
		_ = directory.Write("nested/.editorconfig", inner);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		Assert.AreEqual(
			new MarkupOptions(MarkupProfiles.Xml.Policy).MaxBlankLines,
			configuration.GetOptions(path, MarkupProfiles.Xml).MaxBlankLines
		);

		Assert.IsEmpty(notices.Items);
	}

	[TestMethod]
	public void ALineThatIsNotAKeyWithAValueIsReported()
	{
		using TempDirectory directory = new();

		string path = directory.Write("sample.xml", "<root>\r\n  <a>1</a>\r\n</root>\r\n");

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\nstanza_markup_max_blank_lines 2\n")
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		_ = configuration.GetOptions(path, MarkupProfiles.Xml);

		Assert.HasCount(1, notices.Items);
		Assert.Contains("is not a key with a value", notices.Items[0].Message);
	}

	[TestMethod]
	public void AMalformedRootValueIsReported()
	{
		using TempDirectory directory = new();
		string path = directory.Write("nested/sample.xml", string.Empty);

		// A true root above the malformed file stops the search, so no file above the temp directory is read.
		_ = directory.Write(".editorconfig", MarkupFixtures.EditorConfig(string.Empty));

		_ = directory.Write(
			"nested/.editorconfig",
			"root = ture\n\n[*.xml]\nstanza_markup_max_blank_lines = 2\n"
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		_ = configuration.GetOptions(path, MarkupProfiles.Xml);

		Assert.HasCount(1, notices.Items);
		Assert.Contains("root", notices.Items[0].Message);
	}

	[TestMethod]
	public void AMalformedSectionHeaderIsReportedAndItsKeysAreDropped()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",

			MarkupFixtures.EditorConfig(
				"[*.xml]\nstanza_markup_max_blank_lines = 3\n[*.txt\n"
					+ "stanza_markup_max_blank_lines = 7\n"
			)
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		Assert.AreEqual(3, configuration.GetOptions(path, MarkupProfiles.Xml).MaxBlankLines);
		Assert.HasCount(1, notices.Items);
		Assert.Contains("is not a section header", notices.Items[0].Message);
	}

	// A key under the malformed header would otherwise join the section above it or every file, and the first would show.
	[TestMethod]
	public void TheKeysUnderAMalformedSectionHeaderBelongToNoFile()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",

			MarkupFixtures.EditorConfig(
				"[*.xml]\nstanza_markup_scope = containers\n[*.txt\nstanza_markup_max_blank_lines = 7\n"
			)
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out _
		);

		MarkupOptions options = configuration.GetOptions(path, MarkupProfiles.Xml);

		Assert.IsTrue(options.Policy.ContainersOnly);
		Assert.AreEqual(1, options.MaxBlankLines);
	}

	[TestMethod]
	[DataRow("= 3")]
	[DataRow(": 3")]
	public void ALineThatStartsWithItsSeparatorHasNoKeyAndIsReported(string line)
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		_ = directory.Write(".editorconfig", MarkupFixtures.EditorConfig($"[*.xml]\n{line}\n"));

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		_ = configuration.GetOptions(path, MarkupProfiles.Xml);

		Assert.HasCount(1, notices.Items);
		Assert.Contains("is not a key with a value", notices.Items[0].Message);
	}

	[TestMethod]
	public void TheLastOfTwoEqualKeysInOneSectionWins()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",

			MarkupFixtures.EditorConfig(
				"[*.xml]\nstanza_markup_max_blank_lines = 2\nstanza_markup_max_blank_lines = 5\n"
			)
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		Assert.AreEqual(5, configuration.GetOptions(path, MarkupProfiles.Xml).MaxBlankLines);
		Assert.IsEmpty(notices.Items);
	}

	[TestMethod]
	public void ARootThatIsUnsetKeepsTheSearchGoingWithoutAWarning()
	{
		using TempDirectory directory = new();
		string path = directory.Write("nested/sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\nstanza_markup_max_blank_lines = 4\n")
		);

		_ = directory.Write("nested/.editorconfig", "root = unset\n");

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		Assert.AreEqual(4, configuration.GetOptions(path, MarkupProfiles.Xml).MaxBlankLines);
		Assert.IsEmpty(notices.Items);
	}

	// Each bad line is reported with its own number, so two lines with one text are two reports.
	[TestMethod]
	public void TwoBadLinesWithTheSameTextAreEachReportedAtTheirOwnLine()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",

			MarkupFixtures.EditorConfig(
				"[*.xml]\nstanza_markup_max_blank_lines 2\nstanza_markup_max_blank_lines 2\n"
			)
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		_ = configuration.GetOptions(path, MarkupProfiles.Xml);

		Assert.HasCount(2, notices.Items);
		Assert.AreEqual(4, notices.Items[0].Line);
		Assert.AreEqual(5, notices.Items[1].Line);
	}

	[TestMethod]
	public void ASectionHeaderWithTrailingTextIsReported()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml] extra\nstanza_markup_max_blank_lines = 7\n")
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		Assert.AreEqual(1, configuration.GetOptions(path, MarkupProfiles.Xml).MaxBlankLines);
		Assert.HasCount(1, notices.Items);
		Assert.Contains("is not a section header", notices.Items[0].Message);
	}

	[TestMethod]
	public void ASectionGlobIgnoresCaseAndReadsABackslashAsAnEscape()
	{
		using TempDirectory directory = new();

		string path = directory.Write("nested/sample.xml", string.Empty);

		// A backslash escapes the character after it, so the header names a file called nestedsample.xml.
		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[nested\\sample.xml]\nstanza_markup_max_blank_lines = 4\n")
		);

		MarkupConfiguration escaped = MarkupFixtures.CreateConfiguration(directory.Root, out _);

		Assert.AreNotEqual(4, escaped.GetOptions(path, MarkupProfiles.Xml).MaxBlankLines);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[NESTED/*.XML]\nstanza_markup_max_blank_lines = 5\n")
		);

		MarkupConfiguration upperCase = MarkupFixtures.CreateConfiguration(directory.Root, out _);

		Assert.AreEqual(5, upperCase.GetOptions(path, MarkupProfiles.Xml).MaxBlankLines);
	}

	[TestMethod]
	public void AnUndocumentedSectionClassIsReported()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[file[^abc].xml]\nstanza_markup_max_blank_lines = 7\n")
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		Assert.AreEqual(1, configuration.GetOptions(path, MarkupProfiles.Xml).MaxBlankLines);
		Assert.HasCount(1, notices.Items);
		Assert.Contains("whose pattern is refused", notices.Items[0].Message);

		Assert.AreEqual(
			3,
			notices.Items[0].Line,
			"The problem names no line of its section header."
		);
	}

	[TestMethod]
	public void ABackslashThatEscapesAWildcardInASectionHeaderIsReported()
	{
		using TempDirectory directory = new();
		string path = directory.Write("src/sample.xml", string.Empty);

		// Roslyn reads the header as a file called src*.xml, so the file below src keeps its default.
		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[src\\*.xml]\nstanza_markup_max_blank_lines = 7\n")
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		Assert.AreEqual(1, configuration.GetOptions(path, MarkupProfiles.Xml).MaxBlankLines);
		Assert.HasCount(1, notices.Items);
		Assert.Contains("a backslash escapes '*'", notices.Items[0].Message);
		Assert.AreEqual(3, notices.Items[0].Line);
	}

	[TestMethod]
	public void AWholeLineCommentIsNotASetting()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",

			MarkupFixtures.EditorConfig(
				"# stanza_markup_max_blank_lines = 9\n[*.xml]\n; stanza_markup_max_blank_lines = 8\nstanza_markup_max_blank_lines = 2\n"
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
	public void AKeyIsReadWithoutRegardToCase()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\nSTANZA_Markup_Max_Blank_Lines = 2\n")
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		Assert.AreEqual(2, configuration.GetOptions(path, MarkupProfiles.Xml).MaxBlankLines);
		Assert.IsEmpty(notices.Items);
	}

	// A FIFO named .editorconfig states no length either, and opening one blocks until a writer does.
	[TestMethod]
	public void AnEditorConfigThatStatesNoLengthIsSkippedWithoutBeingOpened()
	{
		using TempDirectory directory = new();
		string path = directory.Write("nested/sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\nstanza_markup_max_blank_lines = 3\n")
		);

		string empty = directory.Write("nested/.editorconfig", string.Empty);

		using FileStream hold = new(
			path: empty,
			mode: FileMode.Open,
			access: FileAccess.ReadWrite,
			share: FileShare.None
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		Assert.AreEqual(3, configuration.GetOptions(path, MarkupProfiles.Xml).MaxBlankLines);
		Assert.IsEmpty(notices.Items);
	}

	[TestMethod]
	public void AnEditorConfigThatCannotBeReadIsReportedAndTheSearchGoesOn()
	{
		using TempDirectory directory = new();
		string path = directory.Write("nested/sample.xml", string.Empty);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\nstanza_markup_max_blank_lines = 3\n")
		);

		// An exclusive handle keeps the file from being opened for reading.
		string locked = directory.Write(
			"nested/.editorconfig",
			"[*.xml]\nstanza_markup_max_blank_lines = 9\n"
		);

		using FileStream hold = new(
			path: locked,
			mode: FileMode.Open,
			access: FileAccess.ReadWrite,
			share: FileShare.None
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		Assert.AreEqual(3, configuration.GetOptions(path, MarkupProfiles.Xml).MaxBlankLines);
		Assert.HasCount(1, notices.Items);

		string message = notices.Items[0].Message;

		Assert.StartsWith("'nested/.editorconfig' could not be read (", message);
		Assert.EndsWith("); its settings were ignored.", message);
	}
}
