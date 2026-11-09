using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.MarkupLiterals;

namespace Nickelony.Stanza.Analyzers.Tests.MarkupLiterals;

[TestClass]
public sealed class RemoveMarkupElementPaddingTests
{
	[TestMethod]
	public async Task ABlankLineAfterTheStartTagAndBeforeTheEndTagIsRemoved()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<ItemGroup>

						  <Compile Include="a.cs" />

						</ItemGroup>
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
						<ItemGroup>
						  <Compile Include="a.cs" />
						</ItemGroup>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveMarkupElementPaddingAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.RemoveMarkupElementPadding)
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task EveryBlankLineOfARunGoesAndANestedElementIsLaidOutToo()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<Project>


						  <PropertyGroup>

						    <A>1</A>

						  </PropertyGroup>
						</Project>
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
						<Project>
						  <PropertyGroup>
						    <A>1</A>
						  </PropertyGroup>
						</Project>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveMarkupElementPaddingAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.RemoveMarkupElementPadding)
			);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentAtTheEdgeIsKeptWithItsText()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>

						  <!-- first -->
						  <a />
						  <!-- last -->

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
						  <!-- first -->
						  <a />
						  <!-- last -->
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveMarkupElementPaddingAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.RemoveMarkupElementPadding)
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnElementOfACommentAloneLosesItsPadding()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a>

						    <!-- only -->

						  </a>
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
						    <!-- only -->
						  </a>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveMarkupElementPaddingAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.RemoveMarkupElementPadding)
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task MixedContentAndPreservedSpaceStayAsWritten()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string mixed = """
						<p>

						  text <b />

						</p>
						""";

					const string preserved = """
						<pre xml:space="preserve">

						  <b />

						</pre>
						""";

					const string cdata = """
						<root>

						  <![CDATA[raw]]>
						  <b />

						</root>
						""";

					const string tight = """
						<root>
						  <b />
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveMarkupElementPaddingAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.RemoveMarkupElementPadding)
			);

		Assert.IsEmpty(result.Diagnostics);
		RoslynTestHarness.AssertFixedText(source, result);
	}

	[TestMethod]
	public async Task APragmaOverTheLiteralKeepsItsPadding()
	{
		const string source = """"
			class C
			{
				void M()
				{
					#pragma warning disable ZA2005
					const string markup = """
						<root>

						  <a />

						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveMarkupElementPaddingAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.RemoveMarkupElementPadding)
			);

		Assert.IsEmpty(result.Diagnostics);
		RoslynTestHarness.AssertFixedText(source, result);
	}

	[TestMethod]
	public async Task ABlankLineBetweenTwoElementsIsLeftToTheGapRules()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a />

						  <b />
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveMarkupElementPaddingAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.RemoveMarkupElementPadding)
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task EveryFixOrderSettlesWithTheGapRulesOn()
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


						  <c>3</c>

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
						  <c>3</c>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.RemoveMarkupElementPadding,
				DiagnosticDescriptors.CollapseExtraBlankLinesBetweenMarkupElements,
				DiagnosticDescriptors.KeepSingleLineMarkupElementsTogether
			)
		);

		RoslynTestHarness.AssertEachGapIsClaimedOnce(result);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
