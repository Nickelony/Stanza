using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Configuration;
using Nickelony.Stanza.Markup.Notices;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Markup.Tests;

[TestClass]
public sealed class EditorConfigBoundsTests
{
	private const int HostileLength = 100_000;

	// The two lines of a file that sets one value for a.xml.
	private const string Head = "[a.xml]\nstanza_markup_max_blank_lines = 3\n";

	[TestMethod]
	public void AKeyTooLongToQuoteIsCutInItsWarning()
	{
		string key = "stanza_markup_" + new string('x', HostileLength);

		MarkupNotices notices = ReadOneSection($"{key} = 1");

		Assert.HasCount(1, notices.Items);
		AssertCut(notices.Items[0].Message);
		Assert.Contains("not a key this tool reads", notices.Items[0].Message);
	}

	[TestMethod]
	public void AValueTooLongToQuoteIsCutInItsWarning()
	{
		string value = new('9', HostileLength);

		MarkupNotices notices = ReadOneSection($"stanza_markup_max_blank_lines = {value}");

		Assert.HasCount(1, notices.Items);
		AssertCut(notices.Items[0].Message);
		Assert.Contains("not a whole number", notices.Items[0].Message);
	}

	[TestMethod]
	public void ALineTooLongToQuoteIsCutInItsWarning()
	{
		MarkupNotices notices = ReadOneSection(new string('x', HostileLength));

		Assert.HasCount(1, notices.Items);
		AssertCut(notices.Items[0].Message);
		Assert.Contains("is not a key with a value", notices.Items[0].Message);
	}

	[TestMethod]
	public void AHeaderTooLongToQuoteIsCutInItsWarning()
	{
		string header = new('a', HostileLength);

		MarkupNotices notices = Read($"[{header}.xml]\nstanza_markup_max_blank_lines = 2\n");

		Assert.HasCount(1, notices.Items);
		AssertCut(notices.Items[0].Message);
		Assert.Contains("is refused (it is longer than 1024 characters)", notices.Items[0].Message);
	}

	[TestMethod]
	public void ARootValueTooLongToQuoteIsCutInItsWarning()
	{
		string value = new('x', HostileLength);

		MarkupNotices notices = Read(
			"[*.xml]\nstanza_markup_max_blank_lines = 2\n",
			$"root = {value}\n"
		);

		// The search goes on above the fixture, so only the first warning is the fixture's own.
		AssertCut(notices.Items[0].Message);
		Assert.Contains("neither 'true' nor 'false'", notices.Items[0].Message);
	}

	[TestMethod]
	public void ASectionPastTheCapIsIgnoredWithOneWarning()
	{
		using TempDirectory directory = new();
		StringBuilder text = new();

		for (int index = 0; index <= EditorConfigReader.MaxSections; index++)
		{
			_ = text.Append(
				CultureInfo.InvariantCulture,
				$"[f{index}.xml]\nstanza_markup_max_blank_lines = 3\n"
			);
		}

		_ = directory.Write(".editorconfig", MarkupFixtures.EditorConfig(text.ToString()));

		string last = directory.Write($"f{EditorConfigReader.MaxSections - 1}.xml", string.Empty);
		string past = directory.Write($"f{EditorConfigReader.MaxSections}.xml", string.Empty);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		Assert.AreEqual(3, configuration.GetOptions(last, MarkupProfiles.Xml).MaxBlankLines);
		Assert.AreEqual(1, configuration.GetOptions(past, MarkupProfiles.Xml).MaxBlankLines);
		Assert.HasCount(1, notices.Items);
		Assert.Contains("has more than 500 sections", notices.Items[0].Message);
		Assert.AreEqual(3 + (2 * EditorConfigReader.MaxSections), notices.Items[0].Line);
	}

	[TestMethod]
	public void TextPastTheByteCapIsIgnoredWithOneWarning()
	{
		using TempDirectory directory = new();

		// The padding is one comment line longer than the cap, so the section below it lies past the cap.
		string padding = "# " + new string('x', EditorConfigReader.MaxBytes);

		_ = directory.Write(
			".editorconfig",

			MarkupFixtures.EditorConfig(
				"[a.xml]\nstanza_markup_max_blank_lines = 3\n"
					+ $"{padding}\n[b.xml]\nstanza_markup_max_blank_lines = 3\n"
			)
		);

		string kept = directory.Write("a.xml", string.Empty);
		string past = directory.Write("b.xml", string.Empty);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		Assert.AreEqual(3, configuration.GetOptions(kept, MarkupProfiles.Xml).MaxBlankLines);
		Assert.AreEqual(1, configuration.GetOptions(past, MarkupProfiles.Xml).MaxBlankLines);
		Assert.HasCount(1, notices.Items);
		Assert.Contains("is larger than 1 MiB", notices.Items[0].Message);
	}

	[TestMethod]
	public void AFileThatFillsTheByteCapExactlyIsReadWholeWithoutAWarning()
	{
		using TempDirectory directory = new();

		string section = "[a.xml]\nstanza_markup_max_blank_lines = 3\n";
		string prefix = MarkupFixtures.EditorConfig(string.Empty);

		// A comment line pads the file to the cap, line break included.
		int padding = EditorConfigReader.MaxBytes - prefix.Length - section.Length - 3;

		_ = directory.Write(
			".editorconfig",
			prefix + section + "# " + new string('x', padding) + "\n"
		);

		string path = directory.Write("a.xml", string.Empty);

		Assert.AreEqual(
			EditorConfigReader.MaxBytes,
			new FileInfo(Path.Combine(directory.Root, ".editorconfig")).Length
		);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		Assert.AreEqual(3, configuration.GetOptions(path, MarkupProfiles.Xml).MaxBlankLines);
		Assert.IsEmpty(notices.Items);
	}

	// The cut drops the setting at the end of the file, so the warning that says so comes before the problems.
	[TestMethod]
	public void TheWarningThatSettingsWereCutComesBeforeACappedListOfProblems()
	{
		StringBuilder body = new("[*.xml]\n");

		for (int index = 0; index < EditorConfigReader.MaxProblems + 50; index++)
			_ = body.Append("not a pair\n");

		_ = body.Append(Comment(EditorConfigReader.MaxBytes));
		_ = body.Append("stanza_markup_enabled = false\n");

		MarkupNotices notices = Read(body.ToString());

		Assert.HasCount(EditorConfigReader.MaxProblems + 2, notices.Items);
		Assert.Contains("is larger than 1 MiB", notices.Items[0].Message);
		Assert.Contains("is not a key with a value", notices.Items[1].Message);
		Assert.Contains("50 more problems", notices.Items[^1].Message);
	}

	[TestMethod]
	public void ASectionPastTheCapIsNamedBeforeACappedListOfProblems()
	{
		StringBuilder body = new("[*.xml]\n");

		for (int index = 0; index < EditorConfigReader.MaxProblems + 5; index++)
			_ = body.Append("not a pair\n");

		for (int index = 0; index <= EditorConfigReader.MaxSections; index++)
			_ = body.Append(CultureInfo.InvariantCulture, $"[f{index}.xml]\nk = v\n");

		MarkupNotices notices = Read(body.ToString());

		Assert.Contains("has more than 500 sections", notices.Items[0].Message);
		Assert.Contains("is not a key with a value", notices.Items[1].Message);
	}

	[TestMethod]
	public void OneProblemPastTheCapIsCountedInTheSingular()
	{
		StringBuilder body = new("[*.xml]\n");

		for (int index = 0; index < EditorConfigReader.MaxProblems + 1; index++)
			_ = body.Append("not a pair\n");

		MarkupNotices notices = Read(body.ToString());

		Assert.HasCount(EditorConfigReader.MaxProblems + 1, notices.Items);
		Assert.Contains("has 1 more problem, which is not reported", notices.Items[^1].Message);
	}

	[TestMethod]
	public void ARefusedGlobKeepsItsPlaceInLineOrderWhenTheCapIsFull()
	{
		StringBuilder body = new("[file[^abc].xml]\nstanza_markup_max_blank_lines = 7\n");

		for (int index = 0; index < EditorConfigReader.MaxProblems + 20; index++)
			_ = body.Append("not a pair\n");

		MarkupNotices notices = Read(body.ToString());

		// The glob is refused after the file is read, yet its line comes before every bad line below it.
		Assert.HasCount(EditorConfigReader.MaxProblems + 1, notices.Items);
		Assert.Contains("whose pattern is refused", notices.Items[0].Message);
		Assert.AreEqual(3, notices.Items[0].Line);
		Assert.Contains("21 more problems", notices.Items[^1].Message);
	}

	// Cutting the byte cap off at a line the cap splits must keep the settings above that line.
	[TestMethod]
	public void AFileOneBlankLineOverTheByteCapIsReadWholeWithoutAWarning()
	{
		(MarkupNotices notices, int[] values) = ReadFiles(
			Head + Comment(EditorConfigReader.MaxBytes - Head.Length) + "\n",
			"a.xml"
		);

		Assert.AreEqual(3, values[0]);
		Assert.IsEmpty(notices.Items);
	}

	[TestMethod]
	public void ALineWhoseBreakIsTheByteAfterTheCapIsReadWholeWithoutAWarning()
	{
		string comment = "# " + new string('x', EditorConfigReader.MaxBytes - Head.Length - 2);

		(MarkupNotices notices, int[] values) = ReadFiles(Head + comment + "\n", "a.xml");

		Assert.AreEqual(3, values[0]);
		Assert.IsEmpty(notices.Items);
	}

	[TestMethod]
	public void ATextByteAfterTheByteCapIsReportedAsIgnored()
	{
		(MarkupNotices notices, int[] values) = ReadFiles(
			Head + Comment(EditorConfigReader.MaxBytes - Head.Length) + "x",
			"a.xml"
		);

		Assert.AreEqual(3, values[0]);
		Assert.HasCount(1, notices.Items);
		Assert.Contains("everything after its line 3 was ignored", notices.Items[0].Message);
	}

	[TestMethod]
	public void ALineLongerThanTheByteCapIsNamedAndTheLinesAboveItAreKept()
	{
		string line = "# " + new string('x', EditorConfigReader.MaxBytes);

		(MarkupNotices notices, int[] values) = ReadFiles(Head + line, "a.xml");

		Assert.AreEqual(3, values[0]);
		Assert.HasCount(1, notices.Items);
		Assert.Contains("everything after its line 2 was ignored", notices.Items[0].Message);
	}

	[TestMethod]
	public void AFileWhoseFirstLineIsLongerThanTheByteCapIsIgnoredAndSaysSo()
	{
		(MarkupNotices notices, int[] values) = ReadFiles(
			"# " + new string('x', EditorConfigReader.MaxBytes) + "\n" + Head,
			"a.xml"
		);

		Assert.AreEqual(1, values[0]);
		Assert.HasCount(1, notices.Items);
		Assert.Contains("first line longer than 1 MiB", notices.Items[0].Message);
		Assert.Contains("the whole file was ignored", notices.Items[0].Message);
	}

	[TestMethod]
	public void LinesBrokenByCarriageReturnsAloneAreKeptUpToTheByteCap()
	{
		string head = Head.Replace('\n', '\r');
		string section = "[b.xml]\rstanza_markup_max_blank_lines = 3\r";

		(MarkupNotices notices, int[] values) = ReadFiles(
			head + Comment(EditorConfigReader.MaxBytes, '\r') + section,
			"a.xml",
			"b.xml"
		);

		Assert.AreEqual(3, values[0]);
		Assert.AreEqual(1, values[1]);
		Assert.HasCount(1, notices.Items);
		Assert.Contains("is larger than 1 MiB", notices.Items[0].Message);
	}

	[TestMethod]
	public void ALineBreakOfTwoBytesThatTheByteCapSplitsKeepsItsLine()
	{
		// The carriage return is the last byte inside the cap and the line feed the first one past it.
		string line =
			"# " + new string('x', EditorConfigReader.MaxBytes - Head.Length - 3) + "\r\n";

		(MarkupNotices notices, int[] values) = ReadFiles(
			Head + line + "[b.xml]\nstanza_markup_max_blank_lines = 3\n",
			"a.xml",
			"b.xml"
		);

		Assert.AreEqual(3, values[0]);
		Assert.AreEqual(1, values[1]);
		Assert.HasCount(1, notices.Items);
		Assert.Contains("everything after its line 3 was ignored", notices.Items[0].Message);
	}

	[TestMethod]
	public void ACharacterTheByteCapSplitsIsDroppedWithItsLine()
	{
		// The euro sign takes three bytes, and the cap falls after the first of them.
		string euro = ((char)0x20AC).ToString();

		string line =
			"# " + new string('x', EditorConfigReader.MaxBytes - Head.Length - 3) + euro + "\n";

		(MarkupNotices notices, int[] values) = ReadFiles(
			Head + line + "[b.xml]\nstanza_markup_max_blank_lines = 3\n",
			"a.xml",
			"b.xml"
		);

		Assert.AreEqual(3, values[0]);
		Assert.AreEqual(1, values[1]);
		Assert.HasCount(1, notices.Items);
		Assert.Contains("everything after its line 2 was ignored", notices.Items[0].Message);
		Assert.DoesNotContain(((char)0xFFFD).ToString(), notices.Items[0].Message);
	}

	[TestMethod]
	public void AKeyWhoseQuoteEndsInsideASurrogatePairIsCutBeforeThePair()
	{
		// The pair starts at the last character a quote keeps, so a cut after it would strand half of it.
		string pair = char.ConvertFromUtf32(0x1F600);
		string key = "stanza_markup_" + new string('x', 185) + pair + "tail";

		MarkupNotices notices = ReadOneSection($"{key} = 1");

		string message = notices.Items[0].Message;

		Assert.Contains(new string('x', 185) + "...", message);
		Assert.DoesNotContain(pair, message);
		Assert.IsFalse(HoldsALoneSurrogate(message), "the quote ends on half of a pair.");
	}

	[TestMethod]
	public void AFileOfMalformedLinesReportsTheFirstOnesAndCountsTheRest()
	{
		const int extra = 900;

		StringBuilder body = new("[*.xml]\n");

		for (int index = 0; index < EditorConfigReader.MaxProblems + extra; index++)
			_ = body.Append("not a pair\n");

		MarkupNotices notices = Read(body.ToString());

		Assert.HasCount(EditorConfigReader.MaxProblems + 1, notices.Items);

		// The malformed lines start at line 4, one below the section header.
		for (int index = 0; index < EditorConfigReader.MaxProblems; index++)
			Assert.AreEqual(4 + index, notices.Items[index].Line);

		MarkupNotice last = notices.Items[^1];

		Assert.Contains($"{extra} more problems", last.Message);
		Assert.IsNull(last.Line);
	}

	[TestMethod]
	public void AFileWithAsManyProblemsAsTheCapReportsAllOfThemWithoutACount()
	{
		StringBuilder body = new("[*.xml]\n");

		for (int index = 0; index < EditorConfigReader.MaxProblems; index++)
			_ = body.Append("not a pair\n");

		MarkupNotices notices = Read(body.ToString());

		Assert.HasCount(EditorConfigReader.MaxProblems, notices.Items);
		Assert.DoesNotContain("more problems", notices.Items[^1].Message);
	}

	[TestMethod]
	public void TheWarningsOfOneFileAreInTheOrderOfTheirLines()
	{
		// The refused glob of line 3 is found after the file is read, and the bad line of line 5 while it is read.
		MarkupNotices notices = Read(
			"[file[^abc].xml]\nstanza_markup_max_blank_lines = 7\nnot a pair\n"
		);

		Assert.HasCount(2, notices.Items);
		Assert.AreEqual(3, notices.Items[0].Line);
		Assert.Contains("whose pattern is refused", notices.Items[0].Message);
		Assert.AreEqual(5, notices.Items[1].Line);
		Assert.Contains("is not a key with a value", notices.Items[1].Message);
	}

	private static void AssertCut(string message)
	{
		Assert.IsLessThan(1_000, message.Length, "the warning quoted the whole text.");
		Assert.Contains("...", message);
	}

	private static MarkupNotices ReadOneSection(string line) => Read($"[*.xml]\n{line}\n");

	// One comment line of exactly this many bytes, break included.
	private static string Comment(int bytes, char lineBreak = '\n') =>
		"# " + new string('x', bytes - 3) + lineBreak;

	private static bool HoldsALoneSurrogate(string text)
	{
		for (int index = 0; index < text.Length; index++)
		{
			if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length)
			{
				if (!char.IsLowSurrogate(text[++index]))
					return true;
			}
			else if (char.IsSurrogate(text[index]))
			{
				return true;
			}
		}

		return false;
	}

	// The file sits in a directory of its own, so the root of the fixture stops the search above it.
	private static (MarkupNotices Notices, int[] MaxBlankLines) ReadFiles(
		string editorConfig,
		params string[] names
	)
	{
		using TempDirectory directory = new();

		_ = directory.Write(Path.Combine("sub", ".editorconfig"), editorConfig);

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		int[] values =
		[
			.. names.Select(name =>
				configuration
					.GetOptions(
						directory.Write(Path.Combine("sub", name), string.Empty),
						MarkupProfiles.Xml
					)
					.MaxBlankLines
			),
		];

		return (notices, values);
	}

	private static MarkupNotices Read(string body, string rootLine = "root = true\n")
	{
		using TempDirectory directory = new(editorConfig: false);
		string path = directory.Write("sample.xml", string.Empty);

		_ = directory.Write(".editorconfig", $"{rootLine}\n{body}");

		MarkupConfiguration configuration = MarkupFixtures.CreateConfiguration(
			directory.Root,
			out MarkupNotices notices
		);

		_ = configuration.GetOptions(path, MarkupProfiles.Xml);

		return notices;
	}
}
