using System;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Files;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Markup.Tests;

[TestClass]
public sealed class MarkupAtomicWriteTests
{
	private const string Document = """
		<root>
		  <child>café</child>
		</root>

		""";

	[TestMethod]
	public void AFileThatChangedAfterItWasReadIsNotOverwritten()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", Document);

		if (!MarkupSource.TryRead(path, out MarkupSource? source, out _))
			Assert.Fail("The file was refused.");

		File.WriteAllText(path, Document);
		File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(5));

		Assert.IsFalse(source.TryWrite(path, "<root/>", out string? error));
		Assert.Contains("changed", error!);
		Assert.AreEqual(Document, File.ReadAllText(path));
	}

	[TestMethod]
	public void AFileDeletedAfterItWasReadIsNotWritten()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", Document);

		if (!MarkupSource.TryRead(path, out MarkupSource? source, out _))
			Assert.Fail("The file was refused.");

		File.Delete(path);

		Assert.IsFalse(source.TryWrite(path, "<root/>", out string? error));
		Assert.IsNotNull(error);
		Assert.IsFalse(File.Exists(path), "the write recreated a file that was deleted.");
		Assert.IsEmpty(MarkupFixtures.GetTemporaryFiles(directory));
	}

	// A replace that fails after it moved the original away leaves the temp file as the only copy, so it takes the original's place.
	[TestMethod]
	public void AReplaceThatLosesTheOriginalKeepsTheOnlyCopy()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", Document);

		bool written = Replace(
			path,

			swap: (_, destination) =>
			{
				File.Delete(destination);
				throw new IOException("The replace failed halfway.");
			},

			out string? error
		);

		Assert.IsTrue(written, error);
		Assert.AreEqual("<root/>", File.ReadAllText(path));
		Assert.IsEmpty(MarkupFixtures.GetTemporaryFiles(directory));
	}

	// The fallback move is the last resort, so when it fails too the error names the temp file that keeps the text.
	[TestMethod]
	public void AFallbackMoveThatFailsNamesTheTempFileThatKeepsTheText()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", Document);

		bool written = Replace(
			path,

			swap: (temporary, destination) =>
			{
				// A directory at the original's name makes the move into its place fail as well.
				File.Delete(destination);
				_ = Directory.CreateDirectory(destination);
				throw new IOException("The replace failed halfway.");
			},

			out string? error
		);

		string[] leftovers = MarkupFixtures.GetTemporaryFiles(directory);

		Assert.IsFalse(written);
		Assert.HasCount(1, leftovers);
		Assert.Contains($"the new text is left in '{Path.GetFileName(leftovers[0])}'", error!);
		Assert.AreEqual("<root/>", File.ReadAllText(leftovers[0]));
	}

	// The temp file lives in the original's directory, so a name at the limit of the file system leaves no room to add to it.
	[TestMethod]
	public void AFileWithTheLongestNameTheFileSystemAcceptsIsWritten()
	{
		using TempDirectory directory = new();
		string path = directory.Write($"{new string('a', 251)}.xml", Document);

		if (!MarkupSource.TryRead(path, out MarkupSource? source, out _))
			Assert.Fail("The file was refused.");

		Assert.IsTrue(source.TryWrite(path, "<root/>", out string? error), error);
		Assert.AreEqual("<root/>", File.ReadAllText(path));
		Assert.IsEmpty(MarkupFixtures.GetTemporaryFiles(directory));
	}

	[TestMethod]
	public void ATempFileNameDoesNotGrowWithTheNameOfTheFile()
	{
		using TempDirectory directory = new();
		string path = directory.Write($"{new string('a', 200)}.xml", Document);

		bool written = Replace(
			path,

			swap: (temporary, destination) =>
			{
				// A directory at the original's name makes the move into its place fail as well.
				File.Delete(destination);
				_ = Directory.CreateDirectory(destination);
				throw new IOException("The replace failed halfway.");
			},

			out _
		);

		string[] leftovers = MarkupFixtures.GetTemporaryFiles(directory);

		Assert.IsFalse(written);
		Assert.HasCount(1, leftovers);
		Assert.IsLessThan(100, Path.GetFileName(leftovers[0]).Length);
		Assert.AreEqual("<root/>", File.ReadAllText(leftovers[0]));
	}

	[TestMethod]
	public void AReplaceThatFailsBeforeItMovesTheOriginalLeavesItAsItWas()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", Document);

		bool written = Replace(
			path,
			swap: (_, _) => throw new IOException("The replace failed."),
			out string? error
		);

		Assert.IsFalse(written);
		Assert.Contains("The replace failed.", error!);
		Assert.AreEqual(Document, File.ReadAllText(path));
		Assert.IsEmpty(MarkupFixtures.GetTemporaryFiles(directory));
	}

	// The file can change between the first check and the swap, so the check runs again with the temp file written.
	[TestMethod]
	public void AFileThatChangesWhileTheTempFileIsWrittenIsNotReplacedAndLeavesNoTempFile()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", Document);

		int checks = 0;
		bool swapped = false;

		MarkupWriteSteps steps = new(
			Swap: (_, _) => swapped = true,
			HasChanged: (_, _) => ++checks > 1
		);

		bool written = MarkupAtomicWrite.TryReplace(
			path,
			text: "<root/>",
			encoding: Encoding.UTF8,
			stamp: ReadStamp(path),
			steps,
			out string? error
		);

		Assert.IsFalse(written);
		Assert.AreEqual(2, checks);
		Assert.IsFalse(swapped, "the swap ran after the file changed.");
		Assert.Contains("changed after it was read", error!);
		Assert.AreEqual(Document, File.ReadAllText(path));
		Assert.IsEmpty(MarkupFixtures.GetTemporaryFiles(directory));
	}

	// The second check runs with the temp file written, so a failure there can come after the original has gone.
	[TestMethod]
	public void AnAccessFailureAfterTheOriginalWentLeavesTheTempFileAsTheOnlyCopy()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", Document);

		int checks = 0;

		MarkupWriteSteps steps = new(
			Swap: (_, _) => Assert.Fail("The swap ran after the check failed."),

			HasChanged: (_, _) =>
			{
				if (++checks < 2)
					return false;

				File.Delete(path);
				throw new IOException("The file went away under the check.");
			}
		);

		bool written = MarkupAtomicWrite.TryReplace(
			path,
			text: "<root/>",
			encoding: Encoding.UTF8,
			stamp: ReadStamp(path),
			steps,
			out string? error
		);

		string[] leftovers = MarkupFixtures.GetTemporaryFiles(directory);

		Assert.IsFalse(written);
		Assert.Contains("The file went away under the check.", error!);
		Assert.IsFalse(File.Exists(path), "the failed write brought the original back.");
		Assert.HasCount(1, leftovers);
		Assert.AreEqual("<root/>", File.ReadAllText(leftovers[0]));
	}

	// With the original still in place the new text has a copy to fall back on, so a failed write cleans up after itself.
	[TestMethod]
	public void AnAccessFailureWhileTheOriginalStandsLeavesNoTempFile()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", Document);

		int checks = 0;

		MarkupWriteSteps steps = new(
			Swap: (_, _) => Assert.Fail("The swap ran after the check failed."),

			HasChanged: (_, _) =>
			{
				if (++checks < 2)
					return false;

				throw new IOException("The check could not read the file.");
			}
		);

		bool written = MarkupAtomicWrite.TryReplace(
			path,
			text: "<root/>",
			encoding: Encoding.UTF8,
			stamp: ReadStamp(path),
			steps,
			out string? error
		);

		Assert.IsFalse(written);
		Assert.Contains("The check could not read the file.", error!);
		Assert.AreEqual(Document, File.ReadAllText(path));
		Assert.IsEmpty(MarkupFixtures.GetTemporaryFiles(directory));
	}

	// A write that keeps the mtime and changes the length is still a change, so the length is compared too.
	[TestMethod]
	public void AFileWhoseLengthChangedUnderTheSameWriteTimeIsNotOverwritten()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", Document);

		if (!MarkupSource.TryRead(path, out MarkupSource? source, out _))
			Assert.Fail("The file was refused.");

		DateTime written = File.GetLastWriteTimeUtc(path);

		File.WriteAllText(path, Document + "<!-- more -->\n");
		File.SetLastWriteTimeUtc(path, written);

		Assert.IsFalse(source.TryWrite(path, "<root/>", out string? error));
		Assert.Contains("changed", error!);
		Assert.AreEqual(Document + "<!-- more -->\n", File.ReadAllText(path));
	}

	// The temp name keeps a fixed start of the original's name, so a cut inside a surrogate pair must drop the pair.
	[TestMethod]
	public void ATempNameThatWouldCutASurrogatePairKeepsNoHalfOfIt()
	{
		using TempDirectory directory = new();

		string start = new('a', 31);
		string pair = char.ConvertFromUtf32(0x1F600);

		string path = directory.Write($"{start}{pair}.xml", Document);
		string? temporaryName = null;

		void MoveIntoPlace(string temporary, string target)
		{
			temporaryName = Path.GetFileName(temporary);
			File.Move(temporary, target, overwrite: true);
		}

		bool written = Replace(path, MoveIntoPlace, out _);

		Assert.IsTrue(written);
		Assert.IsNotNull(temporaryName);
		Assert.StartsWith(start + ".", temporaryName);
		Assert.EndsWith(MarkupAtomicWrite.TemporarySuffix, temporaryName);
		Assert.AreEqual("<root/>", File.ReadAllText(path));
	}

	private static bool Replace(string path, Action<string, string> swap, out string? error)
	{
		MarkupWriteSteps steps = MarkupAtomicWrite.DefaultSteps with { Swap = swap };

		return MarkupAtomicWrite.TryReplace(
			path,
			text: "<root/>",
			encoding: Encoding.UTF8,
			stamp: ReadStamp(path),
			steps,
			out error
		);
	}

	private static MarkupFileStamp ReadStamp(string path) =>
		new(new FileInfo(path).Length, File.GetLastWriteTimeUtc(path));

	[TestMethod]
	public void AFileThatStayedTheSameIsWritten()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", Document);

		if (!MarkupSource.TryRead(path, out MarkupSource? source, out _))
			Assert.Fail("The file was refused.");

		Assert.IsTrue(source.TryWrite(path, "<root/>", out _));
		Assert.AreEqual("<root/>", File.ReadAllText(path));
	}

	[TestMethod]
	[OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
	[UnsupportedOSPlatform("windows")]
	public void AWriteKeepsTheUnixModeOfTheFile()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", Document);

		// Group and other write bits are ones a common umask takes from a new file, so only a copied mode keeps them.
		const UnixFileMode mode =
			UnixFileMode.UserRead
			| UnixFileMode.UserWrite
			| UnixFileMode.GroupRead
			| UnixFileMode.GroupWrite
			| UnixFileMode.OtherRead
			| UnixFileMode.OtherWrite;

		File.SetUnixFileMode(path, mode);

		if (!MarkupSource.TryRead(path, out MarkupSource? source, out _))
			Assert.Fail("The file was refused.");

		Assert.IsTrue(source.TryWrite(path, "<root/>", out _));
		Assert.AreEqual(mode, File.GetUnixFileMode(path));
	}
}
