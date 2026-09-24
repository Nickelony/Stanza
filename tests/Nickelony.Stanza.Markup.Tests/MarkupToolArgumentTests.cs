using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

using static Nickelony.Stanza.Markup.Tests.MarkupFixtures;
using static Nickelony.Stanza.Tests.Shared.MarkupFormatFixtures;

namespace Nickelony.Stanza.Markup.Tests;

[TestClass]
public sealed class MarkupToolArgumentTests
{
	[TestMethod]
	public void AnUnreadableCommandLinePointsAtTheHelpInsteadOfPrintingIt()
	{
		using TempDirectory directory = new();

		RunResult result = Run(directory, "check", "--bogus", "sample.xml");

		Assert.AreEqual(2, result.ExitCode);
		Assert.Contains("error: unknown option '--bogus'.", result.Error);
		Assert.Contains("Run 'stanza-markup --help' for the usage.", result.Error);
		Assert.DoesNotContain("Usage:", result.Error);
	}

	[TestMethod]
	public void AnOptionForTheOtherCommandIsNamedBeforeThePath()
	{
		using TempDirectory directory = new();

		RunResult result = Run(directory, "format", "--format", "msbuild");

		Assert.AreEqual(2, result.ExitCode);
		Assert.Contains("the --format msbuild option only applies to 'check'", result.Error);
		Assert.DoesNotContain("at least one path", result.Error);
	}

	[TestMethod]
	[DataRow(new[] { "check", "--help", "." }, "--help", true)]
	[DataRow(new[] { "-h" }, "--help", true)]
	[DataRow(new[] { "tidy", "--help" }, "--help", true)]
	[DataRow(new[] { "format", "--version", "sample.xml" }, "--version", false)]
	[DataRow(new[] { "tidy", "--version" }, "--version", false)]
	public void AHelpOrVersionSwitchWinsOverTheRestOfTheLine(
		string[] args,
		string alone,
		bool printsUsage
	)
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.xml", LineFeed(Source));

		RunResult result = Run(directory, args);
		RunResult expected = Run(directory, alone);

		Assert.AreEqual(
			0,
			result.ExitCode,
			"the switch must be honored beside a command or a stray word."
		);

		Assert.IsNotEmpty(result.Output.Trim());

		Assert.AreEqual(
			expected.Output,
			result.Output,
			"each spelling must print what the switch prints alone."
		);

		Assert.AreEqual(printsUsage, result.Output.Contains("Usage:", StringComparison.Ordinal));
		Assert.DoesNotContain("file(s)", result.Output);
		Assert.AreEqual(LineFeed(Source), directory.Read("sample.xml"));
	}

	[TestMethod]
	public void APositionalThatNamesNoCommandReportsAMissingCommand()
	{
		using TempDirectory directory = new();

		RunResult result = Run(directory, "tidy", "sample.xml");

		Assert.AreEqual(
			2,
			result.ExitCode,
			"a line whose first positional is no command must fail the run."
		);

		Assert.Contains(
			"a command is required, either 'format' or 'check', and 'tidy' is not one.",
			result.Error
		);

		Assert.DoesNotContain("unknown command", result.Error);
	}

	[TestMethod]
	public void APathIsRequired()
	{
		using TempDirectory directory = new();

		RunResult result = Run(directory, "check");

		Assert.AreEqual(2, result.ExitCode);
		Assert.Contains("at least one path", result.Error);
	}

	[TestMethod]
	public void AnEmptyPathIsRefused()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.xml", LineFeed(Source));

		RunResult result = Run(directory, "format", "");

		Assert.AreEqual(2, result.ExitCode);
		Assert.Contains("an empty path", result.Error);
		Assert.AreEqual(LineFeed(Source), directory.Read("sample.xml"));
	}

	[TestMethod]
	public void ACommandLineErrorBeatsHelp()
	{
		using TempDirectory directory = new();

		RunResult trailing = Run(directory, "--help", "--bogus");

		Assert.AreEqual(2, trailing.ExitCode, "help must not mask an option after it.");
		Assert.Contains("unknown option '--bogus'", trailing.Error, "the option must be reported.");

		RunResult leading = Run(directory, "--bogus", "--help");

		Assert.AreEqual(2, leading.ExitCode, "the option must fail the run either way.");
		Assert.Contains("unknown option '--bogus'", leading.Error, "the error must be reported.");
	}

	[TestMethod]
	public void ACommandLineErrorBeatsVersion()
	{
		using TempDirectory directory = new();

		RunResult result = Run(directory, "format", "--version", "--nonsense");

		Assert.AreEqual(2, result.ExitCode, "the version switch must not mask the option.");
		Assert.Contains("unknown option '--nonsense'", result.Error, "the error must be reported.");
	}

	[TestMethod]
	public void AnOptionThatTakesAValueDoesNotSwallowTheNextOption()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.xml", LineFeed(Source));

		RunResult profile = Run(directory, "format", "--profile", "--strict", "sample.xml");

		Assert.AreEqual(2, profile.ExitCode, "the missing profile value must fail the run.");
		Assert.Contains("needs a value", profile.Error, "the profile value is missing.");

		RunResult excluded = Run(directory, "format", "--exclude", "--strict", "sample.xml");

		Assert.AreEqual(2, excluded.ExitCode, "the missing exclude value must fail the run.");
		Assert.Contains("needs a value", excluded.Error, "the exclude value is missing.");

		RunResult trailing = Run(directory, "format", "sample.xml", "--profile");

		Assert.AreEqual(2, trailing.ExitCode, "the option at the end has no value at all.");
		Assert.Contains("needs a value", trailing.Error, "the trailing value is missing.");
	}

	[TestMethod]
	public void AnInlineValueReachesTheRun()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.txt", LineFeed(Source));
		_ = directory.Write("excluded.txt", LineFeed(Source));

		RunResult result = Run(
			directory,
			"format",
			"--profile=xml",
			"--exclude=excluded.txt",
			"*.txt"
		);

		Assert.AreEqual(0, result.ExitCode, "the inline values must be accepted.");
		Assert.AreEqual(LineFeed(Formatted), directory.Read("sample.txt"));
		Assert.AreEqual(LineFeed(Source), directory.Read("excluded.txt"));
	}

	[TestMethod]
	public void ABareDashIsRefusedAsAnUnknownOption()
	{
		using TempDirectory directory = new();

		RunResult result = Run(directory, "format", "-");

		Assert.AreEqual(2, result.ExitCode, "a bare dash must fail the run.");
		Assert.Contains("unknown option '-'", result.Error, "the bare dash must be reported.");
	}

	// A script reads the line as the version; a release tag may carry a prerelease suffix, and the build metadata is cut.
	[TestMethod]
	public void TheVersionIsTheBareVersionOnALineOfItsOwn()
	{
		using TempDirectory directory = new();

		RunResult result = Run(directory, "--version");

		Assert.AreEqual(0, result.ExitCode);
		Assert.MatchesRegex(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$", result.Output.Trim());
	}

	[TestMethod]
	public void TheDoubleDashEndsTheOptionsAndTheCommandMayFollowIt()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.xml", LineFeed(Source));

		RunResult result = Run(directory, "--", "format", "sample.xml");

		Assert.AreEqual(0, result.ExitCode);
		Assert.Contains("1 file(s) formatted.", result.Output);
		Assert.AreEqual(LineFeed(Formatted), directory.Read("sample.xml"));
	}

	[TestMethod]
	public void AnArgumentAfterTheDoubleDashIsAPathEvenWhenItStartsWithADash()
	{
		using TempDirectory directory = new();
		_ = directory.Write("-dashed.xml", LineFeed(Source));

		RunResult result = Run(directory, "format", "--", "-dashed.xml");

		Assert.AreEqual(0, result.ExitCode);
		Assert.AreEqual(LineFeed(Formatted), directory.Read("-dashed.xml"));
	}

	[TestMethod]
	public void ACommandLineWithoutACommandNamesTheMissingOne()
	{
		using TempDirectory directory = new();

		RunResult result = Run(directory);

		Assert.AreEqual(2, result.ExitCode, "a line with no command must fail the run.");
		Assert.Contains("a command is required", result.Error);
	}

	[TestMethod]
	public void ACommandLineErrorLeavesTheFileUntouched()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.xml", LineFeed(Source));

		RunResult result = Run(directory, "format", "--profile=", "sample.xml");

		Assert.AreEqual(2, result.ExitCode);
		Assert.AreEqual(LineFeed(Source), directory.Read("sample.xml"));
	}

	[TestMethod]
	public void TheCommandIsReadWithoutRegardToCase()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.xml", LineFeed(Source));

		RunResult result = Run(directory, "CHECK", "sample.xml");

		Assert.AreEqual(
			1,
			result.ExitCode,
			"the upper-case command must check rather than format."
		);

		Assert.AreEqual(LineFeed(Source), directory.Read("sample.xml"));
	}
}
