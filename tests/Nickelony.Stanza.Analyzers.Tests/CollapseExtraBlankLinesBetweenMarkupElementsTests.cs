using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.MarkupLiterals;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class CollapseExtraBlankLinesBetweenMarkupElementsTests
{
	[TestMethod]
	public async Task AMarkupRunOfBlankLinesIsCapped()
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
			await RoslynTestHarness.RunAsync<CollapseExtraBlankLinesBetweenMarkupElementsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ASilencedTightRuleHandsItsRunBackToTheCap()
	{
		const string source = """"
			class C
			{
				void M()
				{
					#pragma warning disable ZA0102
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
					#pragma warning disable ZA0102
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
			await RoslynTestHarness.RunAsync<CollapseExtraBlankLinesBetweenMarkupElementsAnalyzer>(
				source,

				TestRuleSets.Only(
					DiagnosticDescriptors.CollapseExtraBlankLinesBetweenMarkupElements,
					DiagnosticDescriptors.KeepMultiLineMarkupElementsTogether
				)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AMarkupRunInsideAUtf8LiteralIsCappedToo()
	{
		const string source = """"
			class C
			{
				void M()
				{
					var markup = """
						<root>
						  <a>1</a>



						  <b>2</b>
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
						  <a>1</a>

						  <b>2</b>
						</root>
						"""u8;
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<CollapseExtraBlankLinesBetweenMarkupElementsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AMarkupRunIsMeasuredAgainstTheBudgetTheFileStates()
	{
		const string withinBudget = """"
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

		const string overBudget = """"
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

		IReadOnlyDictionary<string, string> allowsTwo = new Dictionary<string, string>
		{
			["stanza_markup_max_blank_lines"] = "2",
		};

		IReadOnlyDictionary<string, string> allowsNone = new Dictionary<string, string>
		{
			["stanza_markup_max_blank_lines"] = "0",
		};

		AnalyzerRunResult fitting =
			await RoslynTestHarness.RunAsync<CollapseExtraBlankLinesBetweenMarkupElementsAnalyzer>(
				withinBudget,
				TestRuleSets.StandaloneRulesEnabled,
				analyzerConfigOptions: allowsTwo
			);

		Assert.IsEmpty(fitting.Diagnostics);

		AnalyzerRunResult capped =
			await RoslynTestHarness.RunAsync<CollapseExtraBlankLinesBetweenMarkupElementsAnalyzer>(
				overBudget,
				TestRuleSets.StandaloneRulesEnabled,
				analyzerConfigOptions: allowsTwo
			);

		Assert.HasCount(1, capped.Diagnostics);
		RoslynTestHarness.AssertFixedText(withinBudget, capped);

		AnalyzerRunResult disabled =
			await RoslynTestHarness.RunAsync<CollapseExtraBlankLinesBetweenMarkupElementsAnalyzer>(
				overBudget,
				TestRuleSets.StandaloneRulesEnabled,
				analyzerConfigOptions: allowsNone
			);

		Assert.IsEmpty(disabled.Diagnostics);
	}

	[TestMethod]
	public async Task ANegativeBlankLineBudgetReadsAsTheDefaultOfOne()
	{
		const string twoBlankLines = """"
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

		const string oneBlankLine = """"
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

		IReadOnlyDictionary<string, string> negative = new Dictionary<string, string>
		{
			["stanza_markup_max_blank_lines"] = "-1",
		};

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<CollapseExtraBlankLinesBetweenMarkupElementsAnalyzer>(
				twoBlankLines,
				TestRuleSets.StandaloneRulesEnabled,
				analyzerConfigOptions: negative
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(oneBlankLine, result);
	}

	[TestMethod]
	public async Task AMarkupRunThatHoldsACommentIsLeftAlone()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a>1</a>

						  <!-- note -->

						  <b>2</b>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<CollapseExtraBlankLinesBetweenMarkupElementsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ALineThatHoldsNoBreakSpaceKeepsTheRunApart()
	{
		const string space = "\u00A0";

		string source = $$""""
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a>1</a>

						  {{space}}
						  <b>2</b>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<CollapseExtraBlankLinesBetweenMarkupElementsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}
}
