using System.IO;

namespace Nickelony.Stanza.Markup.Paths;

/// <summary>Resolves a command-line glob to the directory to walk and the pattern to match.</summary>
internal static class GlobPath
{
	/// <summary>The directory a glob argument starts from, or the working directory when it names none.</summary>
	public static string GetSearchRoot(string argument, string workingDirectory)
	{
		string normalized = GlobMatcher.Normalize(argument);
		string directoryPart = FindDirectoryPart(normalized, GlobMatcher.FindWildcard(normalized)) ?? ".";

		return ResolveDirectory(directoryPart, workingDirectory);
	}

	public static GlobTarget Resolve(string argument, string workingDirectory)
	{
		string normalized = GlobMatcher.Normalize(argument);
		int wildcard = GlobMatcher.FindWildcard(normalized);

		// A pattern with no wildcard and a separator is a real path, so it is resolved whole, as the search root is.
		// Appending it to the working directory would leave ./sub and ..\sibling spelled out and match nothing,
		// and double a fully qualified path.
		if (wildcard < 0)
		{
			if (!normalized.Contains('/'))
				return new(null, normalized);

			string fullPath = GlobMatcher.Normalize(Path.GetFullPath(argument, workingDirectory));
			int last = fullPath.LastIndexOf('/');

			// The last segment is escaped because a path that folds back, as sub/.. does, can end in a name that holds a wildcard.
			return Join(fullPath[..last], GlobMatcher.Escape(fullPath[(last + 1)..]));
		}

		string? directoryPart = FindDirectoryPart(normalized, wildcard);

		if (directoryPart is null)
		{
			// A pattern that names a separator matches whole paths, so a wildcard in its first segment is rooted;
			// unrooted, it would silently select nothing.
			return normalized.Contains('/') ? Join(Path.GetFullPath(".", workingDirectory), normalized) : new(null, normalized);
		}

		return Join(ResolveDirectory(directoryPart, workingDirectory), normalized[directoryPart.Length..]);
	}

	/// <summary>The part of a glob up to the last separator before its first wildcard.</summary>
	private static string? FindDirectoryPart(string normalized, int wildcard)
	{
		// An argument with no wildcard has no separator before one to search from.
		int separator = wildcard < 0 ? -1 : normalized.LastIndexOf('/', wildcard);
		return separator < 0 ? null : normalized[..(separator + 1)];
	}

	/// <summary>A file system root ends in a separator already, which the directory drops as any other.</summary>
	private static GlobTarget Join(string directory, string rest) => new(GlobMatcher.Normalize(directory).TrimEnd('/'), $"/{rest}");

	/// <summary>
	/// Keeps the trailing separator while resolving, so <c>/</c> and <c>C:/</c> name a root, not a current directory.
	/// </summary>
	private static string ResolveDirectory(string directory, string workingDirectory) =>
		Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory, workingDirectory));
}
