using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using Nickelony.Stanza.Markup.Core;

namespace Nickelony.Stanza.Markup.Files;

/// <summary>Reads a file into text and keeps its encoding and stamp, so a write puts the file back as it was.</summary>
internal sealed class MarkupSource
{
	private readonly MarkupFileStamp _stamp;

	/// <summary>Only <see cref="TryRead"/> makes one, after it has checked the bytes and the encoding.</summary>
	private MarkupSource(string text, Encoding encoding, string? writeBackRefusal, MarkupFileStamp stamp)
	{
		Text = text;
		Encoding = encoding;
		WriteBackRefusal = writeBackRefusal;
		_stamp = stamp;
	}

	public string Text { get; }

	/// <summary>Carries the byte order mark the file had, or its absence, so a write reproduces it.</summary>
	public Encoding Encoding { get; }

	public string? WriteBackRefusal { get; }

	/// <summary>
	/// The most bytes one character takes (UTF-32), so a pre-read byte bound never refuses a document it can lay out.
	/// </summary>
	private const int MaxBytesPerCharacter = 4;

	/// <summary>The document bound is a test seam: the tool reads every file with the default.</summary>
	/// <remarks>
	/// The stated length is the one the scan listed, which saves a stat of each file; without it the file is asked.
	/// </remarks>
	public static bool TryRead(
		string path,
		[NotNullWhen(true)] out MarkupSource? source,
		[NotNullWhen(false)] out string? error,
		long maxDocumentCharacters = MarkupLimits.DefaultMaxDocumentCharacters,
		long? statedLength = null
	)
	{
		byte[] bytes;
		MarkupFileStamp stamp;

		source = null;

		try
		{
			if ((statedLength ?? FindLength(path)) == 0)
			{
				// A file that states no length, as a pipe never does, is empty text and is not opened:
				// opening a FIFO blocks until a writer opens it, and none ever does.
				stamp = new MarkupFileStamp(Length: 0, File.GetLastWriteTimeUtc(path));
				bytes = [];
			}
			else
			{
				using var stream = new FileStream(path, mode: FileMode.Open, access: FileAccess.Read, share: FileShare.Read);

				// The stamp is taken before the read, so a write that lands during it is caught: Unix does not enforce the share mode.
				stamp = new MarkupFileStamp(stream.Length, File.GetLastWriteTimeUtc(stream.SafeFileHandle));
				bytes = ReadBounded(stream, maxDocumentCharacters, out string? sizeRefusal);

				if (sizeRefusal is not null)
				{
					error = sizeRefusal;
					return false;
				}
			}
		}
		catch (Exception exception) when (IsAccessFailure(exception))
		{
			error = $"could not be read: {exception.GetType().Name}: {exception.Message}";
			return false;
		}

		MarkupEncodingChoice choice = MarkupEncodings.Resolve(bytes);

		if (choice.Refusal is { } refusal)
		{
			error = refusal;
			return false;
		}

		try
		{
			error = null;

			source = new(
				text: choice.Encoding.GetString(bytes, choice.PreambleLength, bytes.Length - choice.PreambleLength),
				encoding: choice.Encoding,
				writeBackRefusal: choice.WriteBackRefusal,
				stamp
			);

			return true;
		}
		catch (ArgumentException)
		{
			// Every encoding used here except Latin-1 throws on a byte it cannot decode,
			// so a bad file is refused.
			// Latin-1 maps every byte, so a file read as Latin-1 never reaches this catch.
			error = $"could not be read: the bytes could not be decoded as {choice.Encoding.WebName}, so it was left alone.";
			return false;
		}
	}

	private static long? FindLength(string path)
	{
		var info = new FileInfo(path);
		return info.Exists ? info.Length : null;
	}

	public static byte[] ReadBounded(Stream stream, long maxDocumentCharacters, out string? refusal)
	{
		refusal = null;

		// A stream the tool cannot seek is not a regular file, so a read of it may never end:
		// it is refused before a byte of it is read.
		if (!stream.CanSeek)
		{
			refusal = "could not be read: it is not a regular file.";
			return [];
		}

		long limit = maxDocumentCharacters > long.MaxValue / MaxBytesPerCharacter ? long.MaxValue : maxDocumentCharacters * MaxBytesPerCharacter;
		bool isBounded = maxDocumentCharacters > 0 && limit <= long.MaxValue - MaxBytesPerCharacter;

		// One character's worth of bytes is allowed above the bound,
		// so a marked file that fits is not refused over its mark.
		if (isBounded && stream.Length > limit + MaxBytesPerCharacter)
		{
			refusal =
				$"could not be read: it holds {stream.Length} bytes, past the {limit} bytes a document of {maxDocumentCharacters} characters can hold.";

			return [];
		}

		// A file longer than the runtime's array limit is refused rather than handed to an allocation that throws.
		// With the bound off, or wider than one buffer can hold, this is the only size check left.
		if (stream.Length > Array.MaxLength)
		{
			refusal = $"could not be read: it holds {stream.Length} bytes, more than the {Array.MaxLength} bytes one buffer can hold.";
			return [];
		}

		byte[] bytes = new byte[stream.Length];
		stream.ReadExactly(bytes);

		return bytes;
	}

	/// <summary>
	/// The tool reads <see cref="WriteBackRefusal"/> before formatting a file to write, so no refused file gets here.
	/// </summary>
	public bool TryWrite(string path, string text, [NotNullWhen(false)] out string? error) =>
		MarkupAtomicWrite.TryReplace(path, text, encoding: Encoding, stamp: _stamp, out error);

	internal static bool IsAccessFailure(Exception exception) =>
		exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;
}
