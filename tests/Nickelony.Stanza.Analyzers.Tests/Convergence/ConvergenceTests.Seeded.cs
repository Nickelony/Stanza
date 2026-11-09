using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Convergence;

public sealed partial class ConvergenceTests
{
	public static IEnumerable<object[]> Seeds { get; } =
	[.. Enumerable.Range(1, DefaultSeedCount).Select(seed => new object[] { seed })];

	[TestMethod]
	[DynamicData(
		nameof(Seeds),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task TheLayoutOfAGeneratedFileSettlesAndKeepsItsTokens(int seed)
	{
		int findings = await AssertLayoutSettlesAsync(seed);

		Assert.IsGreaterThan(
			0,
			findings,
			"The generated file needed no fix, so it proves nothing."
		);
	}

	[TestMethod]
	[DynamicData(
		nameof(Seeds),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task DamagedDocumentationCommentsSettleAndKeepTheirWords(int seed)
	{
		int findings = await AssertDocumentationSettlesAsync(seed);

		Assert.IsGreaterThan(
			0,
			findings,
			"The generated file needed no fix, so it proves nothing."
		);
	}

	// The loose run rule and the rules that ask for blank lines write opposite fixes into the same gaps.
	// Each set of them has to claim a gap once and settle, whether one fix at a time or one Fix All per rule.
	[TestMethod]
	[DynamicData(
		nameof(Seeds),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task StatementsSetApartByBlankLinesSettleWithTheRulesThatShareTheirGaps(int seed)
	{
		int findings = await AssertLooseRunsSettleAsync(seed);

		Assert.IsGreaterThan(
			0,
			findings,
			"The generated statements needed no fix, so they prove nothing."
		);
	}

	// A method that opens with guards gives the guard rule a gap beside every other rule that asks for one.
	[TestMethod]
	[DynamicData(
		nameof(Seeds),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task StatementsBelowGuardsSettleWithTheRulesThatShareTheirGaps(int seed)
	{
		int findings = await AssertGuardedRunsSettleAsync(seed);

		Assert.IsGreaterThan(
			0,
			findings,
			"The generated statements needed no fix, so they prove nothing."
		);
	}

	// The seeds reach the guard rule only when a guard stands tight against the statement below it.
	[TestMethod]
	public async Task TheGuardedSourcesGiveTheGuardRuleAGapToSetApart()
	{
		int reported = 0;

		for (int seed = 1; seed <= DefaultSeedCount; seed++)
		{
			AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(
				SourceGenerators.CreateGuardedSource(new Random(seed), looseRun: 4),
				TestRuleSets.Only(DiagnosticDescriptors.SeparateGuardClauses)
			);

			reported += result.Diagnostics.Count(static diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.SeparateGuardClauses.Id
			);
		}

		Assert.IsGreaterThan(
			0,
			reported,
			"No seed holds a guard tight against the statement below it."
		);
	}

	// The seeds reach the base call rule only when a base call opens or closes the method tight against its neighbor.
	[TestMethod]
	public async Task TheGuardedSourcesGiveTheBaseCallRuleAGapToSetApart()
	{
		int reported = 0;

		for (int seed = 1; seed <= DefaultSeedCount; seed++)
		{
			AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(
				SourceGenerators.CreateGuardedSource(new Random(seed), looseRun: 4),
				TestRuleSets.Only(DiagnosticDescriptors.SeparateBaseCall),
				new Dictionary<string, string> { ["stanza_guard_roots"] = "Guard" }
			);

			reported += result.Diagnostics.Count(static diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.SeparateBaseCall.Id
			);
		}

		Assert.IsGreaterThan(
			0,
			reported,
			"No seed holds a base call tight against the statement beside it."
		);
	}

	// The loose member rule shares its gaps with the grouping rules, the import rules and the commented sibling rule.
	[TestMethod]
	[DynamicData(
		nameof(Seeds),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task MembersSetApartByBlankLinesSettleWithTheRulesThatShareTheirGaps(int seed)
	{
		int findings = await AssertLooseMemberRunsSettleAsync(seed);

		Assert.IsGreaterThan(
			0,
			findings,
			"The generated members needed no fix, so they prove nothing."
		);
	}

	// The two loose run rules share a limit and each reads its own part of a file.
	[TestMethod]
	[DynamicData(
		nameof(Seeds),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task StatementsAndMembersOfOneFileSettleWithTheRulesThatShareTheirGaps(int seed)
	{
		int findings = await AssertLooseFileRunsSettleAsync(seed);

		Assert.IsGreaterThan(
			0,
			findings,
			"The generated file needed no fix, so it proves nothing."
		);
	}

	// A limit of zero turns the two loose run rules off, whatever the file holds.
	[TestMethod]
	[DynamicData(
		nameof(Seeds),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ALimitOfZeroLeavesTheLooseRunsOfAFileAsWritten(int seed)
	{
		string source = SourceGenerators.CreateFileSource(new Random(seed), looseRun: 5);

		string[] looseRules =
		[
			DiagnosticDescriptors.GatherRunOfLooseStatements.Id,
			DiagnosticDescriptors.GatherRunOfLooseMembers.Id,
		];

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.Only(
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.GatherRunOfLooseMembers
		);

		AnalyzerRunResult off = await RoslynTestHarness.ReportAllAsync(
			source,
			rules,
			new Dictionary<string, string> { ["stanza_max_loose_run_size"] = "0" }
		);

		AnalyzerRunResult on = await RoslynTestHarness.ReportAllAsync(
			source,
			rules,
			new Dictionary<string, string> { ["stanza_max_loose_run_size"] = "1" }
		);

		Assert.IsFalse(
			off.Diagnostics.Any(diagnostic => looseRules.Contains(diagnostic.Id)),
			"A loose run rule reported at a limit of zero."
		);

		Assert.IsTrue(
			on.Diagnostics.Any(diagnostic => looseRules.Contains(diagnostic.Id)),
			"The file holds no loose run, so it proves nothing."
		);
	}
}
