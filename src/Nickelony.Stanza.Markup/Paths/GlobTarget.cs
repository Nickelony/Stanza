using System;
using System.Linq;

namespace Nickelony.Stanza.Markup.Paths;

/// <summary>A resolved glob: the directory it starts from, and the rooted pattern for the path below it.</summary>
/// <remarks>
/// The directory is compared as text, so however long it is, it never reaches the size limit of the pattern's regex.
/// </remarks>
internal readonly record struct GlobTarget(string? Directory, string Pattern)
{
	/// <summary>How many levels below the directory a match can sit, or <see langword="null"/> for any depth.</summary>
	public int? MaxDepth
	{
		get
		{
			// A star never crosses a separator, so without ** a match has as many segments as the pattern has separators.
			if (Directory is null || Pattern.Contains("**", StringComparison.Ordinal))
				return null;

			return Pattern.Count(static character => character == '/');
		}
	}

	/// <summary>Without a directory, the pattern names no separator and matches a file name at any depth.</summary>
	public bool IsMatch(string path)
	{
		if (Directory is null)
			return GlobMatcher.IsMatch(Pattern, path);

		string normalized = GlobMatcher.Normalize(path);

		// Matching ignores case on every platform, in the directory as in the pattern.
		return normalized.Length > Directory.Length
			&& normalized[Directory.Length] == '/'
			&& normalized.StartsWith(Directory, StringComparison.OrdinalIgnoreCase)
			&& GlobMatcher.IsMatch(Pattern, normalized[Directory.Length..]);
	}
}
