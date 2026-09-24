using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

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

			foreach (string id in ids)
			{
				Assert.ContainsSingle(
					options.Where(option => IsOptionOf(option, id)),
					$"{form} does not offer {id} exactly once. Add one product option for each id of package-ids.txt."
				);
			}

			foreach (string option in options)
			{
				Assert.IsTrue(
					s_otherProducts.Contains(option, StringComparer.Ordinal)
						|| ids.Any(id => IsOptionOf(option, id)),

					$"{form} offers '{option}', which is neither a package of package-ids.txt nor one of the other products."
				);
			}
		}
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
