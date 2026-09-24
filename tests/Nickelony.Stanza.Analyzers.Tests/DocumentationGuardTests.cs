using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>Guards the comment ceilings Vale cannot count.</summary>
[TestClass]
[TestCategory("Repository")]
public sealed partial class DocumentationGuardTests
{
	[GeneratedRegex(@"An XML doc block is at most (?<limit>\d+)", RegexOptions.CultureInvariant)]
	private static partial Regex XmlDocBlockCeiling();

	[GeneratedRegex(@"inline comment run at most (?<limit>\d+)", RegexOptions.CultureInvariant)]
	private static partial Regex InlineCommentCeiling();

	[GeneratedRegex(@"A line never runs past (?<limit>\d+) columns", RegexOptions.CultureInvariant)]
	private static partial Regex MarkdownLineCeiling();

	[GeneratedRegex(@"\]\([^)]*\)", RegexOptions.CultureInvariant)]
	private static partial Regex LinkTarget();

	// The machine-owned files, which no wrap rule binds.
	private static readonly string[] s_unwrappedFiles =
	[
		"AnalyzerReleases.Shipped.md",
		"AnalyzerReleases.Unshipped.md",
	];

	[TestMethod]
	public void MarkdownLinesStayWithinTheWrapCeiling()
	{
		int ceiling = ReadCeiling(MarkdownLineCeiling(), name: "Markdown line");
		string root = RepositoryPaths.Root();

		// A table row cannot wrap, and a link target is not text a reader counts.
		string[] violations =
		[
			.. from file in MarkdownScan.Files(root, s_unwrappedFiles)
			from line in MarkdownScan.ProseLines(File.ReadLines(file))
			where !line.Text.StartsWith('|')
			let width = LinkTarget().Replace(line.Text, "]").Length
			where width > ceiling
			select $"{MarkdownScan.Relative(root, file)}:{line.Number}: {width} columns, limit {ceiling}",
		];

		GuardAssert.None(
			violations,
			"Markdown lines pass the wrap ceiling of STYLEGUIDE.md",
			closing: "Break each line at a comma or a conjunction."
		);
	}

	[TestMethod]
	public void SourceDocCommentsStayWithinShape()
	{
		int ceiling = ReadCeiling(XmlDocBlockCeiling(), name: "XML doc block");
		SyntaxKind kind = SyntaxKind.SingleLineDocumentationCommentTrivia;

		string[] violations = FindLongRuns(kind, marker: "///", ceiling);

		AssertWithinShape(violations, "Source documentation comments");
	}

	[TestMethod]
	public void InlineCommentRunsStayWithinShape()
	{
		int ceiling = ReadCeiling(InlineCommentCeiling(), name: "inline comment run");
		SyntaxKind kind = SyntaxKind.SingleLineCommentTrivia;

		string[] violations = FindLongRuns(kind, marker: "//", ceiling);

		AssertWithinShape(violations, "Inline comment runs");
	}

	private static string[] FindLongRuns(SyntaxKind kind, string marker, int ceiling)
	{
		return
		[
			.. from file in SourceFiles()
			from run in LineRuns(CommentLines(file, kind))
			where run.Length > ceiling
			select $"{RepoPath(file)}: {run.Length}-line {marker} run at {run.First}, limit {ceiling}",
		];
	}

	private static int ReadCeiling(Regex pattern, string name)
	{
		Match match = pattern.Match(File.ReadAllText(RepositoryPaths.Below("STYLEGUIDE.md")));

		Assert.IsTrue(match.Success, $"STYLEGUIDE.md does not state the {name} ceiling.");

		return int.Parse(match.Groups["limit"].Value, CultureInfo.InvariantCulture);
	}

	private static string[] SourceFiles()
	{
		return
		[
			.. new[] { "src", "tests" }
				.SelectMany(directory =>
					RepositoryFiles.Under(RepositoryPaths.Below(directory), "*.cs")
				)
				.OrderBy(file => file, StringComparer.Ordinal),
		];
	}

	/// <summary>
	/// Lines of the given comment kinds that start a line; strings and disabled text never count.
	/// </summary>
	private static HashSet<int> CommentLines(string path, params SyntaxKind[] kinds)
	{
		var source = SourceText.From(File.ReadAllText(path));

		SyntaxTree tree = CSharpSyntaxTree.ParseText(
			source,
			new CSharpParseOptions(LanguageVersion.Preview),
			path
		);

		HashSet<int> lines = [];

		foreach (SyntaxTrivia trivia in tree.GetRoot().DescendantTrivia())
		{
			if (Array.IndexOf(kinds, trivia.Kind()) < 0 || !StartsLine(source, trivia))
				continue;

			int start = source.Lines.IndexOf(trivia.FullSpan.Start) + 1;
			int last = trivia.FullSpan.End;

			// A doc comment's span reaches past its last line;
			// the walk takes the ending back to the last character the comment itself holds.
			while (last > trivia.FullSpan.Start && char.IsWhiteSpace(source[last - 1]))
				last--;

			int end = source.Lines.IndexOf(last - 1) + 1;

			for (int line = start; line <= end; line++)
				lines.Add(line);
		}

		return lines;
	}

	// The `///` markers sit in a doc comment's FullSpan but outside its own span.
	private static bool StartsLine(SourceText sourceText, SyntaxTrivia trivia)
	{
		TextLine line = sourceText.Lines.GetLineFromPosition(trivia.SpanStart);
		int pointer = line.Start;

		while (pointer < line.End && char.IsWhiteSpace(sourceText[pointer]))
			pointer++;

		return pointer >= trivia.FullSpan.Start && pointer < trivia.FullSpan.End;
	}

	private static IEnumerable<(int First, int Length)> LineRuns(IEnumerable<int> lines)
	{
		int[] sorted = [.. lines.OrderBy(line => line)];

		int index = 0;

		while (index < sorted.Length)
		{
			int first = sorted[index];
			int last = first;

			index++;

			while (index < sorted.Length && sorted[index] == last + 1)
			{
				last = sorted[index];
				index++;
			}

			yield return (first, last - first + 1);
		}
	}

	private static string RepoPath(string path) =>
		Path.GetRelativePath(RepositoryPaths.Root(), path).Replace('\\', '/');

	private static void AssertWithinShape(string[] violations, string subject)
	{
		GuardAssert.None(
			violations,
			$"{subject} break the ceilings of STYLEGUIDE.md",
			closing: "Split the run, or move its fact to the page that owns it."
		);
	}
}
