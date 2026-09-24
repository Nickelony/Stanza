using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.MarkupLiterals;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class SeparateMarkupCommentMarkerFromTextTests
{
	[TestMethod]
	public async Task AMarkupMarkerIsSeparatedOnlyWhereTheSpaceIsNotPreserved()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string preserved = """
						<root xml:space="preserve">
						  <!--a note-->
						</root>
						""";

					const string reopened = """
						<root xml:space="preserve">
						  <group xml:space="default">
						    <!--a note-->
						  </group>
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
					const string preserved = """
						<root xml:space="preserve">
						  <!--a note-->
						</root>
						""";

					const string reopened = """
						<root xml:space="preserve">
						  <group xml:space="default">
						    <!-- a note -->
						  </group>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMarkupCommentMarkerFromTextAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AMarkerBeforeANoBreakSpaceIsLeftAlone()
	{
		const string space = "\u00A0";

		string source = $$""""
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <!--{{space}}text -->
						  <a>1</a>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMarkupCommentMarkerFromTextAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AMarkupCommentMarkerIsSeparated()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <!--a note-->
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
						  <!-- a note -->
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMarkupCommentMarkerFromTextAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AMarkupCommentMarkerInsideAUtf8LiteralIsSeparated()
	{
		const string source = """"
			class C
			{
				void M()
				{
					var markup = """
						<root>
						  <!--a note-->
						</root>
						"""u8;
				}
			}
			"""";

		const string fixedSource = """"
			class C
			{
				void M()
				{
					var markup = """
						<root>
						  <!-- a note -->
						</root>
						"""u8;
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMarkupCommentMarkerFromTextAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AMarkupMarkerWithWhitespaceAfterItIsLeftAlone()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <!--  a padded note  -->
						  <!--
						    a note
						  -->
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMarkupCommentMarkerFromTextAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AMarkupCommentOutsideTheRootElementIsSeparated()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<!--a note-->
						<root/>
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
						<!-- a note -->
						<root/>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMarkupCommentMarkerFromTextAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
