using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Documentation;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class MarkDocumentationParagraphsTests
{
	// The marker has closed its paragraph already, so the blank line after it is a break the tool's reprint drops too.
	[TestMethod]
	public async Task ABlankLineAfterAParagraphMarkerIsAParagraphBreak()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				/// <para>The first thought.</para>
				///
				/// The second thought.
				/// </summary>
				internal int Bind()
				{
					return 1;
				}

				/// <summary>
				/// <para>The first thought.</para>
				///
				/// <para>The second thought.</para>
				/// </summary>
				internal int Bound()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>
				/// <para>
				/// The first thought.
				/// </para>
				/// <para>
				/// The second thought.
				/// </para>
				/// </summary>
				internal int Bind()
				{
					return 1;
				}

				/// <summary>
				/// <para>
				/// The first thought.
				/// </para>
				/// <para>
				/// The second thought.
				/// </para>
				/// </summary>
				internal int Bound()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<MarkDocumentationParagraphsAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.MarkDocumentationParagraphs)
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABlankLineInsideAnElementBecomesParagraphs()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				/// The first thought.
				///
				/// The second thought.
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>
				/// <para>
				/// The first thought.
				/// </para>
				/// <para>
				/// The second thought.
				/// </para>
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<MarkDocumentationParagraphsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheTextOfEachParagraphIsLaidOutToo()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				/// Binds the options from the configuration section.
				///
				/// Validates them, lets the host fail at startup rather than at the first request it serves, and reports every problem it found.
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>
				/// <para>
				/// Binds the options from the configuration section.
				/// </para>
				/// <para>
				/// Validates them, lets the host fail at startup rather than at the first request it serves,
				/// and reports every problem it found.
				/// </para>
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<MarkDocumentationParagraphsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AParagraphElementThatHoldsABlankLineIsSplit()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <remarks>
				/// <para>
				/// The first thought.
				///
				/// The second thought.
				/// </para>
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
				/// <para>
				/// The first thought.
				/// </para>
				/// <para>
				/// The second thought.
				/// </para>
				/// </remarks>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<MarkDocumentationParagraphsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentWithoutABlankLineIsNotReported()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options from the configuration section and validates them before the host is built.</summary>
				/// <returns>The bound options.</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<MarkDocumentationParagraphsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics, "A comment without a paragraph break was reported.");
	}

	[TestMethod]
	public async Task AStrayBlankLineAfterTheStartTagIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				///
				/// The first thought.
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<MarkDocumentationParagraphsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(
			result.Diagnostics,
			"A blank line that separates no paragraphs was reported."
		);
	}

	// The blank line before the end tag separates nothing, but the one between the paragraphs is still a break.
	[TestMethod]
	public async Task ARealBreakIsReportedThoughABlankLineTrailsTheText()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				/// First paragraph.
				///
				/// Second paragraph.
				///
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>
				/// <para>
				/// First paragraph.
				/// </para>
				/// <para>
				/// Second paragraph.
				/// </para>
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<MarkDocumentationParagraphsAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.MarkDocumentationParagraphs)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AStrayBlankLineBeforeTheEndTagIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				/// The first thought.
				///
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<MarkDocumentationParagraphsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(
			result.Diagnostics,
			"A blank line that separates no paragraphs was reported."
		);
	}

	[TestMethod]
	public async Task ABreakInsideACodeBlockIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				/// Binds the options from the configuration section.
				/// <code>
				/// var options = Bind(section, environment);
				///
				/// var again = Bind(section, environment);
				/// </code>
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<MarkDocumentationParagraphsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics, "A break inside a code block was reported.");
	}

	[TestMethod]
	public async Task ABlankLineInsideAPhraseElementIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the value.</summary>
				/// <c>the first thought
				///
				/// and the second one</c>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<MarkDocumentationParagraphsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics, "A phrase element is never split into paragraphs.");
	}

	[TestMethod]
	public async Task AParagraphElementKeepsTheTagTheAuthorWrote()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the value.</summary>
				/// <para class="note">
				/// The first thought.
				///
				/// The second thought.
				/// </para>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Binds the value.</summary>
				/// <para class="note">
				/// The first thought.
				/// </para>
				/// <para>
				/// The second thought.
				/// </para>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<MarkDocumentationParagraphsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ANestedParagraphMarkerKeepsTheTagTheAuthorWrote()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the value.</summary>
				/// <remarks>
				/// <para class="note">
				/// The first thought.
				///
				/// The second thought.
				/// </para>
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
				/// <summary>Binds the value.</summary>
				/// <remarks>
				/// <para class="note">
				/// The first thought.
				/// </para>
				/// <para>
				/// The second thought.
				/// </para>
				/// </remarks>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<MarkDocumentationParagraphsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task APrefixedParagraphMarkerKeepsItsPrefix()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the value.</summary>
				/// <x:para xmlns:x="urn:stanza">
				/// The first thought.
				///
				/// The second thought.
				/// </x:para>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Binds the value.</summary>
				/// <x:para xmlns:x="urn:stanza">
				/// The first thought.
				/// </x:para>
				/// <para>
				/// The second thought.
				/// </para>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<MarkDocumentationParagraphsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// Roslyn ends a line at U+2028, U+2029 and U+0085, while the paragraph rule reads only `\r` and `\n`,
	// so a rewrite would join the member to the comment.
	[TestMethod]
	[DataRow(0x2028)]
	[DataRow(0x2029)]
	[DataRow(0x85)]
	public async Task ACommentThatEndsALineAtAnotherBreakIsLeftAlone(int codePoint)
	{
		string source = """
			internal sealed class Sample
			{
				/// <summary>
				/// The first thought.
				///
				/// The second thought.
				/// </summary>{break}internal int Bind() => 1;
			}
			""".Replace("{break}", (char)codePoint + "\t", StringComparison.Ordinal);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<MarkDocumentationParagraphsAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.MarkDocumentationParagraphs)
			);

		Assert.IsEmpty(result.Diagnostics);
	}
}
