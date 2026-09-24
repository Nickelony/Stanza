using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Cli;
using Nickelony.Stanza.Markup.Core;
using Nickelony.Stanza.Markup.Core.Formatting;
using Nickelony.Stanza.Tests.Shared;

using static Nickelony.Stanza.Markup.Tests.MarkupFixtures;
using static Nickelony.Stanza.Tests.Shared.MarkupFormatFixtures;

namespace Nickelony.Stanza.Markup.Tests;

[TestClass]
public sealed class MarkupToolFormatTests
{
	private const string Overflowing = """
		<root>
		  <a>
		    one
		  </a>



		  <b>
		    two
		  </b>
		</root>

		""";

	public static IEnumerable<object[]> GetCheckRows()
	{
		return
		[
			[
				"a file that needs formatting",
				Source,
				1,
				"needs formatting: sample.xml\n1 file(s) need formatting.\n",
			],

			["a formatted file", Formatted, 0, ""],
		];
	}

	[TestMethod]
	public void ASecondFormatRunReportsNothing()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", LineFeed(Source));

		RunResult first = Run(directory, "format", "sample.xml");

		Assert.AreEqual(0, first.ExitCode);
		Assert.Contains("formatted sample.xml", first.Output);
		Assert.Contains("1 file(s) formatted.", first.Output);
		Assert.AreEqual(LineFeed(Formatted), directory.Read("sample.xml"));

		Assert.IsEmpty(
			GetTemporaryFiles(directory),
			"a settled write replaced the file without consuming its temp file."
		);

		DateTime written = File.GetLastWriteTimeUtc(path);

		RunResult second = Run(directory, "format", "sample.xml");

		Assert.AreEqual(0, second.ExitCode);
		Assert.AreEqual(string.Empty, second.Output);
		Assert.AreEqual(string.Empty, second.Error);
		Assert.AreEqual(LineFeed(Formatted), directory.Read("sample.xml"));
		Assert.AreEqual(written, File.GetLastWriteTimeUtc(path), "a second run must not write.");
	}

	[TestMethod]
	[DynamicData(
		nameof(GetCheckRows),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public void TheCheckCommandReportsWithoutWriting(
		string name,
		string source,
		int exitCode,
		string report
	)
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", LineFeed(source));

		DateTime written = File.GetLastWriteTimeUtc(path);

		RunResult result = Run(directory, "check", "sample.xml");

		Assert.AreEqual(exitCode, result.ExitCode, name);
		Assert.AreEqual(report, LineFeed(result.Output), name);
		Assert.AreEqual(string.Empty, result.Error, name);
		Assert.AreEqual(LineFeed(source), directory.Read("sample.xml"), name);

		Assert.AreEqual(
			written,
			File.GetLastWriteTimeUtc(path),
			$"{name}: the check must not write."
		);
	}

	[TestMethod]
	public void ARunThatFailsOnOneFileStillCountsTheOthers()
	{
		using TempDirectory directory = new();
		_ = directory.Write("broken.xml", "<root>\n");
		_ = directory.Write("sample.xml", LineFeed(Source));

		RunResult result = Run(directory, "check", "--strict", ".");

		Assert.AreEqual(2, result.ExitCode);
		Assert.Contains("1 file(s) need formatting.", result.Output);
	}

	[TestMethod]
	public void TheCheckCommandCanReportEveryGapAsAnMSBuildDiagnostic()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.xml", LineFeed(Source));

		RunResult result = Run(directory, "check", "--format", "msbuild", "sample.xml");

		Assert.AreEqual(1, result.ExitCode);

		Assert.Contains(
			"sample.xml(5,1): warning ZA0101: Add a blank line before this element",
			result.Output
		);

		Assert.DoesNotContain("needs formatting", result.Output);

		// The machine-readable shape keeps the human summary line out of its stream.
		Assert.DoesNotContain("file(s)", result.Output);
	}

	[TestMethod]
	public void TheCheckCommandReportsACappedRunUnderItsOwnCode()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.xml", LineFeed(Overflowing));

		RunResult result = Run(directory, "check", "--format", "msbuild", "sample.xml");

		Assert.AreEqual(1, result.ExitCode);

		Assert.Contains(
			"sample.xml(8,1): warning ZA0104: Remove the extra blank lines before this element",
			result.Output
		);

		Assert.DoesNotContain("ZA0101", result.Output);
	}

	[TestMethod]
	public void TheCheckCommandReportsAMarkerUnderTheCommentSpacingCode()
	{
		using TempDirectory directory = new();

		_ = directory.Write("sample.xml", LineFeed("<root>\n  <!--a note-->\n</root>\n"));

		RunResult result = Run(directory, "check", "--format", "msbuild", "sample.xml");

		Assert.AreEqual(1, result.ExitCode);

		Assert.Contains(
			"sample.xml(2,3): warning ZA0105: Add one space between this comment marker and the text",
			result.Output
		);

		Assert.Contains("sample.xml(2,13): warning ZA0105:", result.Output);
	}

	[TestMethod]
	public void AFindingIsAWarningWhateverSeverityTheFileSetsForItsRule()
	{
		using TempDirectory directory = new();

		_ = directory.Write("sample.xml", LineFeed("<root>\n  <!--a note-->\n</root>\n"));

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\ndotnet_diagnostic.ZA0105.severity = error\n")
		);

		RunResult result = Run(directory, "check", "--format", "msbuild", "--strict", "sample.xml");

		Assert.AreEqual(1, result.ExitCode);
		Assert.Contains("sample.xml(2,3): warning ZA0105:", result.Output);
		Assert.DoesNotContain("error ZA0105", result.Output);
	}

	[TestMethod]
	public void ADocumentationRuleAloneDoesNotWriteAMemberBody()
	{
		using TempDirectory directory = new();

		string source = LineFeed(
			"<member name=\"X\">\n  <summary>alpha beta</summary>\n</member>\n"
		);

		_ = directory.Write("sample.xml", source);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\ndotnet_diagnostic.ZA0210.severity = warning\n")
		);

		RunResult result = Run(directory, "check", "--format", "msbuild", "sample.xml");

		Assert.AreEqual(0, result.ExitCode);
		Assert.AreEqual(string.Empty, result.Output);
		Assert.AreEqual(string.Empty, result.Error);
		Assert.AreEqual(source, directory.Read("sample.xml"));
	}

	[TestMethod]
	public void TheReprintOptionForcesTheMemberBodies()
	{
		using TempDirectory directory = new();

		_ = directory.Write(
			"sample.xml",
			LineFeed("<member name=\"X\">\n  <summary>alpha beta gamma</summary>\n</member>\n")
		);

		// The member body takes its layout from the documentation keys; the width is the one key set here.
		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\nstanza_max_line_length = 12\n")
		);

		RunResult result = Run(directory, "format", "--reprint", "sample.xml");

		Assert.AreEqual(0, result.ExitCode);
		Assert.Contains("formatted sample.xml", result.Output);

		Assert.AreEqual(
			LineFeed(
				"<member name=\"X\">\n  <summary>\n  alpha beta\n  gamma\n  </summary>\n</member>\n"
			),

			directory.Read("sample.xml")
		);

		Assert.AreEqual(0, Run(directory, "check", "--reprint", "sample.xml").ExitCode);
	}

	[TestMethod]
	public void TheReprintWritesTheBlockChildrenTight()
	{
		using TempDirectory directory = new();

		_ = directory.Write(
			"sample.xml",

			LineFeed(
				"<member name=\"X\">\n  <summary>alpha beta gamma</summary>\n"
					+ "  <returns>delta epsilon zeta</returns>\n</member>\n"
			)
		);

		_ = directory.Write(
			".editorconfig",

			MarkupFixtures.EditorConfig(
				"[*.xml]\nstanza_markup_doc_reprint = members\nstanza_max_line_length = 12\n"
			)
		);

		RunResult result = Run(directory, "format", "sample.xml");

		Assert.AreEqual(0, result.ExitCode);

		string written = directory.Read("sample.xml");

		Assert.Contains("  </summary>\n  <returns>", written);
		Assert.DoesNotContain("\n\n  <returns>", written);
	}

	[TestMethod]
	[DataRow("format broken.xml", 0, "warning: broken.xml")]
	[DataRow("format --strict broken.xml", 2, "error: broken.xml")]
	public void AParseFailureIsAWarningThatStrictRaisesToAnError(
		string command,
		int exitCode,
		string report
	)
	{
		using TempDirectory directory = new();
		_ = directory.Write("broken.xml", "<root>\n  <a>\n</root>\n");

		RunResult result = Run(directory, command.Split(' '));

		Assert.AreEqual(exitCode, result.ExitCode);
		Assert.Contains(report, result.Error);
	}

	[TestMethod]
	public void AnUnclosedFileReportsItsEndOfFileUnderMsbuild()
	{
		using TempDirectory directory = new();

		_ = directory.Write("broken.xml", LineFeed("<doc>\n<members>\n<member>\n<summary/>\n"));

		RunResult result = Run(directory, "check", "--format", "msbuild", "broken.xml");

		Assert.Contains(
			"broken.xml(5,1): warning ZA0100: could not be laid out: Unexpected end of file has occurred. "
				+ "The following elements are not closed: member, members, doc.",

			result.Output
		);
	}

	[TestMethod]
	public void AReprintIsReportedOnTheOpeningLineOfTheMember()
	{
		using TempDirectory directory = new();

		_ = directory.Write(
			"member.xml",

			LineFeed(
				"<doc>\n  <members>\n    <member name=\"M:Sample.Type.Example\">\n      <summary>\n"
					+ "      Binds the options from the configuration section and validates every value before "
					+ "the host starts, reporting failures early enough to keep the service from running with "
					+ "invalid settings.\n      </summary>\n    </member>\n  </members>\n</doc>\n"
			)
		);

		RunResult result = Run(
			directory,
			"check",
			"--format",
			"msbuild",
			"--reprint",
			"member.xml"
		);

		Assert.Contains(
			"member.xml(3,1): warning ZA0106: Reprint the body of this member",
			result.Output
		);
	}

	[TestMethod]
	public void TheProfileCanBeForcedForAFileWithAnotherExtension()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.txt", LineFeed(Source));

		RunResult result = Run(directory, "format", "--profile", "xml", "sample.txt");

		Assert.AreEqual(0, result.ExitCode);
		Assert.AreEqual(LineFeed(Formatted), directory.Read("sample.txt"));
	}

	[TestMethod]
	public void AFileNoProfileClaimsIsSkippedWithANoteThatStrictLeavesAlone()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.txt", LineFeed(Source));

		// A hook that names every changed file names files of other types too, and the run keeps passing.
		RunResult format = Run(directory, "format", "sample.txt");
		RunResult strictCheck = Run(directory, "check", "--strict", "sample.txt");

		foreach (RunResult result in new[] { format, strictCheck })
		{
			Assert.AreEqual(0, result.ExitCode);
			Assert.Contains("note: 'sample.txt'", result.Error);
			Assert.Contains("--profile", result.Error);
		}
	}

	[TestMethod]
	public void TheMsBuildShapeWritesTheRunsNoticesToStandardOutput()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.xml", LineFeed(Source));

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\nstanza_markup_max_blank_lines = many\n")
		);

		string[] arguments =
		[
			"check",
			"--format",
			"msbuild",
			"--exclude",
			"file[z-a].xml",
			"sample.xml",
			"missing/*.xml",
		];

		RunResult result = Run(directory, arguments);

		Assert.Contains(
			".editorconfig(4,1): warning : '.editorconfig' sets stanza_markup_max_blank_lines",
			result.Output
		);

		Assert.Contains(
			"stanza-markup : warning : the exclude pattern 'file[z-a].xml' is refused",
			result.Output
		);

		Assert.Contains(
			"stanza-markup : error : 'missing/*.xml' does not match any file.",
			result.Output
		);

		Assert.DoesNotContain("warning", result.Error);

		// A warning about a directory names it as its origin; one about a glob has none to name.
		Directory.CreateDirectory(Path.Combine(directory.Root, "empty"));

		RunResult scan = Run(directory, "check", "--format", "msbuild", "empty");

		Assert.Contains("empty : warning : 'empty' holds no file this run inspects", scan.Output);
	}

	[TestMethod]
	[DataRow("human", "warning: 'nested/.editorconfig' could not be read (")]
	[DataRow(
		"msbuild",
		"nested/.editorconfig : warning : 'nested/.editorconfig' could not be read ("
	)]
	public void AnEditorConfigThatCannotBeReadIsReportedUnderEachShape(string format, string report)
	{
		using TempDirectory directory = new();
		_ = directory.Write("nested/sample.xml", LineFeed(Formatted));

		// An exclusive handle keeps the file from being opened for reading.
		string locked = directory.Write(
			"nested/.editorconfig",
			"[*.xml]\nstanza_markup_scope = all\n"
		);

		using FileStream hold = new(
			path: locked,
			mode: FileMode.Open,
			access: FileAccess.ReadWrite,
			share: FileShare.None
		);

		RunResult result = Run(directory, "check", "--format", format, "nested/sample.xml");

		Assert.AreEqual(0, result.ExitCode, result.Error);
		Assert.Contains(report, result.Output + result.Error);
	}

	// A control character could start a terminal escape sequence, so a message writes its code.
	[TestMethod]
	[DataRow("human")]
	[DataRow("msbuild")]
	public void AControlCharacterInAnEditorConfigValueIsWrittenAsItsCode(string format)
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.xml", LineFeed(Formatted));

		string value = $"many{(char)0x1B}[31m";

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig($"[*.xml]\nstanza_markup_max_blank_lines = {value}\n")
		);

		RunResult result = Run(directory, "check", "--format", format, "sample.xml");
		string reported = result.Output + result.Error;

		Assert.Contains(@"to 'many\x1B[31m'", reported);
		Assert.DoesNotContain(((char)0x1B).ToString(), reported);
	}

	[TestMethod]
	[DataRow("human")]
	[DataRow("msbuild")]
	public void AControlCharacterInAParseFailureIsWrittenAsItsCode(string format)
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.xml", $"<root>{(char)0x1B}</root>\n");

		RunResult result = Run(directory, "check", "--format", format, "sample.xml");
		string reported = result.Output + result.Error;

		Assert.Contains("could not be laid out", reported);
		Assert.DoesNotContain(((char)0x1B).ToString(), reported);
	}

	[TestMethod]
	public void AnUnknownProfileIsRefused()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.xml", LineFeed(Source));

		RunResult result = Run(directory, "format", "--profile", "nope", "sample.xml");

		Assert.AreEqual(2, result.ExitCode);
		Assert.Contains("unknown profile", result.Error);
	}

	[TestMethod]
	public void AnEditorConfigCanSwitchAFileOff()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.xml", LineFeed(Source));

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[sample.xml]\nstanza_markup_enabled = false\n")
		);

		RunResult result = Run(directory, "check", ".");

		Assert.AreEqual(0, result.ExitCode);
		Assert.AreEqual(string.Empty, result.Output);
	}

	[TestMethod]
	[OSCondition(OperatingSystems.Windows)]
	public void AFileNamedTwiceInAnotherLetterCaseIsReportedOnce()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.xml", LineFeed(Source));

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[sample.xml]\nstanza_markup_enabled = false\n")
		);

		RunResult result = Run(directory, "check", "sample.xml", "SAMPLE.xml");

		Assert.AreEqual(0, result.ExitCode);

		Assert.HasCount(
			1,

			result
				.Error.Split('\n')
				.Where(static line => line.Contains("is disabled", StringComparison.Ordinal))
		);
	}

	[TestMethod]
	public void AnEditorConfigCanClaimAFileType()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.template", LineFeed(Source));

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.template]\nstanza_markup_profile = xml\n")
		);

		RunResult result = Run(directory, "format", ".");

		Assert.AreEqual(0, result.ExitCode);
		Assert.AreEqual(LineFeed(Formatted), directory.Read("sample.template"));
	}

	[TestMethod]
	public void TheCommandLineWinsOverTheEditorConfig()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.xml", LineFeed(Source));

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\nstanza_markup_profile = msbuild\n")
		);

		RunResult result = Run(directory, "format", "--profile", "xml", "sample.xml");

		Assert.AreEqual(0, result.ExitCode);
		Assert.AreEqual(LineFeed(Formatted), directory.Read("sample.xml"));
	}

	// A forced profile replaces the profile and its scope, but the budget of blank lines still comes from the file's chain.
	[TestMethod]
	public void AForcedProfileStillReadsTheBlankLineBudgetOfTheEditorConfig()
	{
		using TempDirectory directory = new();

		_ = directory.Write("sample.xml", LineFeed(Source).Replace("  </a>\n", "  </a>\n\n\n\n"));

		Assert.AreEqual(1, Run(directory, "check", "--profile", "xml", "sample.xml").ExitCode);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\nstanza_markup_max_blank_lines = 3\n")
		);

		RunResult budgeted = Run(directory, "check", "--profile", "xml", "sample.xml");

		Assert.AreEqual(0, budgeted.ExitCode, budgeted.Output + budgeted.Error);
	}

	[TestMethod]
	public void ABlankLineBudgetOfZeroLetsAnEnabledTightRuleRemoveTheGap()
	{
		using TempDirectory directory = new();

		string widened = LineFeed(Source).Replace("  </a>\n", "  </a>\n\n\n");
		_ = directory.Write("sample.xml", widened);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\nstanza_markup_max_blank_lines = 0\n")
		);

		RunResult withoutRule = Run(directory, "format", "sample.xml");

		Assert.AreEqual(0, withoutRule.ExitCode);
		Assert.AreEqual(widened, directory.Read("sample.xml"));

		_ = directory.Write(
			".editorconfig",

			MarkupFixtures.EditorConfig(
				"[*.xml]\nstanza_markup_max_blank_lines = 0\n"
					+ "dotnet_diagnostic.ZA0102.severity = warning\n"
			)
		);

		RunResult withRule = Run(directory, "format", "sample.xml");

		Assert.AreEqual(0, withRule.ExitCode);
		Assert.AreEqual(LineFeed(Source), directory.Read("sample.xml"));
	}

	[TestMethod]
	public void EveryMessageOnStandardErrorStartsWithItsLevel()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.xml", LineFeed(Formatted));

		Assert.StartsWith(
			"error: 'missing.xml' does not exist.",
			Run(directory, "check", "missing.xml").Error
		);

		Assert.StartsWith(
			"error: '*.nothing' does not match any file.",
			Run(directory, "check", "*.nothing").Error
		);

		_ = Directory.CreateDirectory(Path.Combine(directory.Root, "empty"));

		Assert.StartsWith(
			"warning: 'empty' holds no file this run inspects.",
			Run(directory, "check", "empty").Error
		);

		Assert.StartsWith(
			"error: 'empty' holds no file this run inspects.",
			Run(directory, "check", "--strict", "empty").Error
		);

		Assert.StartsWith(
			"error: unknown option",
			Run(directory, "check", "--nonsense", "sample.xml").Error,
			StringComparison.OrdinalIgnoreCase
		);
	}

	[TestMethod]
	public void TheHumanFailureCarriesThePositionOfAParseFailure()
	{
		using TempDirectory directory = new();
		_ = directory.Write("broken.xml", "<root>\n  <a>\n</root>\n");

		RunResult result = Run(directory, "check", "broken.xml");

		Assert.Contains("broken.xml(3,3) could not be laid out", result.Error);
	}

	[TestMethod]
	public void TheHumanFailureOfAnUnreadableFileNamesNoPosition()
	{
		using TempDirectory directory = new();
		_ = WriteUndecodable(directory, "legacy.xml");

		RunResult result = Run(directory, "check", "legacy.xml");

		Assert.Contains("warning: legacy.xml could not be read:", result.Error);
		Assert.DoesNotContain("legacy.xml(", result.Error);
	}

	[TestMethod]
	public void TheMsBuildFormatIsSilentAboutAFormattedFile()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.xml", LineFeed(Formatted));

		RunResult result = Run(directory, "check", "--format", "msbuild", "sample.xml");

		Assert.AreEqual(0, result.ExitCode);
		Assert.AreEqual(string.Empty, result.Output);
	}

	[TestMethod]
	public void AnUndecodableFileIsReportedAtTheFirstPositionInTheMsBuildShape()
	{
		using TempDirectory directory = new();
		_ = WriteUndecodable(directory, "legacy.xml");

		RunResult relaxed = Run(directory, "check", "--format", "msbuild", "legacy.xml");

		Assert.AreEqual(0, relaxed.ExitCode);

		Assert.Contains(
			"legacy.xml(1,1): warning ZA0100: could not be read: "
				+ "the bytes could not be decoded as utf-8, so it was left alone.",

			relaxed.Output
		);

		Assert.DoesNotContain("ZA0101", relaxed.Output);

		RunResult strict = Run(directory, "check", "--format", "msbuild", "--strict", "legacy.xml");

		Assert.AreEqual(2, strict.ExitCode);
		Assert.Contains("legacy.xml(1,1): error ZA0100:", strict.Output);
	}

	[TestMethod]
	public void AForcedProfileOnTheCommandLineIgnoresTheScopeKey()
	{
		using TempDirectory directory = new();

		// Text-only children are layout under the xml profile but content under a containers scope.
		_ = directory.Write(
			"sample.xml",
			LineFeed("<root>\n  <a>\n    one\n  </a>\n  <b>\n    two\n  </b>\n</root>\n")
		);

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[*.xml]\nstanza_markup_scope = containers\n")
		);

		Assert.AreEqual(0, Run(directory, "check", "sample.xml").ExitCode);

		Assert.AreEqual(1, Run(directory, "check", "--profile", "xml", "sample.xml").ExitCode);
	}

	[TestMethod]
	[DataRow(nameof(MarkupFileOutcome.Defect), false, true)]
	[DataRow(nameof(MarkupFileOutcome.Defect), true, true)]
	[DataRow(nameof(MarkupFileOutcome.NotWritten), false, true)]
	[DataRow(nameof(MarkupFileOutcome.NotWritten), true, true)]
	[DataRow(nameof(MarkupFileOutcome.Failed), true, true)]
	[DataRow(nameof(MarkupFileOutcome.Failed), false, false)]
	[DataRow(nameof(MarkupFileOutcome.Changed), true, false)]
	[DataRow(nameof(MarkupFileOutcome.Unchanged), true, false)]
	public void OnlyAPlainFailureIsSoftenedByTheMode(string outcome, bool strict, bool fails)
	{
		Assert.AreEqual(
			fails,
			MarkupTool.FailsTheRun(Enum.Parse<MarkupFileOutcome>(outcome), strict)
		);
	}

	// Only the overlap guard of the formatter yields a defect, so the report is checked on the failure it would give.
	[TestMethod]
	[DataRow(true, false, "error: a.xml(2,3) produced boom, so it was left alone.", "Defect")]
	[DataRow(true, true, "error: a.xml(2,3) produced boom, so it was left alone.", "Defect")]
	[DataRow(false, false, "warning: a.xml(2,3) could not be laid out: boom", "Failed")]
	[DataRow(false, true, "error: a.xml(2,3) could not be laid out: boom", "Failed")]
	public void ADefectIsAnErrorInEveryModeAndARefusalOnlyUnderStrict(
		bool isDefect,
		bool strict,
		string report,
		string outcome
	)
	{
		using StringWriter output = new();
		using StringWriter error = new();

		var failure = new MarkupFailure("boom", Line: 2, Column: 3);

		MarkupTool.ReportFailure(
			displayPath: "a.xml",
			failure,
			isDefect,
			strict,
			reporter: new MarkupHumanReporter(output, error)
		);

		MarkupFileOutcome result = MarkupTool.GetFailureOutcome(
			new MarkupFormatResult("x", [], failure) { IsDefect = isDefect }
		);

		Assert.AreEqual(outcome, result.ToString());
		Assert.AreEqual(report + Environment.NewLine, error.ToString());
		Assert.AreEqual(isDefect || strict, MarkupTool.FailsTheRun(result, strict));
	}

	[TestMethod]
	public void AMixedDocumentKeepsEveryBreakItWrote()
	{
		using TempDirectory directory = new();

		_ = directory.Write(
			"sample.xml",
			"<root>\r\n  <a>\r\n    one\r\n  </a>\n  <b>\n    two\n  </b>\r\n</root>\r\n"
		);

		RunResult first = Run(directory, "format", "sample.xml");

		Assert.AreEqual(0, first.ExitCode);

		Assert.AreEqual(
			"<root>\r\n  <a>\r\n    one\r\n  </a>\n\n  <b>\n    two\n  </b>\r\n</root>\r\n",
			directory.Read("sample.xml")
		);

		RunResult second = Run(directory, "check", "sample.xml");

		Assert.AreEqual(0, second.ExitCode);
	}

	[TestMethod]
	public void AFileWhoseReportThrowsFailsTheRunAndTheNextFileIsStillJudged()
	{
		using TempDirectory directory = new();
		_ = directory.Write("a-broken.xml", "<root>\n  <a>\n</root>\n");
		_ = directory.Write("b-sample.xml", LineFeed(Source));

		using FailingWriter error = new(failures: 1);
		using StringWriter output = new();

		int exitCode = RunWithWriters(directory, output, error, args: ["check", "."]);

		Assert.AreEqual(2, exitCode, "a file the tool could not process fails the run.");

		Assert.Contains(
			"could not be processed: IOException: The channel went away.",
			error.ToString()
		);

		Assert.Contains("b-sample.xml", output.ToString(), "the file after it is still judged.");
	}
}
