using System;
using System.Collections.Generic;
using System.IO;
using Nickelony.Stanza.Markup.Paths;
using Nickelony.Stanza.Shared;

namespace Nickelony.Stanza.Markup.Configuration;

internal sealed class EditorConfigChain(IReadOnlyList<EditorConfigSection> sections)
{
	private const string UnsetValue = "unset";

	public IReadOnlyDictionary<string, string> Evaluate(string filePath)
	{
		Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

		string? directory = null;
		string relativePath = string.Empty;

		foreach (EditorConfigSection section in sections)
		{
			// The sections of one file are neighbors, so the path is made relative to each directory once.
			if (!string.Equals(section.Directory, directory, StringComparison.Ordinal))
			{
				directory = section.Directory;
				relativePath = Path.GetRelativePath(directory, filePath);
			}

			// A glob is read against the directory of the .editorconfig that holds the section, and a path that leaves it never matches.
			if (RelativePathScope.LeavesDirectory(relativePath) || !GlobMatcher.IsMatch(section.Glob, relativePath))
				continue;

			foreach (KeyValuePair<string, string> pair in section.Values)
			{
				if (IsUnset(pair.Value))
					values.Remove(pair.Key);
				else
					values[pair.Key] = pair.Value;
			}
		}

		return values;
	}

	public static bool IsUnset(string value) => string.Equals(value, UnsetValue, StringComparison.OrdinalIgnoreCase);
}
