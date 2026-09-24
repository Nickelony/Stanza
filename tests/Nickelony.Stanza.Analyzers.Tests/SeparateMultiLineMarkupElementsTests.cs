using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.MarkupLiterals;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class SeparateMultiLineMarkupElementsTests
{
	[TestMethod]
	public async Task MarkupInsideARawStringIsSeparated()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <child>
						    one
						  </child>
						  <child>
						    two
						  </child>
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
						  <child>
						    one
						  </child>

						  <child>
						    two
						  </child>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineMarkupElementsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task MarkupThatNeedsNoBlankLineIsLeftAlone()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string tight = """
						<root>
						  <a>1</a>
						  <b>2</b>
						</root>
						""";

					const string mixed = """
						<p>text <b>bold</b></p>
						""";

					const string preserved = """
						<root xml:space="preserve">
						  <a>
						    one
						  </a>
						  <b>
						    two
						  </b>
						</root>
						""";

					const string usage = """
						stanza-markup <command> [options] <path>
						""";

					const string singleLine = """<root><a>1</a></root>""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineMarkupElementsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACommentAboveAMarkupElementMovesWithIt()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <child>
						    one
						  </child>
						  <!-- the second child -->
						  <child>
						    two
						  </child>
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
						  <child>
						    one
						  </child>

						  <!-- the second child -->
						  <child>
						    two
						  </child>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineMarkupElementsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AWrappedLeafIsSeparatedUnlessTheScopeIsContainers()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<ItemGroup>
						  <PackageReference
						    Include="a" />
						  <PackageReference Include="b" />
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
						  <PackageReference
						    Include="a" />

						  <PackageReference Include="b" />
						</ItemGroup>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineMarkupElementsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);

		AnalyzerRunResult containersOnly =
			await RoslynTestHarness.RunAsync<SeparateMultiLineMarkupElementsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_markup_scope"] = "containers",
				}
			);

		Assert.IsEmpty(containersOnly.Diagnostics);
	}

	[TestMethod]
	public async Task AWhitespaceOnlyCDataInARawStringIsLeftAlone()
	{
		const string source = """"
			class C
			{
				string M() =>
					"""
					<root>
					  <a>
					    <b/>
					  </a><![CDATA[
					  ]]><c/>
					</root>
					""";
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineMarkupElementsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACommentOnThePreviousElementsOwnLineDoesNotShrinkTheGap()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <child>one</child><!-- note -->
						  <child>
						    two
						  </child>
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
						  <child>one</child><!-- note -->

						  <child>
						    two
						  </child>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineMarkupElementsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);

		Diagnostic diagnostic = result.Diagnostics[0];

		Assert.HasCount(1, diagnostic.AdditionalLocations);

		int gapLine = diagnostic.Location.GetLineSpan().StartLinePosition.Line;
		int previousLine = diagnostic.AdditionalLocations[0].GetLineSpan().StartLinePosition.Line;

		Assert.AreEqual(7, gapLine);
		Assert.AreEqual(6, previousLine);

		// An empty anchor span draws no squiggle, so the finding would be invisible.
		Assert.IsGreaterThan(
			0,
			diagnostic.Location.SourceSpan.Length,
			"the anchor has to cover the line it names."
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnInterpolatedRawStringIsNotInspected()
	{
		const string source = """"
			class C
			{
				void M(int value)
				{
					string markup = $"""
						<root>
						  <child>
						    {value}
						  </child>
						  <child>
						    two
						  </child>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineMarkupElementsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AFindingBelowASeparatorIsReportedOnItsFileLine()
	{
		const string separator = "\u2028";

		string source = $$""""
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a>
						    one{{separator}}
						  </a>
						  <b>three</b>
						</root>
						""";
				}
			}
			"""";

		string fixedSource = $$""""
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a>
						    one{{separator}}
						  </a>

						  <b>three</b>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineMarkupElementsAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.SeparateMultiLineMarkupElements)
			);

		Assert.HasCount(1, result.Diagnostics);

		Assert.AreEqual(
			10,
			result.DiagnosticLines[0],
			"the finding is reported one line above the element it asks a blank line before."
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
