using System;
using System.IO;
using System.IO.Compression;
using System.IO.Pipes;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Files;
using Nickelony.Stanza.Tests.Shared;

using static Nickelony.Stanza.Tests.Shared.MarkupFormatFixtures;

namespace Nickelony.Stanza.Markup.Tests;

[TestClass]
public sealed class MarkupSourceTests
{
	// The bound is 20 bytes: 16 for four characters of MaxBytesPerCharacter bytes each, plus one character's 4 bytes for a preamble.
	// The refusal names the 16, so a file one byte past the bound is refused with that message.
	[TestMethod]
	[DataRow(false, 20)]
	[DataRow(true, 17)]
	public void AFileAtTheByteBoundIsReadAndOneByteOverIsRefused(bool marked, int lettersAtBound)
	{
		using TempDirectory directory = new();

		byte[] mark = marked ? [0xEF, 0xBB, 0xBF] : [];

		string bound = Path.Combine(directory.Root, "bound.xml");
		string past = Path.Combine(directory.Root, "past.xml");

		File.WriteAllBytes(bound, [.. mark, .. Enumerable.Repeat((byte)'a', lettersAtBound)]);
		File.WriteAllBytes(past, [.. mark, .. Enumerable.Repeat((byte)'a', lettersAtBound + 1)]);

		Assert.IsTrue(
			MarkupSource.TryRead(
				path: bound,
				source: out MarkupSource? read,
				error: out _,
				maxDocumentCharacters: 4
			)
		);

		Assert.AreEqual(new string('a', lettersAtBound), read!.Text);

		Assert.IsFalse(
			MarkupSource.TryRead(
				path: past,
				source: out _,
				out string? error,
				maxDocumentCharacters: 4
			)
		);

		Assert.Contains("past the 16 bytes", error!);
	}

	[TestMethod]
	public void AReadFailureNamesTheExceptionType()
	{
		using TempDirectory directory = new();

		// A directory is not a file, so opening it as one is an access failure.
		_ = MarkupSource.TryRead(directory.Root, out MarkupSource? source, out string? error);

		Assert.IsNull(source);
		Assert.IsNotNull(error);
		Assert.Contains("could not be read: ", error);
		Assert.Contains(nameof(UnauthorizedAccessException), error);
	}

	[TestMethod]
	public void ASourceThatCannotBeSeekedIsRefused()
	{
		// A deflate stream cannot seek, as a device or a pipe cannot, so ReadBounded refuses it before it asks for a length.
		using var inner = new MemoryStream([1, 2, 3]);
		using var source = new DeflateStream(inner, CompressionMode.Decompress);

		byte[] bytes = MarkupSource.ReadBounded(source, 16, out string? refusal);

		Assert.IsEmpty(bytes);
		Assert.AreEqual("could not be read: it is not a regular file.", refusal);
	}

	// A pipe opens only once a writer does, so a file that states no length is read as empty without being opened.
	[TestMethod]
	public void AFileThatStatesNoLengthIsReadAsEmptyWithoutBeingOpened()
	{
		using TempDirectory directory = new();
		string path = directory.Write("empty.xml", string.Empty);

		// An open for reading fails while another handle holds the file without sharing it.
		using FileStream held = new(
			path,
			mode: FileMode.Open,
			access: FileAccess.ReadWrite,
			share: FileShare.None
		);

		Assert.IsTrue(
			MarkupSource.TryRead(path, out MarkupSource? source, out string? error),
			error
		);

		Assert.AreEqual(string.Empty, source!.Text);
	}

	// A pipe cannot seek and states no length, so it is read as empty, not refused with the failure of a length it cannot give.
	[TestMethod]
	[OSCondition(OperatingSystems.Windows)]
	public async Task APipeIsReadAsEmptyInsteadOfFailingOnItsLength()
	{
		string name = $"stanza-tests-{Guid.NewGuid():N}";

		await using NamedPipeServerStream server = new(name, PipeDirection.InOut);

		Assert.IsTrue(
			MarkupSource.TryRead($@"\\.\pipe\{name}", out MarkupSource? source, out string? error),
			error
		);

		Assert.AreEqual(string.Empty, source!.Text);
	}

	[TestMethod]
	public void ASourcePastTheBufferLimitIsRefused()
	{
		using var source = new HugeSource();

		byte[] bytes = MarkupSource.ReadBounded(source, 0, out string? refusal);

		Assert.IsEmpty(bytes);
		Assert.IsNotNull(refusal);
		Assert.Contains($"more than the {Array.MaxLength} bytes one buffer can hold", refusal);
	}

	[TestMethod]
	public void AZeroBoundLeavesTheSizeUnchecked()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", Source);

		_ = MarkupSource.TryRead(
			path,
			out MarkupSource? source,
			out string? error,
			maxDocumentCharacters: 0
		);

		Assert.IsNotNull(source);
		Assert.IsNull(error);
	}

	[TestMethod]
	public void AnEmptyFileIsReadAsNoText()
	{
		using TempDirectory directory = new();
		string path = directory.Write("empty.xml", string.Empty);

		Assert.IsTrue(MarkupSource.TryRead(path, out MarkupSource? source, out string? error));
		Assert.IsNull(error);
		Assert.AreEqual(string.Empty, source!.Text);
	}

	// The scan lists each file's length, and a file that lists none is not opened, so a pipe cannot block the run.
	[TestMethod]
	public void AFileThatStatesALengthOfZeroIsReadAsNoTextAndNeverOpened()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", Source);

		// The file refuses every other open, so a read that opened it would fail instead of returning no text.
		using var held = new FileStream(
			path,
			mode: FileMode.Open,
			access: FileAccess.Read,
			share: FileShare.None
		);

		Assert.IsTrue(
			MarkupSource.TryRead(path, out MarkupSource? source, out string? error, statedLength: 0)
		);

		Assert.IsNull(error);
		Assert.AreEqual(string.Empty, source!.Text);
	}

	// The length a scan lists is read once, so a write that lands before the read is judged by the read, not the list.
	[TestMethod]
	public void AFileGrownSinceTheListingIsReadWhole()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", Source);

		Assert.IsTrue(
			MarkupSource.TryRead(path, out MarkupSource? source, error: out _, statedLength: 4)
		);

		Assert.AreEqual(Source, source!.Text);
	}

	[TestMethod]
	public void AFileEmptiedSinceTheListingIsReadAsNoText()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		Assert.IsTrue(
			MarkupSource.TryRead(path, out MarkupSource? source, error: out _, statedLength: 400)
		);

		Assert.AreEqual(string.Empty, source!.Text);
	}

	// A stream that states more bytes than a buffer can hold without ever holding them.
	private sealed class HugeSource : MemoryStream
	{
		public override long Length => (long)Array.MaxLength + 1;
	}
}
