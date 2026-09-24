using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>Checks that each assertion of the harness fails on the result it refuses.</summary>
[TestClass]
public sealed class HarnessAssertionTests
{
	private static readonly SyntaxTree s_tree = CSharpSyntaxTree.ParseText("class C { }");

	private static DiagnosticDescriptor Groups => DiagnosticDescriptors.SeparateMemberGroups;

	private static DiagnosticDescriptor Constructs =>
		DiagnosticDescriptors.SeparateMultiLineConstructs;

	private static DiagnosticDescriptor CommentMarker =>
		DiagnosticDescriptors.SeparateSingleLineCommentMarkerFromText;

	private static DiagnosticDescriptor TrailingComment =>
		DiagnosticDescriptors.SeparateTrailingCommentFromCode;

	[TestMethod]
	public void FixedTextThatMatchesTheExpectedTextPasses() =>
		RoslynTestHarness.AssertFixedText("a\r\nb", new AnalyzerRunResult([], "a\r\nb"));

	[TestMethod]
	public void FixedTextThatDiffersFailsAndShowsBothTexts()
	{
		AssertFailedException failure = Assert.ThrowsExactly<AssertFailedException>(() =>
			RoslynTestHarness.AssertFixedText("a\nb", new AnalyzerRunResult([], "a\nc"), "The fix.")
		);

		Assert.Contains("The fix. [actual] a\nc [expected] a\nb", failure.Message);
	}

	[TestMethod]
	public void FixedTextThatDiffersOnlyInItsLineEndingFailsAndShowsTheCarriageReturn()
	{
		AssertFailedException failure = Assert.ThrowsExactly<AssertFailedException>(() =>
			RoslynTestHarness.AssertFixedText("a\nb", new AnalyzerRunResult([], "a\r\nb"))
		);

		Assert.Contains("[actual] a~\nb [expected] a\nb", failure.Message);
	}

	[TestMethod]
	public void FixedTextThatDiffersOnlyInATabFailsAndShowsTheTab()
	{
		AssertFailedException failure = Assert.ThrowsExactly<AssertFailedException>(() =>
			RoslynTestHarness.AssertFixedText("a  b", new AnalyzerRunResult([], "a\tb"))
		);

		Assert.Contains("[actual] a<b [expected] a  b", failure.Message);
	}

	[TestMethod]
	public void AResultWithNoReportFailsTheGapCheck()
	{
		_ = Assert.ThrowsExactly<AssertFailedException>(() =>
			RoslynTestHarness.AssertEachGapIsClaimedOnce(Result())
		);
	}

	[TestMethod]
	public void AReportThatNamesNoGapFailsTheGapCheck()
	{
		AssertFailedException failure = Assert.ThrowsExactly<AssertFailedException>(() =>
			RoslynTestHarness.AssertEachGapIsClaimedOnce(Result(Report(Groups, start: 0)))
		);

		Assert.Contains("names no gap, or more than one", failure.Message);
	}

	[TestMethod]
	public void AReportThatNamesTwoGapsFailsTheGapCheck()
	{
		AssertFailedException failure = Assert.ThrowsExactly<AssertFailedException>(() =>
			RoslynTestHarness.AssertEachGapIsClaimedOnce(Result(Report(Groups, start: 0, 4, 6)))
		);

		Assert.Contains("names no gap, or more than one", failure.Message);
	}

	[TestMethod]
	public void AReportWithNoLocationFailsTheGapCheck()
	{
		var nowhere = Diagnostic.Create(Groups, Location.None);

		AssertFailedException failure = Assert.ThrowsExactly<AssertFailedException>(() =>
			RoslynTestHarness.AssertEachGapIsClaimedOnce(Result(nowhere))
		);

		Assert.Contains("names no gap, or more than one", failure.Message);
	}

	[TestMethod]
	public void TwoRulesThatClaimOneGapFailTheGapCheck()
	{
		AnalyzerRunResult result = Result(
			Report(Groups, start: 0, 4),
			Report(Constructs, start: 0, 4)
		);

		AssertFailedException failure = Assert.ThrowsExactly<AssertFailedException>(() =>
			RoslynTestHarness.AssertEachGapIsClaimedOnce(result)
		);

		Assert.Contains("Two reports claim the same gap: ZA0002,ZA0004", failure.Message);
	}

	[TestMethod]
	public void ACommentRuleAndAThirdRuleThatClaimOneGapFailTheGapCheck()
	{
		AnalyzerRunResult result = Result(
			Report(CommentMarker, start: 0, 4),
			Report(TrailingComment, start: 0, 4),
			Report(Groups, start: 0, 4)
		);

		_ = Assert.ThrowsExactly<AssertFailedException>(() =>
			RoslynTestHarness.AssertEachGapIsClaimedOnce(result)
		);
	}

	[TestMethod]
	public void ReportsOfSeparateGapsAndTheTwoCommentRulesOnOneGapPassTheGapCheck()
	{
		AnalyzerRunResult result = Result(
			Report(Groups, start: 0, 4),
			Report(Groups, start: 6, 4),
			Report(CommentMarker, start: 8, 10),
			Report(TrailingComment, start: 8, 10)
		);

		RoslynTestHarness.AssertEachGapIsClaimedOnce(result);
	}

	private static Diagnostic Report(DiagnosticDescriptor rule, int start, params int[] neighbors)
	{
		IEnumerable<Location> gaps = neighbors.Select(neighbor =>
			Location.Create(s_tree, new TextSpan(neighbor, 0))
		);

		return Diagnostic.Create(
			rule,
			Location.Create(s_tree, new TextSpan(start, 0)),
			additionalLocations: gaps
		);
	}

	private static AnalyzerRunResult Result(params Diagnostic[] reports) =>
		new(ImmutableArray.Create(reports), "text");
}
