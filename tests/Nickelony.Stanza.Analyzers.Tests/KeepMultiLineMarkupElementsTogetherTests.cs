using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.MarkupLiterals;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class KeepMultiLineMarkupElementsTogetherTests
{
	[TestMethod]
	public async Task AnEnabledTightRuleTakesARunOfAnySize()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a>
						    1
						  </a>



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
						  <a>
						    1
						  </a>
						  <b>2</b>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepMultiLineMarkupElementsTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepMultiLineMarkupElementsTogether)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABlankLineBetweenTwoMultiLineElementsIsTakenAway()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a>
						    one
						  </a>

						  <b>
						    two
						  </b>
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
						  <a>
						    one
						  </a>
						  <b>
						    two
						  </b>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepMultiLineMarkupElementsTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepMultiLineMarkupElementsTogether)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AFileEnablingBothHalvesLeavesTheGapToTheTightOne()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a>
						    one
						  </a>
						  <b>
						    two
						  </b>

						  <c>
						    three
						  </c>
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
						  <a>
						    one
						  </a>
						  <b>
						    two
						  </b>
						  <c>
						    three
						  </c>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.SeparateMultiLineMarkupElements,
				DiagnosticDescriptors.KeepMultiLineMarkupElementsTogether
			)
		);

		Assert.HasCount(1, result.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.KeepMultiLineMarkupElementsTogether.Id,
			result.Diagnostics[0].Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ElementsThatAlreadyStandTogetherAreLeftAlone()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a>
						    1
						  </a>
						  <b>2</b>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepMultiLineMarkupElementsTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepMultiLineMarkupElementsTogether)
			);

		Assert.IsEmpty(result.Diagnostics);
	}
}
