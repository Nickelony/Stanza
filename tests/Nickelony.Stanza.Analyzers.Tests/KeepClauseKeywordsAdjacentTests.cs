using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class KeepClauseKeywordsAdjacentTests
{
	[TestMethod]
	public async Task AClauseKeywordSitsNextToTheBranchBeforeIt()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					if (value > 0)
					{
						DoThing();
					}

					else
					{
						DoThing();
					}
				}

				void N()
				{
					try
					{
						DoThing();
					}

					catch (Exception)
					{
						DoThing();
					}

					finally
					{
						DoThing();
					}
				}

				void P()
				{
					do
					{
						DoThing();
					}

					while (true);
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int value)
				{
					if (value > 0)
					{
						DoThing();
					}
					else
					{
						DoThing();
					}
				}

				void N()
				{
					try
					{
						DoThing();
					}
					catch (Exception)
					{
						DoThing();
					}
					finally
					{
						DoThing();
					}
				}

				void P()
				{
					do
					{
						DoThing();
					}
					while (true);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepClauseKeywordsAdjacentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(4, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AdjacentClausesAndBraceLessBodiesStayTight()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					if (value > 0)
						DoThing();
					else
						DoThing();
				}

				void N()
				{
					try
					{
						DoThing();
					}
					finally
					{
						DoThing();
					}
				}
			}
			""";

		// Without a brace on either side of the keyword there is nothing to measure,
		// and clauses that already sit next to their brace stay as they are.
		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepClauseKeywordsAdjacentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task FixAllKeepsEveryClauseKeywordAdjacentInOnePass()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					if (value > 0)
					{
						DoThing();
						DoThing();
					}

					else
					{
						DoThing();
						DoThing();
					}
				}

				void N()
				{
					try
					{
						DoThing();
					}

					catch (Exception)
					{
						DoThing();
					}

					finally
					{
						DoThing();
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int value)
				{
					if (value > 0)
					{
						DoThing();
						DoThing();
					}
					else
					{
						DoThing();
						DoThing();
					}
				}

				void N()
				{
					try
					{
						DoThing();
					}
					catch (Exception)
					{
						DoThing();
					}
					finally
					{
						DoThing();
					}
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunFixAllAsync(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(3, result.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.KeepClauseKeywordsAdjacent.Id,
			result.Diagnostics[0].Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheClauseKeywordFixFindsTheGapWithoutAnAdditionalLocation()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					if (value > 0)
					{
						DoThing();
					}

					else
					{
						DoThing();
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int value)
				{
					if (value > 0)
					{
						DoThing();
					}
					else
					{
						DoThing();
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<KeepClauseKeywordsAdjacentAnalyzer>(
				source
			);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheClausePairSettlesOnTheHalfThatOwnsTheGap()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					if (value > 0)
					{
						DoThing();
					}

					else
					{
						DoThing();
					}

					try
					{
						DoThing();
					}
					catch (Exception)
					{
						DoThing();
					}
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunUnsettledAsync(
			source,
			TestRuleSets.ClauseRulesEnabled
		);

		// Both halves measure the same boundary, so the file cannot have it both ways:
		// the default half owns the gap and the other one stays quiet over it.
		Assert.HasCount(
			1,

			result.Diagnostics.Where(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.KeepClauseKeywordsAdjacent.Id
			)
		);

		Assert.IsEmpty(
			result.Diagnostics.Where(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.SeparateClauseKeywordFromBraceBeforeIt.Id
			)
		);

		AnalyzerRunResult secondRun = await RoslynTestHarness.RunUnsettledAsync(
			result.FixedText,
			TestRuleSets.ClauseRulesEnabled
		);

		Assert.IsEmpty(secondRun.Diagnostics, "The pair keeps fighting over the gap.");
	}
}
