using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Documentation;
using Nickelony.Stanza.Tests.Shared;

using static Nickelony.Stanza.Analyzers.Tests.DocumentationFamilyFixtures;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class RemoveDocumentationPaddingTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> PaddingOnly { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.RemoveDocumentationPadding);

	[TestMethod]
	public async Task APaddingLineAfterAStartTagIsRemoved()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				///
				/// Binds the options from the configuration section.
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
				/// Binds the options from the configuration section.
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDocumentationPaddingAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task APaddingLineBeforeAnEndTagIsRemoved()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				/// Binds the options from the configuration section.
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
				/// Binds the options from the configuration section.
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDocumentationPaddingAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task APaddingLineBetweenTwoElementsIsRemoved()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options from the configuration section.</summary>
				///
				/// <returns>The bound options.</returns>
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
			await RoslynTestHarness.RunAsync<RemoveDocumentationPaddingAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task APaddingLineBetweenAnExpandedSummaryAndReturnsIsRemoved()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				/// Binds the options from the configuration section.
				/// </summary>
				///
				/// <returns>
				/// The bound options.
				/// </returns>
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
				/// Binds the options from the configuration section.
				/// </summary>
				/// <returns>
				/// The bound options.
				/// </returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDocumentationPaddingAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABlankLineOnEitherSideOfATopLevelElementIsRemoved()
	{
		const string source = """
			internal sealed class Sample
			{
				/// A first thought.
				///
				/// <remarks>Binds the options from the configuration section.</remarks>
				///
				/// A second thought.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// A first thought.
				/// <remarks>Binds the options from the configuration section.</remarks>
				/// A second thought.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDocumentationPaddingAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABlankLineBesideARootInlineElementIsKept()
	{
		const string source = """
			internal sealed class Sample
			{
				/// Binds the options or <c>null</c>
				///
				/// when the section is missing.
				internal int First()
				{
					return 1;
				}

				/// The first thought.
				///
				/// <see langword="null"/> means the section is missing.
				internal int Second()
				{
					return 2;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDocumentationPaddingAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ARunOfPaddingLinesIsOneChange()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				///
				///
				///
				/// Binds the options from the configuration section.
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
				/// Binds the options from the configuration section.
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDocumentationPaddingAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task APlainCommentKeepsItsParagraphBreak()
	{
		const string source = """
			internal sealed class Sample
			{
				/// The first thought.
				///
				/// The second thought.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDocumentationPaddingAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ABlankFirstLineIsLeftToTheLayoutRule()
	{
		const string source = """
			internal sealed class Sample
			{
				///
				/// The first thought.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDocumentationPaddingAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	// The empty first line above everything the comment holds is the layout rule's, which writes the comment from its first line.
	[TestMethod]
	public async Task AnEmptyFirstLineAboveTheFirstElementIsLeftToTheLayoutRule()
	{
		const string source = """
			internal sealed class Sample
			{
				///
				/// <summary>Binds the options.</summary>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDocumentationPaddingAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACommentThatHoldsARealBreakIsHandedOver()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				///
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

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDocumentationPaddingAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	public static IEnumerable<object[]> ParagraphRuleStates { get; } =
	[
		[
			"the paragraph rule set to none",

			"""
				internal sealed class Sample
				{
					/// <summary>
					///
					/// The first thought.
					///
					/// The second thought.
					/// </summary>
					internal int Bind()
					{
						return 1;
					}
				}
				""",

			"""
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
				""",

			ParagraphRuleOff,
		],

		[
			"the paragraph rule off by default",

			"""
				internal sealed class Sample
				{
					/// <summary>
					///
					/// The first thought.
					///
					/// The second thought.
					/// </summary>
					internal int Bind()
					{
						return 1;
					}
				}
				""",

			"""
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
				""",

			PaddingOnly,
		],

		[
			"the paragraph rule silenced by a pragma",

			"""
				internal sealed class Sample
				{
				#pragma warning disable ZA0202
					/// <summary>
					///
					/// The first thought.
					///
					/// The second thought.
					/// </summary>
					internal int Bind()
					{
						return 1;
					}
				#pragma warning restore ZA0202
				}
				""",

			"""
				internal sealed class Sample
				{
				#pragma warning disable ZA0202
					/// <summary>
					/// The first thought.
					///
					/// The second thought.
					/// </summary>
					internal int Bind()
					{
						return 1;
					}
				#pragma warning restore ZA0202
				}
				""",

			TestRuleSets.StandaloneRulesEnabled,
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(ParagraphRuleStates),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ThePaddingIsReportedWhenTheParagraphRuleIsOff(
		string name,
		string source,
		string fixedSource,
		IReadOnlyDictionary<string, ReportDiagnostic> options
	)
	{
		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDocumentationPaddingAnalyzer>(source, options);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result, name);
	}

	[TestMethod]
	public async Task APragmaBetweenTheCommentAndTheMemberLeavesTheBreakToTheParagraphRule()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				///
				/// The first thought.
				///
				/// The second thought.
				/// </summary>
			#pragma warning disable ZA0202
				internal int Bind()
				{
					return 1;
				}
			#pragma warning restore ZA0202
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDocumentationPaddingAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(
			result.Diagnostics,
			"The paragraph rule owns the comment at the break inside it, which the pragma does not cover."
		);
	}

	[TestMethod]
	public async Task ACommentThatHoldsACodeBlockIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				///
				/// Binds the options from the configuration section.
				/// </summary>
				/// <code>
				/// int value = 1;
				///
				/// int other = 2;
				/// </code>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDocumentationPaddingAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	// A character reference is prose, so the blank line between two lines of them separates something and stays.
	[TestMethod]
	public async Task ABodyOfCharacterReferencesKeepsTheBlankLineBetweenThem()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				///
				/// &#32;
				///
				/// &#32;
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
				/// &#32;
				///
				/// &#32;
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDocumentationPaddingAnalyzer>(
				source,
				ParagraphRuleOff
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentThatHoldsOnlyACdataSectionIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				///
				/// <![CDATA[ ]]>
				///
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDocumentationPaddingAnalyzer>(
				source,
				ParagraphRuleOff
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACommentThatSharesItsLineWithCodeLosesItsPadding()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					return 1;
				} /// <summary>
				///
				/// Binds the options from the configuration section.
				/// </summary>
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					return 1;
				} /// <summary>
				/// Binds the options from the configuration section.
				/// </summary>
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<RemoveDocumentationPaddingAnalyzer>(
				source,
				ParagraphRuleOff
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task EveryPaddingRunGoesInOneFixAllPass()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				///
				/// Binds the options from the configuration section.
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
				/// Binds the options from the configuration section.
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunFixAllAsync(source, ParagraphRuleOff);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
