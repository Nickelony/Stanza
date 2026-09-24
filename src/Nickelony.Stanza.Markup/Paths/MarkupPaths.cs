using System;
using System.IO;
using Nickelony.Stanza.Shared;

namespace Nickelony.Stanza.Markup.Paths;

/// <summary>The working directory of a run, and how a report writes a path.</summary>
internal sealed class MarkupPaths(string workingDirectory)
{
	public string WorkingDirectory { get; } = workingDirectory;

	/// <summary>Whether the platform cannot resolve a path, as one too long or with a refused character.</summary>
	public static bool IsUnresolvable(Exception exception) => exception is ArgumentException or NotSupportedException or PathTooLongException;

	/// <summary>Writes a path relative to the working directory, or in full when it leaves it.</summary>
	/// <seealso href="https://github.com/Nickelony/Stanza/wiki/Markup-command-reference#output"/>
	public string GetDisplayPath(string path)
	{
		string relativePath = RepositoryPath.ToRelative(WorkingDirectory, path);
		string shown = RelativePathScope.LeavesDirectory(relativePath) ? GlobMatcher.Normalize(path) : relativePath;

		return ToolHost.EscapeNonPrintingCharacters(shown);
	}
}
