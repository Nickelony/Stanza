using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Format.Running;

namespace Nickelony.Stanza.Format.Tests;

[TestClass]
public sealed class FormatChecksTests
{
	[TestMethod]
	public void TheCSharpierReportKeepsEachLineItDoesNotKnow()
	{
		const string output = """
			src/A.cs: error: Was not formatted.
			  ----------------------------- Expected: Around Line 3 -----------------------------
			src/B.csproj: error: Was not formatted.
			src/C.cs: error: Failed to compile so was not formatted.
			Checked 12 files in 345ms.
			""";

		CSharpierReport report = FormatChecks.ReadCSharpierReport(output);

		Assert.AreEqual(12, report.CheckedFiles);
		Assert.AreSequenceEqual(["src/A.cs", "src/B.csproj"], report.Unformatted);

		Assert.AreSequenceEqual(
			["src/C.cs: error: Failed to compile so was not formatted."],
			report.Unknown
		);
	}

	[TestMethod]
	public void ALineThatNamesAnErrorIsAFailureAndAWarningIsNot()
	{
		const string output = """
			.\Bad.cs: error: Was not formatted due to syntax errors.
			  (1,8): error CS1001: Identifier expected
			Warning ./A.csproj - The project references another CSharpier.MsBuild.
			./B.csproj: warning: The project references another CSharpier.MsBuild.
			Checked 3 files in 345ms.
			""";

		CSharpierReport report = FormatChecks.ReadCSharpierReport(output);

		Assert.AreSequenceEqual(
			[@".\Bad.cs: error: Was not formatted due to syntax errors."],
			report.Failures
		);

		Assert.HasCount(3, report.Unknown);
	}

	[TestMethod]
	public void OnlyTheAttributesOfAnEntryPinAnEndingAndNotAPathThatSpellsOne()
	{
		const string listing = "i/lf    w/crlf  attr/text=auto        \tdocs/eol=lf.md\0";
		Assert.IsEmpty(FormatChecks.CollectWrongLineEndings(listing));
	}

	[TestMethod]
	public void AReportWithNoSummaryCheckedNoFile() =>
		Assert.AreEqual(-1, FormatChecks.ReadCSharpierReport("").CheckedFiles);

	[TestMethod]
	public void TwoTextsThatDifferInABlankLineAndInLineEndingsMatch()
	{
		Assert.IsTrue(FormatChecks.MatchesApartFromBlankLines("a\n\n\tb\n", "a\r\n\tb\r\n"));
		Assert.IsTrue(FormatChecks.MatchesApartFromBlankLines((char)0xFEFF + "a\n", "a\n"));
	}

	[TestMethod]
	public void TwoTextsThatDifferInALineDoNotMatch() =>
		Assert.IsFalse(FormatChecks.MatchesApartFromBlankLines("a\n\tb\n", "a\n    b\n"));

	[TestMethod]
	public void AWhitespaceOnlyLineDoesNotMatchTheEmptyLineCSharpierWrites() =>
		Assert.IsFalse(FormatChecks.MatchesApartFromBlankLines("a\n\nb\n", "a\n    \nb\n"));

	[TestMethod]
	public void ARunOfBlankLinesThatCSharpierShortensDoesNotMatch()
	{
		Assert.IsFalse(FormatChecks.MatchesApartFromBlankLines("a\n\nb\n", "a\n\n\nb\n"));
		Assert.IsFalse(FormatChecks.MatchesApartFromBlankLines("a\nb\n", "a\n\n\nb\n"));
	}

	[TestMethod]
	public void ABlankLineThatCSharpierDropsAtEitherEndDoesNotMatch()
	{
		Assert.IsFalse(FormatChecks.MatchesApartFromBlankLines("a\n", "\na\n"));
		Assert.IsFalse(FormatChecks.MatchesApartFromBlankLines("a\n", "a\n\n"));
	}

	[TestMethod]
	public void ARunOfBlankLinesThatCSharpierKeepsMatches()
	{
		// A raw string literal keeps its blank lines, so CSharpier's output holds the same run.
		const string raw = "s = \"\"\"\n\n\n\"\"\";\n";
		Assert.IsTrue(FormatChecks.MatchesApartFromBlankLines("a;\n" + raw, "a;\n\n" + raw));
	}

	[TestMethod]
	public void AFileWhoseEndingDiffersFromItsPinIsWrong()
	{
		const string wrongLf = "i/lf    w/crlf  attr/text eol=lf      \tdocs/a.md";
		const string wrongCrlf = "i/lf    w/lf    attr/text eol=crlf    \tsrc/C.cs";

		string listing = string.Join(
			(char)0,
			wrongLf,
			"i/lf    w/lf    attr/text eol=lf      \tdocs/b.md",
			wrongCrlf,
			"i/lf    w/crlf  attr/text=auto        \tnotes.txt",
			"i/none  w/none  attr/text eol=crlf    \tsrc/Empty.cs",
			"i/-text w/-text attr/text=auto eol=lf \ticon.png",
			"i/-text w/crlf  attr/text eol=lf      \tdata.bin"
		);

		Assert.AreSequenceEqual(
			[wrongLf, wrongCrlf],
			FormatChecks.CollectWrongLineEndings(listing)
		);
	}
}
