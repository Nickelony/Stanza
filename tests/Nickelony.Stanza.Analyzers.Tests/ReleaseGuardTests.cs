using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

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

		bool underAReleaseHeading = false;

		foreach (string line in lines)
		{
			if (line.StartsWith("### ", StringComparison.Ordinal))
			{
				// Only the sections the release-tracking format defines are release headings:
				// a row under any other heading has lost the section it belongs to.
				underAReleaseHeading = IsReleaseHeading(line);
				continue;
			}

			string id = ReadRuleId(line);

			if (id.Length == 0)
				continue;

			Assert.IsTrue(
				underAReleaseHeading || shipped.Contains(id),
				$"{id} is listed neither under a release heading nor in the shipped file."
			);
		}

		// The unshipped file is protected at build time:
		// the release-tracking rule RS2000 refuses a descriptor with no row,
		// so a runtime copy of that check could never fail.
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
