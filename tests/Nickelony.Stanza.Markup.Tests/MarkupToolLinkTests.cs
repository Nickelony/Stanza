using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Collection;
using Nickelony.Stanza.Tests.Shared;

using static Nickelony.Stanza.Markup.Tests.MarkupFixtures;
using static Nickelony.Stanza.Tests.Shared.MarkupFormatFixtures;

namespace Nickelony.Stanza.Markup.Tests;

[TestClass]
public sealed class MarkupToolLinkTests
{
	[TestMethod]
	public void ALinkedFileIsLeftAloneInBothPositions()
	{
		using TempDirectory directory = new();
		string target = directory.Write("target.xml", LineFeed(Formatted));

		LinkOrSkip(Path.Combine(directory.Root, "link.xml"), target);

		RunResult named = Run(directory, "check", "link.xml");

		Assert.AreEqual(0, named.ExitCode);
		Assert.Contains("is a link and was left alone", named.Error);

		RunResult scanned = Run(directory, "check", ".");

		Assert.AreEqual(0, scanned.ExitCode);
		Assert.Contains("is a link and was left alone", scanned.Error);
	}

	[TestMethod]
	public void ALinkWarningNamesTheLinkAsItsOriginUnderTheMsBuildShape()
	{
		using TempDirectory directory = new();
		string target = directory.Write("target.xml", LineFeed(Formatted));

		LinkOrSkip(Path.Combine(directory.Root, "link.xml"), target);

		foreach (string argument in new[] { "link.xml", "." })
		{
			RunResult result = Run(directory, "check", "--format", "msbuild", argument);
			Assert.Contains("link.xml : warning : 'link.xml' is a link", result.Output, argument);
		}
	}

	// A format leaves the link and the file it points to as they are, so the target keeps its text.
	[TestMethod]
	public void FormatLeavesALinkedFileAndItsTargetAlone()
	{
		using TempDirectory directory = new();
		string target = directory.Write("target.xml", LineFeed(Source));

		LinkOrSkip(Path.Combine(directory.Root, "link.xml"), target);

		RunResult named = Run(directory, "format", "link.xml");

		Assert.AreEqual(0, named.ExitCode);
		Assert.Contains("is a link and was left alone", named.Error);
		Assert.AreEqual(LineFeed(Source), directory.Read("target.xml"));

		// A write moves a new file over the name, which would leave the target as it was and the link replaced.
		Assert.IsNotNull(
			new FileInfo(Path.Combine(directory.Root, "link.xml")).LinkTarget,
			"The link was replaced by a file."
		);
	}

	// The .editorconfig is read before the link policy, so a disabled named link gets the note --strict never escalates.
	[TestMethod]
	public void ANamedLinkThatTheEditorConfigDisablesIsANote()
	{
		using TempDirectory directory = new();
		string target = directory.Write("target.xml", LineFeed(Formatted));

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[link.xml]\nstanza_markup_enabled = false\n")
		);

		LinkOrSkip(Path.Combine(directory.Root, "link.xml"), target);

		RunResult named = Run(directory, "check", "link.xml", "--strict");

		Assert.AreEqual(0, named.ExitCode, named.Error);
		Assert.Contains("is disabled by its .editorconfig", named.Error);
		Assert.DoesNotContain("is a link", named.Error);
	}

	[TestMethod]
	public void ALinkToAFileNoProfileClaimsIsNotReportedByAScan()
	{
		using TempDirectory directory = new();
		string target = directory.Write("target.txt", "text\n");

		_ = directory.Write("a.xml", LineFeed(Formatted));

		LinkOrSkip(Path.Combine(directory.Root, "link.txt"), target);

		RunResult scanned = Run(directory, "check", ".", "--strict");

		Assert.AreEqual(0, scanned.ExitCode);
		Assert.DoesNotContain("is a link", scanned.Error);
	}

	// A hook names each changed file, links included, so a named link to a file no profile claims gets the note a file gets.
	[TestMethod]
	public void ANamedLinkToAFileNoProfileClaimsIsANote()
	{
		using TempDirectory directory = new();
		string target = directory.Write("target.txt", "text\n");

		LinkOrSkip(Path.Combine(directory.Root, "link.txt"), target);

		RunResult named = Run(directory, "check", "link.txt", "--strict");

		Assert.AreEqual(0, named.ExitCode, named.Error);
		Assert.Contains("is not a file type any profile claims", named.Error);
		Assert.DoesNotContain("is a link", named.Error);
	}

	// A scan reads the .editorconfig before the link policy too, so a link in a disabled directory fails no --strict run.
	[TestMethod]
	public void ALinkTheEditorConfigDisablesIsNotReportedByAScan()
	{
		using TempDirectory directory = new();
		string target = directory.Write("target.xml", LineFeed(Formatted));

		_ = directory.Write("vendor/kept.xml", LineFeed(Formatted));

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[vendor/**]\nstanza_markup_enabled = false\n")
		);

		LinkOrSkip(Path.Combine(directory.Root, "vendor", "link.xml"), target);

		RunResult scanned = Run(directory, "check", ".", "--strict");

		Assert.AreEqual(0, scanned.ExitCode, scanned.Error);
		Assert.DoesNotContain("is a link", scanned.Error);
	}

	// The listing of a directory is denied through an access list on Windows and the mode on Unix, and a scan reports either.
	[TestMethod]
	public void ASubdirectoryTheScanCannotReadIsAWarningAndAnErrorUnderStrict()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.xml", LineFeed(Formatted));
		_ = directory.Write("locked/sample.xml", LineFeed(Source));

		using var denied = DeniedDirectory.DenyOrSkip(Path.Combine(directory.Root, "locked"));

		RunResult relaxed = Run(directory, "check", ".");

		Assert.AreEqual(0, relaxed.ExitCode);
		Assert.Contains("'locked' could not be scanned", relaxed.Error);

		RunResult strict = Run(directory, "check", "--strict", ".");

		Assert.AreEqual(2, strict.ExitCode);

		// The warning names the directory as its origin, so the Error List can point at it.
		RunResult msbuild = Run(directory, "check", "--format", "msbuild", ".");

		Assert.Contains("locked : warning : 'locked' could not be scanned", msbuild.Output);
	}

	// A glob rooted at a drive meets many directories it cannot list, and one line per directory floods the run.
	[TestMethod]
	public void SubdirectoriesTheScanCannotReadAreOneWarningThatCountsThem()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.xml", LineFeed(Formatted));
		_ = directory.Write("first/sample.xml", LineFeed(Source));
		_ = directory.Write("second/sample.xml", LineFeed(Source));
		_ = directory.Write("third/sample.xml", LineFeed(Formatted));

		using var firstDenied = DeniedDirectory.DenyOrSkip(Path.Combine(directory.Root, "first"));
		using var secondDenied = DeniedDirectory.DenyOrSkip(Path.Combine(directory.Root, "second"));

		RunResult relaxed = Run(directory, "check", "**/*.xml");

		Assert.AreEqual(0, relaxed.ExitCode, relaxed.Error);

		string[] warnings =
		[
			.. relaxed
				.Error.Split('\n')
				.Where(static line => line.Contains("could not be scanned")),
		];

		Assert.HasCount(1, warnings);
		Assert.Contains("2 directories could not be scanned, the first being '", warnings[0]);

		RunResult msbuild = Run(directory, "check", "--format", "msbuild", "**/*.xml");

		Assert.Contains(" : warning : 2 directories could not be scanned", msbuild.Output);
	}

	// No entry of Windows or Unix makes the read of a target fail on demand, so a reader that throws stands in.
	[TestMethod]
	public void AnEntryWhoseTargetCannotBeReadCountsAsALinkAndNamesTheReason()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", LineFeed(Source));

		bool io = MarkupLinkPolicy.IsLinkOrUnreadable(
			new FileInfo(path),
			static _ => throw new IOException("The target is not readable"),
			out string? ioError
		);

		bool denied = MarkupLinkPolicy.IsLinkOrUnreadable(
			new FileInfo(path),
			static _ => throw new UnauthorizedAccessException("The target is denied"),
			out string? deniedError
		);

		Assert.IsTrue(io);
		Assert.AreEqual("The target is not readable", ioError);
		Assert.IsTrue(denied);
		Assert.AreEqual("The target is denied", deniedError);

		Assert.AreEqual(
			"'sample.xml' could not be inspected: The target is denied.",
			MarkupLinkPolicy.Describe("sample.xml", deniedError)
		);

		// A system message that ends its own sentence keeps one period.
		Assert.AreEqual(
			"'sample.xml' could not be inspected: Access is denied.",
			MarkupLinkPolicy.Describe("sample.xml", "Access is denied.")
		);
	}

	[TestMethod]
	public void AFailureThatIsNoAccessFailureIsNotTakenForALink()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", LineFeed(Source));

		_ = Assert.ThrowsExactly<InvalidOperationException>(() =>
			MarkupLinkPolicy.IsLinkOrUnreadable(
				new FileInfo(path),
				static _ => throw new InvalidOperationException("A defect."),
				out _
			)
		);
	}

	[TestMethod]
	public void AnEntryWithATargetIsALinkAndOneWithoutIsNot()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", LineFeed(Source));

		Assert.IsTrue(
			MarkupLinkPolicy.IsLinkOrUnreadable(
				new FileInfo(path),
				static _ => "elsewhere.xml",
				out string? linkError
			)
		);

		Assert.IsNull(linkError);

		Assert.IsFalse(
			MarkupLinkPolicy.IsLinkOrUnreadable(
				new FileInfo(path),
				static _ => null,
				out string? plainError
			)
		);

		Assert.IsNull(plainError);
	}

	// Unix returns no link target where it cannot read one, so a real entry never reaches the inspection catch there:
	// a directory the scan cannot read is no link and no inspection failure, and the walk reports it as not scanned.
	[TestMethod]
	[OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
	[UnsupportedOSPlatform("windows")]
	public void AnUnreadableDirectoryIsNeitherALinkNorAnInspectionFailureOnUnix()
	{
		using TempDirectory directory = new();
		_ = directory.Write("locked/sample.xml", LineFeed(Source));

		string locked = Path.Combine(directory.Root, "locked");
		using var denied = DeniedDirectory.DenyOrSkip(locked);

		bool isLink = MarkupLinkPolicy.IsLinkOrUnreadable(
			new DirectoryInfo(locked),
			out string? error
		);

		Assert.IsFalse(isLink);
		Assert.IsNull(error);

		RunResult result = Run(directory, "check", ".");

		Assert.Contains("'locked' could not be scanned", result.Error);
		Assert.DoesNotContain("could not be inspected", result.Error);
	}

	[TestMethod]
	public void ALinkedDirectoryIsScannedWhenNamedAndNeverEnteredWhenFound()
	{
		using TempDirectory directory = new();
		_ = directory.Write("real/sample.xml", LineFeed(Source));

		if (
			!TempDirectory.TryCreateDirectoryLink(
				Path.Combine(directory.Root, "linkdir"),
				Path.Combine(directory.Root, "real")
			)
		)
		{
			Assert.Inconclusive(
				"This machine allows neither a symbolic link nor a junction for a directory."
			);
		}

		RunResult named = Run(directory, "format", "linkdir");

		Assert.AreEqual(0, named.ExitCode);
		Assert.Contains("1 file(s) formatted.", named.Output);
		Assert.AreEqual(LineFeed(Formatted), directory.Read("real/sample.xml"));

		_ = directory.Write("real/sample.xml", LineFeed(Source));

		RunResult scanned = Run(directory, "check", ".");

		Assert.AreEqual(1, scanned.ExitCode);
		Assert.Contains("real/sample.xml", scanned.Output);
		Assert.DoesNotContain("linkdir/sample.xml", scanned.Output);
	}

	// A linked directory named on the command line is scanned, so the real directory and the link reach one file twice.
	[TestMethod]
	[DataRow("real/sample.xml", "linkdir/sample.xml")]
	[DataRow("linkdir/sample.xml", "real/sample.xml")]
	[DataRow("real", "linkdir")]
	[DataRow("linkdir", "real")]
	[DataRow("real/*.xml", "linkdir/*.xml")]
	public void AFileThatTwoSpellingsReachThroughALinkedDirectoryIsInspectedOnce(
		string first,
		string second
	)
	{
		using TempDirectory directory = new();
		_ = directory.Write("real/sample.xml", LineFeed(Source));

		if (
			!TempDirectory.TryCreateDirectoryLink(
				Path.Combine(directory.Root, "linkdir"),
				Path.Combine(directory.Root, "real")
			)
		)
		{
			Assert.Inconclusive(
				"This machine allows neither a symbolic link nor a junction for a directory."
			);
		}

		RunResult result = Run(directory, "check", first, second);

		Assert.AreEqual(1, result.ExitCode, result.Error);
		Assert.AreEqual(1, result.Output.Split("needs formatting: ").Length - 1, result.Output);
		Assert.Contains("1 file(s) need formatting.", result.Output);
	}

	private static void LinkOrSkip(string linkPath, string targetPath)
	{
		if (!TempDirectory.TryCreateLink(linkPath, targetPath, directory: false))
		{
			Assert.Inconclusive(
				"This machine does not allow a symbolic link to be created without elevation."
			);
		}
	}
}
