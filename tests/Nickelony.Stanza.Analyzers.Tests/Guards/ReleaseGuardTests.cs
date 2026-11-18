using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Guards;

/// <summary>Guards the rows of the release tracking files.</summary>
[TestClass]
[TestCategory("Repository")]
public sealed partial class ReleaseGuardTests
{
	[GeneratedRegex(@"^\s*`?(ZA\d{4})`?\s*\|", RegexOptions.CultureInvariant)]
	private static partial Regex RuleRow();

	[TestMethod]
	public void TheUnshippedTrackingRowsKeepTheirShape()
	{
		string path = RepositoryPaths.Below(
			"src",
			"Nickelony.Stanza.Analyzers",
			"AnalyzerReleases.Unshipped.md"
		);

		string[] lines = File.ReadAllLines(path);

		Assert.StartsWith(
			"; Unshipped analyzer release",
			lines[0],
			"The unshipped release tracking file does not start with its header."
		);

		Assert.Contains<string>(
			"### New Rules",
			lines,
			"The unshipped release tracking file carries no '### New Rules' section."
		);

		Assert.Contains<string>(
			"Rule ID | Category | Severity | Notes",
			lines,
			"The unshipped release tracking file carries no rule table header."
		);

		HashSet<string> shipped = new(
			ReadRuleIds(
				RepositoryPaths.Below(
					"src",
					"Nickelony.Stanza.Analyzers",
					"AnalyzerReleases.Shipped.md"
				)
			),

			StringComparer.Ordinal
		);

		// A release moves every row out of the unshipped file, so the shipped rows then show that the reads worked.
		int rows = lines.Count(line => ReadRuleId(line).Length != 0) + shipped.Count;

		Assert.IsGreaterThan(0, rows, "No rule row was read, so no row was checked.");

		GuardAssert.None(
			[.. FindStrayRows(lines, shipped)],
			"Rule rows of the unshipped file that sit under no release heading and are not shipped"
		);
		// The unshipped file is protected at build time:
		// the release-tracking rule RS2000 refuses a descriptor with no row,
		// so a runtime copy of that check could never fail.
	}

	[TestMethod]
	public void ARuleRowUnderNoReleaseHeadingIsFoundUnlessItIsShipped()
	{
		string[] lines =
		[
			"; Unshipped analyzer release",
			"### New Rules",
			"Rule ID | Category | Severity | Notes",
			"ZA0001 | Layout | Warning | A",
			"### Notes",
			"ZA0002 | Layout | Warning | B",
			"ZA0003 | Layout | Warning | C",
		];

		HashSet<string> shipped = new(["ZA0003"], StringComparer.Ordinal);

		string[] strays = [.. FindStrayRows(lines, shipped)];

		Assert.ContainsSingle(strays);
		Assert.Contains("ZA0002", strays[0]);
	}

	// Only the sections the release-tracking format defines are release headings:
	// a row under any other heading has lost the section it belongs to.
	private static IEnumerable<string> FindStrayRows(
		IEnumerable<string> lines,
		HashSet<string> shipped
	)
	{
		bool underAReleaseHeading = false;

		foreach (string line in lines)
		{
			if (line.StartsWith("### ", StringComparison.Ordinal))
			{
				underAReleaseHeading = IsReleaseHeading(line);
				continue;
			}

			string id = ReadRuleId(line);

			if (id.Length != 0 && !underAReleaseHeading && !shipped.Contains(id))
				yield return $"{id} is listed neither under a release heading nor in the shipped file.";
		}
	}

	private static IEnumerable<string> ReadRuleIds(string path) =>
		File.ReadAllLines(path).Select(ReadRuleId).Where(id => id.Length != 0);

	private static bool IsReleaseHeading(string line) =>
		line is "### New Rules" or "### Changed Rules" or "### Removed Rules";

	private static string ReadRuleId(string line)
	{
		Match match = RuleRow().Match(line);
		return match.Success ? match.Groups[1].Value : string.Empty;
	}
}
