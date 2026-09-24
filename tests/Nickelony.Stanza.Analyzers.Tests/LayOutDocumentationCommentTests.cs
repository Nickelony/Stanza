using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Documentation;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed partial class LayOutDocumentationCommentTests
{
	// Laying the comment out would leave the code on the closer's line, so a comment that shares that line is left as written.
	[TestMethod]
	public async Task ADelimitedCommentWhoseCloserSharesItsLineWithCodeIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/** <summary>Binds the options</summary> */ internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.LayOutDocumentationComment)
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnEmptyFirstLineIsDropped()
	{
		const string source = """
			internal sealed class Sample
			{
				///
				/// <summary>Binds the options.</summary>
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Binds the options.</summary>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.LayOutDocumentationComment)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// A form feed is character data rather than layout, so the layout keeps it and the words it joins.
	[TestMethod]
	public async Task AFormFeedAfterAnElementIsKeptAsText()
	{
		const string source =
			"internal sealed class Sample\n{\n\t/// <summary>Binds <c>x</c>\fthe options from the configuration section, validates them and lets the host fail at startup.</summary>\n\tinternal int Bind() => 1;\n}\n";

		const string fixedSource =
			"internal sealed class Sample\n{\n\t/// <summary>\n\t/// Binds <c>x</c>\fthe options from the configuration section, validates them and lets the host\n\t/// fail at startup.\n\t/// </summary>\n\tinternal int Bind() => 1;\n}\n";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.LayOutDocumentationComment)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// An attribute or a run that leaves the empty element rule out never writes the shorter tag, so the layout cannot wait for it.
	[TestMethod]
	public async Task ACommentWhoseEmptyElementRuleIsSuppressedIsStillLaidOut()
	{
		const string source = """
			using System.Diagnostics.CodeAnalysis;

			internal sealed class Sample
			{
				/// <summary>Binds the <c></c> options, validates them and lets the host fail at startup rather than at the first request.</summary>
				[SuppressMessage("Formatting", "ZA0209")]
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,

				TestRuleSets.Only(
					DiagnosticDescriptors.LayOutDocumentationComment,
					DiagnosticDescriptors.WriteEmptyElementAsSelfClosingTag
				)
			);

		const string fixedSource = """
			using System.Diagnostics.CodeAnalysis;

			internal sealed class Sample
			{
				/// <summary>
				/// Binds the <c></c> options, validates them and lets the host fail at startup rather than at
				/// the first request.
				/// </summary>
				[SuppressMessage("Formatting", "ZA0209")]
				internal int Bind() => 1;
			}
			""";

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnElementThatDoesNotFitBecomesABlockAndAShortOneStaysOnItsLine()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options from the configuration section, validates them and lets the host fail at startup rather than at the first request.</summary>
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
				/// <summary>
				/// Binds the options from the configuration section, validates them and lets the host fail at
				/// startup rather than at the first request.
				/// </summary>
				/// <returns>The bound options.</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheTextAfterAParagraphMarkerOpensAParagraphOfItsOwn()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options.</summary>
				/// <remarks>
				/// <para>First.</para>
				/// Second.
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
				/// <summary>Binds the options.</summary>
				/// <remarks>
				/// <para>
				/// First.
				/// </para>
				/// <para>
				/// Second.
				/// </para>
				/// </remarks>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnElementWithAnEndTagAndNoContentIsWrittenAsItStands()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options from the configuration section, validates them and lets the host fail at startup rather than at the first request.</summary>
				/// <param name="x"></param>
				/// <param name="y">
				/// </param>
				internal int Bind(int x, int y)
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>
				/// Binds the options from the configuration section, validates them and lets the host fail at
				/// startup rather than at the first request.
				/// </summary>
				/// <param name="x"></param>
				/// <param name="y"></param>
				internal int Bind(int x, int y)
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.LayOutDocumentationComment)
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnElementThatPreservesItsSpaceKeepsTheCommentAsItIs()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options.</summary>
				/// <remarks>Keeps <custom xml:space="preserve">the   options</custom>.</remarks>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AListBetweenTwoProseRunsIsNotABlankLine()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <remarks>Alpha.
				///<list type="bullet"><item><description>Two.</description></item></list>
				///Beta.</remarks>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <remarks>
				/// <para>
				/// Alpha.
				/// </para>
				/// <list type="bullet">
				/// <item><description>Two.</description></item>
				/// </list>
				/// <para>
				/// Beta.
				/// </para>
				/// </remarks>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		Assert.IsNotEmpty(result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheCommentReportedOnIsLaidOutRatherThanTheOneAfterIt()
	{
		const string source = """
			internal sealed class Sample
			{
				/** <summary>Binds the options from the configuration section, validates them and lets the host fail at startup rather than at the first request.</summary> */
				/**
				 * Keeps the options.
				 */
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		// Two comments document the same member, and only the first one is over the width,
		// so the fix has to reach the comment its own diagnostic names.
		const string fixedSource = """
			internal sealed class Sample
			{
				/**
				 * <summary>
				 * Binds the options from the configuration section, validates them and lets the host fail at
				 * startup rather than at the first request.
				 * </summary>
				 */
				/**
				 * Keeps the options.
				 */
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
	public async Task APunctuationMarkWrittenAfterAnElementStaysGluedToIt()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Reads <c>null</c>, ignores <see langword="null"/>.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(
			result.Diagnostics,
			"the punctuation is already glued to the element, so nothing is reported."
		);
	}

	[TestMethod]
	public async Task AWordGluedToTheElementAfterItIsNotLeftAtALineEnd()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Separates a run of two or more declarations that share the exact same type; the type is compared by the text of its tokens, and a <c>const</c> declaration never joins a run of non-<c>const</c> ones.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>
				/// Separates a run of two or more declarations that share the exact same type; the type
				/// is compared by the text of its tokens, and a <c>const</c> declaration never joins a run
				/// of non-<c>const</c> ones.
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		// A break after `non-` would read as a space inside the word,
		// so `non-` stays on the line of the element it prefixes.
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ASpaceWrittenBeforeAClosingMarkIsTakenAway()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Reads <c>null</c> , and writes <see langword="null"/> .</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Reads <c>null</c>, and writes <see langword="null"/>.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// A mark written apart from its word inside a line joins it as one at a line start does,
	// so no line starts with the mark and a second layout finds nothing to join.
	[TestMethod]
	public async Task ASpaceBeforeAClosingMarkInsideALineIsTakenAwayToo()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>when empty ? returns otherwise the otherwise value</summary>
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
				/// when empty?
				/// returns
				/// otherwise the
				/// otherwise value
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.LayOutDocumentationComment),

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_doc_max_line_length"] = "20",
				}
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// A word made of marks joins the word before it, so a mark the prose names goes in a code span, which is one word.
	[TestMethod]
	public async Task AMarkNamedInACodeSpanKeepsItsSpace()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Reads the `;` mark and then the `?` mark.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ADotFileNamedInProseKeepsTheSpaceBeforeIt()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>A throw-away working directory that is its own
				/// .editorconfig root.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>A throw-away working directory that is its own .editorconfig root.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	public static IEnumerable<object?[]> LineWordLimits { get; } =
	[
		[
			"the default limit",
			null,

			"""
				internal sealed class Sample
				{
					/// <returns>Binds the options from the configuration section and validates them before the host is built.</returns>
					internal int Bind()
					{
						return 1;
					}
				}
				""",

			"""
				internal sealed class Sample
				{
					/// <returns>
					/// Binds the options from the configuration section and validates them before the host
					/// is built.
					/// </returns>
					internal int Bind()
					{
						return 1;
					}
				}
				""",
		],

		[
			"a limit of zero",
			new Dictionary<string, string> { ["stanza_doc_min_line_words"] = "0" },

			"""
				internal sealed class Sample
				{
					/// <returns>Binds the options from the configuration section and validates them before the host is built.</returns>
					internal int Bind()
					{
						return 1;
					}
				}
				""",

			"""
				internal sealed class Sample
				{
					/// <returns>
					/// Binds the options from the configuration section and validates them before the host is
					/// built.
					/// </returns>
					internal int Bind()
					{
						return 1;
					}
				}
				""",
		],

		[
			"thresholds that are not counts",

			new Dictionary<string, string>
			{
				["stanza_doc_min_tail_words"] = "two",
				["stanza_doc_min_line_words"] = "-1",
				["stanza_doc_indent_size"] = "two",
			},

			"""
				internal sealed class Sample
				{
					/// <returns>Binds the options from the configuration section and validates them before the host is built.</returns>
					internal int Bind()
					{
						return 1;
					}
				}
				""",

			"""
				internal sealed class Sample
				{
					/// <returns>
					/// Binds the options from the configuration section and validates them before the host
					/// is built.
					/// </returns>
					internal int Bind()
					{
						return 1;
					}
				}
				""",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(LineWordLimits),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task TheLineWordLimitDecidesWhetherARunMayEndOnOneWord(
		string name,
		IReadOnlyDictionary<string, string>? options,
		string source,
		string fixedSource
	)
	{
		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,
				analyzerConfigOptions: options
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result, name);
	}

	[TestMethod]
	public async Task AnElementInsideTheTextIsNeverBrokenUp()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <returns><see langword="null"/> when the section is missing, otherwise the bound options, which are also validated before they are handed out.</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <returns>
				/// <see langword="null"/> when the section is missing, otherwise the bound options, which are
				/// also validated before they are handed out.
				/// </returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnAttributeValueKeepsItsOwnWhitespace()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <returns><see langword="null  "/> when the section is missing, otherwise the bound options, which are also validated before they are handed out.</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <returns>
				/// <see langword="null  "/> when the section is missing, otherwise the bound options, which are
				/// also validated before they are handed out.
				/// </returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnAttributeValueKeepsItsWhitespaceBesideASpacedEquals()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <returns><see cref  =  "ab  cd"/> when the section is missing, otherwise the bound options, which are also validated before they are handed out.</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <returns>
				/// <see cref = "ab  cd"/> when the section is missing, otherwise the bound options, which are
				/// also validated before they are handed out.
				/// </returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ALineBreakInsideATagIsNotPartOfItsText()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <remarks>Binds <c>the
				/// options</c> from the section.</remarks>
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
				/// Binds <c>the options</c> from
				/// the section.
				/// </remarks>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "46",
				}
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// A code span is code to the null rule, so the fill moves it whole and keeps the spaces inside it.
	[TestMethod]
	[DataRow(
		"Returns the value of `options.Section  ?? fallback` when the section is missing.",
		"\t/// Returns the value\n\t/// of `options.Section  ?? fallback` when\n\t/// the section is missing."
	)]
	[DataRow(
		"Returns the value of `options.Section\n\t/// ?? fallback` when the section is missing.",
		"\t/// Returns the value of\n\t/// `options.Section ?? fallback` when the\n\t/// section is missing."
	)]
	public async Task ACodeSpanIsFilledAsOneWord(string text, string fixedLines)
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

		string fixedSource =
			"internal sealed class Sample\n{\n\t/// <summary>\n"
			+ fixedLines
			+ "\n\t/// </summary>\n\tinternal int Bind()\n\t{\n\t\treturn 1;\n\t}\n}";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "46",
				}
			);

		Assert.HasCount(1, result.Diagnostics);

		// The text of a row can hold either break, so the fill is compared by its lines.
		Assert.AreEqual(
			fixedSource,
			result.FixedText.ReplaceLineEndings("\n"),
			"The fill split the code span."
		);
	}

	[TestMethod]
	public async Task APhraseElementIsNeverWrittenAsABlock()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <c>Binds the options
				/// from the configuration section</c>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <c>Binds the options from the configuration section</c>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "46",
				}
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnElementWiderThanTheWidthIsLeftWhole()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <remarks>Binds <c>the options from the configuration section</c> anyway.</remarks>
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
				/// Binds
				/// <c>the options from the configuration section</c>
				/// anyway.
				/// </remarks>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "46",
				}
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheSlashOfATagWrittenAcrossLinesComesBackUp()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <returns>Binds <see cref="SomeMemberName"
				/// /> the options from the section.</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <returns>
				/// Binds
				/// <see cref="SomeMemberName" /> the
				/// options from the section.
				/// </returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "38",
				}
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ATagLongerThanTheWidthIsLeftWhole()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <returns><see cref="DefaultConfigurationSectionOptions" /> binds them.</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <returns>
				/// <see cref="DefaultConfigurationSectionOptions" />
				/// binds them.
				/// </returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "38",
				}
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentThatHoldsABlankLineIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				/// The first thought, which is written long enough that it does not fit the line it is on.
				///
				/// The second thought, which is short.
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics, "A blank line inside an element was merged away.");
	}

	[TestMethod]
	public async Task AParagraphElementKeepsItsOwnLines()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <remarks>
				/// <para>The first thought.</para>
				/// <para>The second thought.</para>
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
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentThatHoldsACodeBlockIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options from the configuration section, validates them and lets the host fail at startup rather than at the first request.</summary>
				/// <code>
				/// var options = Bind(section, environment, category, validateOnStart);
				/// </code>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics, "A comment this rule does not lay out was reported.");
	}

	[TestMethod]
	public async Task AProseOnlyExampleIsLaidOutAsABlock()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <example>Binds the options of an item.</example>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <example>
				/// Binds the options of an item.
				/// </example>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnExampleThatHoldsACodeBlockIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <example>Binds the options from the configuration section, validates them and lets the host fail at startup rather than at the first request.
				/// <code>
				/// var options = Bind(section, environment, category, validateOnStart);
				/// </code>
				/// </example>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics, "A comment this rule does not lay out was reported.");
	}

	[TestMethod]
	public async Task ATailShorterThanTheMinimumIsMovedOntoTheNextLine()
	{
		const string source = """
			internal sealed class Sample
			{
				/// Binds the options of the sample, validating the host fails at startup.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string hanging = """
			internal sealed class Sample
			{
				/// Binds the options of the sample, validating
				/// the host fails at startup.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string onTheComma = """
			internal sealed class Sample
			{
				/// Binds the options of the sample,
				/// validating the host fails at startup.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		var cases = new (string? Minimum, string Expected)[]
		{
			(null, onTheComma),
			("0", hanging),
			("1", hanging),
			("2", onTheComma),
		};

		foreach ((string? minimum, string fixedSource) in cases)
		{
			var options = new Dictionary<string, string> { ["stanza_max_line_length"] = "52" };

			if (minimum is not null)
				options["stanza_doc_min_tail_words"] = minimum;

			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
					source,
					TestRuleSets.StandaloneRulesEnabled,
					analyzerConfigOptions: options
				);

			RoslynTestHarness.AssertFixedText(
				fixedSource,
				result,
				$"[{minimum ?? "default"}] {RoslynTestHarness.Describe(result.FixedText)} [expected] {RoslynTestHarness.Describe(fixedSource)}"
			);
		}
	}

	// A fixture sets the two minimums apart, so they stay two keys: each changes the layout the other leaves alone.
	[TestMethod]
	public async Task TheLineMinimumAndTheTailMinimumEachChangeALayoutTheOtherLeavesAlone()
	{
		const string tailSource = """
			internal sealed class Sample
			{
				/// Binds the options of the sample, validating the host fails at startup.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string tailOnTheComma = """
			internal sealed class Sample
			{
				/// Binds the options of the sample,
				/// validating the host fails at startup.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string tailHanging = """
			internal sealed class Sample
			{
				/// Binds the options of the sample, validating
				/// the host fails at startup.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string lineSource = """
			internal sealed class Sample
			{
				/// <returns>Binds the options from the configuration section and validates them before the host is built.</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string lineAvoidsARunt = """
			internal sealed class Sample
			{
				/// <returns>
				/// Binds the options from the configuration section and validates them before the host
				/// is built.
				/// </returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string lineAllowsARunt = """
			internal sealed class Sample
			{
				/// <returns>
				/// Binds the options from the configuration section and validates them before the host is
				/// built.
				/// </returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		var cases = new (string Source, string? Width, string Key, string Expected)[]
		{
			(tailSource, "52", "stanza_doc_min_line_words", tailOnTheComma),
			(tailSource, "52", "stanza_doc_min_tail_words", tailHanging),
			(lineSource, null, "stanza_doc_min_line_words", lineAllowsARunt),
			(lineSource, null, "stanza_doc_min_tail_words", lineAvoidsARunt),
		};

		foreach ((string source, string? width, string key, string fixedSource) in cases)
		{
			var options = new Dictionary<string, string> { [key] = "0" };

			if (width is not null)
				options["stanza_max_line_length"] = width;

			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
					source,
					TestRuleSets.StandaloneRulesEnabled,
					analyzerConfigOptions: options
				);

			RoslynTestHarness.AssertFixedText(
				fixedSource,
				result,
				$"[{key}] {RoslynTestHarness.Describe(result.FixedText)}"
			);
		}
	}

	[TestMethod]
	public async Task ARaisedTailMinimumReachesALongerTail()
	{
		const string source = """
			internal sealed class Sample
			{
				/// Binds the options of the sample, validates them before the host is built.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string filled = """
			internal sealed class Sample
			{
				/// Binds the options of the sample, validates them
				/// before the host is built.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string onTheComma = """
			internal sealed class Sample
			{
				/// Binds the options of the sample,
				/// validates them before the host is built.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		var cases = new (string Minimum, string Expected)[] { ("2", filled), ("3", onTheComma) };

		foreach ((string minimum, string fixedSource) in cases)
		{
			var options = new Dictionary<string, string>
			{
				["stanza_max_line_length"] = "60",
				["stanza_doc_min_tail_words"] = minimum,
			};

			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
					source,
					TestRuleSets.StandaloneRulesEnabled,
					analyzerConfigOptions: options
				);

			RoslynTestHarness.AssertFixedText(
				fixedSource,
				result,
				$"[{minimum}] {RoslynTestHarness.Describe(result.FixedText)} [expected] {RoslynTestHarness.Describe(fixedSource)}"
			);
		}
	}

	[TestMethod]
	public async Task ABreakIsNotLeftBeforeAClauseEnd()
	{
		const string source = """
			internal sealed class Sample
			{
				/// aaa bbb ccc ddd eee fff, ggg hhh iii jjj kkk, lll mmm nnn ooo ppp.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// aaa bbb ccc ddd eee fff, ggg hhh iii
				/// jjj kkk, lll mmm nnn ooo ppp.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "48",
				}
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ASpacedDashNeverOpensALine()
	{
		const string source = """
			internal sealed class Sample
			{
				/// Binds the options from the configuration section, which doesn't do much - which means the host fails at startup rather than at the first request it serves.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// Binds the options from
				/// the configuration section,
				/// which doesn't do much -
				/// which means the host
				/// fails at startup rather
				/// than at the first request
				/// it serves.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "34",
				}
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AQuestionMarkWeighsLikeAPeriod()
	{
		const string source = """
			internal sealed class Sample
			{
				/// Binds the options from the configuration section, and does the host fail at startup rather than at the first request it serves? It binds them anyway!
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// Binds the options from the
				/// configuration section, and does the
				/// host fail at startup rather than at
				/// the first request it serves? It binds
				/// them anyway!
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "46",
				}
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AClosingBracketNeverOpensALine()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options (the ones the section names) and lets the host fail at startup rather than at the first request it serves.</summary>
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
				/// Binds the options (the ones
				/// the section names) and lets the host fail
				/// at startup rather than at the first request
				/// it serves.
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "52",
				}
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnOpeningParenthesisStartsATail()
	{
		const string source = """
			internal sealed class Sample
			{
				/// Binds the options of the sample and (the configuration) binds them.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// Binds the options of the sample and
				/// (the configuration) binds them.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "52",
				}
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	public static IEnumerable<object[]> ZeroTailBreaks { get; } =
	[
		[
			"an opening parenthesis at the break",

			new Dictionary<string, string>
			{
				["stanza_max_line_length"] = "52",
				["stanza_doc_min_tail_words"] = "0",
			},

			"""
				internal sealed class Sample
				{
					/// Binds the options of the sample and (the configuration) binds them.
					internal int Bind()
					{
						return 1;
					}
				}
				""",

			"""
				internal sealed class Sample
				{
					/// Binds the options of the sample and (the
					/// configuration) binds them.
					internal int Bind()
					{
						return 1;
					}
				}
				""",
		],

		[
			"a clause end after the break",

			new Dictionary<string, string>
			{
				["stanza_max_line_length"] = "48",
				["stanza_doc_min_tail_words"] = "0",
			},

			"""
				internal sealed class Sample
				{
					/// aaa bbb ccc ddd eee fff, ggg hhh iii jjj kkk, lll mmm nnn ooo ppp.
					internal int Bind()
					{
						return 1;
					}
				}
				""",

			"""
				internal sealed class Sample
				{
					/// aaa bbb ccc ddd eee fff, ggg hhh iii jjj
					/// kkk, lll mmm nnn ooo ppp.
					internal int Bind()
					{
						return 1;
					}
				}
				""",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(ZeroTailBreaks),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ATailMinimumOfZeroBreaksAfterTheLastWordThatFits(
		string name,
		IReadOnlyDictionary<string, string> options,
		string source,
		string fixedSource
	)
	{
		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,
				analyzerConfigOptions: options
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result, name);
	}

	public static IEnumerable<object[]> WeakTailLists { get; } =
	[
		[
			"a word the list names",

			new Dictionary<string, string>
			{
				["stanza_max_line_length"] = "52",
				["stanza_doc_weak_tail_words"] = "the",
			},

			"""
				internal sealed class Sample
				{
					/// Binds the options of the sample and read the value from the section.
					internal int Bind()
					{
						return 1;
					}
				}
				""",

			"""
				internal sealed class Sample
				{
					/// Binds the options of the sample and read
					/// the value from the section.
					internal int Bind()
					{
						return 1;
					}
				}
				""",
		],

		[
			"a listed word matched without its punctuation or case",

			new Dictionary<string, string>
			{
				["stanza_max_line_length"] = "52",
				["stanza_doc_weak_tail_words"] = "The,",
			},

			"""
				internal sealed class Sample
				{
					/// Binds the options of the sample and read The value from the section.
					internal int Bind()
					{
						return 1;
					}
				}
				""",

			"""
				internal sealed class Sample
				{
					/// Binds the options of the sample and read
					/// The value from the section.
					internal int Bind()
					{
						return 1;
					}
				}
				""",
		],

		[
			"a list that names none",

			new Dictionary<string, string>
			{
				["stanza_max_line_length"] = "52",
				["stanza_doc_weak_tail_words"] = "none",
			},

			"""
				internal sealed class Sample
				{
					/// Binds the options of the sample and read the value from the section.
					internal int Bind()
					{
						return 1;
					}
				}
				""",

			"""
				internal sealed class Sample
				{
					/// Binds the options of the sample and read the
					/// value from the section.
					internal int Bind()
					{
						return 1;
					}
				}
				""",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(WeakTailLists),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AWeakTailWordTheListNamesIsMovedOffTheBreak(
		string name,
		IReadOnlyDictionary<string, string> options,
		string source,
		string fixedSource
	)
	{
		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,
				analyzerConfigOptions: options
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result, name);
	}

	public static IEnumerable<object[]> Tolerances { get; } =
	[
		[
			"a tolerance wide enough for the clause",

			new Dictionary<string, string>
			{
				["stanza_max_line_length"] = "32",
				["stanza_doc_min_tail_words"] = "3",
				["stanza_doc_max_overflow_columns"] = "4",
			},

			"""
				internal sealed class Sample
				{
					/// <returns>Binds the options from the configuration section, which the host reads at startup.</returns>
					internal int Bind()
					{
						return 1;
					}
				}
				""",

			"""
				internal sealed class Sample
				{
					/// <returns>
					/// Binds the options from
					/// the configuration section,
					/// which the host reads
					/// at startup.
					/// </returns>
					internal int Bind()
					{
						return 1;
					}
				}
				""",
		],

		[
			"the default tolerance without a key",

			new Dictionary<string, string>
			{
				["stanza_max_line_length"] = "32",
				["stanza_doc_min_tail_words"] = "3",
			},

			"""
				internal sealed class Sample
				{
					/// <returns>Binds the options from the configuration section, which the host reads at startup.</returns>
					internal int Bind()
					{
						return 1;
					}
				}
				""",

			"""
				internal sealed class Sample
				{
					/// <returns>
					/// Binds the options from
					/// the configuration section,
					/// which the host reads
					/// at startup.
					/// </returns>
					internal int Bind()
					{
						return 1;
					}
				}
				""",
		],

		[
			"no defect at the break",
			new Dictionary<string, string> { ["stanza_max_line_length"] = "32" },

			"""
				internal sealed class Sample
				{
					/// <returns>Binds the options from the configuration section, which the host reads at startup.</returns>
					internal int Bind()
					{
						return 1;
					}
				}
				""",

			"""
				internal sealed class Sample
				{
					/// <returns>
					/// Binds the
					/// options from the
					/// configuration section,
					/// which the host reads
					/// at startup.
					/// </returns>
					internal int Bind()
					{
						return 1;
					}
				}
				""",
		],

		[
			"a tolerance of zero",

			new Dictionary<string, string>
			{
				["stanza_max_line_length"] = "32",
				["stanza_doc_min_tail_words"] = "3",
				["stanza_doc_max_overflow_columns"] = "0",
			},

			"""
				internal sealed class Sample
				{
					/// <returns>Binds the options from the configuration section, which the host reads at startup.</returns>
					internal int Bind()
					{
						return 1;
					}
				}
				""",

			"""
				internal sealed class Sample
				{
					/// <returns>
					/// Binds the options
					/// from the configuration
					/// section, which the host
					/// reads at startup.
					/// </returns>
					internal int Bind()
					{
						return 1;
					}
				}
				""",
		],

		[
			"a tolerance too narrow for the clause",

			new Dictionary<string, string>
			{
				["stanza_max_line_length"] = "32",
				["stanza_doc_min_tail_words"] = "3",
				["stanza_doc_max_overflow_columns"] = "1",
			},

			"""
				internal sealed class Sample
				{
					/// <returns>Binds the options from the configuration section, which the host reads at startup.</returns>
					internal int Bind()
					{
						return 1;
					}
				}
				""",

			"""
				internal sealed class Sample
				{
					/// <returns>
					/// Binds the options
					/// from the configuration
					/// section, which the host
					/// reads at startup.
					/// </returns>
					internal int Bind()
					{
						return 1;
					}
				}
				""",
		],
	];

	// A tail minimum of three makes a break at the width split the clause,
	// and only a tolerance that covers the overflow keeps it whole.
	[TestMethod]
	[DynamicData(
		nameof(Tolerances),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ALineStandsOverTheWidthOnlyWithinTheToleranceToKeepAClauseWhole(
		string name,
		IReadOnlyDictionary<string, string> options,
		string source,
		string fixedSource
	)
	{
		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,
				analyzerConfigOptions: options
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result, name);
	}

	[TestMethod]
	public async Task AWordNoBreakCanShortenStandsOverTheWidthWhateverTheKeySays()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <returns>Binds <c>the options from the configuration section</c> at startup.</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <returns>
				/// Binds
				/// <c>the options from the configuration section</c>
				/// at startup.
				/// </returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		foreach (string tolerance in new[] { "0", "4", "20" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
					source,
					TestRuleSets.StandaloneRulesEnabled,

					analyzerConfigOptions: new Dictionary<string, string>
					{
						["stanza_max_line_length"] = "32",
						["stanza_doc_max_overflow_columns"] = tolerance,
					}
				);

			RoslynTestHarness.AssertFixedText(
				fixedSource,
				result,
				$"[{tolerance}] {RoslynTestHarness.Describe(result.FixedText)}"
			);
		}
	}

	[TestMethod]
	public async Task APlainParagraphIsFilledFromItsWords()
	{
		const string source = """
			internal sealed class Sample
			{
				/// The text a documentation comment rule asks for. The comment travels with the diagnostic as its additional location and the layout as a property, because a fix has no analyzer options of its own, and the layout is computed a second time here from the same two, so the fix writes exactly what the rule reported.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// The text a documentation comment rule asks for. The comment travels with the diagnostic as
				/// its additional location and the layout as a property, because a fix has no analyzer options
				/// of its own, and the layout is computed a second time here from the same two, so the fix
				/// writes exactly what the rule reported.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ATopLevelPhraseElementJoinsTheTextAroundIt()
	{
		const string source = """
			internal sealed class Sample
			{
				/// Binds <c>the options</c> from the configuration section and keeps them for the whole run.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// Binds <c>the options</c> from the
				/// configuration section and keeps them
				/// for the whole run.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "46",
				}
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheBreaksBetweenThePiecesOfACommentAreNotRead()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <c>Binds</c>
				/// the options of <see cref="SomeMemberName"/> from the configuration section.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <c>Binds</c> the options of
				/// <see cref="SomeMemberName"/> from the
				/// configuration section.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "46",
				}
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnElementWithABlockFormKeepsALineOfItsOwn()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <seealso cref="Sample"/>
				/// <seealso cref="Bind"/>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheSpaceAfterAMarkerIsNormalized()
	{
		const string source = """
			internal sealed class Sample
			{
				///    Binds the options of the item, and keeps them.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// Binds the options of the item, and keeps them.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheLinesOfACommentAreIndentedWhenTheFileNamesAnIndent()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				/// Binds the options from the configuration section, validates them and lets the host fail at startup rather than at the first request.
				/// </summary>
				/// <remarks>
				/// <para>
				/// The section is read once at startup, and the bound options are validated before the host is built, so a missing key fails the run rather than the first request.
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
				/// <summary>
				///   Binds the options
				///   from the configuration section,
				///   validates them and lets the host
				///   fail at startup rather than at the
				///   first request.
				/// </summary>
				/// <remarks>
				///   <para>
				///     The section
				///     is read once at startup, and the
				///     bound options are validated before
				///     the host is built, so a missing
				///     key fails the run rather than the
				///     first request.
				///   </para>
				/// </remarks>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		var options = new Dictionary<string, string>
		{
			["stanza_max_line_length"] = "46",
			["stanza_doc_indent_size"] = "2",
		};

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,
				analyzerConfigOptions: options
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheTextOfAnElementHangsAtTheLevelOfItsTags()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				/// Binds the options from the configuration section, validates them and lets the host fail at startup rather than at the first request.
				/// </summary>
				/// <remarks>
				/// <para>
				/// The section is read once at startup, and the bound options are validated before the host is built, so a missing key fails the run rather than the first request.
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
				/// <summary>
				/// Binds the options from the
				/// configuration section, validates them
				/// and lets the host fail at startup
				/// rather than at the first request.
				/// </summary>
				/// <remarks>
				///   <para>
				///   The section is read once at startup,
				///   and the bound options are validated
				///   before the host is built, so a
				///   missing key fails the run rather
				///   than the first request.
				///   </para>
				/// </remarks>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		var options = new Dictionary<string, string>
		{
			["stanza_max_line_length"] = "46",
			["stanza_doc_indent_size"] = "2",
			["stanza_doc_indent_style"] = "hanging",
		};

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,
				analyzerConfigOptions: options
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheProseBesideAnElementKeepsTheMarker()
	{
		const string source = """
			internal sealed class Sample
			{
				/// Binds <c>the options</c> from the configuration section and keeps them for the whole run, and the section is read once.
				/// <returns>Binds the options from the configuration section and validates them before the host is built.</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// Binds <c>the options</c> from the
				/// configuration section and keeps them
				/// for the whole run, and the section is
				/// read once.
				/// <returns>
				///   Binds the options from the
				///   configuration section and validates
				///   them before the host is built.
				/// </returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		var options = new Dictionary<string, string>
		{
			["stanza_max_line_length"] = "46",
			["stanza_doc_indent_size"] = "2",
		};

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,
				analyzerConfigOptions: options
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheStyleKeyAloneWritesNoIndentation()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options from the configuration section, validates them and lets the host fail at startup rather than at the first request.</summary>
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
				/// Binds the options from the configuration section, validates them and lets the host fail at
				/// startup rather than at the first request.
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		var style = new Dictionary<string, string> { ["stanza_doc_indent_style"] = "hanging" };

		AnalyzerRunResult plain =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		AnalyzerRunResult styled =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,
				analyzerConfigOptions: style
			);

		Assert.HasCount(1, plain.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, plain);
		RoslynTestHarness.AssertFixedText(fixedSource, styled);
	}

	[TestMethod]
	public async Task AnIndentThatLeavesNoWidthHandsTheCommentOver()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options from the configuration section.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		var flush = new Dictionary<string, string> { ["stanza_max_line_length"] = "12" };

		var indented = new Dictionary<string, string>
		{
			["stanza_max_line_length"] = "12",
			["stanza_doc_indent_size"] = "4",
		};

		AnalyzerRunResult flushResult =
			await RoslynTestHarness.ReportAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,
				analyzerConfigOptions: flush
			);

		AnalyzerRunResult indentedResult =
			await RoslynTestHarness.ReportAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,
				analyzerConfigOptions: indented
			);

		Assert.HasCount(1, flushResult.Diagnostics);
		Assert.IsEmpty(indentedResult.Diagnostics, "A comment with no width left is not laid out.");
	}

	[TestMethod]
	public async Task TheDocumentationWidthOverridesTheLineWidthForComments()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options from the configuration section, validates them and lets the host fail at startup rather than at the first request.</summary>
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
				/// Binds the options from the
				/// configuration section, validates them
				/// and lets the host fail at startup
				/// rather than at the first request.
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		var narrow = new Dictionary<string, string> { ["stanza_max_line_length"] = "46" };

		var overridden = new Dictionary<string, string>
		{
			["stanza_max_line_length"] = "200",
			["stanza_doc_max_line_length"] = "46",
		};

		var wide = new Dictionary<string, string> { ["stanza_max_line_length"] = "200" };

		AnalyzerRunResult narrowResult =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,
				analyzerConfigOptions: narrow
			);

		AnalyzerRunResult overriddenResult =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,
				analyzerConfigOptions: overridden
			);

		AnalyzerRunResult wideResult =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,
				analyzerConfigOptions: wide
			);

		Assert.HasCount(1, narrowResult.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, narrowResult);
		RoslynTestHarness.AssertFixedText(fixedSource, overriddenResult);

		Assert.IsEmpty(
			wideResult.Diagnostics,
			"a comment that fits the wide line is not laid out."
		);
	}

	[TestMethod]
	public async Task ADocumentationWidthOfZeroSwitchesTheCommentOff()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options from the configuration section, validates them and lets the host fail at startup rather than at the first request.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		var enabled = new Dictionary<string, string> { ["stanza_max_line_length"] = "46" };

		var off = new Dictionary<string, string>
		{
			["stanza_max_line_length"] = "46",
			["stanza_doc_max_line_length"] = "0",
		};

		AnalyzerRunResult enabledResult =
			await RoslynTestHarness.ReportAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,
				analyzerConfigOptions: enabled
			);

		AnalyzerRunResult offResult =
			await RoslynTestHarness.ReportAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,
				analyzerConfigOptions: off
			);

		Assert.HasCount(1, enabledResult.Diagnostics);
		Assert.IsEmpty(offResult.Diagnostics, "a comment whose width is zero is not laid out.");
	}

	[TestMethod]
	public async Task TheIndentAndMarkerOfACommentCountAgainstTheDocumentationWidth()
	{
		// The member's tab is four columns and the "/// " marker four more,
		// so the summary line is 59 columns.
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options of the sample.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		var fitsWithItsIndentAndMarker = new Dictionary<string, string>
		{
			["stanza_doc_max_line_length"] = "59",
		};

		var oneColumnShort = new Dictionary<string, string>
		{
			["stanza_doc_max_line_length"] = "58",
		};

		// The summary's own 51 columns fit this width, but the indent and the marker do not.
		var fitsWithoutThem = new Dictionary<string, string>
		{
			["stanza_doc_max_line_length"] = "51",
		};

		AnalyzerRunResult fitting =
			await RoslynTestHarness.ReportAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,
				analyzerConfigOptions: fitsWithItsIndentAndMarker
			);

		AnalyzerRunResult short58 =
			await RoslynTestHarness.ReportAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,
				analyzerConfigOptions: oneColumnShort
			);

		AnalyzerRunResult short51 =
			await RoslynTestHarness.ReportAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,
				analyzerConfigOptions: fitsWithoutThem
			);

		Assert.IsEmpty(
			fitting.Diagnostics,
			"a summary that fills the width with its indent and marker is left alone."
		);

		Assert.HasCount(1, short58.Diagnostics);

		Assert.HasCount(
			1,
			short51.Diagnostics,
			"a summary that fits only without the indent and marker is laid out."
		);
	}

	[TestMethod]
	public async Task TheIndentOfACommentIsWrittenWithTabsWhenTheFileNamesTab()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options of the sample.</summary>
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
				/// 	Binds the options of the sample.
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		var options = new Dictionary<string, string>
		{
			["stanza_max_line_length"] = "46",
			["stanza_doc_indent_size"] = "tab",
		};

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,
				analyzerConfigOptions: options
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheSectionScopeOutlinesEveryTagThatDescribesTheMember()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options of <c>the sample</c>.</summary>
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
				/// <summary>
				/// Binds the options of <c>the sample</c>.
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
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_doc_expand"] = "sections",
				}
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheWidestScopeReachesATagTheSectionScopeLeavesAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <note>The bound options.</note>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <note>
				/// The bound options.
				/// </note>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult sectioned =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_doc_expand"] = "sections",
				}
			);

		AnalyzerRunResult widest =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_doc_expand"] = "all",
				}
			);

		Assert.IsEmpty(sectioned.Diagnostics, "A tag that describes nothing keeps its one line.");

		RoslynTestHarness.AssertFixedText(fixedSource, widest);
	}

	[TestMethod]
	public async Task AScopeTheKeyDoesNotNameKeepsTheOneLineForm()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_doc_expand"] = "every",
				}
			);

		Assert.IsEmpty(result.Diagnostics, "A scope that names nothing is read as the default.");
	}

	[TestMethod]
	public async Task ATabInsideAnAttributeValueIsCountedInColumns()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <param name="a	b">Binds the options the section names and validates them at startup.</param>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <param name="a	b">
				/// Binds the options the section names and validates them at startup.
				/// </param>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnElementHoldingAListIsLaidOutWithIt()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				/// Binds the options.
				/// <list type="bullet"><item>One.</item><item>Two.</item></list>
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
				/// Binds the options.
				/// <list type="bullet">
				/// <item>One.</item>
				/// <item>Two.</item>
				/// </list>
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ProseOnBothSidesOfAListGainsParagraphPairs()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				/// Before the list.
				/// <list type="bullet">
				/// <item>One.</item>
				/// </list>
				/// After the list.
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
				/// Before the list.
				/// </para>
				/// <list type="bullet">
				/// <item>One.</item>
				/// </list>
				/// <para>
				/// After the list.
				/// </para>
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task RemarksExampleAndParagraphElementsTakeABlockFormWhateverTheirLength()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Short.</summary>
				/// <remarks>Reads the section.</remarks>
				/// <example>Call it.</example>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Short.</summary>
				/// <remarks>
				/// Reads the section.
				/// </remarks>
				/// <example>
				/// Call it.
				/// </example>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheIndentSizeExampleOfItsPageIsWhatTheLayoutWrites()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options of the sample.</summary>
				/// <remarks><para>The section is read once at startup.</para></remarks>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string flush = """
			internal sealed class Sample
			{
				/// <summary>
				/// Binds the options of the sample.
				/// </summary>
				/// <remarks>
				/// <para>
				/// The section is read once at startup.
				/// </para>
				/// </remarks>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string indented = """
			internal sealed class Sample
			{
				/// <summary>
				///   Binds the options of the sample.
				/// </summary>
				/// <remarks>
				///   <para>
				///     The section is read once at startup.
				///   </para>
				/// </remarks>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		(string? Size, string Expected)[] cases = [(null, flush), ("2", indented)];

		foreach ((string? size, string fixedSource) in cases)
		{
			var options = new Dictionary<string, string> { ["stanza_doc_expand"] = "all" };

			if (size is not null)
				options["stanza_doc_indent_size"] = size;

			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
					source,
					TestRuleSets.StandaloneRulesEnabled,
					analyzerConfigOptions: options
				);

			RoslynTestHarness.AssertFixedText(fixedSource, result);
		}
	}

	[TestMethod]
	public async Task TheIndentStyleExampleOfItsPageIsWhatTheLayoutWrites()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options of the sample.</summary>
				/// <remarks><para>The section is read once at startup.</para></remarks>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string nested = """
			internal sealed class Sample
			{
				/// <summary>
				///   Binds the options of the sample.
				/// </summary>
				/// <remarks>
				///   <para>
				///     The section is read once at startup.
				///   </para>
				/// </remarks>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string hanging = """
			internal sealed class Sample
			{
				/// <summary>
				/// Binds the options of the sample.
				/// </summary>
				/// <remarks>
				///   <para>
				///   The section is read once at startup.
				///   </para>
				/// </remarks>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		(string Style, string Expected)[] cases = [("nested", nested), ("hanging", hanging)];

		foreach ((string style, string fixedSource) in cases)
		{
			var options = new Dictionary<string, string>
			{
				["stanza_doc_expand"] = "sections",
				["stanza_doc_indent_size"] = "2",
				["stanza_doc_indent_style"] = style,
			};

			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
					source,
					TestRuleSets.StandaloneRulesEnabled,
					analyzerConfigOptions: options
				);

			RoslynTestHarness.AssertFixedText(fixedSource, result);
		}
	}

	[TestMethod]
	public async Task TheCommentLayoutRuleOwnsTheListItWrites()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				/// Binds the options.
				/// <list type="bullet"><item>One.</item><item>Two.</item></list>
				/// </summary>
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>
				/// Binds the options.
				/// <list type="bullet">
				/// <item>One.</item>
				/// <item>Two.</item>
				/// </list>
				/// </summary>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunFixAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.LayOutDocumentationComment,
				DiagnosticDescriptors.LayOutItemsOfDocumentationList
			)
		);

		Assert.HasCount(1, result.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.LayOutDocumentationComment.Id,
			result.Diagnostics[0].Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ALineSeparatorInACommentIsNeverWrittenAsALineEnding()
	{
		const char separator = (char)0x2028;

		string source =
			"internal sealed class Sample\r\n{\r\n\t/// <summary>Binds the options, validates them and lets the host fail"
			+ separator
			+ "\t/// at startup rather than at the first request it serves to a caller.</summary>\r\n\tinternal int Bind() => 1;\r\n}\r\n";

		IReadOnlyDictionary<string, ReportDiagnostic> documentationRules = TestRuleSets.Only([
			.. TestRuleSets
				.Declared.Select(rule => rule.Descriptor)
				.Where(descriptor => descriptor.Id.StartsWith("ZA02", StringComparison.Ordinal)),
		]);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,
			documentationRules
		);

		Assert.IsLessThanOrEqualTo(
			1,
			result.FixedText.Count(character => character == separator),
			"a rewritten line ended on the separator."
		);
	}

	[TestMethod]
	public async Task APragmaOverTheEmptyElementRuleHandsTheCommentToTheLayoutRule()
	{
		string source = $$"""
			internal sealed class Sample
			{
			#pragma warning disable {{DiagnosticDescriptors.WriteEmptyElementAsSelfClosingTag.Id}}
				/// <summary>Binds the <c></c> options, validates them and lets the host fail at startup rather than at the first request.</summary>
				internal int Bind() => 1;
			#pragma warning restore {{DiagnosticDescriptors.WriteEmptyElementAsSelfClosingTag.Id}}
			}
			""";

		string fixedSource = $$"""
			internal sealed class Sample
			{
			#pragma warning disable {{DiagnosticDescriptors.WriteEmptyElementAsSelfClosingTag.Id}}
				/// <summary>
				/// Binds the <c></c> options, validates them and lets the host fail at startup rather than at
				/// the first request.
				/// </summary>
				internal int Bind() => 1;
			#pragma warning restore {{DiagnosticDescriptors.WriteEmptyElementAsSelfClosingTag.Id}}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,

				TestRuleSets.Only(
					DiagnosticDescriptors.LayOutDocumentationComment,
					DiagnosticDescriptors.WriteEmptyElementAsSelfClosingTag
				)
			);

		Assert.HasCount(1, result.Diagnostics);

		// The empty element stays as written, which the rule the pragma silences would have changed.
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// Roslyn ends a line at U+2028, U+2029 and U+0085, while the layout reads only `\r` and `\n`,
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
				/// <summary>Returns the value of the thing that is long enough to need a wrap at the width.</summary>{break}internal int Bind() => 1;
			}
			""".Replace("{break}", (char)codePoint + "\t", StringComparison.Ordinal);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.LayOutDocumentationComment)
			);

		Assert.IsEmpty(result.Diagnostics);
	}
}
