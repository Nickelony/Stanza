using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Documentation;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class DocCommentMalformedTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> LayoutRuleOnly { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.LayOutDocumentationComment);

	private static IReadOnlyDictionary<string, ReportDiagnostic> ListRuleOnly { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.LayOutItemsOfDocumentationList);

	[TestMethod]
	[CrossCutting]
	public async Task ACommentWithABrokenTagIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				/// A sentence that is long enough to be laid out again by the width of a comment.
				/// It holds a break <br> in the middle of it.
				/// </summary>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				LayoutRuleOnly,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "60",
				}
			);

		Assert.IsEmpty(result.Diagnostics, "A comment the parser had to repair is laid out.");
	}

	[TestMethod]
	[CrossCutting]
	public async Task ACommentWithAWholeTagIsLaidOut()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				/// A sentence that is long enough to be laid out again by the width of a comment.
				/// It holds a break <br/> in the middle of it.
				/// </summary>
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>
				/// A sentence that is long enough to be laid out again
				/// by the width of a comment. It holds a break <br/> in
				/// the middle of it.
				/// </summary>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				LayoutRuleOnly,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "60",
				}
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task AListInACommentWithABrokenTagIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				/// A sentence with a broken tag <br> in it.
				/// </summary>
				/// <remarks>
				/// <list type="bullet">
				/// <item><description>One value.</description></item>
				/// </list>
				/// </remarks>
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

		Assert.IsEmpty(
			result.Diagnostics,
			"A list of a comment the parser had to repair is written."
		);
	}

	[TestMethod]
	[CrossCutting]
	public async Task AListInAWholeCommentIsLaidOut()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				/// A sentence with a whole tag <br/> in it.
				/// </summary>
				/// <remarks>
				/// <list type="bullet">
				/// <item><description>One value.</description></item>
				/// </list>
				/// </remarks>
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>
				/// A sentence with a whole tag <br/> in it.
				/// </summary>
				/// <remarks>
				/// <list type="bullet">
				/// <item>
				/// <description>One value.</description>
				/// </item>
				/// </list>
				/// </remarks>
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

	// The parser closes an element at the end tag of another, so the text after that tag is outside it.
	[TestMethod]
	[CrossCutting]
	public async Task ACommentWithAnEndTagThatNamesAnotherElementIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Writes the </div> tag that closes the panel.</summary>
				internal void Close() { }

				/// <summary>
				/// A sentence that is long enough to be laid out again by the width of a comment.
				/// It ends on a stray tag.</remarks>
				/// </summary>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				LayoutRuleOnly,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "60",
				}
			);

		Assert.IsEmpty(
			result.Diagnostics,
			"A comment the parser closed at another tag is laid out."
		);
	}

	[TestMethod]
	[CrossCutting]
	public async Task MarkupAfterAStrayTopLevelEndTagIsLeftAsWritten()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options.</summary>
				/// </summary>
				/// <param name="value">The value that the sample binds, as <see cref="Sample" /> reads it when the host starts.</param>
				internal int Bind(int value) => value;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				LayoutRuleOnly,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "60",
				}
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	private const string RepairedComment = """
		internal sealed class Sample
		{
			/// <summary>Returns null for a section that has no period <remarks  > and runs on</summary>
			internal int Bind() => 1;
		}
		""";

	private const string WholeComment = """
		internal sealed class Sample
		{
			/// <summary>Returns null for a section that has no period <remarks  ></remarks> and runs on</summary>
			internal int Bind() => 1;
		}
		""";

	public static IEnumerable<object[]> BrokenComments { get; } =
	[
		["the null keyword rule", new MarkNullKeywordAnalyzer(), RepairedComment, WholeComment],

		[
			"the text end rule",
			new EndDocumentationTextWithPeriodAnalyzer(),
			RepairedComment,
			WholeComment,
		],

		[
			"the text end rule at an end tag that names another element",
			new EndDocumentationTextWithPeriodAnalyzer(),

			"""
				internal sealed class Sample
				{
					/// <summary>Writes the </div> tag that closes the panel</summary>
					internal void Close() { }
				}
				""",

			"""
				internal sealed class Sample
				{
					/// <summary>Writes the <c>div</c> tag that closes the panel</summary>
					internal void Close() { }
				}
				""",
		],

		[
			"the tag whitespace rule",
			new CollapseWhitespaceInsideTagAnalyzer(),
			RepairedComment,
			WholeComment,
		],

		[
			"the paragraph rule",
			new MarkDocumentationParagraphsAnalyzer(),

			"""
				internal sealed class Sample
				{
					/// <summary>
					/// The first thought has a break <br> in it.
					///
					/// The second thought.
					/// </summary>
					internal int Bind() => 1;
				}
				""",

			"""
				internal sealed class Sample
				{
					/// <summary>
					/// The first thought has a break <br/> in it.
					///
					/// The second thought.
					/// </summary>
					internal int Bind() => 1;
				}
				""",
		],

		[
			"the padding rule",
			new RemoveDocumentationPaddingAnalyzer(),

			"""
				internal sealed class Sample
				{
					/// <summary>
					///
					/// A thought with a break <br> in it.
					/// </summary>
					internal int Bind() => 1;
				}
				""",

			"""
				internal sealed class Sample
				{
					/// <summary>
					///
					/// A thought with a break <br/> in it.
					/// </summary>
					internal int Bind() => 1;
				}
				""",
		],

		[
			"the element order rule",
			new OrderElementsOfDocumentationCommentAnalyzer(),

			"""
				internal sealed class Sample
				{
					/// <returns>The count.</returns>
					/// <summary>Binds the options <br>.</summary>
					internal int Bind() => 1;
				}
				""",

			"""
				internal sealed class Sample
				{
					/// <returns>The count.</returns>
					/// <summary>Binds the options.</summary>
					internal int Bind() => 1;
				}
				""",
		],

		[
			"the list order rule",
			new OrderItemsOfDocumentationListAnalyzer(),

			"""
				internal sealed class Sample
				{
					/// <summary>The parts <br>.</summary>
					/// <list type="table">
					/// <item>
					/// <term>One</term>
					/// <description>The first.</description>
					/// </item>
					/// <listheader>
					/// <term>Name</term>
					/// <description>Value</description>
					/// </listheader>
					/// </list>
					internal int Bind() => 1;
				}
				""",

			"""
				internal sealed class Sample
				{
					/// <summary>The parts.</summary>
					/// <list type="table">
					/// <item>
					/// <term>One</term>
					/// <description>The first.</description>
					/// </item>
					/// <listheader>
					/// <term>Name</term>
					/// <description>Value</description>
					/// </listheader>
					/// </list>
					internal int Bind() => 1;
				}
				""",
		],
	];

	// Each whole form reports, so a quiet broken form shows that the repair silenced the rule.
	[TestMethod]
	[DynamicData(
		nameof(BrokenComments),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ARuleLeavesABrokenCommentAloneAndReportsItsWholeForm(
		string name,
		DiagnosticAnalyzer analyzer,
		string brokenSource,
		string wholeSource
	)
	{
		IReadOnlyDictionary<string, ReportDiagnostic> rule = TestRuleSets.Only(
			analyzer.SupportedDiagnostics.Single()
		);

		AnalyzerRunResult wholeResult = await RoslynTestHarness.ReportAsync(
			wholeSource,
			new RunOptions([analyzer]) { DiagnosticOptions = rule }
		);

		Assert.HasCount(1, wholeResult.Diagnostics, $"{name}: the whole form is not reported.");

		AnalyzerRunResult brokenResult = await RoslynTestHarness.ReportAsync(
			brokenSource,
			new RunOptions([analyzer]) { DiagnosticOptions = rule }
		);

		Assert.IsEmpty(
			brokenResult.Diagnostics,
			$"{name}: a comment the parser had to repair is measured."
		);
	}

	// The parser reads the line after a stray end tag as text, so markup written there nests inside it.
	private const string StrayEndTagComment = """
		internal sealed class Sample
		{
			/// <summary>x</summary>
			/// </summary>
			/// <summary>x null y</summary>
			internal int Bind() => 1;
		}
		""";

	[TestMethod]
	[CrossCutting]
	public async Task TheNullKeywordRuleLeavesATextTokenThatHoldsMarkupAlone()
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkNullKeywordAnalyzer>(
			StrayEndTagComment,
			TestRuleSets.Only(DiagnosticDescriptors.MarkNullKeyword)
		);

		Assert.IsEmpty(result.Diagnostics);
	}
}
