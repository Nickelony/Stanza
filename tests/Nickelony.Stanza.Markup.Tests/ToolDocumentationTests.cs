using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Cli;
using Nickelony.Stanza.Markup.Collection;
using Nickelony.Stanza.Markup.Core;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Markup.Tests;

[TestClass]
[TestCategory("Repository")]
public sealed partial class ToolDocumentationTests
{
	private static string CommandReferencePath() =>
		RepositoryPaths.Below("docs", "Markup-command-reference.md");

	[TestMethod]
	public void TheUsageBlockOfTheCommandReferenceIsTheTextTheToolPrints()
	{
		string usage = MarkupCommandLine.Usage.ReplaceLineEndings("\n");
		string reference = File.ReadAllText(CommandReferencePath()).ReplaceLineEndings("\n");

		Assert.Contains(
			usage,
			reference,

			"The usage block of the command reference does not match the text the tool prints. Copy the output of "
				+ "'stanza-markup --help' into it again."
		);
	}

	[TestMethod]
	public void EveryIdNoAnalyzerReportsIsNamedInTheCommandReference()
	{
		// An ID with a page under docs/tool is one no analyzer reports, so the command reference names it;
		// the other ZA01 IDs are markup rules, which the reference links as a group.
		string[] ids =
		[
			.. typeof(MarkupDiagnostics)
				.GetFields(BindingFlags.Public | BindingFlags.Static)
				.Where(field => field.IsLiteral)
				.Select(field => field.GetRawConstantValue() as string)
				.Where(IsDiagnosticId)
				.Select(id => id!)
				.Where(id => id.StartsWith("ZA01", StringComparison.Ordinal)),
		];

		Assert.IsNotEmpty(
			ids,
			"No diagnostic id is found in MarkupDiagnostics, so the check below would pass vacuously."
		);

		string reference = File.ReadAllText(CommandReferencePath());

		foreach (string id in ids)
		{
			bool isToolCode =
				Directory.GetFiles(RepositoryPaths.Below("docs", "tool"), $"{id}-*.md").Length > 0;

			bool isRule =
				Directory.GetFiles(RepositoryPaths.Below("docs", "rules"), $"{id}-*.md").Length > 0;

			Assert.IsTrue(
				isToolCode || isRule,
				$"'{id}' has no page under docs/tool or docs/rules."
			);

			if (isToolCode)
				Assert.Contains(id, reference, $"'{id}' is not named in the command reference.");
		}

		Assert.Contains(
			"README.md#markup-in-raw-strings",
			reference,
			"The command reference does not link the markup rules of the analyzer readme."
		);
	}

	[TestMethod]
	public void TheSkippedDirectoriesOfTheCommandReferenceAreTheOnesTheScanSkips()
	{
		string reference = Whitespace().Replace(File.ReadAllText(CommandReferencePath()), " ");

		Match sentence = SkippedDirectorySentence().Match(reference);

		Assert.IsTrue(
			sentence.Success,
			"The command reference no longer lists the directories a scan skips."
		);

		HashSet<string> documented =
		[
			.. CodeSpan()
				.Matches(sentence.Groups["names"].Value)
				.Select(match => match.Groups["name"].Value),
		];

		Assert.IsTrue(
			documented.SetEquals(MarkupDirectoryWalker.SkippedDirectories),

			"The directories the command reference lists are not the directories the scan skips: "
				+ string.Join(", ", documented.OrderBy(name => name, StringComparer.Ordinal))
		);
	}

	private static bool IsDiagnosticId(string? value)
	{
		if (value is not { Length: 6 } || !value.StartsWith("ZA", StringComparison.Ordinal))
			return false;

		for (int index = 2; index < value.Length; index++)
		{
			if (!char.IsAsciiDigit(value[index]))
				return false;
		}

		return true;
	}

	[GeneratedRegex(@"\s+")]
	private static partial Regex Whitespace();

	[GeneratedRegex("subdirectory named (?<names>.+?), whatever its case")]
	private static partial Regex SkippedDirectorySentence();

	[GeneratedRegex("`(?<name>[^`]+)`")]
	private static partial Regex CodeSpan();
}
