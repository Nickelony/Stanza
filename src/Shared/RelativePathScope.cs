using System;
using System.IO;

namespace Nickelony.Stanza.Shared;

internal static class RelativePathScope
{
	public static bool LeavesDirectory(string relativePath)
	{
		// A path that is still rooted is not inside the directory, which happens across volumes.
		return Path.IsPathRooted(relativePath)
			|| relativePath == ".."
			|| relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
			|| relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
	}

	/// <summary>Whether a <c>/</c>-separated path lies below the directory.</summary>
	public static bool IsBelow(string path, string directory)
	{
		// Names compare as the file system compares them.
		return path.Length > directory.Length + 1
			&& path[directory.Length] == '/'
			&& PathComparison.Comparer.Equals(path[..directory.Length], directory);
	}
}
