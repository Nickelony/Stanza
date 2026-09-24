using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class RemoveBracePaddingTests
{
	[TestMethod]
	public async Task BracePaddingIsRemoved()
	{
		const string source = """
			class C
			{
				void M()
				{

					DoThing();

				}

				void DoThing()
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					DoThing();
				}

				void DoThing()
				{
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveBracePaddingAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		// Only the first method's padded braces report; the second method's empty body stays.
		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task BlankLineBeforeAnOpeningBraceIsRemoved()
	{
		const string source = """
			class C
			{
				void M()

				{
					DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					DoThing();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveBracePaddingAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task BracePaddingExceptionsAreLeftAlone()
	{
		const string source = """
			class C
			{
				void M()
				{
					if (true)
					{
						DoThing();
					}

				}

				void N()
				{

				}

				void P()
				{
					/* not padding */

					DoThing();
				}
			}
			""";

		// RemoveBracePaddingAnalyzer reports none of these:
		// a blank line between two closing braces has no owner in this package,
		// an empty body is not padding, and a comment gap cannot be taken away.
		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveBracePaddingAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AClauseBodyBraceGapIsPaddingLikeAnyOther()
	{
		const string source = """
			class C
			{
				void M(int value)
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

					if (value > 0)
					{

						DoThing();
					}

					else

					{
						DoThing();
					}

					do

					{
						DoThing();
					}
					while (value > 0);
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int value)
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

					if (value > 0)
					{
						DoThing();
					}

					else
					{
						DoThing();
					}

					do
					{
						DoThing();
					}
					while (value > 0);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveBracePaddingAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(6, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentBeforeTheBodyDoesNotHideThePadding()
	{
		const string source = """
			class C
			{
				void M()
				{

					// section marker

					DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					// section marker

					DoThing();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveBracePaddingAnalyzer>(
			source
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheBracePaddingFixFindsTheGapWithoutAnAdditionalLocation()
	{
		const string source = """
			class C
			{
				void M()
				{
					DoThing();

				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					DoThing();
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<RemoveBracePaddingAnalyzer>(
				source
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task FixAllRemovesEveryBracePaddingInOnePass()
	{
		const string source = """
			class C
			{
				void M()
				{

					DoThing();
					DoThing();

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
				{
					DoThing();
					DoThing();
				}

				void N()
				{
					DoThing();
					DoThing();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunFixAllAsync(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(4, result.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.RemoveBracePadding.Id, result.Diagnostics[0].Id);
		Assert.AreEqual(DiagnosticDescriptors.RemoveBracePadding.Id, result.Diagnostics[3].Id);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TypeBodiesAndAccessorListsAreUnpadded()
	{
		const string source = """
			class C
			{
				int Value
				{

					get
					{
						return 1;
					}
				}

				int M() => 1;

			}
			""";

		const string fixedSource = """
			class C
			{
				int Value
				{
					get
					{
						return 1;
					}
				}

				int M() => 1;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveBracePaddingAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheRuleLeavesTheBracesItDoesNotOwnAlone()
	{
		const string source = """
			namespace N
			{

				class C
				{
					void M(int value)
					{
						switch (value)
						{

							case 1:
								break;
						}

						var item = new Item
						{

							Count = 1,
						};
					}
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveBracePaddingAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task APragmaThatSilencesTheConstructRuleLeavesTheGapAboveABlockAlone()
	{
		const string source = """
			class C
			{
				void M()
				{
					int first = 1;

					{
						int second = 2;
					}
				}
			}
			""";

		// The pragma covers the block without standing in the gap:
		// a directive written between the unit and its brace is a gap no rule of the family writes through.
		string withPragma = $$"""
			class C
			{
				void M()
				{
			#pragma warning disable {{DiagnosticDescriptors.SeparateMultiLineConstructs.Id}}
					int first = 1;

					{
						int second = 2;
					}
			#pragma warning restore {{DiagnosticDescriptors.SeparateMultiLineConstructs.Id}}
				}
			}
			""";

		// The gap above a block that follows a sibling is never padding, whether the construct rule is on or silenced.
		AnalyzerRunResult owned = await RoslynTestHarness.RunAsync<RemoveBracePaddingAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.IsEmpty(owned.Diagnostics);

		AnalyzerRunResult silenced = await RoslynTestHarness.RunAsync<RemoveBracePaddingAnalyzer>(
			withPragma,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.IsEmpty(silenced.Diagnostics);
	}
}
