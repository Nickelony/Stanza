using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Analyzers.Braces;
using Nickelony.Stanza.Analyzers.Regions;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed class CollapseExtraBlankLinesTests
{
	private const string CapKey = "stanza_max_blank_lines";

	private static IReadOnlyDictionary<string, ReportDiagnostic> RuleOnly { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.CollapseExtraBlankLines);

	private static string Lf { get; } = ((char)10).ToString();

	[TestMethod]
	public async Task ARunPastTheCapIsCutBackToIt()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total;



				private int _count;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private int _total;

				private int _count;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<CollapseExtraBlankLinesAnalyzer>(source, RuleOnly);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheReportedSpanHoldsTheFirstLinesOfTheRun()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total;



				private int _count;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<CollapseExtraBlankLinesAnalyzer>(source, RuleOnly);

		FileLinePositionSpan span = result.Diagnostics.Single().Location.GetLineSpan();

		// The run holds the lines at indexes 3 to 5, and the last one stays.
		Assert.AreEqual(3, span.StartLinePosition.Line);
		Assert.AreEqual(0, span.StartLinePosition.Character);
		Assert.AreEqual(5, span.EndLinePosition.Line);
		Assert.AreEqual(0, span.EndLinePosition.Character);
	}

	[TestMethod]
	public async Task ARunAtTheCapIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total;

				private int _count;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<CollapseExtraBlankLinesAnalyzer>(source, RuleOnly);

		Assert.IsEmpty(result.Diagnostics, "A run of one blank line is cut.");
	}

	[TestMethod]
	public async Task ACapOfTwoKeepsTwoBlankLines()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total;



				private int _count;


				private int _sum;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private int _total;


				private int _count;


				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<CollapseExtraBlankLinesAnalyzer>(
				source,
				RuleOnly,
				new Dictionary<string, string> { [CapKey] = "2" }
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACapOfZeroIsNoCap()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total;





				private int _count;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<CollapseExtraBlankLinesAnalyzer>(
				source,
				RuleOnly,
				new Dictionary<string, string> { [CapKey] = "0" }
			);

		Assert.IsEmpty(result.Diagnostics, "A cap of zero cuts a run.");
	}

	[TestMethod]
	public async Task ALineOfWhitespaceCountsAsABlankLine()
	{
		string source = string.Join(
			Lf,
			"internal sealed class Sample",
			"{",
			"\tprivate int _total;",
			"\t",
			"  ",
			"\t \t",
			"\tprivate int _count;",
			"}",
			string.Empty
		);

		string fixedSource = string.Join(
			Lf,
			"internal sealed class Sample",
			"{",
			"\tprivate int _total;",
			"\t \t",
			"\tprivate int _count;",
			"}",
			string.Empty
		);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<CollapseExtraBlankLinesAnalyzer>(source, RuleOnly);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentSplitsARunIntoTwoThatEachKeepTheCap()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total;


				// The count.


				private int _count;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private int _total;

				// The count.

				private int _count;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<CollapseExtraBlankLinesAnalyzer>(source, RuleOnly);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ADirectiveSplitsARunIntoTwoThatEachKeepTheCap()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total;


			#nullable enable


				private string _name = "";
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private int _total;

			#nullable enable

				private string _name = "";
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<CollapseExtraBlankLinesAnalyzer>(source, RuleOnly);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARunAtTheStartOfTheFileIsCut()
	{
		string source = string.Join(
			Lf,
			string.Empty,
			string.Empty,
			string.Empty,
			"class C { }",
			""
		);

		string fixedSource = string.Join(Lf, string.Empty, "class C { }", string.Empty);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<CollapseExtraBlankLinesAnalyzer>(source, RuleOnly);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARunAtTheEndOfTheFileIsCut()
	{
		string source = string.Join(Lf, "class C { }", string.Empty, string.Empty, string.Empty);
		string fixedSource = string.Join(Lf, "class C { }", string.Empty, string.Empty);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<CollapseExtraBlankLinesAnalyzer>(source, RuleOnly);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARunInsideAListOfArgumentsIsCut()
	{
		const string source = """
			internal sealed class Sample
			{
				private static void Call(int first, int second) { }

				private static void Run()
				{
					Call(
						1,



						2);
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private static void Call(int first, int second) { }

				private static void Run()
				{
					Call(
						1,

						2);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<CollapseExtraBlankLinesAnalyzer>(source, RuleOnly);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task BlankLinesThatAreTextRatherThanLayoutStayAsWritten()
	{
		const string source = """"
			internal sealed class Sample
			{
				private const string Verbatim = @"a



			b";

				private const string Raw = """
					a



					b
					""";

				private const string Interpolated = $"""
					a



					{Verbatim}
					""";

				/* a



				b */
				private int _block;

				/** a



				b */
				private int _documented;

			#if NEVER



				private int _disabled;



			#endif
				private int _enabled;
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<CollapseExtraBlankLinesAnalyzer>(source, RuleOnly);

		Assert.IsEmpty(result.Diagnostics, "A blank line inside text is cut.");
	}

	[TestMethod]
	public async Task ARunBesideDisabledTextIsCutOutsideIt()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total;



			#if NEVER



				private int _disabled;
			#endif



				private int _count;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private int _total;

			#if NEVER



				private int _disabled;
			#endif

				private int _count;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<CollapseExtraBlankLinesAnalyzer>(source, RuleOnly);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ASilencedRunIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total;

			#pragma warning disable ZA0056



				private int _count;
			#pragma warning restore ZA0056
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<CollapseExtraBlankLinesAnalyzer>(source, RuleOnly);

		Assert.IsEmpty(result.Diagnostics, "A pragma does not silence the rule.");
	}

	[TestMethod]
	public async Task ARuleThatRemovesTheWholeGapAndTheCapSettleOnOneTextInEveryFixOrder()
	{
		const string source = """
			internal sealed class Sample
			{



				private int _total;



				#region Empty



				#endregion



				private int _count;

				private void Run(bool ready, int first, int second, int third)
				{



					if (ready)
					{



						first = 1;



						second = 2;



						third = 3;



						first = 4;
					}



					else
					{
					}



				}
			}
			""";

		RunOptions options = new([
			new CollapseExtraBlankLinesAnalyzer(),
			new RemoveBracePaddingAnalyzer(),
			new CollapseEmptyBlocksAnalyzer(),
			new GatherRunOfLooseStatementsAnalyzer(),
			new KeepClauseKeywordsAdjacentAnalyzer(),
			new RemoveEmptyRegionsAnalyzer(),
		])
		{
			DiagnosticOptions = TestRuleSets.Only(
				DiagnosticDescriptors.CollapseExtraBlankLines,
				DiagnosticDescriptors.RemoveBracePadding,
				DiagnosticDescriptors.CollapseEmptyBlocks,
				DiagnosticDescriptors.GatherRunOfLooseStatements,
				DiagnosticDescriptors.KeepClauseKeywordsAdjacent,
				DiagnosticDescriptors.RemoveEmptyRegions
			),
		};

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source, options);

		const string fixedSource = """
			internal sealed class Sample
			{
				private int _total;

				private int _count;

				private void Run(bool ready, int first, int second, int third)
				{
					if (ready)
					{
						first = 1;
						second = 2;
						third = 3;
						first = 4;
					}
					else
					{ }

				}
			}
			""";

		RoslynTestHarness.AssertFixedText(fixedSource, result);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			expected: result.FixedText,
			source,
			options
		);
	}
}
