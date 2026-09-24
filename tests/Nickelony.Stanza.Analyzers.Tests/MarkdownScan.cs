using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>What the Markdown guards share: their pages and the lines outside fences.</summary>
internal static class MarkdownScan
{
	/// <summary>The Markdown files under the root, but those a guard leaves out by name.</summary>
	internal static IEnumerable<string> Files(string root, IReadOnlyCollection<string> skipped)
	{
		return RepositoryFiles
			.Under(root, "*.md")
			.Where(file => !skipped.Contains(Path.GetFileName(file), StringComparer.Ordinal));
	}

	internal static string Relative(string root, string file) =>
		Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');

	/// <summary>Each line outside a code fence; a fence's own line comes back blank.</summary>
	internal static IEnumerable<(int Number, string Text)> ProseLines(IEnumerable<string> lines)
	{
		bool inFence = false;
		int number = 0;

		foreach (string line in lines)
		{
			number++;

			if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
			{
				inFence = !inFence;
				yield return (number, string.Empty);
				continue;
			}

			if (!inFence)
				yield return (number, line);
		}
	}
}
