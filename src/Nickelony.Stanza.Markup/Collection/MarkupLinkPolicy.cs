using System;
using System.IO;

namespace Nickelony.Stanza.Markup.Collection;

/// <summary>Leaves a link alone, because writing through one would replace it with a regular file.</summary>
internal static class MarkupLinkPolicy
{
	/// <summary>Reports a link or a file whose attributes cannot be read, and says whether it was one.</summary>
	public static bool TryReportLink(FileSystemInfo entry, MarkupCollectionContext context)
	{
		if (!IsLinkOrUnreadable(entry, out string? inspectionError))
			return false;

		string displayPath = context.Paths.GetDisplayPath(entry.FullName);

		context.Notices.AddWarning(Describe(displayPath, inspectionError), displayPath);
		return true;
	}

	/// <summary>
	/// Whether the path has a link target, or unreadable attributes, which count as a link and give the reason.
	/// </summary>
	public static bool IsLinkOrUnreadable(FileSystemInfo entry, out string? error) =>
		IsLinkOrUnreadable(entry, static info => info.LinkTarget, out error);

	/// <summary>The reader of the target is a test seam, since no entry makes a read fail on demand.</summary>
	public static bool IsLinkOrUnreadable(FileSystemInfo entry, Func<FileSystemInfo, string?> readTarget, out string? error)
	{
		try
		{
			error = null;
			// A reparse point is not always a link: a cloud placeholder carries the attribute and has no target.
			return readTarget(entry) is not null;
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
		{
			error = exception.Message;
			return true;
		}
	}

	public static string Describe(string displayPath, string? inspectionError)
	{
		return inspectionError is null
			? $"'{displayPath}' is a link and was left alone."
			: $"'{displayPath}' could not be inspected: {inspectionError.TrimEnd('.')}.";
	}
}
