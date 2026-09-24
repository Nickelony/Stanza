using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Documentation;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class LayOutItemsOfDocumentationListTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> ListRuleOnly { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.LayOutItemsOfDocumentationList);

	// The layout rule leaves alone a comment whose closer shares its line with code, so this rule does not defer to it.
	[TestMethod]
	public async Task AListInACommentTheLayoutRuleDeclinesIsStillLaidOut()
	{
		const string source = """
			internal sealed class Sample
			{
				/**
				 * <list type="bullet">
				 * <item><description>One value.</description></item>
				 * </list>
				 */ internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/**
				 * <list type="bullet">
				 * <item>
				 * <description>One value.</description>
				 * </item>
				 * </list>
				 */ internal int Bind() => 1;
			}
			""";

		Dictionary<string, ReportDiagnostic> bothRules = new(ListRuleOnly)
		{
			[DiagnosticDescriptors.LayOutDocumentationComment.Id] = ReportDiagnostic.Warn,
		};

		AnalyzerRunResult alone =
			await RoslynTestHarness.RunAsync<LayOutItemsOfDocumentationListAnalyzer>(
				source,
				ListRuleOnly,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "46",
				}
			);

		AnalyzerRunResult beside =
			await RoslynTestHarness.RunAsync<LayOutItemsOfDocumentationListAnalyzer>(
				source,
				bothRules,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "46",
				}
			);

		Assert.HasCount(1, alone.Diagnostics);
		Assert.HasCount(1, beside.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, alone);
		RoslynTestHarness.AssertFixedText(fixedSource, beside);
	}

	// The layout rule reports at the member, so a pragma between the comment and the member silences it for the list too.
	[TestMethod]
	public async Task APragmaBetweenTheCommentAndTheMemberHandsTheListBack()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				/// Binds the options.
				/// <list type="bullet"><item>One.</item><item>Two.</item></list>
				/// </summary>
			#pragma warning disable ZA0201
				internal int Bind() => 1;
			#pragma warning restore ZA0201
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
			#pragma warning disable ZA0201
				internal int Bind() => 1;
			#pragma warning restore ZA0201
			}
			""";

		Dictionary<string, ReportDiagnostic> bothRules = new(ListRuleOnly)
		{
			[DiagnosticDescriptors.LayOutDocumentationComment.Id] = ReportDiagnostic.Warn,
		};

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutItemsOfDocumentationListAnalyzer>(
				source,
				bothRules
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnItemThatDoesNotFitOpensUp()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <list type="bullet">
				/// <item><description>One value.</description></item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <list type="bullet">
				/// <item>
				/// <description>One value.</description>
				/// </item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutItemsOfDocumentationListAnalyzer>(
				source,
				ListRuleOnly,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "46",
				}
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// An item too long to fold reads its text as a paragraph, so an element keeps the words and marks it touches,
	// and the harness's second pass proves the fold measures the text the expanded form writes.
	[TestMethod]
	[DataRow("40")]
	[DataRow("60")]
	[DataRow("80")]
	public async Task AnItemTooLongToFoldKeepsItsText(string width)
	{
		const string source = """
			internal sealed class Sample
			{
				/// <list type="bullet">
				/// <item><description>Returns what the caller passed to <c>Create</c>, or nothing (<see cref="C"/>), as <see cref="Foo"/>s.</description></item>
				/// <item><description><b>Note: </b> the cache is shared.</description></item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutItemsOfDocumentationListAnalyzer>(
				source,
				ListRuleOnly,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = width,
				}
			);

		string text = string.Join(
			" ",

			result
				.FixedText.Replace("///", " ")
				.Split((char[])[' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
		);

		Assert.IsNotEmpty(result.Diagnostics);
		Assert.Contains("<c>Create</c>,", text);
		Assert.Contains("(<see cref=\"C\"/>)", text);
		Assert.Contains("<see cref=\"Foo\"/>s.", text);
		Assert.Contains("<b>Note: </b> the", text);
	}

	[TestMethod]
	public async Task AWidthThatLeavesNoRoomForTheListIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <list type="bullet">
				/// <item><description>One value.</description></item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutItemsOfDocumentationListAnalyzer>(
				source,
				ListRuleOnly,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "8",
				}
			);

		Assert.IsEmpty(result.Diagnostics, "A list no line of the comment can hold is reported.");
	}

	[TestMethod]
	public async Task AnItemThatFitsIsJoined()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <list type="bullet">
				/// <item>
				/// <description>One.</description>
				/// </item>
				/// <item>
				/// <description>Two.</description>
				/// </item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <list type="bullet">
				/// <item><description>One.</description></item>
				/// <item><description>Two.</description></item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutItemsOfDocumentationListAnalyzer>(
				source,
				ListRuleOnly,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "60",
				}
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheTagsOfAListOwnTheirLines()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <list type="bullet"><item>One.</item><item>Two.</item></list>
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <list type="bullet">
				/// <item>One.</item>
				/// <item>Two.</item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutItemsOfDocumentationListAnalyzer>(
				source,
				ListRuleOnly,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "60",
				}
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheIndentKeyLaysTheItemsOneLevelIn()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <list type="bullet">
				/// <item><description>One.</description></item>
				/// <item><description>Two.</description></item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <list type="bullet">
				///   <item><description>One.</description></item>
				///   <item><description>Two.</description></item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutItemsOfDocumentationListAnalyzer>(
				source,
				ListRuleOnly,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "60",
					["stanza_doc_indent_size"] = "2",
				}
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ANestedListIsLaidOutOneLevelFurtherIn()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <list type="bullet">
				/// <item>
				/// <description>One.</description>
				/// <list type="bullet"><item>Deep.</item></list>
				/// </item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <list type="bullet">
				///   <item>
				///     <description>One.</description>
				///     <list type="bullet">
				///       <item>Deep.</item>
				///     </list>
				///   </item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutItemsOfDocumentationListAnalyzer>(
				source,
				ListRuleOnly,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "70",
					["stanza_doc_indent_size"] = "2",
				}
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ANestedListIsNeverFoldedOntoItsItem()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <list type="bullet">
				/// <item><description>One.</description><list type="bullet"><item>Deep.</item></list></item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <list type="bullet">
				///   <item>
				///     <description>One.</description>
				///     <list type="bullet">
				///       <item>Deep.</item>
				///     </list>
				///   </item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutItemsOfDocumentationListAnalyzer>(
				source,
				ListRuleOnly,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "200",
					["stanza_doc_indent_size"] = "2",
				}
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AListOfABlockIsLaidOut()
	{
		const string source = """
			internal sealed class Sample
			{
				/**
				 * <list type="bullet"><item>One.</item><item>Two.</item></list>
				 */
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/**
				 * <list type="bullet">
				 * <item>One.</item>
				 * <item>Two.</item>
				 * </list>
				 */
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutItemsOfDocumentationListAnalyzer>(
				source,
				ListRuleOnly,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "60",
				}
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AListOfABlockOnTheOpeningLineIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/** <list type="bullet">
				 * <item><description>One.</description></item>
				 * </list>
				 */
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutItemsOfDocumentationListAnalyzer>(
				source,
				ListRuleOnly,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "60",
				}
			);

		Assert.IsEmpty(result.Diagnostics, "A list that opens the block is left alone.");
	}

	// The compiler strips a star only when every line carries it at one column,
	// so a list written at another column would put a literal star into the XML.
	[TestMethod]
	public async Task AListOfABlockTakesTheStarColumnOfItsComment()
	{
		const string source = """
			internal sealed class Sample
			{
				/**
				  * <remarks>
				  * <list type="bullet"><item>One.</item><item>Two.</item></list>
				  * </remarks>
				  */
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/**
				  * <remarks>
				  * <list type="bullet">
				  * <item>One.</item>
				  * <item>Two.</item>
				  * </list>
				  * </remarks>
				  */
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutItemsOfDocumentationListAnalyzer>(
				source,
				ListRuleOnly,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "60",
				}
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AListOfABlockGainsTheStarItsOtherLinesCarry()
	{
		const string source = """
			internal sealed class Sample
			{
				/**
				 * <remarks>
				   <list type="bullet"><item>One.</item><item>Two.</item></list>
				 * </remarks>
				*/
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/**
				 * <remarks>
				 * <list type="bullet">
				 * <item>One.</item>
				 * <item>Two.</item>
				 * </list>
				 * </remarks>
				*/
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutItemsOfDocumentationListAnalyzer>(
				source,
				ListRuleOnly,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "60",
				}
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// Rewriting only the list's lines would give them a star the other lines lack,
	// and the compiler would keep that star as text.
	[TestMethod]
	public async Task AListOfABlockWithoutStarsIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/**
				   <remarks>
				   <list type="bullet"><item>One.</item><item>Two.</item></list>
				   </remarks>
				*/
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutItemsOfDocumentationListAnalyzer>(
				source,
				ListRuleOnly,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "60",
				}
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	// Stars on the list's lines would change the form of a comment that has no other line to show it.
	[TestMethod]
	public async Task AStarlessBlockThatHoldsOnlyAListIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/**
				   <list type="bullet"><item>One.</item><item>Two.</item></list>
				*/
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutItemsOfDocumentationListAnalyzer>(
				source,
				ListRuleOnly,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "60",
				}
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheTextOfAnItemIsLaidOutAtTheWidth()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <list type="bullet">
				/// <item>
				/// <description>Reads the section and binds every option of the configuration.</description>
				/// </item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <list type="bullet">
				/// <item>
				/// <description>
				/// Reads the section and binds every option of
				/// the configuration.
				/// </description>
				/// </item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutItemsOfDocumentationListAnalyzer>(
				source,
				ListRuleOnly,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "60",
				}
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AListThatHoldsCodeIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <list type="bullet"><item><code>var value = Call(1, 2);</code></item></list>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutItemsOfDocumentationListAnalyzer>(
				source,
				ListRuleOnly,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "60",
				}
			);

		Assert.IsEmpty(result.Diagnostics, "A list that holds code is laid out.");
	}

	[TestMethod]
	public async Task AListWrittenInsideASentenceIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// A sentence in front of <list type="bullet"><item>One.</item></list> and behind it.
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutItemsOfDocumentationListAnalyzer>(
				source,
				ListRuleOnly,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "60",
				}
			);

		Assert.IsEmpty(result.Diagnostics, "A list inside a sentence is laid out.");
	}

	[TestMethod]
	public async Task AListWithABlankLineBetweenItsItemsIsWrittenTight()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <list type="bullet">
				/// <item>One.</item>
				///
				/// <item>Two.</item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <list type="bullet">
				/// <item>One.</item>
				/// <item>Two.</item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutItemsOfDocumentationListAnalyzer>(
				source,
				ListRuleOnly,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "60",
				}
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheWidestScopeExpandsTheItemsOfAList()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <list type="bullet">
				/// <item><description>One value.</description></item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <list type="bullet">
				/// <item>
				/// <description>
				/// One value.
				/// </description>
				/// </item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult sectioned =
			await RoslynTestHarness.RunAsync<LayOutItemsOfDocumentationListAnalyzer>(
				source,
				ListRuleOnly,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_doc_expand"] = "sections",
				}
			);

		AnalyzerRunResult widest =
			await RoslynTestHarness.RunAsync<LayOutItemsOfDocumentationListAnalyzer>(
				source,
				ListRuleOnly,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_doc_expand"] = "all",
				}
			);

		Assert.IsEmpty(
			sectioned.Diagnostics,
			"An item of a list is an element of the list family."
		);

		Assert.HasCount(1, widest.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, widest);
	}

	[TestMethod]
	public async Task AnEmptyElementInsideAWrappingItemKeepsItsShape()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <list type="bullet">
				/// <item><description>Reads <see cref="Item"/>, breaks <br/> and writes it back.</description></item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <list type="bullet">
				/// <item>
				/// <description>
				/// Reads <see cref="Item"/>, breaks <br/> and
				/// writes it back.
				/// </description>
				/// </item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutItemsOfDocumentationListAnalyzer>(
				source,
				ListRuleOnly,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "52",
				}
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task EachListOfACommentIsReportedAtItsTagAndFixedOnItsOwn()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				/// <list type="bullet"><item>One.</item><item>Two.</item></list>
				/// <list type="bullet"><item>Three.</item><item>Four.</item></list>
				/// </summary>
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>
				/// <list type="bullet">
				/// <item>One.</item>
				/// <item>Two.</item>
				/// </list>
				/// <list type="bullet">
				/// <item>Three.</item>
				/// <item>Four.</item>
				/// </list>
				/// </summary>
				internal int Bind() => 1;
			}
			""";

		const string firstOnly = """
			internal sealed class Sample
			{
				/// <summary>
				/// <list type="bullet">
				/// <item>One.</item>
				/// <item>Two.</item>
				/// </list>
				/// <list type="bullet"><item>Three.</item><item>Four.</item></list>
				/// </summary>
				internal int Bind() => 1;
			}
			""";

		const string secondOnly = """
			internal sealed class Sample
			{
				/// <summary>
				/// <list type="bullet"><item>One.</item><item>Two.</item></list>
				/// <list type="bullet">
				/// <item>Three.</item>
				/// <item>Four.</item>
				/// </list>
				/// </summary>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutItemsOfDocumentationListAnalyzer>(
				source,
				ListRuleOnly
			);

		string[] alone = await RoslynTestHarness.ApplyEachFixAloneAsync(
			source,
			DiagnosticDescriptors.LayOutItemsOfDocumentationList,
			ListRuleOnly
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
		Assert.AreSequenceEqual([firstOnly, secondOnly], alone);
	}

	[TestMethod]
	public async Task AListInsideAnElementIsLaidOut()
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

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutItemsOfDocumentationListAnalyzer>(
				source,
				ListRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// Roslyn ends a line at U+2028, U+2029 and U+0085, while the list layout reads only `\r` and `\n`,
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
				/// <list type="bullet">
				/// <item><description>One value.</description></item>
				/// </list>{break}internal int Bind() => 1;
			}
			""".Replace("{break}", (char)codePoint + "\t", StringComparison.Ordinal);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutItemsOfDocumentationListAnalyzer>(
				source,
				ListRuleOnly,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "46",
				}
			);

		Assert.IsEmpty(result.Diagnostics);
	}
}
