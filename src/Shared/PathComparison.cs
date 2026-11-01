using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Nickelony.Stanza.Shared;

internal static class PathComparison
{
	/// <summary>Compares paths as the volume of the working directory compares names.</summary>
	/// <remarks>
	/// Windows ignores case and Linux matches it, so only macOS, which leaves it to each volume, probes once a process.
	/// </remarks>
	public static StringComparer Comparer { get; } = ResolveComparer();

	/// <summary>Reads whether the directory's volume tells names apart by case, or <see langword="null"/>.</summary>
	/// <remarks>
	/// It looks up an entry with the case of its letters swapped, skipping one whose swapped name is another entry.
	/// </remarks>
	public static bool? ReadCaseSensitivity(string directory)
	{
		try
		{
			IEnumerable<string> entries = Directory.EnumerateFileSystemEntries(directory).Select(static entry => Path.GetFileName(entry));
			HashSet<string> names = new(entries, StringComparer.Ordinal);

			foreach (string name in names)
			{
				string swapped = SwapCase(name);

				if (swapped != name && !names.Contains(swapped))
					return !Path.Exists(Path.Combine(directory, swapped));
			}

			return null;
		}
		catch (Exception exception) when (FileFailures.IsFileFailure(exception))
		{
			return null;
		}
	}

	private static StringComparer ResolveComparer()
	{
		if (OperatingSystem.IsWindows())
			return StringComparer.OrdinalIgnoreCase;

		if (!OperatingSystem.IsMacOS())
			return StringComparer.Ordinal;

		// A macOS volume ignores case unless it was formatted otherwise, so a directory that shows nothing keeps that default.
		return ReadCaseSensitivity(Environment.CurrentDirectory) == true ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
	}

	private static string SwapCase(string name)
	{
		char[] characters = name.ToCharArray();

		for (int index = 0; index < characters.Length; index++)
		{
			char character = characters[index];
			characters[index] = char.IsUpper(character) ? char.ToLowerInvariant(character) : char.ToUpperInvariant(character);
		}

		return new string(characters);
	}
}
