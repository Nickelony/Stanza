using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Nickelony.Stanza.Format.Running;

/// <summary>Reads the output of the commands a check runs, apart from running them.</summary>
internal static partial class FormatChecks
{
	[GeneratedRegex(@"^Checked (?<count>\d+) files? in ", RegexOptions.CultureInvariant)]
	private static partial Regex CheckedSummary();

	[GeneratedRegex(@"^(?<path>.+): error: Was not formatted\.$", RegexOptions.CultureInvariant)]
	private static partial Regex UnformattedFile();

	[GeneratedRegex(@"\beol=(?<ending>crlf|lf)\b", RegexOptions.CultureInvariant)]
	private static partial Regex PinnedEnding();

	[GeneratedRegex(@"^i/(?<index>\S*)\s+w/(?<written>\S*)", RegexOptions.CultureInvariant)]
	private static partial Regex ContentEndings();

	/// <summary>
	/// An indented line explains the entry above it, and any other line is kept, so no CSharpier failure can pass.
	/// </summary>
	public static CSharpierReport ReadCSharpierReport(string output)
	{
		int checkedFiles = -1;

		List<string> unformatted = [];
		List<string> unknown = [];

		foreach (string line in output.Split('\n').Select(static line => line.TrimEnd('\r')))
		{
			if (line.Trim().Length == 0 || char.IsWhiteSpace(line[0]))
				continue;

			if (CheckedSummary().Match(line) is { Success: true } summary)
				checkedFiles = int.Parse(summary.Groups["count"].Value, CultureInfo.InvariantCulture);
			else if (UnformattedFile().Match(line) is { Success: true } file)
				unformatted.Add(file.Groups["path"].Value);
			else
				unknown.Add(line);
		}

		return new CSharpierReport(checkedFiles, [.. unformatted], [.. unknown]);
	}

	/// <summary>Ignores the line endings and every gap of one blank line or none between two lines.</summary>
	/// <remarks>
	/// A blank line no Stanza rule keeps passes, and a write removes it; a longer run or a whitespace-only line fails.
	/// </remarks>
	public static bool MatchesApartFromBlankLines(string expected, string actual) =>
		ReadLayout(expected).SequenceEqual(ReadLayout(actual), StringComparer.Ordinal);

	/// <summary>
	/// Skips a binary file, one whose attributes pin no ending, one with no line break, and one the working tree lacks.
	/// </summary>
	public static ImmutableArray<string> CollectWrongLineEndings(string listing)
	{
		// Each entry ends with a NUL character, and only the text before its tab is read, as a path may hold anything.
		return
		[
			.. from line in GitRepository.ReadPaths(listing)
			let meta = line.Split('\t')[0]
			let pinned = PinnedEnding().Match(meta)
			let endings = ContentEndings().Match(meta)
			let written = endings.Groups["written"].Value
			where pinned.Success && endings.Groups["index"].Value != "-text" && written is not ("" or "none" or "-text")
			where written != pinned.Groups["ending"].Value
			select line,
		];
	}

	/// <summary>Reads the files <see cref="GitRepository.ReadMarkedFiles"/> named.</summary>
	public static ImmutableArray<string> CollectMarkedFiles(string output) => [.. GitRepository.ReadPaths(output)];

	/// <summary>Reads the lines that are not empty, and the runs of empty lines that a match compares.</summary>
	private static List<string> ReadLayout(string text)
	{
		List<string> layout = [];
		int run = 0;

		foreach (string line in text.TrimStart((char)0xFEFF).Split('\n').Select(static line => line.TrimEnd('\r')))
		{
			if (line.Length == 0)
			{
				run++;
				continue;
			}

			// A run of one line or none between two lines is dropped, whether or not a Stanza rule keeps it; a run at either end always counts.
			// A run is kept as a line break and its length, which no line of the text can be.
			if (layout.Count == 0 || run > 1)
				layout.Add($"\n{run}");

			layout.Add(line);
			run = 0;
		}

		layout.Add($"\n{run}");
		return layout;
	}
}
