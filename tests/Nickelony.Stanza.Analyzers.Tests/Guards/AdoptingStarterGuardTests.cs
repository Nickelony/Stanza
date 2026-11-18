using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Guards;

/// <summary>
/// Guards that the starter .editorconfig of the adoption guide names real rules and keys.
/// </summary>
[TestClass]
[TestCategory("Repository")]
public sealed class AdoptingStarterGuardTests
{
	private static readonly string[] s_severities =
	[
		"error",
		"warning",
		"suggestion",
		"silent",
		"none",
		"default",
	];

	[TestMethod]
	public void TheStarterNamesOnlyRulesSeveritiesCategoriesAndKeysThatExist()
	{
		DiagnosticDescriptor[] descriptors =
		[
			.. RoslynTestHarness.AllAnalyzers.Select(analyzer =>
				analyzer.SupportedDiagnostics.Single()
			),
		];

		(List<string> problems, int checkedLines) = FindProblems(
			ReadStarter(),
			descriptors,
			key => File.Exists(RepositoryPaths.Below("docs", "options", $"{key}.md"))
		);

		Assert.IsGreaterThan(0, checkedLines, "The starter holds no setting to check.");

		GuardAssert.None(
			problems,
			"The starter .editorconfig of the adoption guide names something that does not exist"
		);
	}

	[TestMethod]
	public void AStarterLineThatNamesNothingRealIsFound()
	{
		DiagnosticDescriptor[] descriptors = [DiagnosticDescriptors.SeparateMemberGroups];

		string id = DiagnosticDescriptors.SeparateMemberGroups.Id;
		string category = DiagnosticDescriptors.SeparateMemberGroups.Category;

		string[] sound =
		[
			$"dotnet_diagnostic.{id}.severity = warning",
			$"# dotnet_analyzer_diagnostic.category-{category}.severity = suggestion",
			"stanza_real_option = true",
			"root = true",
		];

		(List<string> problems, int checkedLines) = FindProblems(
			sound,
			descriptors,
			key => key == "stanza_real_option"
		);

		Assert.IsEmpty(problems);
		Assert.AreEqual(3, checkedLines);

		string[] broken =
		[
			"dotnet_diagnostic.ZA9999.severity = warning",
			$"dotnet_diagnostic.{id}.severity = loud",
			$"dotnet_diagnostic.{id}.level = warning",
			"dotnet_analyzer_diagnostic.category-Nothing.severity = warning",
			"stanza_misspelled_option = true",
		];

		(List<string> found, _) = FindProblems(broken, descriptors, key => false);

		Assert.HasCount(6, found);
	}

	private static (List<string> Problems, int CheckedLines) FindProblems(
		IEnumerable<string> lines,
		DiagnosticDescriptor[] descriptors,
		Func<string, bool> hasOptionPage
	)
	{
		List<string> problems = [];
		int checkedLines = 0;

		foreach (string line in lines)
		{
			// A commented line is a line the reader may enable, so it must be as real as an active one.
			string setting = line.TrimStart().TrimStart('#').Trim();

			if (setting.Length == 0 || !setting.Contains('=', StringComparison.Ordinal))
				continue;

			string key = setting[..setting.IndexOf('=')].Trim();
			string value = setting[(setting.IndexOf('=') + 1)..].Trim();

			if (key.StartsWith("dotnet_diagnostic.", StringComparison.Ordinal))
			{
				checkedLines++;

				string id = key["dotnet_diagnostic.".Length..].Replace(".severity", string.Empty);

				if (!descriptors.Any(descriptor => descriptor.Id == id))
					problems.Add($"{key} names a rule that does not exist.");

				if (!key.EndsWith(".severity", StringComparison.Ordinal))
					problems.Add($"{key} is not a severity key.");

				if (!s_severities.Contains(value))
					problems.Add($"{key} = {value} is not a severity.");
			}
			else if (
				key.StartsWith("dotnet_analyzer_diagnostic.category-", StringComparison.Ordinal)
			)
			{
				checkedLines++;

				string category = key["dotnet_analyzer_diagnostic.category-".Length..]
					.Replace(".severity", string.Empty);

				if (!descriptors.Any(descriptor => descriptor.Category == category))
					problems.Add($"{key} names a category no rule is in.");

				if (!s_severities.Contains(value))
					problems.Add($"{key} = {value} is not a severity.");
			}
			else if (key.StartsWith("stanza_", StringComparison.Ordinal))
			{
				checkedLines++;

				if (!hasOptionPage(key))
					problems.Add($"{key} has no option page.");
			}
		}

		return (problems, checkedLines);
	}

	private static List<string> ReadStarter()
	{
		string[] lines = File.ReadAllLines(
			RepositoryPaths.Below("docs", "Adopting-the-analyzers.md")
		);

		int heading = Array.FindIndex(
			lines,
			line => line.StartsWith("## Starter ", StringComparison.Ordinal)
		);

		Assert.IsGreaterThanOrEqualTo(0, heading, "The adoption guide has no starter section.");

		int open = Array.FindIndex(lines, heading, line => line == "```ini");
		int close = Array.FindIndex(lines, open + 1, line => line == "```");

		Assert.IsGreaterThan(heading, open, "The starter section has no ini block.");

		return [.. lines[(open + 1)..close]];
	}
}
