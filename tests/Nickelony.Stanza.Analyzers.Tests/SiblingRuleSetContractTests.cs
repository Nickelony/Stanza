using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>Holds each set of sibling rules to the same contract, one row per set.</summary>
[TestClass]
public sealed class SiblingRuleSetContractTests
{
	public static IEnumerable<object[]> Sets => SiblingRuleSets.Rows();

	[TestMethod]
	[DynamicData(
		nameof(Sets),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task MultiLineSiblingsAreKeptTogether(SiblingRuleSet siblings)
	{
		AnalyzerRunResult result = await RunAsync(
			siblings.Build((siblings.MultiLine, SiblingRuleSet.Blank)),
			siblings.KeepMultiLine
		);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(
			siblings.Build((siblings.MultiLine, SiblingRuleSet.Tight)),
			result
		);
	}

	[TestMethod]
	[DynamicData(
		nameof(Sets),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task SingleLineSiblingsAreKeptTogether(SiblingRuleSet siblings)
	{
		AnalyzerRunResult result = await RunAsync(
			siblings.Build((siblings.SingleLine, SiblingRuleSet.Blank)),
			siblings.KeepSingleLine
		);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(
			siblings.Build((siblings.SingleLine, SiblingRuleSet.Tight)),
			result
		);
	}

	[TestMethod]
	[DynamicData(
		nameof(Sets),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ADirectiveBetweenSingleLineSiblingsKeepsTheGap(SiblingRuleSet siblings)
	{
		AnalyzerRunResult result = await RunAsync(
			siblings.Build((siblings.SingleLine, SiblingRuleSet.Directive)),
			siblings.KeepSingleLine
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	[DynamicData(
		nameof(Sets),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task TheMultiLineTightRuleLeavesTheOtherShapesAlone(SiblingRuleSet siblings)
	{
		// Nothing to join: one container is tight already, one is single-line,
		// and a comment sits on both sides of the gap in the other two.
		AnalyzerRunResult result = await RunAsync(
			siblings.Build(
				(siblings.MultiLine, SiblingRuleSet.Tight),
				(siblings.SingleLine, SiblingRuleSet.Blank),
				(siblings.MultiLine, SiblingRuleSet.CommentBlank),
				(siblings.MultiLine, SiblingRuleSet.TwoNotes)
			),

			siblings.KeepMultiLine
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	[DynamicData(
		nameof(Sets),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task BothTightRulesSettleOnOnePolicy(SiblingRuleSet siblings)
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			siblings.Build(
				(siblings.MultiLine, SiblingRuleSet.Blank),
				(siblings.SingleLine, SiblingRuleSet.Blank)
			),

			TestRuleSets.Only(siblings.KeepMultiLine, siblings.KeepSingleLine)
		);

		Assert.HasCount(
			1,
			result.Diagnostics.Where(diagnostic => diagnostic.Id == siblings.KeepMultiLine.Id)
		);

		Assert.HasCount(
			1,
			result.Diagnostics.Where(diagnostic => diagnostic.Id == siblings.KeepSingleLine.Id)
		);

		RoslynTestHarness.AssertFixedText(
			siblings.Build(
				(siblings.MultiLine, SiblingRuleSet.Tight),
				(siblings.SingleLine, SiblingRuleSet.Tight)
			),

			result
		);
	}

	[TestMethod]
	[DynamicData(
		nameof(Sets),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task TheTwoMultiLineRulesSettleOnTheHalfThatOwnsTheGap(SiblingRuleSet siblings)
	{
		IReadOnlyDictionary<string, ReportDiagnostic> multiLineRules = TestRuleSets.Only(
			siblings.Separate,
			siblings.KeepMultiLine
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunUnsettledAsync(
			siblings.Build((siblings.MultiLine, SiblingRuleSet.Blank)),
			multiLineRules
		);

		// A blank line at the boundary: the half that owns the gap decides, and the other has nothing to say.
		DiagnosticDescriptor owner = StanzaRules
			.Find(siblings.Separate.Id)!
			.Pair!.OwnerWhenBothEnabled;

		Assert.HasCount(
			owner == siblings.KeepMultiLine ? 1 : 0,
			result.Diagnostics.Where(diagnostic => diagnostic.Id == siblings.KeepMultiLine.Id)
		);

		Assert.IsEmpty(
			result.Diagnostics.Where(diagnostic => diagnostic.Id == siblings.Separate.Id)
		);

		// The half that stayed quiet never took the gap back, so the pair settles.
		AnalyzerRunResult secondRun = await RoslynTestHarness.RunUnsettledAsync(
			result.FixedText,
			multiLineRules
		);

		Assert.IsEmpty(secondRun.Diagnostics, "The pair keeps fighting over the gap.");
	}

	[TestMethod]
	[DynamicData(
		nameof(Sets),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ACommentAboveTheSecondSiblingTravelsWithIt(SiblingRuleSet siblings)
	{
		AnalyzerRunResult multiLine = await RunAsync(
			siblings.Build((siblings.MultiLine, SiblingRuleSet.CommentTravels)),
			siblings.KeepMultiLine
		);

		Assert.HasCount(1, multiLine.Diagnostics);

		RoslynTestHarness.AssertFixedText(
			siblings.Build((siblings.MultiLine, SiblingRuleSet.CommentTravelsFixed)),
			multiLine
		);

		AnalyzerRunResult singleLine = await RunAsync(
			siblings.Build((siblings.SingleLine, SiblingRuleSet.CommentTravels)),
			siblings.KeepSingleLine
		);

		Assert.HasCount(1, singleLine.Diagnostics);

		RoslynTestHarness.AssertFixedText(
			siblings.Build((siblings.SingleLine, SiblingRuleSet.CommentTravelsFixed)),
			singleLine
		);
	}

	// Each build above checks its own source, so a set that wrote a carriage return would fail every row that uses it.
	[TestMethod]
	public void ASourceBuiltWithACarriageReturnFailsTheBuild()
	{
		SiblingRuleSet mixed = SiblingRuleSets.All[0] with { Separator = "\r\n" };

		_ = Assert.ThrowsExactly<AssertFailedException>(() =>
			mixed.Build((mixed.MultiLine, SiblingRuleSet.Blank))
		);
	}

	private static Task<AnalyzerRunResult> RunAsync(string source, DiagnosticDescriptor rule)
	{
		DiagnosticAnalyzer analyzer = RoslynTestHarness.AllAnalyzers.Single(candidate =>
			candidate.SupportedDiagnostics.Single().Id == rule.Id
		);

		return RoslynTestHarness.RunOneAsync(source, analyzer, TestRuleSets.Only(rule));
	}
}
