using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>
/// Guards that no long sentence appears on two pages, so a fact cannot drift between them.
/// </summary>
[TestClass]
[TestCategory("Repository")]
public sealed partial class DocumentationRepetitionGuardTests
{
	// A sentence this long states a claim, so a copy of it on two pages is a second home for the claim.
	private const int LongSentenceWords = 12;

	// The rule source, the changelog, and machine-owned text, where a repeated sentence is not a second home.
	private static readonly string[] s_excludedFiles =
	[
		"STYLEGUIDE.md",
		"CHANGELOG.md",
		"AnalyzerReleases.Shipped.md",
		"AnalyzerReleases.Unshipped.md",
	];

	[GeneratedRegex(@"^\s*(?:[-*+]|\d+[.)])\s+", RegexOptions.CultureInvariant)]
	private static partial Regex ListItem();

	[GeneratedRegex(@"\]\([^)]*\)", RegexOptions.CultureInvariant)]
	private static partial Regex LinkTarget();

	[GeneratedRegex(@"(?<=[.!?])\s+", RegexOptions.CultureInvariant)]
	private static partial Regex SentenceEnd();

	[TestMethod]
	public void NoLongSentenceAppearsOnTwoPages()
	{
		string root = RepositoryPaths.Root();
		Dictionary<string, SortedSet<string>> pagesBySentence = new(StringComparer.Ordinal);

		foreach (string file in ProsePages(root))
		{
			string relative = MarkdownScan.Relative(root, file);

			foreach (string sentence in Sentences(File.ReadLines(file)))
			{
				if (!pagesBySentence.TryGetValue(sentence, out SortedSet<string>? pages))
					pagesBySentence[sentence] = pages = new(StringComparer.Ordinal);

				pages.Add(relative);
			}
		}

		var violations = pagesBySentence
			.Where(pair => pair.Value.Count > 1)
			.Select(pair => $"{string.Join(", ", pair.Value)}: {pair.Key}")
			.ToList();

		GuardAssert.None(
			violations,

			$"A sentence of at least {LongSentenceWords} words appears on more than one page. "
				+ "Keep it on the page that owns the fact and link that page from the others"
		);
	}

	// The guide pages: the root pages and the pages directly under docs/.
	// A rule, option or tool page follows its template, so a sentence it shares with its siblings is the template's, not a fact's.
	private static IEnumerable<string> ProsePages(string root)
	{
		string docs = Path.Combine(root, "docs");

		return MarkdownScan
			.Files(root, s_excludedFiles)
			.Where(file =>
				Path.GetDirectoryName(file) is { } directory
				&& (PathsEqual(directory, root) || PathsEqual(directory, docs))
			);
	}

	private static bool PathsEqual(string first, string second)
	{
		return string.Equals(
			Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)),
			Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)),
			StringComparison.OrdinalIgnoreCase
		);
	}

	// Joins the wrapped lines of each paragraph and returns its sentences of at least LongSentenceWords words.
	private static List<string> Sentences(IEnumerable<string> lines)
	{
		List<string> sentences = [];
		StringBuilder paragraph = new();

		foreach ((_, string line) in MarkdownScan.ProseLines(lines.Append(string.Empty)))
		{
			string trimmed = line.Trim();

			if (trimmed.Length == 0 || trimmed[0] is '#' or '|' or '<')
			{
				Flush(paragraph, sentences);
				continue;
			}

			if (ListItem().IsMatch(line))
			{
				Flush(paragraph, sentences);
				trimmed = ListItem().Replace(trimmed, string.Empty, 1);
			}

			paragraph.Append(' ').Append(trimmed);
		}

		return sentences;
	}

	private static void Flush(StringBuilder paragraph, List<string> sentences)
	{
		if (paragraph.Length == 0)
			return;

		string text = LinkTarget().Replace(paragraph.ToString(), "]");

		text = text.Replace("[", string.Empty)
			.Replace("]", string.Empty)
			.Replace("`", string.Empty);

		text = text.Replace("*", string.Empty);

		foreach (string sentence in SentenceEnd().Split(text.Trim()))
		{
			string normalized = string.Join(
				" ",
				sentence.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
			);

			if (normalized.Split(' ').Length >= LongSentenceWords)
				sentences.Add(normalized.ToLowerInvariant());
		}

		paragraph.Clear();
	}
}
