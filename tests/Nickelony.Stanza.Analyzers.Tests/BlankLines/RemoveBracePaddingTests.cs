using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

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

	// The blank lines around the last comment before a closing brace go like the other padding, whether they stand above it or below it.
	[TestMethod]
	[DataRow("\t\tDoThing();\n\t\t// Done.\n\n\t}", "\t\tDoThing();\n\t\t// Done.\n\t}")]
	[DataRow("\t\tDoThing();\n\n\t\t// Done.\n\n\t}", "\t\tDoThing();\n\t\t// Done.\n\t}")]
	[DataRow("\t\tDoThing();\n\n\t\t// Done.\n\t}", "\t\tDoThing();\n\t\t// Done.\n\t}")]
	[DataRow("\t\tDoThing();\n\t\t/* Done. */\n\n\t}", "\t\tDoThing();\n\t\t/* Done. */\n\t}")]
	[DataRow("\t\tDoThing(); // Done.\n\n\t}", "\t\tDoThing(); // Done.\n\t}")]
	public async Task PaddingAroundTheLastCommentBeforeAClosingBraceIsRemoved(
		string tail,
		string fixedTail
	)
	{
		string source = "class C\n{\n\tvoid M()\n\t{\n" + tail + "\n}\n";
		string fixedSource = "class C\n{\n\tvoid M()\n\t{\n" + fixedTail + "\n}\n";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveBracePaddingAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
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
	public async Task ANamespaceBodyAndASwitchStatementAreUnpadded()
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
					}
				}
			}
			""";

		const string fixedSource = """
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
					}
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveBracePaddingAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(4, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task InitializersAndAnonymousObjectsAreUnpadded()
	{
		const string source = """
			class C
			{
				void M()
				{
					var item = new Item

					{

						Count = 1,

					};

					var list = new List<int>
					{

						1,
						2

					};

					var array = new[]
					{

						1,
						2,

					};

					var anonymous = new

					{

						Count = 1

					};

					var copy = new Rec() with
					{

						Name = "a",

					};
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					var item = new Item
					{
						Count = 1,
					};

					var list = new List<int>
					{
						1,
						2
					};

					var array = new[]
					{
						1,
						2,
					};

					var anonymous = new
					{
						Count = 1
					};

					var copy = new Rec() with
					{
						Name = "a",
					};
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveBracePaddingAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(12, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ASwitchExpressionIsUnpadded()
	{
		const string source = """
			class C
			{
				int M(int value) =>
					value switch

					{

						1 => 10,
						_ => 20,

					};
			}
			""";

		const string fixedSource = """
			class C
			{
				int M(int value) =>
					value switch
					{
						1 => 10,
						_ => 20,
					};
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveBracePaddingAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheBlankLineBetweenNestedInitializersIsNotHeaderPadding()
	{
		const string source = """
			class C
			{
				void M()
				{
					var pairs = new Dictionary<int, int>
					{
						{ 1, 2 },

						{ 3, 4 },
					};

					int[][] rows =
					{
						new[] { 1, 2 },

						new[] { 3, 4 },
					};

					var nested = new Item
					{
						Tags = new List<string>

						{
							"a",
						},
					};
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					var pairs = new Dictionary<int, int>
					{
						{ 1, 2 },

						{ 3, 4 },
					};

					int[][] rows =
					{
						new[] { 1, 2 },

						new[] { 3, 4 },
					};

					var nested = new Item
					{
						Tags = new List<string>
						{
							"a",
						},
					};
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
	public async Task AnEmptyInitializerOrSwitchIsLeftToItsOwnRule()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					var item = new Item
					{

					};

					switch (value)
					{

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
	public async Task ADirectiveBesideABraceLeavesTheGapAloneInBothViews()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1:
							break;

			#if B
						case 2:
							break;
			#endif
					}
				}
			}
			""";

		foreach (string view in new[] { "", "#define B\n" })
		{
			AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveBracePaddingAnalyzer>(
				view + source,
				TestRuleSets.StandaloneRulesEnabled
			);

			Assert.IsEmpty(result.Diagnostics, $"view '{view.Trim()}'");
		}
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

	// Each bare block asks whether a unit stands before it, so a read of every unit of the file per block is quadratic.
	[TestMethod]
	public void TheCostGrowsLinearlyWithTheNumberOfBareBlocksInAFile()
	{
		static string Blocks(int count) => string.Concat(Enumerable.Repeat("{\n}\n", count));

		LinearCost.AssertLinear(
			Blocks(250),
			Blocks(1_000),

			static root =>
			{
				foreach (BlockSyntax block in root.DescendantNodes().OfType<BlockSyntax>())
					RemoveBracePaddingAnalyzer.IsStatementBlock(block);
			}
		);
	}

	[TestMethod]
	public async Task ACheckedBlockHasItsPaddingRemovedLikeAnOrdinaryBlock()
	{
		const string source = """
			class C
			{
				void M()
				{
					checked
					{

						DoThing();
					}

					unchecked
					{

					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					checked
					{
						DoThing();
					}

					unchecked
					{

					}
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
}
