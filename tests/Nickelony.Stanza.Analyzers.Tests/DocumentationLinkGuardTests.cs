using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>Guards that every link in the Markdown resolves to a file and a heading.</summary>
[TestClass]
[TestCategory("Repository")]
public sealed partial class DocumentationLinkGuardTests
{
	private const string RepositoryUrl = "https://github.com/Nickelony/Stanza/";
	private const string WikiUrl = RepositoryUrl + "wiki/";
	private const string BlobUrl = RepositoryUrl + "blob/main/";

	// wiki.ps1 publishes this readme as the wiki's Home page.
	private const string HomePage = "src/Nickelony.Stanza.Analyzers/README.md";

	[TestMethod]
	public void EveryLinkInTheMarkdownResolvesToAFileAndAnAnchor()
	{
		string root = RepositoryPaths.Root();
		Dictionary<string, string> pages = WikiPages(root);
		Dictionary<string, HashSet<string>> anchors = new(StringComparer.Ordinal);
		List<string> violations = [];

		foreach (string file in MarkdownScan.Files(root, skipped: []))
		{
			string relative = MarkdownScan.Relative(root, file);

			foreach ((int number, string line) in MarkdownScan.ProseLines(File.ReadLines(file)))
			{
				foreach (Match match in LinkPattern().Matches(line))
				{
					string target = match.Groups[1].Value;
					string? problem = Check(root, file, target, pages, anchors);

					if (problem is not null)
						violations.Add($"{relative}:{number}  {target}  ({problem})");
				}
			}
		}

		GuardAssert.None(violations, "Links that do not resolve to a file and a heading");
	}

	private static string? Check(
		string root,
		string file,
		string target,
		Dictionary<string, string> pages,
		Dictionary<string, HashSet<string>> anchors
	)
	{
		string location;
		string anchor;
		string full;

		if (target.StartsWith(WikiUrl, StringComparison.Ordinal))
		{
			(location, anchor) = Split(target[WikiUrl.Length..]);
			string page = location.Length == 0 ? "Home" : location;

			if (!pages.TryGetValue(page, out string? path))
				return "no wiki page of that name";

			full = Path.Combine(root, path);
		}
		else if (target.StartsWith(BlobUrl, StringComparison.Ordinal))
		{
			(location, anchor) = Split(target[BlobUrl.Length..]);
			full = Path.Combine(root, location);
		}
		else if (
			target.Contains("://", StringComparison.Ordinal)
			|| target.StartsWith("mailto:", StringComparison.Ordinal)
		)
		{
			return null;
		}
		else
		{
			(location, anchor) = Split(target);

			full =
				location.Length == 0
					? file
					: Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, location));
		}

		if (!File.Exists(full) && !Directory.Exists(full))
			return "no such file";

		if (
			anchor.Length == 0
			|| !File.Exists(full)
			|| !full.EndsWith(".md", StringComparison.Ordinal)
		)
		{
			return null;
		}

		return Anchors(full, anchors).Contains(anchor)
			? null
			: $"no heading '{anchor}' in {Path.GetRelativePath(root, full).Replace('\\', '/')}";
	}

	private static (string Location, string Anchor) Split(string target)
	{
		int hash = target.IndexOf('#');
		return hash < 0 ? (target, string.Empty) : (target[..hash], target[(hash + 1)..]);
	}

	private static Dictionary<string, string> WikiPages(string root)
	{
		Dictionary<string, string> pages = new(StringComparer.Ordinal) { ["Home"] = HomePage };

		foreach (string file in RepositoryFiles.Under(Path.Combine(root, "docs"), "*.md"))
		{
			pages[Path.GetFileNameWithoutExtension(file)] = Path.GetRelativePath(root, file)
				.Replace('\\', '/');
		}

		return pages;
	}

	// GitHub numbers a repeated heading's anchor with a suffix: the second "Usage" becomes "usage-1".
	private static HashSet<string> Anchors(string file, Dictionary<string, HashSet<string>> cache)
	{
		if (cache.TryGetValue(file, out HashSet<string>? known))
			return known;

		HashSet<string> anchors = new(StringComparer.Ordinal);
		Dictionary<string, int> seen = new(StringComparer.Ordinal);

		foreach (string heading in Headings(file))
		{
			string slug = Slug(heading);
			int prior = seen.GetValueOrDefault(slug);

			anchors.Add(prior == 0 ? slug : $"{slug}-{prior}");
			seen[slug] = prior + 1;
		}

		cache[file] = anchors;

		return anchors;
	}

	private static IEnumerable<string> Headings(string file)
	{
		foreach ((_, string line) in MarkdownScan.ProseLines(File.ReadLines(file)))
		{
			Match match = HeadingPattern().Match(line);

			if (match.Success)
				yield return match.Groups[1].Value;
		}
	}

	// The anchor GitHub derives from a heading: link text only, lower case, no punctuation, a hyphen per space.
	private static string Slug(string heading)
	{
		string text = LinkTextPattern().Replace(heading, "$1").ToLowerInvariant();
		return PunctuationPattern().Replace(text, string.Empty).Trim().Replace(' ', '-');
	}

	[GeneratedRegex(@"\]\(([^)\s]+)\)")]
	private static partial Regex LinkPattern();

	[GeneratedRegex(@"^#{1,6}\s+(.*?)\s*$")]
	private static partial Regex HeadingPattern();

	[GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
	private static partial Regex LinkTextPattern();

	[GeneratedRegex(@"[^\p{L}\p{N}\s_-]")]
	private static partial Regex PunctuationPattern();
}
