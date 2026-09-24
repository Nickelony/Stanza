using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Documentation;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class EndDocumentationTextWithPeriodTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> TextRuleOnly { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.EndDocumentationTextWithPeriod);

	[TestMethod]
	[DataRow("See https://github.com/Nickelony/Stanza")]
	[DataRow("Reads the file at data/settings")]
	public async Task ATextThatStopsAtALinkOrAPathGetsNoPeriod(string text)
	{
		string source = $$"""
			internal sealed class Sample
			{
				/// <summary>{{text}}</summary>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<EndDocumentationTextWithPeriodAnalyzer>(
				source,
				TextRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ATextThatStopsAtAWordIsGivenAPeriod()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options from the configuration section</summary>
				/// <returns>The bound options</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Binds the options from the configuration section.</summary>
				/// <returns>The bound options.</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<EndDocumentationTextWithPeriodAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheReportCoversTheCharacterTheMissingPeriodFollows()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options</summary>
				/// <returns>The bound options or <see langword="null"/></returns>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<EndDocumentationTextWithPeriodAnalyzer>(
				source,
				TextRuleOnly
			);

		Assert.AreSequenceEqual(
			["s", ">"],

			[
				.. result.Diagnostics.Select(diagnostic =>
					SourceText.From(source).ToString(diagnostic.Location.SourceSpan)
				),
			]
		);
	}

	[TestMethod]
	public async Task AMarkAfterTheLastWordClosesTheTextAlready()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Was the section bound?</summary>
				/// <returns>The bound options (the ones the host reads)</returns>
				/// <param name="value">The value, which the host reads on startup;</param>
				internal int Bind(int value)
				{
					return value;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<EndDocumentationTextWithPeriodAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ATextThatEndsOnAnElementIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options of <see cref="DefaultConfigurationSectionOptions"/></summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<EndDocumentationTextWithPeriodAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	// An entity ends on its semicolon, a mark, and a CDATA section is text the comment walk does not read.
	[TestMethod]
	[DataRow("Compares the value with &lt;")]
	[DataRow("Compares the value with <![CDATA[x < y]]>")]
	public async Task ATextThatEndsOnAnEntityOrACDataSectionIsLeftAlone(string text)
	{
		string source = $$"""
			internal sealed class Sample
			{
				/// <summary>{{text}}</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<EndDocumentationTextWithPeriodAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AKeywordElementThatEndsTheTextTakesThePeriod()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Returns <see langword="null"/></summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Returns <see langword="null"/>.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<EndDocumentationTextWithPeriodAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnInlineElementIsNotASentence()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options of <c>the configuration section</c></summary>
				/// <c>the section the host reads</c>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<EndDocumentationTextWithPeriodAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACodeBlockIsNotASentence()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options from the configuration section.</summary>
				/// <code>
				/// int value = 1
				/// </code>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<EndDocumentationTextWithPeriodAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheProseBesideACodeSampleIsStillJudged()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options from the configuration section</summary>
				/// <example>
				/// <code>
				/// int value = 1
				/// </code>
				/// </example>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<EndDocumentationTextWithPeriodAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Binds the options from the configuration section.</summary>
				/// <example>
				/// <code>
				/// int value = 1
				/// </code>
				/// </example>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentWithoutAnElementIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// Binds the options from the configuration section
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<EndDocumentationTextWithPeriodAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheParagraphOfARemarksBlockIsJudged()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <remarks>
				/// <para>Binds the options from the configuration section</para>
				/// <para>Validates them and reports every problem it found.</para>
				/// </remarks>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <remarks>
				/// <para>Binds the options from the configuration section.</para>
				/// <para>Validates them and reports every problem it found.</para>
				/// </remarks>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<EndDocumentationTextWithPeriodAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task EveryStoppingTextOfACommentIsFixedInOnePass()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options from the configuration section</summary>
				/// <returns>The bound options</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Binds the options from the configuration section.</summary>
				/// <returns>The bound options.</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunFixAllAsync(source, TextRuleOnly);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ADocumentationTextEndFixWorksWithoutTheAdditionalLocation()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options from the configuration section</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Binds the options from the configuration section.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<EndDocumentationTextWithPeriodAnalyzer>(
				source,
				TextRuleOnly
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentThatSharesItsLineWithCodeIsGivenItsPeriod()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					return 1;
				} /// <summary>Binds the options from the configuration section</summary>
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					return 1;
				} /// <summary>Binds the options from the configuration section.</summary>
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<EndDocumentationTextWithPeriodAnalyzer>(
				source,
				TextRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
