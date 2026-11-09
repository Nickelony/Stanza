using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.MarkupLiterals;

namespace Nickelony.Stanza.Analyzers.Tests.MarkupLiterals;

[TestClass]
public sealed class SeparateCommentedMarkupElementTests
{
	[TestMethod]
	public async Task ACommentAboveASingleLineElementIsSeparatedFromTheElementBeforeIt()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a>1</a>
						  <!-- the second element -->
						  <b>2</b>
						</root>
						""";
				}
			}
			"""";

		const string fixedSource = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a>1</a>

						  <!-- the second element -->
						  <b>2</b>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateCommentedMarkupElementAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.SeparateCommentedMarkupElement)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AGapThatNeedsNoBlankLineIsLeftAlone()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string firstChild = """
						<root>
						  <!-- the first element -->
						  <a>1</a>
						</root>
						""";

					const string separated = """
						<root>
						  <a>1</a>

						  <!-- the second element -->
						  <b>2</b>
						</root>
						""";

					const string trailing = """
						<root>
						  <a>1</a> <!-- the first element -->
						  <b>2</b>
						</root>
						""";

					const string detached = """
						<root>
						  <a>1</a>
						  <!-- a section marker -->

						  <b>2</b>
						</root>
						""";

					const string sharedLine = """
						<root>
						  <a>1</a>
						  <!-- the second element --> <b>2</b>
						</root>
						""";

					const string plain = """
						<root>
						  <a>1</a>
						  <b>2</b>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateCommentedMarkupElementAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.SeparateCommentedMarkupElement)
			);

		Assert.IsEmpty(result.Diagnostics);
		RoslynTestHarness.AssertFixedText(source, result);
	}

	[TestMethod]
	public async Task AMultiLineNeighborStaysWithTheMultiLineRule()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string before = """
						<root>
						  <a>
						    1
						  </a>
						  <!-- the second element -->
						  <b>2</b>
						</root>
						""";

					const string after = """
						<root>
						  <a>1</a>
						  <!-- the second element -->
						  <b>
						    2
						  </b>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateCommentedMarkupElementAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.SeparateCommentedMarkupElement)
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AWrappedLeafIsLeftAloneUnderEitherScope()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<ItemGroup>
						  <PackageReference Include="a" />
						  <!-- the second package -->
						  <PackageReference
						    Include="b" />
						</ItemGroup>
						""";
				}
			}
			"""";

		foreach (string scope in new[] { "all", "containers" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<SeparateCommentedMarkupElementAnalyzer>(
					source,
					TestRuleSets.Only(DiagnosticDescriptors.SeparateCommentedMarkupElement),

					analyzerConfigOptions: new Dictionary<string, string>
					{
						["stanza_markup_scope"] = scope,
					}
				);

			Assert.IsEmpty(result.Diagnostics, $"the rule reports under the scope '{scope}'.");
		}
	}

	[TestMethod]
	public async Task ACommentOnItsOwnLineBelowATrailingCommentStillFires()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a>1</a> <!-- about the first element -->
						  <!-- the second element -->
						  <b>2</b>
						</root>
						""";
				}
			}
			"""";

		const string fixedSource = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a>1</a> <!-- about the first element -->

						  <!-- the second element -->
						  <b>2</b>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateCommentedMarkupElementAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.SeparateCommentedMarkupElement)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARunOfCommentsAndAnInstructionGetsOneBlankLineAboveItsFirstLine()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a>1</a>
						  <!-- the second element -->
						  <?pi target?>
						  <b>2</b>
						</root>
						""";
				}
			}
			"""";

		const string fixedSource = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a>1</a>

						  <!-- the second element -->
						  <?pi target?>
						  <b>2</b>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateCommentedMarkupElementAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.SeparateCommentedMarkupElement)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task APragmaOverTheLiteralKeepsItsGapAsWritten()
	{
		const string source = """"
			class C
			{
				void M()
				{
					#pragma warning disable ZA2004
					const string markup = """
						<root>
						  <a>1</a>
						  <!-- the second element -->
						  <b>2</b>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateCommentedMarkupElementAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.SeparateCommentedMarkupElement)
			);

		Assert.IsEmpty(result.Diagnostics);
		RoslynTestHarness.AssertFixedText(source, result);
	}

	[TestMethod]
	public async Task TheSingleLineTightRuleRemovesTheBlankLineAboveACommentWhereThisRuleIsOn()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a>1</a>

						  <!-- the second element -->
						  <b>2</b>
						  <c>3</c>

						  <d>4</d>
						</root>
						""";
				}
			}
			"""";

		const string fixedSource = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a>1</a>
						  <!-- the second element -->
						  <b>2</b>
						  <c>3</c>
						  <d>4</d>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult both = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.KeepSingleLineMarkupElementsTogether,
				DiagnosticDescriptors.SeparateCommentedMarkupElement
			)
		);

		Assert.HasCount(2, both.Diagnostics);

		Assert.IsEmpty(
			both.Diagnostics.Where(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.SeparateCommentedMarkupElement.Id
			)
		);

		RoslynTestHarness.AssertFixedText(fixedSource, both);

		AnalyzerRunResult alone = await RoslynTestHarness.RunAllAsync(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.KeepSingleLineMarkupElementsTogether)
		);

		Assert.HasCount(2, alone.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, alone);
	}

	[TestMethod]
	public async Task ASilencedCommentedRuleLeavesTheSingleLineTightRuleWorking()
	{
		const string source = """"
			class C
			{
				void M()
				{
					#pragma warning disable ZA2004
					const string markup = """
						<root>
						  <a>1</a>

						  <!-- the second element -->
						  <b>2</b>
						</root>
						""";
				}
			}
			"""";

		const string fixedSource = """"
			class C
			{
				void M()
				{
					#pragma warning disable ZA2004
					const string markup = """
						<root>
						  <a>1</a>
						  <!-- the second element -->
						  <b>2</b>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.KeepSingleLineMarkupElementsTogether,
				DiagnosticDescriptors.SeparateCommentedMarkupElement
			)
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ASilencedSingleLineTightRuleHandsItsBlankLineBackToThisRule()
	{
		const string source = """"
			class C
			{
				void M()
				{
					#pragma warning disable ZA2003
					const string markup = """
						<root>
						  <a>1</a>
						  <!-- the second element -->
						  <b>2</b>
						</root>
						""";
				}
			}
			"""";

		const string fixedSource = """"
			class C
			{
				void M()
				{
					#pragma warning disable ZA2003
					const string markup = """
						<root>
						  <a>1</a>

						  <!-- the second element -->
						  <b>2</b>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.KeepSingleLineMarkupElementsTogether,
				DiagnosticDescriptors.SeparateCommentedMarkupElement
			)
		);

		Assert.HasCount(1, result.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.SeparateCommentedMarkupElement.Id,
			result.Diagnostics[0].Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheCapCutsARunAboveACommentBackToTheCapWhereOnlyThisRuleIsOn()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a>1</a>



						  <!-- the second element -->
						  <b>2</b>
						</root>
						""";
				}
			}
			"""";

		const string fixedSource = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a>1</a>

						  <!-- the second element -->
						  <b>2</b>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.CollapseExtraBlankLinesBetweenMarkupElements,
				DiagnosticDescriptors.SeparateCommentedMarkupElement
			)
		);

		Assert.HasCount(1, result.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.CollapseExtraBlankLinesBetweenMarkupElements.Id,
			result.Diagnostics[0].Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task EveryFixOrderSettlesWithAllThreeMarkupSpacingRulesOn()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a>1</a>
						  <!-- the second element -->
						  <b>2</b>

						  <c>3</c>


						  <!-- the fourth element -->
						  <d>4</d>
						  <e>5</e>
						</root>
						""";
				}
			}
			"""";

		const string fixedSource = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a>1</a>
						  <!-- the second element -->
						  <b>2</b>
						  <c>3</c>
						  <!-- the fourth element -->
						  <d>4</d>
						  <e>5</e>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.CollapseExtraBlankLinesBetweenMarkupElements,
				DiagnosticDescriptors.KeepSingleLineMarkupElementsTogether,
				DiagnosticDescriptors.SeparateCommentedMarkupElement
			)
		);

		RoslynTestHarness.AssertEachGapIsClaimedOnce(result);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
