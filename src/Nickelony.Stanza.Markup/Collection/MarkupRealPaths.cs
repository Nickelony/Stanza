using System;
using System.Collections.Generic;
using System.IO;
using Nickelony.Stanza.Shared;

namespace Nickelony.Stanza.Markup.Collection;

/// <summary>Resolves the directory links in a path, so two spellings of one file give the same path.</summary>
internal sealed class MarkupRealPaths
{
	/// <summary>Each directory resolves once for all its files, not once for each file.</summary>
	private readonly Dictionary<string, string> _directories = new(PathComparison.Comparer);

	public string GetRealPath(string filePath)
	{
		string? directory = Path.GetDirectoryName(filePath);
		return directory is null ? filePath : Path.Combine(GetRealDirectory(directory), Path.GetFileName(filePath));
	}

	private string GetRealDirectory(string directory)
	{
		if (_directories.TryGetValue(directory, out string? cached))
			return cached;

		string? parent = Path.GetDirectoryName(directory);
		string resolved = parent is null ? directory : Path.Combine(GetRealDirectory(parent), Path.GetFileName(directory));

		if (FindLinkTarget(resolved) is { } target)
			resolved = target;

		_directories.Add(directory, resolved);

		return resolved;
	}

	private static string? FindLinkTarget(string directory)
	{
		try
		{
			return new DirectoryInfo(directory).ResolveLinkTarget(returnFinalTarget: true)?.FullName;
		}
		catch (Exception exception) when (FileFailures.IsFileFailure(exception))
		{
			// A directory whose link cannot be read keeps its own spelling.
			return null;
		}
	}
}
