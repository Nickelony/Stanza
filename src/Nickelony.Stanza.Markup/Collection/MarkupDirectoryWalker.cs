using System;
using System.Collections.Generic;
using System.IO;

namespace Nickelony.Stanza.Markup.Collection;

/// <summary>Walks a directory tree, listing each directory by name, past links and skipped or excluded ones.</summary>
internal static class MarkupDirectoryWalker
{
	private static readonly HashSet<string> s_skippedDirectories = new(StringComparer.OrdinalIgnoreCase)
	{
		".git",
		".hg",
		".svn",
		".vs",
		".idea",
		"bin",
		"obj",
		"node_modules",
	};

	/// <summary>The directory names a scan never enters, whatever their case; a test seam.</summary>
	public static IReadOnlyCollection<string> SkippedDirectories => s_skippedDirectories;

	/// <summary>Lists the files below a directory, to <paramref name="maxDepth"/> levels when it is set.</summary>
	public static IEnumerable<FileSystemInfo> EnumerateEntries(string directory, MarkupCollectionContext context, int? maxDepth = null)
	{
		Stack<(string Directory, int Depth)> pending = new();
		List<(string DisplayPath, string Reason)> unscanned = [];

		pending.Push((directory, 1));

		while (pending.Count > 0)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			(string current, int depth) = pending.Pop();

			foreach (FileSystemInfo entry in ReadEntries(directory: current, context, unscanned))
			{
				context.CancellationToken.ThrowIfCancellationRequested();

				if (entry is not DirectoryInfo)
				{
					yield return entry;
					continue;
				}

				// A directory at the depth of the deepest match holds nothing the pattern can still reach.
				if (depth >= maxDepth)
					continue;

				if (MarkupLinkPolicy.IsLinkOrUnreadable(entry, out string? inspectionError))
				{
					// A linked directory is pruned silently; one whose attributes cannot be read is reported,
					// so the walk never passes over a directory it cannot inspect without a word.
					if (inspectionError is not null)
					{
						string displayPath = context.Paths.GetDisplayPath(entry.FullName);
						context.Notices.AddWarning(MarkupLinkPolicy.Describe(displayPath, inspectionError), displayPath);
					}

					continue;
				}

				if (s_skippedDirectories.Contains(entry.Name) || context.Excludes.CoversPath(entry.FullName, context.Paths))
					continue;

				pending.Push((entry.FullName, depth + 1));
			}
		}

		ReportUnscanned(unscanned, context);
	}

	/// <summary>Reports the directories the walk could not list as one warning, so a drive root stays short.</summary>
	private static void ReportUnscanned(List<(string DisplayPath, string Reason)> unscanned, MarkupCollectionContext context)
	{
		if (unscanned.Count == 0)
			return;

		(string displayPath, string reason) = unscanned[0];

		string message =
			unscanned.Count == 1
				? $"'{displayPath}' could not be scanned: {reason}"
				: $"{unscanned.Count} directories could not be scanned, the first being '{displayPath}': {reason}";

		context.Notices.AddWarning(message, displayPath);
	}

	private static List<FileSystemInfo> ReadEntries(
		string directory,
		MarkupCollectionContext context,
		List<(string DisplayPath, string Reason)> unscanned
	)
	{
		List<FileSystemInfo> entries = [];

		try
		{
			CollectEntries(new DirectoryInfo(directory).EnumerateFileSystemInfos(), entries, context);
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
		{
			unscanned.Add((context.Paths.GetDisplayPath(directory), exception.Message));
		}

		entries.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));

		return entries;
	}

	/// <summary>Adds each entry as it is listed, so a cancel stops the listing of a large directory too.</summary>
	public static void CollectEntries(IEnumerable<FileSystemInfo> source, List<FileSystemInfo> entries, MarkupCollectionContext context)
	{
		foreach (FileSystemInfo entry in source)
		{
			context.CancellationToken.ThrowIfCancellationRequested();
			entries.Add(entry);
		}
	}
}
