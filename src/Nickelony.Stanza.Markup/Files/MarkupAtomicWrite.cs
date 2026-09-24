using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Nickelony.Stanza.Markup.Files;

/// <summary>Replaces a file through a flushed temp file, so it is never half written.</summary>
internal static class MarkupAtomicWrite
{
	/// <summary>The extension of the temp file a killed write leaves.</summary>
	/// <remarks>
	/// No profile claims it, so a scan skips a leftover unless a profile key or a forced glob does.
	/// </remarks>
	public const string TemporarySuffix = ".stanza-tmp";

	/// <summary>The characters of the original's name a temp name keeps, so a name at the limit still fits.</summary>
	private const int MaxNameStart = 32;

	/// <summary>The steps a write takes outside a test.</summary>
	public static MarkupWriteSteps DefaultSteps { get; } = new(Swap, HasChangedSinceRead);

	public static bool TryReplace(string path, string text, Encoding encoding, MarkupFileStamp stamp, [NotNullWhen(false)] out string? error) =>
		TryReplace(path, text, encoding, stamp, steps: DefaultSteps, out error);

	/// <summary>The steps are a parameter so a test can fail the swap or change the file between the checks.</summary>
	public static bool TryReplace(
		string path,
		string text,
		Encoding encoding,
		MarkupFileStamp stamp,
		MarkupWriteSteps steps,
		[NotNullWhen(false)] out string? error
	)
	{
		// The random part keeps another process from guessing the name and planting a link there first.
		string temporary = BuildTemporaryPath(path);

		// A file already at the name is not this write's to delete.
		bool ownsTemporary = false;

		// The checks read the file system too, so a file that vanishes under them is reported as not written, not as a defect.
		try
		{
			// A read-only file is the author's mark that it stays as it is.
			// Windows refuses the replace over one and a Unix rename ignores the bit, so the tool refuses on both.
			if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0)
			{
				error = "the file is read-only, so it was left alone.";
				return false;
			}

			// A save by another program between the read and this write would be lost, so the file must still be as it was read.
			// The check runs again right before the replace below, which leaves the shortest window the file system allows.
			if (steps.HasChanged(path, stamp))
			{
				error = "the file changed after it was read, so it was left alone.";
				return false;
			}

			// A sibling temp file is flushed to the device before the rename,
			// so the original is untouched or replaced whole.
			ownsTemporary = !File.Exists(temporary);
			WriteAndFlush(path: temporary, text, encoding, mode: ReadFileMode(path));

			// The swap puts a new file at the name, so another hard link keeps the old content;
			// on Unix only the mode this call copies carries over.
			CopyFileMode(path, temporary);

			if (steps.HasChanged(path, stamp))
			{
				DiscardTemporary(temporary);

				error = "the file changed after it was read, so it was left alone.";
				return false;
			}

			return TrySwap(steps, temporary, path, out error);
		}
		catch (Exception exception) when (MarkupSource.IsAccessFailure(exception))
		{
			// The temp file is deleted only while the original exists, so a failure never deletes the only copy.
			if (ownsTemporary && File.Exists(path))
				DiscardTemporary(temporary);

			error = Describe(exception);
			return false;
		}
	}

	/// <summary>Names the temp file beside the original by a fixed-length start of the original's name.</summary>
	private static string BuildTemporaryPath(string path)
	{
		string name = Path.GetFileName(path);

		// The start of the name says which file a killed write left it for; a cut between the halves of a surrogate pair is avoided.
		int length = Math.Min(name.Length, MaxNameStart);

		if (length > 0 && char.IsHighSurrogate(name[length - 1]))
			length--;

		string temporaryName = $"{name[..length]}.{Convert.ToHexString(RandomNumberGenerator.GetBytes(8))}{TemporarySuffix}";

		return Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, temporaryName);
	}

	/// <summary>Replaces the original with the temp file, or moves it there when the replace fails halfway.</summary>
	private static bool TrySwap(MarkupWriteSteps steps, string temporary, string path, [NotNullWhen(false)] out string? error)
	{
		try
		{
			steps.Swap(temporary, path);
		}
		catch (Exception exception) when (MarkupSource.IsAccessFailure(exception) && !File.Exists(path))
		{
			// A replace can fail after it moved the original away, so the temp file is the only copy and goes into its place.
			try
			{
				File.Move(temporary, path);
			}
			catch (Exception moveFailure) when (MarkupSource.IsAccessFailure(moveFailure))
			{
				error = $"{Describe(moveFailure)}; the new text is left in '{Path.GetFileName(temporary)}'.";
				return false;
			}
		}

		error = null;
		return true;
	}

	private static string Describe(Exception exception) => $"{exception.GetType().Name}: {exception.Message}";

	/// <summary>On Windows the replace keeps the access list and the alternate streams of the original.</summary>
	private static void Swap(string temporary, string path)
	{
		if (OperatingSystem.IsWindows() && File.Exists(path))
			File.Replace(sourceFileName: temporary, destinationFileName: path, destinationBackupFileName: null, ignoreMetadataErrors: true);
		else
			File.Move(temporary, path, overwrite: true);
	}

	private static bool HasChangedSinceRead(string path, MarkupFileStamp stamp)
	{
		var info = new FileInfo(path);
		return !info.Exists || info.Length != stamp.Length || info.LastWriteTimeUtc != stamp.LastWriteUtc;
	}

	/// <summary>
	/// Starts the file at the source's Unix mode less the umask, so a private file is never briefly readable by others.
	/// </summary>
	private static void WriteAndFlush(string path, string text, Encoding encoding, UnixFileMode? mode)
	{
		FileStreamOptions options = new()
		{
			// A new file only, so a link already standing at the name fails the write rather than redirecting it.
			Mode = FileMode.CreateNew,
			Access = FileAccess.Write,
			Share = FileShare.None,
		};

		if (mode is { } unixMode && !OperatingSystem.IsWindows())
			options.UnixCreateMode = unixMode;

		using var stream = new FileStream(path, options);
		using var writer = new StreamWriter(stream, encoding);

		writer.Write(text);
		writer.Flush();

		stream.Flush(flushToDisk: true);
	}

	private static UnixFileMode? ReadFileMode(string path)
	{
		if (OperatingSystem.IsWindows() || !File.Exists(path))
			return null;

		try
		{
			return File.GetUnixFileMode(path);
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
		{
			return null;
		}
	}

	private static void CopyFileMode(string source, string destination)
	{
		if (OperatingSystem.IsWindows() || !File.Exists(source))
			return;

		try
		{
			File.SetUnixFileMode(destination, File.GetUnixFileMode(source));
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
		{
			// A mode that cannot be copied does not fail the write.
		}
	}

	private static void DiscardTemporary(string path)
	{
		try
		{
			File.Delete(path);
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
		{
			// The write's own error is the one reported, so a failed delete stays silent and the temp file is left behind.
		}
	}
}
