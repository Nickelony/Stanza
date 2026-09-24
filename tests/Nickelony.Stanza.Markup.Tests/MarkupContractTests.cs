extern alias stanza;

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Configuration;
using Nickelony.Stanza.Markup.Core.Options;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Markup.Tests;

[TestClass]
[TestCategory("Repository")]
public sealed class MarkupContractTests
{
	[TestMethod]
	public void EveryProfileAndExtensionIsNamedByTheBehaviorPage()
	{
		string[] rows =
		[
			.. File.ReadLines(BehaviorPath())
				.Where(line => line.StartsWith("| `", StringComparison.Ordinal)),
		];

		foreach (MarkupProfile profile in MarkupProfiles.All)
		{
			string row =
				rows.SingleOrDefault(line =>
					line.StartsWith($"| `{profile.Name}` |", StringComparison.Ordinal)
				)
				?? throw new AssertFailedException(
					$"Add a row for '{profile.Name}' to the profile table of the behavior page."
				);

			foreach (string extension in profile.Extensions)
			{
				Assert.Contains(
					$"`{extension}`",
					row,
					$"The row of the '{profile.Name}' profile does not name '{extension}'."
				);
			}
		}
	}

	[TestMethod]
	public void EveryMarkupKeyIsNamedByTheConfigurationPage()
	{
		string[] keys =
		[
			.. typeof(MarkupKeys)
				.GetFields(BindingFlags.Public | BindingFlags.Static)
				.Where(field => field.IsLiteral)
				.Select(field => field.GetRawConstantValue() as string)
				.Where(IsMarkupKey)
				.Select(key => key!),
		];

		Assert.IsNotEmpty(
			keys,
			"No markup key is found in MarkupKeys, so the check below would pass vacuously."
		);

		string[] rows =
		[
			.. File.ReadLines(ConfigurationPath())
				.Where(line => line.StartsWith("| [`", StringComparison.Ordinal)),
		];

		foreach (string key in keys)
		{
			int matches = rows.Count(row =>
				row.StartsWith($"| [`{key}`]", StringComparison.Ordinal)
			);

			Assert.IsGreaterThanOrEqualTo(
				1,
				matches,
				$"Add a row for '{key}' to the keys table of the configuration page."
			);
		}
	}

	private static bool IsMarkupKey(string? value)
	{
		return value is not null
			&& value.Length > MarkupKeys.MarkupPrefix.Length
			&& value.StartsWith(MarkupKeys.MarkupPrefix, StringComparison.Ordinal);
	}

	private static string BehaviorPath() => RepositoryPaths.Below("docs", "Markup-behavior.md");

	private static string ConfigurationPath() =>
		RepositoryPaths.Below("docs", "Markup-configuration.md");
}
