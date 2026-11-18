using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Guards;

/// <summary>Guards that each issue form offers a product option per package that ships.</summary>
[TestClass]
[TestCategory("Repository")]
public sealed class IssueTemplateGuardTests
{
	// The options that name no package.
	private static readonly string[] s_otherProducts = ["Documentation"];

	[TestMethod]
	public void EachIssueFormOffersOneProductPerPackageAndTheOtherProducts()
	{
		string[] ids =
		[
			.. File.ReadLines(RepositoryPaths.Below(".github", "scripts", "package-ids.txt"))
				.Select(line => line.Trim())
				.Where(line => line.Length > 0),
		];

		foreach (string form in new[] { "bug_report.yml", "feature_request.yml" })
		{
			string path = RepositoryPaths.Below(".github", "ISSUE_TEMPLATE", form);
			string[] options = ReadProductOptions(path);

			Assert.IsNotEmpty(options, $"{form} has no product dropdown with options.");

			GuardAssert.None(FindProblems(form, options, ids), $"{form} offers the wrong products");
		}
	}

	[TestMethod]
	public void AMissingDoubledOrUnknownProductOptionIsFound()
	{
		string[] ids = ["Nickelony.Stanza.Analyzers", "Nickelony.Stanza.Markup"];

		string[] sound =
		[
			"Nickelony.Stanza.Analyzers",
			"Nickelony.Stanza.Markup (stanza-markup)",
			"Documentation",
		];

		Assert.IsEmpty(FindProblems("form.yml", sound, ids));

		List<string> problems = FindProblems(
			"form.yml",
			["Nickelony.Stanza.Analyzers", "Nickelony.Stanza.Analyzers", "Unknown"],
			ids
		);

		Assert.HasCount(3, problems);
		Assert.Contains("does not offer Nickelony.Stanza.Analyzers", problems[0]);
		Assert.Contains("does not offer Nickelony.Stanza.Markup", problems[1]);
		Assert.Contains("offers 'Unknown'", problems[2]);
	}

	private static List<string> FindProblems(string form, string[] options, string[] ids)
	{
		List<string> problems = [];

		foreach (string id in ids)
		{
			if (options.Count(option => IsOptionOf(option, id)) != 1)
			{
				problems.Add(
					$"{form} does not offer {id} exactly once. Add one product option for each id of package-ids.txt."
				);
			}
		}

		foreach (string option in options)
		{
			if (
				!s_otherProducts.Contains(option, StringComparer.Ordinal)
				&& !ids.Any(id => IsOptionOf(option, id))
			)
			{
				problems.Add(
					$"{form} offers '{option}', which is neither a package of package-ids.txt nor one of the other products."
				);
			}
		}

		return problems;
	}

	// A tool's option names the command after the ID, as in `Nickelony.Stanza.Markup (stanza-markup)`.
	private static bool IsOptionOf(string option, string id) =>
		option == id || option.StartsWith(id + " (", StringComparison.Ordinal);

	private static string[] ReadProductOptions(string path)
	{
		string[] lines = File.ReadAllLines(path);

		int product = Array.FindIndex(lines, line => line.Trim() == "id: product");

		int options =
			product < 0 ? -1 : Array.FindIndex(lines, product, line => line.Trim() == "options:");

		if (options < 0)
			return [];

		List<string> found = [];

		for (int index = options + 1; index < lines.Length; index++)
		{
			string line = lines[index].Trim();

			if (!line.StartsWith("- ", StringComparison.Ordinal))
				break;

			found.Add(line[2..].Trim());
		}

		return [.. found];
	}
}
