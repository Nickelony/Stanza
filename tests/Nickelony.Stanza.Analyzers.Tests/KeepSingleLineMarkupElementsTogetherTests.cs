using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.MarkupLiterals;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class KeepSingleLineMarkupElementsTogetherTests
{
	[TestMethod]
	public async Task ABlankLineBetweenTwoSingleLineElementsIsTakenAway()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a>1</a>

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
						  <b>2</b>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepSingleLineMarkupElementsTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepSingleLineMarkupElementsTogether)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task SingleLineElementsWithNoBlankLineBetweenThemAndAMultiLineGapAreLeftAlone()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a>1</a>
						  <b>2</b>

						  <c>
						    3
						  </c>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepSingleLineMarkupElementsTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepSingleLineMarkupElementsTogether)
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task APragmaOverTheLiteralKeepsItsBlankLine()
	{
		const string source = """"
			class C
			{
				void M()
				{
					#pragma warning disable ZA0103
					const string markup = """
						<root>
						  <a>1</a>

						  <b>2</b>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepSingleLineMarkupElementsTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepSingleLineMarkupElementsTogether)
			);

		Assert.IsEmpty(result.Diagnostics);
		RoslynTestHarness.AssertFixedText(source, result);
	}

	[TestMethod]
	public async Task TheTwoKeepRulesTogetherEachClaimTheirOwnGaps()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a>1</a>

						  <b>2</b>

						  <c>
						    3
						  </c>

						  <d>
						    4
						  </d>
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
						  <b>2</b>
						  <c>
						    3
						  </c>
						  <d>
						    4
						  </d>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.KeepSingleLineMarkupElementsTogether,
				DiagnosticDescriptors.KeepMultiLineMarkupElementsTogether
			)
		);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertEachGapIsClaimedOnce(result);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
