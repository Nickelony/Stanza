using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>Guards that each analyzer and fix provider takes the name its rule asks.</summary>
[TestClass]
[TestCategory("Repository")]
public sealed class AnalyzerStructureGuardTests
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

			// A directory holds the plural of its family's name, as Bodies holds the Body rules.
			string plural = family.EndsWith('y') ? family[..^1] + "ies" : family + "s";

			Assert.IsTrue(
				directories.Contains(plural, StringComparer.Ordinal),
				$"{provider.GetType().Name} fixes the rules of {string.Join(", ", directories)}, none of which is {plural}."
			);
		}
	}
}
