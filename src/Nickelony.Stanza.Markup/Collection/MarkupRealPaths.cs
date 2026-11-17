using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Nickelony.Stanza.Shared;

namespace Nickelony.Stanza.Markup.Collection;

/// <summary>Resolves the links and short names in a path, so two spellings of one file give one path.</summary>
internal sealed class MarkupRealPaths
{
	/// <summary>Each directory resolves once for all its files, not once for each file.</summary>
	private readonly Dictionary<string, string> _directories = new(PathComparison.Comparer);

	public string GetRealPath(string filePath)
	{
		string? directory = Path.GetDirectoryName(filePath);

		return directory is null
			? filePath
			: ResolveShortName(Path.Combine(GetRealDirectory(directory), Path.GetFileName(filePath)));
	}

	private string GetRealDirectory(string directory)
	{
		if (_directories.TryGetValue(directory, out string? cached))
			return cached;

		string? parent = Path.GetDirectoryName(directory);
		string resolved = parent is null ? directory : Path.Combine(GetRealDirectory(parent), Path.GetFileName(directory));

		resolved = ResolveShortName(resolved);

		if (FindLinkTarget(resolved) is { } target)
			resolved = target;

		_directories.Add(directory, resolved);

		return resolved;
	}

	/// <summary>Writes a short 8.3 name as its long one, so two spellings of a directory compare equal.</summary>
	/// <remarks>
	/// .NET already expands a short name in the paths it returns, so this step is defense in depth.
	/// </remarks>
	private static string ResolveShortName(string path)
	{
		// A short name always holds a tilde, and only Windows writes one.
		if (!OperatingSystem.IsWindows() || !Path.GetFileName(path).Contains('~'))
			return path;

		StringBuilder buffer = new(capacity: path.Length + 1);
		uint length = GetLongPathName(path, buffer, (uint)buffer.Capacity);

		if (length > buffer.Capacity)
		{
			buffer.EnsureCapacity((int)length);
			length = GetLongPathName(path, buffer, (uint)buffer.Capacity);
		}

		// A path that does not exist, or that the call cannot read, keeps its own spelling.
		return length == 0 ? path : buffer.ToString(startIndex: 0, length: (int)length);
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetLongPathNameW")]
	private static extern uint GetLongPathName(string shortPath, StringBuilder longPath, uint length);

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
