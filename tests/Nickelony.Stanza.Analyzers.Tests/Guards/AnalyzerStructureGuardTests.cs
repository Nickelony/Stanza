using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Guards;

/// <summary>Guards analyzer and provider names and places, and which files bind a model.</summary>
[TestClass]
[TestCategory("Repository")]
public sealed partial class AnalyzerStructureGuardTests
{
	[TestMethod]
	public void EveryAnalyzerIsNamedAfterItsDescriptorAndLivesInAFileOfThatName()
	{
		var fieldsById = TestRuleSets.Declared.ToDictionary(
			rule => rule.Descriptor.Id,
			rule => rule.Field,
			StringComparer.Ordinal
		);

		foreach (DiagnosticAnalyzer analyzer in RoslynTestHarness.AllAnalyzers)
		{
			Type type = analyzer.GetType();
			string field = fieldsById[analyzer.SupportedDiagnostics[0].Id];

			Assert.AreEqual(
				field + "Analyzer",
				type.Name,
				$"{type.Name} is not its descriptor field {field} plus Analyzer."
			);

			string directory = type.Namespace!.Split('.')[^1];

			string file = RepositoryPaths.Below(
				"src",
				"Nickelony.Stanza.Analyzers",
				directory,
				type.Name + ".cs"
			);

			Assert.IsTrue(
				File.Exists(file),
				$"{type.Name} is not in a file of that name under {directory}."
			);
		}
	}

	[TestMethod]
	public void EveryDescriptorFieldIsItsTitleWithoutArticles()
	{
		foreach ((string field, DiagnosticDescriptor descriptor) in TestRuleSets.Declared)
		{
			string[] words = descriptor
				.Title.ToString(CultureInfo.InvariantCulture)
				.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries);

			string expected = string.Concat(
				words
					.Where(static word => word is not ("a" or "an" or "the" or "its"))
					.Select(static word => char.ToUpperInvariant(word[0]) + word[1..])
			);

			Assert.AreEqual(expected, field, $"{descriptor.Id} is titled '{descriptor.Title}'.");
		}
	}

	[TestMethod]
	public void EveryFixProviderIsNamedForTheDirectoryOfARuleItFixes()
	{
		foreach (CodeFixProvider provider in RoslynTestHarness.AllCodeFixProviders)
		{
			string family = provider
				.GetType()
				.Name.Replace("CodeFixProvider", "", StringComparison.Ordinal);

			string[] directories =
			[
				.. RoslynTestHarness
					.AllAnalyzers.Where(analyzer =>
						provider.FixableDiagnosticIds.Contains(analyzer.SupportedDiagnostics[0].Id)
					)
					.Select(static analyzer => analyzer.GetType().Namespace!.Split('.')[^1])
					.Distinct(),
			];

			Assert.IsTrue(
				directories.Any(directory => NamesFamily(directory, family)),
				$"{provider.GetType().Name} fixes the rules of {string.Join(", ", directories)}, none of which is its family {family}."
			);
		}
	}

	// A directory is its family's name, as Documentation is, or its plural, as Bodies is the Body rules' and Braces the Brace rules'.
	private static bool NamesFamily(string directory, string family)
	{
		return directory == family
			|| directory == family + "s"
			|| (family.EndsWith('y') && directory == family[..^1] + "ies");
	}

	[GeneratedRegex(@"`(?<first>ZA\d{4})`(?:-`(?<last>ZA\d{4})`)?", RegexOptions.CultureInvariant)]
	private static partial Regex RuleIdRange();

	[GeneratedRegex(@"ZA\d{4}", RegexOptions.CultureInvariant)]
	private static partial Regex RuleId();

	[TestMethod]
	public void TheArchitecturePageMapsEveryRuleToItsDirectoryAndItsFixProvider()
	{
		string[] rows =
		[
			.. File.ReadLines(RepositoryPaths.Below("docs", "Architecture.md"))
				.SkipWhile(static line =>
					!line.StartsWith("| Rule IDs |", StringComparison.Ordinal)
				)
				.Skip(2)
				.TakeWhile(static line => line.StartsWith('|')),
		];

		Assert.IsNotEmpty(rows, "The Architecture page has no table of rule IDs.");

		HashSet<string> declared =
		[
			.. TestRuleSets.Declared.Select(static rule => rule.Descriptor.Id),
		];

		(string Id, string Directory, string Provider)[] claims = [.. rows.SelectMany(ReadRow)];
		List<string> offenders = [];

		// A bound that is no rule means the row claims IDs that do not exist.
		foreach (Match bound in RuleId().Matches(string.Join(' ', rows)))
		{
			if (!declared.Contains(bound.Value))
				offenders.Add($"{bound.Value} bounds a row of the table and is no rule.");
		}

		foreach (string id in declared.Order(StringComparer.Ordinal))
		{
			(string Id, string Directory, string Provider)[] rowsOfRule =
			[
				.. claims.Where(claim => claim.Id == id),
			];

			string directory = RoslynTestHarness
				.AllAnalyzers.Single(analyzer =>
					analyzer.SupportedDiagnostics.Any(descriptor => descriptor.Id == id)
				)
				.GetType()
				.Namespace!.Split('.')[^1];

			string provider =
				RoslynTestHarness
					.AllCodeFixProviders.SingleOrDefault(provider =>
						provider.FixableDiagnosticIds.Contains(id)
					)
					?.GetType()
					.Name
				?? "None";

			if (rowsOfRule.Length != 1)
			{
				offenders.Add($"{id} is in {rowsOfRule.Length} rows of the table, not one.");
			}
			else if (rowsOfRule[0] != (id, directory, provider))
			{
				offenders.Add(
					$"{id} is in {directory}/ with {provider}, but its row says {rowsOfRule[0].Directory}/ with {rowsOfRule[0].Provider}."
				);
			}
		}

		GuardAssert.None(offenders, "The table of the Architecture page does not match the rules");
	}

	// Each rule ID a row of the table covers, with the directory and the provider the row names; "None" is no provider.
	private static IEnumerable<(string Id, string Directory, string Provider)> ReadRow(string row)
	{
		string[] cells = row.Split(
			'|',
			StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
		);

		string directory = cells[1].Trim('`').TrimEnd('/');
		string provider = cells[2].StartsWith('`') ? cells[2].Trim('`') : "None";

		foreach (Match range in RuleIdRange().Matches(cells[0]))
		{
			int first = int.Parse(range.Groups["first"].Value[2..], CultureInfo.InvariantCulture);

			int last = range.Groups["last"].Success
				? int.Parse(range.Groups["last"].Value[2..], CultureInfo.InvariantCulture)
				: first;

			for (int number = first; number <= last; number++)
			{
				yield return (
					"ZA" + number.ToString("D4", CultureInfo.InvariantCulture),
					directory,
					provider
				);
			}
		}
	}

	[TestMethod]
	public void OnlyTheSymbolReadersBindASemanticModelInTheBlankLineRules()
	{
		string directory = RepositoryPaths.Below("src", "Nickelony.Stanza.Analyzers", "BlankLines");

		string[] binding =
		[
			.. Directory
				.EnumerateFiles(directory, "*.cs")
				.Where(file =>
					File.ReadAllText(file).Contains("SemanticModel", StringComparison.Ordinal)
				)
				.Select(file => Path.GetFileName(file))
				.Select(static name => name!)
				.Order(StringComparer.Ordinal),
		];

		// The subscription reader serves ZA0007, and the target reader serves ZA0008 where its setting is `split`;
		// both hold their answers in StatementReads, which only they use.
		Assert.AreSequenceEqual(
			["BlankLineAssignmentTargets.cs", "BlankLineSubscriptions.cs", "StatementReads.cs"],
			binding
		);
	}
}
