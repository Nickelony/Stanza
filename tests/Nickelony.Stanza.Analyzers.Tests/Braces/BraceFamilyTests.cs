using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Braces;

/// <summary>Checks that the brace rules agree on a chain and claim each gap once.</summary>
[TestClass]
public sealed class BraceFamilyTests
{
	// No rule removes the blank line before an else that follows no brace.
	[TestMethod]
	public async Task ABlankLineBeforeAnElseWhoseBracesWereDroppedStaysUnowned()
	{
		const string source = """
			class C
			{
				void M(bool flag)
				{
					if (flag)
						Call();

					else
						Other();
				}

				void Call() { }

				void Other() { }
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.RemoveRedundantBraces,
				DiagnosticDescriptors.KeepClauseKeywordsAdjacent
			)
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	[DynamicData(
		nameof(BraceFamilyFixtures.HeldBlocks),
		typeof(BraceFamilyFixtures),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task TheBraceRulesAgreeOnAChainWhoseBlockHoldsALineBesidesItsStatement(
		string shape,
		string block
	)
	{
		string braced = BraceFamilyFixtures.Chain(block, bracedFirst: true);

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.Only(
			DiagnosticDescriptors.UseBracesConsistently,
			DiagnosticDescriptors.RemoveRedundantBraces
		);

		// The half braced chain gets its braces, and the braced chain keeps them: neither undoes the other.
		AnalyzerRunResult fromHalfBraced = await RoslynTestHarness.RunAllAsync(
			BraceFamilyFixtures.Chain(block, bracedFirst: false),
			rules
		);

		AnalyzerRunResult fromBraced = await RoslynTestHarness.RunAllAsync(braced, rules);

		RoslynTestHarness.AssertFixedText(braced, fromHalfBraced);
		Assert.IsEmpty(fromBraced.Diagnostics, shape);
	}

	[TestMethod]
	public async Task TheBraceRulesAgreeOnAChainWhoseBlockHoldsOnlyACommentOnTheStatementsLine()
	{
		string braced = BraceFamilyFixtures.Chain("{|\tDoThing(); // Done.|}", bracedFirst: true);
		string unbraced = BraceFamilyFixtures.Chain("\tDoThing(); // Done.", bracedFirst: false);

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.Only(
			DiagnosticDescriptors.UseBracesConsistently,
			DiagnosticDescriptors.RemoveRedundantBraces
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(braced, rules);

		RoslynTestHarness.AssertFixedText(unbraced, result);
	}

	// CSharpier joins an empty block body to its header, so these are the empty bodies it keeps below one.
	[TestMethod]
	public async Task TheBraceRulesKeepAnEmptyLoopBodyThatCSharpierKeepsBelowItsHeader()
	{
		const string source = """
			class C
			{
				unsafe void M(bool flag, int[] items, System.IDisposable resource, int[] numbers)
				{
					for (int i = 0; i < 1; i++)
						;

					while (flag)
						;

					foreach (int item in items)
						;

					using (resource)
						;

					lock (items)
						;

					fixed (int* pointer = numbers)
						;

					for (int i = 0; i < 1; i++)
					{
						// Nothing to do.
					}

					while (flag)
					{
						// Nothing to do.
					}
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.CollapseEmptyBlocks,
				DiagnosticDescriptors.UseBracesConsistently,
				DiagnosticDescriptors.RemoveRedundantBraces
			),

			analyzerConfigOptions: new Dictionary<string, string>
			{
				["csharp_prefer_braces"] = "when_multiline:warning",
				["csharp_preserve_single_line_blocks"] = "true",
			}
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheBraceFamilyOnlyClaimsEachGapOnce()
	{
		const string source = """
			class C
			{
				void M(int value)
				{

					if (value > 0)
						return;
					else if (value < 0)
					{
						DoThing();
						DoThing();
					}

					else

					{

						DoThing();
					}
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		string reportedIds = string.Join(
			",",
			result.Diagnostics.Select(diagnostic => diagnostic.Id).Distinct().OrderBy(id => id)
		);

		// Padding, the multi-line chain and the clause keyword all fire;
		// the redundant brace rule stays out of a chain a multi-line branch pins.
		// Every gap is claimed once: padding takes the gaps next to a brace,
		// the clause keyword takes the gap before the else, and brace consistency takes the braceless branch of the chain.
		Assert.AreEqual(
			$"{DiagnosticDescriptors.KeepClauseKeywordsAdjacent.Id},{DiagnosticDescriptors.RemoveBracePadding.Id},{DiagnosticDescriptors.UseBracesConsistently.Id}",
			reportedIds
		);

		Assert.HasCount(5, result.Diagnostics);
		RoslynTestHarness.AssertEachGapIsClaimedOnce(result);
	}

	[TestMethod]
	public async Task TheEmptyBlockRuleOwnsABodyWithNothingButBlankLines()
	{
		const string source = """
			class C
			{
				void M()
				{

				}

				void N()
				{

					DoThing();
					DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{ }

				void N()
				{
					DoThing();
					DoThing();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		string reportedIds = string.Join(
			",",
			result.Diagnostics.Select(diagnostic => diagnostic.Id).Distinct().OrderBy(id => id)
		);

		Assert.AreEqual(
			$"{DiagnosticDescriptors.RemoveBracePadding.Id},{DiagnosticDescriptors.CollapseEmptyBlocks.Id}",
			reportedIds
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
