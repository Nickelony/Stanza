using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Documentation;

namespace Nickelony.Stanza.Analyzers.Tests.Documentation;

[TestClass]
public sealed class CapitalizeDocumentationTextTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> TextRuleOnly { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.CapitalizeDocumentationText);

	[TestMethod]
	public async Task ATextThatStartsWithALowercaseLetterIsCapitalized()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>binds the options from the configuration section.</summary>
				/// <returns>the bound options.</returns>
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
			await RoslynTestHarness.RunAsync<CapitalizeDocumentationTextAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheReportCoversTheLetterTheFixCapitalizes()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				/// binds the options.
				/// </summary>
				/// <returns>zero.</returns>
				internal int Bind() => 0;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<CapitalizeDocumentationTextAnalyzer>(
				source,
				TextRuleOnly
			);

		Assert.AreSequenceEqual(
			["b", "z"],

			[
				.. result.Diagnostics.Select(diagnostic =>
					SourceText.From(source).ToString(diagnostic.Location.SourceSpan)
				),
			]
		);
	}

	[TestMethod]
	public async Task ALetterOutsideAsciiIsCapitalized()
	{
		string source = $$"""
			internal sealed class Sample
			{
				/// <summary>{{(char)0xE9}}lan of the options.</summary>
				internal int Bind() => 1;
			}
			""";

		string fixedSource = $$"""
			internal sealed class Sample
			{
				/// <summary>{{(char)0xC9}}lan of the options.</summary>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<CapitalizeDocumentationTextAnalyzer>(
				source,
				TextRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// An element, a deliberate case, an address, a file name, a listed keyword or a mark keeps the start as written.
	[TestMethod]
	[DataRow("<see langword=\"true\"/> if found.")]
	[DataRow("<see cref=\"Sample\"/> reads it.")]
	[DataRow("<paramref name=\"value\"/> is read.")]
	[DataRow("<typeparamref name=\"T\"/> is read.")]
	[DataRow("<c>value</c> is read.")]
	[DataRow("iOS and macOS read it.")]
	[DataRow("eBay and jQuery read it.")]
	[DataRow("maxCount is read.")]
	[DataRow("https://github.com/Nickelony/Stanza is read.")]
	[DataRow("data/settings is read.")]
	[DataRow("appsettings.json is read.")]
	[DataRow("null when the section is missing.")]
	[DataRow("2 sections are read.")]
	[DataRow("\"quoted\" text is read.")]
	[DataRow("(bracketed) text is read.")]
	[DataRow("&lt;T&gt; is read.")]
	public async Task ATextWhoseStartKeepsItsCaseIsLeftAlone(string text)
	{
		string source = $$"""
			internal sealed class Sample
			{
				/// <summary>{{text}}</summary>
				internal int Bind<T>(int value) => value;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<CapitalizeDocumentationTextAnalyzer>(
				source,
				TextRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics, text);
	}

	[TestMethod]
	public async Task ALetterWithNoSingleUppercaseFormIsLeftAlone()
	{
		string source = $$"""
			internal sealed class Sample
			{
				/// <summary>{{(char)0xDF}} is read.</summary>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<CapitalizeDocumentationTextAnalyzer>(
				source,
				TextRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheKeywordKeyListsTheWordsTheRuleLeavesAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>true if the section is bound.</summary>
				internal bool Bind() => true;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>True if the section is bound.</summary>
				internal bool Bind() => true;
			}
			""";

		AnalyzerRunResult unlisted =
			await RoslynTestHarness.RunAsync<CapitalizeDocumentationTextAnalyzer>(
				source,
				TextRuleOnly
			);

		RoslynTestHarness.AssertFixedText(fixedSource, unlisted);

		AnalyzerRunResult listed =
			await RoslynTestHarness.RunAsync<CapitalizeDocumentationTextAnalyzer>(
				source,
				TextRuleOnly,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_doc_langwords"] = "null, true",
				}
			);

		Assert.IsEmpty(listed.Diagnostics);
	}

	[TestMethod]
	public async Task AnInlineElementACodeBlockAndAListAreNotJudged()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds <c>the options</c> here.</summary>
				/// <c>the section the host reads</c>
				/// <remarks>
				/// Reads them.
				/// <list type="bullet">
				/// <item><description>one item.</description></item>
				/// </list>
				/// </remarks>
				/// <code>
				/// int value = 1;
				/// </code>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<CapitalizeDocumentationTextAnalyzer>(
				source,
				TextRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnElementThatHoldsACodeSampleIsNotJudged()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>binds the options.</summary>
				/// <example>
				/// reads them.
				/// <code>
				/// int value = 1;
				/// </code>
				/// </example>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Binds the options.</summary>
				/// <example>
				/// reads them.
				/// <code>
				/// int value = 1;
				/// </code>
				/// </example>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<CapitalizeDocumentationTextAnalyzer>(
				source,
				TextRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentWithoutAnElementIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// binds the options from the configuration section.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<CapitalizeDocumentationTextAnalyzer>(
				source,
				TextRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task EachParagraphOfARemarksBlockIsJudged()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <remarks>
				/// <para>binds the options from the configuration section.</para>
				/// <para>validates them and reports every problem it found.</para>
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
			await RoslynTestHarness.RunAsync<CapitalizeDocumentationTextAnalyzer>(
				source,
				TextRuleOnly
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task EveryTextOfACommentIsCapitalizedInOnePass()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>binds the options from the configuration section.</summary>
				/// <returns>the bound options.</returns>
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
	public async Task ACapitalFixWorksWithoutTheAdditionalLocation()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>binds the options from the configuration section.</summary>
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
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<CapitalizeDocumentationTextAnalyzer>(
				source,
				TextRuleOnly
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
