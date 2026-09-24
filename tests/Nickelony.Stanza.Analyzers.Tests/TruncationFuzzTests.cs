using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>Cuts the fixtures of these tests short, where a user's file stops mid-edit.</summary>
[TestClass]
public sealed class TruncationFuzzTests
{
	private const string SoakVariable = "STANZA_SOAK_SEEDS";

	// Every Nth fixture is cut at this many evenly spaced offsets, so the default run stays brief.
	private const int FixtureStride = 6;

	private const int CutsPerFixture = 6;

	// The default timeout of these tests would stop a soak that cuts every fixture at every offset.
	private const int MaxSoakMilliseconds = 1_800_000;

	// A batch is one project, and the cuts of a fixture declare the same types, so its cost climbs steeply with the count.
	private const int BatchSources = 100;

	[TestMethod]
	public async Task NoAnalyzerOrFixThrowsOnASampleOfFixturesCutShort()
	{
		string[] fixtures = ReadFixtures();

		Assert.IsNotEmpty(fixtures, "No fixture was read, so nothing was cut.");

		List<string> cuts = [];

		for (int index = 0; index < fixtures.Length; index += FixtureStride)
		{
			string fixture = fixtures[index];

			for (int cut = 1; cut <= CutsPerFixture; cut++)
				cuts.Add(fixture[..(fixture.Length * cut / (CutsPerFixture + 1))]);
		}

		foreach (string[] batch in cuts.Chunk(BatchSources))
			await RoslynTestHarness.AssertSurvivesAsync(batch);
	}

	[TestMethod]
	[TestCategory("Soak")]
	[Timeout(MaxSoakMilliseconds)]
	public async Task NoAnalyzerOrFixThrowsOnAnyFixtureCutAtAnyOffset()
	{
		string? setting = Environment.GetEnvironmentVariable(SoakVariable);

		bool isCount = int.TryParse(
			s: setting,
			style: NumberStyles.None,
			provider: CultureInfo.InvariantCulture,
			result: out int count
		);

		if (!isCount || count < 1)
			ExpectedSkip.Skip($"Set {SoakVariable} to run every cut of every fixture.");

		List<string> batch = [];

		foreach (string fixture in ReadFixtures())
		{
			for (int length = 1; length < fixture.Length; length++)
			{
				batch.Add(fixture[..length]);

				if (batch.Count < BatchSources)
					continue;

				await SurviveAsync(batch);

				batch.Clear();
			}
		}

		if (batch.Count > 0)
			await SurviveAsync(batch);
	}

	// Each batch gets the budget of a whole test, so the soak's time grows with the fixtures and not against the budget.
	private static async Task SurviveAsync(List<string> batch)
	{
		HarnessBudget.Restart();
		await RoslynTestHarness.AssertSurvivesAsync(batch);
	}

	// A fixture is a raw string literal of these tests that reads as a type or a namespace.
	private static string[] ReadFixtures()
	{
		string directory = RepositoryPaths.Below("tests", "Nickelony.Stanza.Analyzers.Tests");

		return
		[
			.. RepositoryFiles
				.Under(directory, "*.cs")
				.SelectMany(file =>
					CSharpSyntaxTree
						.ParseText(File.ReadAllText(file))
						.GetRoot()
						.DescendantTokens()
						.Where(token =>
							token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken)
							|| token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken)
						)
						.Select(token => token.ValueText)
				)
				.Where(text =>
					text.Contains("class ", StringComparison.Ordinal)
					|| text.Contains("struct ", StringComparison.Ordinal)
					|| text.Contains("record ", StringComparison.Ordinal)
					|| text.Contains("interface ", StringComparison.Ordinal)
					|| text.Contains("namespace ", StringComparison.Ordinal)
				)
				.Distinct(StringComparer.Ordinal)
				.Order(StringComparer.Ordinal),
		];
	}
}
