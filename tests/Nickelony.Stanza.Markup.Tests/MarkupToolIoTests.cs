using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

using static Nickelony.Stanza.Markup.Tests.MarkupFixtures;
using static Nickelony.Stanza.Tests.Shared.MarkupFormatFixtures;

namespace Nickelony.Stanza.Markup.Tests;

[TestClass]
public sealed class MarkupToolIoTests
{
	private const string Legacy = """
		<?xml version="1.0" encoding="windows-1252"?>
		<root>
		  <a>
		    one
		  </a>
		  <b>
		    two
		  </b>
		</root>

		""";

	private const string LegacySeparated = """
		<?xml version="1.0" encoding="windows-1252"?>
		<root>
		  <a>
		    one
		  </a>

		  <b>
		    two
		  </b>
		</root>

		""";

	private const string LegacyLatin1 = """
		<?xml version="1.0" encoding="iso-8859-1"?>
		<root>
		  <a>
		    café
		  </a>
		  <b>
		    two
		  </b>
		</root>

		""";

	// A Windows share-mode lock has no Unix equivalent, so this fixture runs on Windows only.
	[TestMethod]
	[OSCondition(OperatingSystems.Windows)]
	public void AFileThatCannotBeWrittenFailsTheRunWithTheFailureExitCode()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", LineFeed(Source));

		using FileStream handle = File.Open(
			path,
			mode: FileMode.Open,
			access: FileAccess.Read,
			share: FileShare.Read
		);

		RunResult result = Run(directory, "format", "sample.xml");

		Assert.AreEqual(2, result.ExitCode);
		Assert.Contains("could not be written", result.Error);
		Assert.AreEqual(LineFeed(Source), directory.Read("sample.xml"));

		Assert.IsEmpty(
			GetTemporaryFiles(directory),
			"a write that failed at the replace left its temp file behind."
		);
	}

	// A directory that grants no write leaves no room for the temp file, so the write fails and the file keeps its text.
	[TestMethod]
	[OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
	[UnsupportedOSPlatform("windows")]
	public void AFileInADirectoryThatCannotBeWrittenFailsTheRunOnUnix()
	{
		using TempDirectory directory = new();

		string path = directory.Write("locked/sample.xml", LineFeed(Source));
		string locked = Path.GetDirectoryName(path)!;

		File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserExecute);

		try
		{
			if (CanCreateFileIn(locked))
			{
				Assert.Inconclusive(
					"This process writes past the directory's mode, as a root process does."
				);
			}

			RunResult result = Run(directory, "format", "locked/sample.xml");

			Assert.AreEqual(2, result.ExitCode);
			Assert.Contains("could not be written", result.Error);
			Assert.AreEqual(LineFeed(Source), directory.Read("locked/sample.xml"));
		}
		finally
		{
			File.SetUnixFileMode(
				locked,
				UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
			);
		}
	}

	// A Unix file name may hold a line break, which would split a report line, so the name carries the break as its code.
	[TestMethod]
	[OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
	public void AFileNameThatHoldsALineBreakStaysOnItsReportLine()
	{
		using TempDirectory directory = new();
		_ = directory.Write("a" + (char)10 + "b.xml", LineFeed(Source));

		RunResult result = Run(directory, "check", ".", "--format", "msbuild");

		Assert.AreEqual(1, result.ExitCode, result.Error);
		Assert.Contains("a" + (char)92 + "x0Ab.xml(", result.Output);
	}

	// The file's own mark is stripped, so a second one reaches the parser, which refuses it by name.
	[TestMethod]
	public void AFileThatRepeatsItsMarkIsRefusedInTermsThatNameIt()
	{
		using TempDirectory directory = new();
		string path = Path.Combine(directory.Root, "sample.xml");

		File.WriteAllBytes(path, [0xEF, 0xBB, 0xBF, 0xEF, 0xBB, 0xBF, .. "<root/>\n"u8]);

		RunResult result = Run(directory, "check", "sample.xml");

		Assert.AreEqual(0, result.ExitCode, result.Error);

		Assert.Contains(
			"warning: sample.xml(1,1) could not be laid out: The document starts with a byte order mark (U+FEFF)",
			result.Error
		);
	}

	// A line separator ends a line on some terminals, and a right-to-left override reorders what follows it.
	// A tag character lies beyond U+FFFF and hides text, so the name is read by code point.
	[TestMethod]
	[DataRow(0x2028, "2028")]
	[DataRow(0x2029, "2029")]
	[DataRow(0x202E, "202E")]
	[DataRow(0x200B, "200B")]
	[DataRow(0xE0041, "E0041")]
	public void AFileNameThatHoldsAFormatOrSeparatorCharacterWritesItsCode(
		int codePoint,
		string code
	)
	{
		using TempDirectory directory = new();
		_ = directory.Write($"a{char.ConvertFromUtf32(codePoint)}b.xml", LineFeed(Source));

		RunResult result = Run(directory, "check", ".");

		Assert.AreEqual(1, result.ExitCode, result.Error);
		Assert.Contains($"needs formatting: a{(char)92}x{code}b.xml", result.Output);
	}

	// The scan lists a length of 0 for the file, so the read takes it for empty text without opening it.
	[TestMethod]
	public void AZeroByteFileTheScanListsIsLeftAloneAndTheOthersAreStillJudged()
	{
		using TempDirectory directory = new();
		string empty = directory.Write("empty.xml", string.Empty);

		_ = directory.Write("sample.xml", LineFeed(Source));

		// An open of the file would fail while another handle holds it, and the run would report that.
		using FileStream held = new(
			path: empty,
			mode: FileMode.Open,
			access: FileAccess.ReadWrite,
			share: FileShare.None
		);

		RunResult result = Run(directory, "check", ".", "--strict");

		Assert.AreEqual(1, result.ExitCode, result.Error);
		Assert.Contains("needs formatting: sample.xml", result.Output);
		Assert.DoesNotContain("empty.xml", result.Output + result.Error);
	}

	// A file that grants no read is reported and skipped, so the other files are still judged.
	[TestMethod]
	[OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
	[UnsupportedOSPlatform("windows")]
	public void AFileThatCannotBeReadIsAWarningOnUnix()
	{
		using TempDirectory directory = new();
		string path = directory.Write("hidden.xml", LineFeed(Source));

		File.SetUnixFileMode(path, UnixFileMode.None);

		try
		{
			if (CanRead(path))
			{
				Assert.Inconclusive(
					"This process reads past the file's mode, as a root process does."
				);
			}

			RunResult relaxed = Run(directory, "check", "hidden.xml");

			Assert.AreEqual(0, relaxed.ExitCode);
			Assert.Contains("could not be read", relaxed.Error);

			Assert.AreEqual(2, Run(directory, "check", "--strict", "hidden.xml").ExitCode);
		}
		finally
		{
			File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
		}
	}

	// Opening a FIFO blocks until a writer opens it, so a run that opened one named like a markup file would never end.
	[TestMethod]
	[OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
	public void AFifoNamedLikeAMarkupFileIsNeverOpened()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.xml", LineFeed(Formatted));

		MakeFifo(Path.Combine(directory.Root, "pipe.xml"));

		foreach (string argument in new[] { "pipe.xml", "." })
		{
			RunResult result = RunWithinTimeout(directory, "check", argument);
			Assert.AreEqual(0, result.ExitCode, $"{argument}: {result.Error}");
		}
	}

	[TestMethod]
	[OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
	public void AFifoNamedEditorConfigIsNeverOpened()
	{
		using TempDirectory directory = new();
		_ = directory.Write("nested/sample.xml", LineFeed(Formatted));

		MakeFifo(Path.Combine(directory.Root, "nested", ".editorconfig"));

		RunResult result = RunWithinTimeout(directory, "check", "nested/sample.xml");

		Assert.AreEqual(0, result.ExitCode, result.Error);
		Assert.AreEqual(string.Empty, result.Error);
	}

	private static void MakeFifo(string path)
	{
		ProcessStartInfo start = new("mkfifo")
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		};

		start.ArgumentList.Add(path);

		RunResult made = ChildProcess.Run(start, "mkfifo");

		Assert.AreEqual(0, made.ExitCode, made.Error);
	}

	private static RunResult RunWithinTimeout(TempDirectory directory, params string[] args)
	{
		Task<RunResult> run = Task.Run(() => Run(directory, args));

		Assert.IsTrue(
			run.Wait(TimeSpan.FromSeconds(20)),
			"The run did not end, as one blocked on a FIFO does not."
		);

		return run.Result;
	}

	private static bool CanCreateFileIn(string directory)
	{
		try
		{
			File.WriteAllText(Path.Combine(directory, "probe.tmp"), string.Empty);
			File.Delete(Path.Combine(directory, "probe.tmp"));

			return true;
		}
		catch (UnauthorizedAccessException)
		{
			return false;
		}
	}

	private static bool CanRead(string path)
	{
		try
		{
			_ = File.ReadAllBytes(path);
			return true;
		}
		catch (UnauthorizedAccessException)
		{
			return false;
		}
	}

	// A Windows replace needs the attribute cleared, while a Unix rename ignores it, so the tool refuses on both.
	[TestMethod]
	public void AReadOnlyFileThatCannotBeWrittenFailsTheRunWithExitCodeTwo()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", LineFeed(Source));

		File.SetAttributes(path, FileAttributes.ReadOnly);

		try
		{
			RunResult result = Run(directory, "format", "sample.xml");

			Assert.AreEqual(2, result.ExitCode);
			Assert.Contains("could not be written", result.Error);
			Assert.Contains("read-only", result.Error);
			Assert.AreEqual(LineFeed(Source), directory.Read("sample.xml"));

			Assert.IsEmpty(
				GetTemporaryFiles(directory),
				"a write that failed at the replace left its temp file behind."
			);
		}
		finally
		{
			File.SetAttributes(path, FileAttributes.Normal);
		}
	}

	// A rename drops what NTFS keeps beside the content, so the Windows write replaces the file instead.
	[TestMethod]
	[OSCondition(OperatingSystems.Windows)]
	public void AWriteKeepsTheAlternateStreamsOfTheFile()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", LineFeed(Source));

		try
		{
			File.WriteAllText(path + ":note", "kept");
		}
		catch (Exception exception)
			when (exception is IOException or NotSupportedException or UnauthorizedAccessException)
		{
			Assert.Inconclusive($"The volume holds no alternate streams: {exception.Message}");
			return;
		}

		RunResult result = Run(directory, "format", "sample.xml");

		Assert.AreEqual(0, result.ExitCode);
		Assert.AreEqual(LineFeed(Formatted), directory.Read("sample.xml"));
		Assert.AreEqual("kept", File.ReadAllText(path + ":note"));
	}

	[TestMethod]
	public void AFileWithAnUnsupportedEncodingIsLeftAlone()
	{
		using TempDirectory directory = new();
		string path = directory.Write("legacy.xml", LineFeed(Legacy));
		byte[] before = File.ReadAllBytes(path);

		RunResult result = Run(directory, "format", "legacy.xml");

		Assert.AreEqual(0, result.ExitCode);
		Assert.Contains("declares the encoding 'windows-1252'", result.Error);
		Assert.AreSequenceEqual(before, File.ReadAllBytes(path));

		RunResult strict = Run(directory, "format", "--strict", "legacy.xml");

		Assert.AreEqual(2, strict.ExitCode);
	}

	[TestMethod]
	public void ACheckJudgesAFileWithAnUnsupportedEncoding()
	{
		using TempDirectory directory = new();
		_ = directory.Write("legacy.xml", LineFeed(LegacySeparated));
		_ = directory.Write("dirty.xml", LineFeed(Legacy));

		Assert.AreEqual(0, Run(directory, "check", "--strict", "legacy.xml").ExitCode);
		Assert.AreEqual(1, Run(directory, "check", "dirty.xml").ExitCode);
	}

	// A format that would write nothing has no encoding to refuse, so a formatted file passes it as it passes a check.
	[TestMethod]
	public void AFormatRefusesAnUnsupportedEncodingOnlyForAFileItWouldWrite()
	{
		using TempDirectory directory = new();
		string path = directory.Write("legacy.xml", LineFeed(LegacySeparated));
		byte[] before = File.ReadAllBytes(path);

		RunResult result = Run(directory, "format", "--strict", "legacy.xml");

		Assert.AreEqual(0, result.ExitCode, result.Error);
		Assert.AreEqual(string.Empty, result.Error);
		Assert.AreSequenceEqual(before, File.ReadAllBytes(path));
	}

	[TestMethod]
	public void AFileThatNamesASingleByteCodePageIsFormattedInIt()
	{
		using TempDirectory directory = new();
		string path = directory.Write("legacy.xml", string.Empty);
		Encoding latin1 = Encoding.Latin1;

		string source = LineFeed(LegacyLatin1);
		string expected = source.Replace("  </a>\n  <b>", "  </a>\n\n  <b>");

		File.WriteAllText(path, source, latin1);

		RunResult result = Run(directory, "format", "legacy.xml");

		Assert.AreEqual(0, result.ExitCode);
		Assert.AreSequenceEqual(latin1.GetBytes(expected), File.ReadAllBytes(path));
	}

	[TestMethod]
	[DataRow("check")]
	[DataRow("format")]
	public void AnEmptyFileHasNothingToDo(string command)
	{
		using TempDirectory directory = new();
		_ = directory.Write("empty.xml", string.Empty);

		RunResult result = Run(directory, command, "empty.xml");

		Assert.AreEqual(0, result.ExitCode);
		Assert.AreEqual(string.Empty, result.Error);
		Assert.AreEqual(string.Empty, directory.Read("empty.xml"));
	}

	[TestMethod]
	public void AWriteLeavesAnotherHardLinkOfTheFileWithTheOldContent()
	{
		using TempDirectory directory = new();

		string first = directory.Write("a.xml", LineFeed(Source));
		string second = Path.Combine(directory.Root, "b.xml");

		if (!TempDirectory.TryCreateHardLink(second, first))
			Assert.Inconclusive("This machine does not allow a hard link to be created.");

		RunResult result = Run(directory, "format", "a.xml");

		Assert.AreEqual(0, result.ExitCode);
		Assert.AreEqual(LineFeed(Formatted), directory.Read("a.xml"));
		Assert.AreEqual(LineFeed(Source), directory.Read("b.xml"));
	}

	[TestMethod]
	public void AnOutputThatFailsIsReportedAsAToolFailure()
	{
		using TempDirectory directory = new();
		using FailingWriter output = new(failures: 1);
		using StringWriter error = new();

		int exitCode = RunWithWriters(directory, output, error, args: ["--version"]);

		Assert.AreEqual(2, exitCode, "a defect must use the documented failure code.");
		Assert.Contains("error: IOException", error.ToString());
	}
}
