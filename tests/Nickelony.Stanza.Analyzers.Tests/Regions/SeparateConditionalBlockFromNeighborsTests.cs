using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Regions;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Regions;

[TestClass]
public sealed class SeparateConditionalBlockFromNeighborsTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> BlockRuleOnly { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors);

	[TestMethod]
	public async Task TheGapsAboveAnIfAndBelowItsEndifArePadded()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total;
				#if TRACE
				private int _trace;
				#endif
				private int _sum;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private int _total;

				#if TRACE
				private int _trace;
				#endif

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);

		RoslynTestHarness.AssertChangeRidesDiagnostic(result);
	}

	[TestMethod]
	public async Task ASeparatedBlockIsNotReportedAgain()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total;

				#if TRACE
				private int _trace;
				#endif

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics, "A separated block is separated again.");
	}

	[TestMethod]
	public async Task TheBranchesAndTheLinesBesideElifAndElseStayAsWritten()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total;

				#if A
				private int _a;
				#elif B
				private int _b;
				#else
				private int _c;
				#endif

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics, "Only the outside of the block is judged.");
	}

	[TestMethod]
	public async Task ABlockIsSeparatedFromTheCodeButNotFromTheBracesOfItsContainer()
	{
		const string source = """
			internal sealed class Sample
			{
				private void M()
				{
					#if TRACE
					DoThing();
					#endif
				}

				private void N()
				{
					DoThing();
					#if TRACE
					DoThing();
					#endif
					DoThing();
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private void M()
				{
					#if TRACE
					DoThing();
					#endif
				}

				private void N()
				{
					DoThing();

					#if TRACE
					DoThing();
					#endif

					DoThing();
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABlockThatEndsItsContainerIsStillSeparatedAbove()
	{
		const string source = """
			#define TRACE
			internal sealed class Sample
			{
				private int _total;
				#if TRACE
				private int _trace;
				#endif
			}
			""";

		const string fixedSource = """
			#define TRACE
			internal sealed class Sample
			{
				private int _total;

				#if TRACE
				private int _trace;
				#endif
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABlockWhoseBranchTheViewSkipsIsSeparatedAsInTheOtherView()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total;
				#if TRACE
				private int _trace;
				#endif
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private int _total;

				#if TRACE
				private int _trace;
				#endif
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABlockBesideAnotherDirectiveKeepsThatGapTight()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total;

				#if A
				private int _a;
				#endif
				#if B
				private int _b;
				#endif
				private int _sum;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private int _total;

				#if A
				private int _a;
				#endif
				#if B
				private int _b;
				#endif

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics, "The #endif above the #if B keeps the gap tight.");
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnIfBesideAnotherDirectiveIsNotSeparatedFromIt()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total;

				#nullable enable
				#if A
				private int _a;
				#endif

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics, "The directive above the #if keeps the gap tight.");
	}

	[TestMethod]
	public async Task ACommentBeforeAnIfTakesTheBlankLine()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total;
				// Only when tracing.
				#if TRACE
				private int _trace;
				#endif
				private int _sum;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private int _total;

				// Only when tracing.
				#if TRACE
				private int _trace;
				#endif

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABlockAroundStatementsAndUsingDirectivesIsSeparated()
	{
		const string source = """
			using System;
			#if TRACE
			using System.Diagnostics;
			#endif
			using System.Text;

			internal sealed class Sample
			{
				private void M()
				{
					DoThing();
					#if TRACE
					DoThing();
					#endif
					DoThing();
				}
			}
			""";

		const string fixedSource = """
			using System;

			#if TRACE
			using System.Diagnostics;
			#endif

			using System.Text;

			internal sealed class Sample
			{
				private void M()
				{
					DoThing();

					#if TRACE
					DoThing();
					#endif

					DoThing();
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.HasCount(4, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABlockOfTopLevelStatementsIsSeparated()
	{
		const string source = """
			DoThing();
			#if TRACE
			DoThing();
			#endif
			DoThing();
			""";

		const string fixedSource = """
			DoThing();

			#if TRACE
			DoThing();
			#endif

			DoThing();
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheMembersOfAnEnumAreUnitsOfTheirContainer()
	{
		const string source = """
			internal enum Level
			{
				Low,
				#if TRACE
				Trace,
				#endif
				High,
			}
			""";

		const string fixedSource = """
			internal enum Level
			{
				Low,

				#if TRACE
				Trace,
				#endif

				High,
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("an argument list", "Call(\n1,\n#if A\n2\n#else\n3\n#endif\n);")]
	[DataRow("an array initializer", "int[] values =\n{\n1,\n#if A\n2,\n#endif\n3,\n};")]
	[DataRow(
		"an object initializer",
		"var item = new Item\n{\nName = \"x\",\n#if A\nCount = 2,\n#endif\nTags = [],\n};"
	)]
	[DataRow("a collection expression", "int[] values =\n[\n1,\n#if A\n2,\n#endif\n3,\n];")]
	public async Task ABlockInsideAListKeepsItsGap(string name, string body)
	{
		string source =
			"#define A\ninternal sealed class Sample\n{\n\tprivate void M(bool done, int value)\n\t{\n"
			+ body
			+ "\n\t}\n}\n";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics, $"The block inside {name} keeps its gap.");
	}

	[TestMethod]
	public async Task ABlockInsideAParameterListKeepsItsGap()
	{
		const string source = """
			internal sealed class Sample
			{
				private void M(
					int first,
			#if TRACE
					int second,
			#endif
					int third
				) { }
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics, "The block inside a parameter list keeps its gap.");
	}

	[TestMethod]
	public async Task ABlockThatOpensBeforeAnElseIsLeftWholeInTheViewThatHoldsTheElse()
	{
		const string source = """
			#define TRACE
			internal sealed class Sample
			{
				private void M(bool done)
				{
					if (done)
					{
					}
			#if TRACE
					else
					{
					}
			#endif
					DoThing();
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics, "The #endif alone is no reason to judge the block.");
	}

	[TestMethod]
	public async Task ABlockNestedInDisabledTextIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total;
			#if X
				private int _a;
			#if Y
				private int _b;
			#endif
				private int _c;
			#endif
				private int _sum;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private int _total;

			#if X
				private int _a;
			#if Y
				private int _b;
			#endif
				private int _c;
			#endif

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABlockNestedInActiveCodeIsJudgedInTheViewThatActivatesIt()
	{
		const string source = """
			#define X
			internal sealed class Sample
			{
				private int _total;
				#if X
				private int _a;
				#if Y
				private int _b;
				#endif
				private int _c;
				#endif
				private int _sum;
			}
			""";

		const string fixedSource = """
			#define X
			internal sealed class Sample
			{
				private int _total;

				#if X
				private int _a;

				#if Y
				private int _b;
				#endif

				private int _c;
				#endif

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.HasCount(4, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABlockAtTheEdgesOfAFileHasNoNeighborToSeparateFrom()
	{
		const string source = """
			#if TRACE
			internal sealed class Traced { }
			#endif
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ABlockWithoutItsEndifIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total;
				#if TRACE
				private int _trace;
				private int _sum;
			}
			""";

		// The source is no code the compiler accepts, and a run over every analyzer fails if one throws on it.
		ImmutableArray<Diagnostic> diagnostics = await RoslynTestHarness.RunWithoutCompilingAsync(
			source,
			BlockRuleOnly
		);

		Assert.IsEmpty(diagnostics, "A block that never closes is no block to judge.");
	}

	[TestMethod]
	public async Task ABlockFixWorksWithoutTheAdditionalLocation()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total;

				#if TRACE
				private int _trace;
				#endif
				private int _sum;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private int _total;

				#if TRACE
				private int _trace;
				#endif

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
				source,
				BlockRuleOnly
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task ABlockGapTakesTheEndingOfTheLineAboveIt()
	{
		const string source =
			"class C\r\n{\r\n\tint a;\r\n\t#if X\n\tint b;\n\t#endif\n\tint c;\r\n}\r\n";

		const string fixedSource =
			"class C\r\n{\r\n\tint a;\r\n\r\n\t#if X\n\tint b;\n\t#endif\n\n\tint c;\r\n}\r\n";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
				source,
				BlockRuleOnly
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// Each row is a block that holds part of a unit in one view or the other.
	public static IEnumerable<object[]> PartialBlocks { get; } =
	[
		[
			"an else clause",

			"""
				internal sealed class Sample
				{
					private void M(bool done)
					{
						if (done)
						{
						}
				#if A
						else
						{
						}
				#endif
						DoThing();
					}
				}
				""",
		],

		[
			"an attribute list",

			"""
				internal sealed class Sample
				{
					private int _total;
				#if A
					[System.Obsolete]
				#endif
					private void M() { }
				}
				""",
		],

		[
			"a loop head",

			"""
				internal sealed class Sample
				{
					private void M()
					{
						DoThing();
				#if A
						for (;;)
				#endif
						{
							DoThing();
						}
					}
				}
				""",
		],

		[
			"a finally clause",

			"""
				internal sealed class Sample
				{
					private void M()
					{
						try
						{
							DoThing();
						}
						catch
						{
						}
				#if A
						finally
						{
						}
				#endif
						DoThing();
					}
				}
				""",
		],

		[
			"an if head with an alternative head",

			"""
				internal sealed class Sample
				{
					private void M(bool first, bool second)
					{
						DoThing();
				#if A
						if (first)
				#else
						if (second)
				#endif
						{
							DoThing();
						}
					}
				}
				""",
		],

		[
			"an elif branch that holds a head",

			"""
				internal sealed class Sample
				{
					private void M(bool first)
					{
						DoThing();
				#if A
						DoThing();
				#elif B
						if (first)
				#else
						DoThing();
				#endif
						{
							DoThing();
						}
					}
				}
				""",
		],

		[
			"a disabled else branch that holds an attribute list",

			"""
				internal sealed class Sample
				{
					private int _total;
				#if A
					private int _extra;
				#else
					[System.Obsolete]
				#endif
					private void M() { }
				}
				""",
		],

		[
			"a second label of a switch section",

			"""
				internal sealed class Sample
				{
					private void M(int value)
					{
						switch (value)
						{
							case 1:
				#if A
							case 2:
				#endif
								break;
						}
					}
				}
				""",
		],

		[
			"a nested block that holds an attribute list",

			"""
				internal sealed class Sample
				{
					private int _total;
				#if A
				#if B
					[System.Obsolete]
				#endif
				#endif
					private void M() { }
				}
				""",
		],

		[
			"a statement head and its closing brace in two blocks",

			"""
				internal sealed class Sample
				{
					private void M(bool done)
					{
						DoThing();
				#if A
						if (done)
						{
				#endif
						DoThing();
				#if A
						}
				#endif
						DoThing();
					}
				}
				""",
		],

		[
			"a namespace head and its closing brace in two blocks",

			"""
				#if A
				namespace N
				{
				#endif
				internal sealed class Sample { }
				#if A
				}
				#endif
				""",
		],

		[
			"a closing brace and an else head",

			"""
				internal sealed class Sample
				{
					private void M(bool done)
					{
						if (done)
						{
							DoThing();
				#if A
						}
						else
						{
				#endif
							DoThing();
						}
					}
				}
				""",
		],

		[
			"a statement that crosses a clause but ends a statement",

			"""
				internal sealed class Sample
				{
					private void M(bool done)
					{
						if (done)
						{
							DoThing();
				#if A
							DoThing();
						}
						else
						{
							DoThing();
				#endif
						}
					}
				}
				""",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(PartialBlocks),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ABlockThatHoldsPartOfAUnitKeepsItsGapsInEveryView(string name, string source)
	{
		foreach (string symbols in new[] { string.Empty, "#define A\n", "#define A\n#define B\n" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
					symbols + source,
					BlockRuleOnly
				);

			Assert.IsEmpty(result.Diagnostics, $"{name} keeps its gaps with [{symbols.Trim()}].");
		}
	}

	[TestMethod]
	public async Task ABlockWhoseDisabledBranchesHoldWholeUnitsIsSeparatedInEveryView()
	{
		const string source = """
			internal sealed class Sample
			{
				private void M(bool done)
				{
					DoThing();
			#if A
					if (done)
					{
					}
			#elif B
					DoThing();
			#else
					// Nothing here.
			#endif
					DoThing();
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private void M(bool done)
				{
					DoThing();

			#if A
					if (done)
					{
					}
			#elif B
					DoThing();
			#else
					// Nothing here.
			#endif

					DoThing();
				}
			}
			""";

		foreach (string symbols in new[] { string.Empty, "#define A\n", "#define B\n" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
					symbols + source,
					BlockRuleOnly
				);

			Assert.HasCount(2, result.Diagnostics, $"The block is judged with [{symbols.Trim()}].");
			RoslynTestHarness.AssertFixedText(symbols + fixedSource, result);
		}
	}

	[TestMethod]
	public async Task ABlockWhoseBranchesHoldWholeTypesAndMethodsIsSeparatedInEveryView()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total;
			#if A
				private void M()
				{
					DoThing();
				}
			#else
				private sealed class Inner
				{
				}
			#endif
				private int _sum;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private int _total;

			#if A
				private void M()
				{
					DoThing();
				}
			#else
				private sealed class Inner
				{
				}
			#endif

				private int _sum;
			}
			""";

		foreach (string symbols in new[] { string.Empty, "#define A\n" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
					symbols + source,
					BlockRuleOnly
				);

			Assert.HasCount(2, result.Diagnostics, $"The block is judged with [{symbols.Trim()}].");
			RoslynTestHarness.AssertFixedText(symbols + fixedSource, result);
		}
	}

	[TestMethod]
	public async Task ABlockWhoseActiveBranchHoldsAWholeBlockIsSeparatedWithTheBlockInside()
	{
		const string source = """
			#define A
			#define B
			internal sealed class Sample
			{
				private int _total;
			#if A
				private void M()
				{
					DoThing();
			#if B
					DoThing();
			#endif
				}
			#endif
				private int _sum;
			}
			""";

		const string fixedSource = """
			#define A
			#define B
			internal sealed class Sample
			{
				private int _total;

			#if A
				private void M()
				{
					DoThing();

			#if B
					DoThing();
			#endif
				}
			#endif

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABlockThatEndsTheLastMemberOfAnEnumIsSeparatedInEveryView()
	{
		const string source = """
			internal enum Sample
			{
				One,
			#if A
				Two
			#endif
			}
			""";

		const string fixedSource = """
			internal enum Sample
			{
				One,

			#if A
				Two
			#endif
			}
			""";

		foreach (string symbols in new[] { string.Empty, "#define A\n" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
					symbols + source,
					BlockRuleOnly
				);

			Assert.HasCount(1, result.Diagnostics, $"The block is judged with [{symbols.Trim()}].");
			RoslynTestHarness.AssertFixedText(symbols + fixedSource, result);
		}
	}

	[TestMethod]
	[DataRow("Two = 2")]
	[DataRow("Two = 1 << 1")]
	[DataRow("Two = One")]
	[DataRow("Two = (1 + 1)")]
	public async Task ALastEnumMemberWithAValueIsSeparatedInEveryView(string member)
	{
		string source = $$"""
			internal enum Sample
			{
				One,
			#if A
				{{member}}
			#endif
			}
			""";

		string fixedSource = $$"""
			internal enum Sample
			{
				One,

			#if A
				{{member}}
			#endif
			}
			""";

		foreach (string symbols in new[] { string.Empty, "#define A\n" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
					symbols + source,
					BlockRuleOnly
				);

			Assert.HasCount(1, result.Diagnostics, $"{member} is judged with [{symbols.Trim()}].");
			RoslynTestHarness.AssertFixedText(symbols + fixedSource, result);
		}
	}

	[TestMethod]
	public async Task ABranchThatEndsInANameOutsideAnEnumKeepsItsGaps()
	{
		// Only the disabled view compiles, since a statement with no semicolon is no code.
		const string source = """
			internal sealed class Sample
			{
				private int M(int total)
				{
					if (total > 0)
					{
						DoThing();
			#if A
						return total
			#endif
					}
					return 0;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics, "Only an enum's last member ends without a comma.");
	}

	[TestMethod]
	public async Task ADisabledMemberIsSeparatedLikeTheActiveOne()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total;
			#if A
				[System.Obsolete]
				private void M() { }
			#endif
				private int _sum;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private int _total;

			#if A
				[System.Obsolete]
				private void M() { }
			#endif

				private int _sum;
			}
			""";

		foreach (string symbols in new[] { string.Empty, "#define A\n" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
					symbols + source,
					BlockRuleOnly
				);

			Assert.HasCount(2, result.Diagnostics, $"The block is judged with [{symbols.Trim()}].");
			RoslynTestHarness.AssertFixedText(symbols + fixedSource, result);
		}
	}

	[TestMethod]
	public async Task AFirstStatementAfterASwitchLabelIsNotSeparatedFromTheLabel()
	{
		const string source = """
			internal sealed class Sample
			{
				private void M(int value)
				{
					switch (value)
					{
						case 1:
			#if A
							DoThing();
			#endif
							break;
						default:
			#if A
							DoThing();
			#endif
							break;
					}
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private void M(int value)
				{
					switch (value)
					{
						case 1:
			#if A
							DoThing();
			#endif

							break;
						default:
			#if A
							DoThing();
			#endif

							break;
					}
				}
			}
			""";

		foreach (string symbols in new[] { string.Empty, "#define A\n" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
					symbols + source,
					BlockRuleOnly
				);

			Assert.HasCount(
				2,
				result.Diagnostics,
				$"Only the gap below is written with [{symbols.Trim()}]."
			);

			RoslynTestHarness.AssertFixedText(symbols + fixedSource, result);
		}
	}

	[TestMethod]
	public async Task TheLabelOfTheNextSectionEndsTheBlockAboveIt()
	{
		const string source = """
			internal sealed class Sample
			{
				private void M(int value)
				{
					switch (value)
					{
						case 1:
							return;
			#if A
							DoThing();
			#endif
						case 2:
							break;
					}
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private void M(int value)
				{
					switch (value)
					{
						case 1:
							return;

			#if A
							DoThing();
			#endif

						case 2:
							break;
					}
				}
			}
			""";

		foreach (string symbols in new[] { string.Empty, "#define A\n" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
					symbols + source,
					BlockRuleOnly
				);

			Assert.HasCount(2, result.Diagnostics, $"The block is judged with [{symbols.Trim()}].");
			RoslynTestHarness.AssertFixedText(symbols + fixedSource, result);
		}
	}

	[TestMethod]
	public async Task ABlockOfWholeSwitchSectionsIsSeparatedFromTheSectionAboveIt()
	{
		const string source = """
			internal sealed class Sample
			{
				private void M(int value)
				{
					switch (value)
					{
						case 1:
							break;
			#if A
						case 2:
							break;
			#endif
					}
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private void M(int value)
				{
					switch (value)
					{
						case 1:
							break;

			#if A
						case 2:
							break;
			#endif
					}
				}
			}
			""";

		foreach (string symbols in new[] { string.Empty, "#define A\n" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
					symbols + source,
					BlockRuleOnly
				);

			Assert.HasCount(1, result.Diagnostics, $"The block is judged with [{symbols.Trim()}].");
			RoslynTestHarness.AssertFixedText(symbols + fixedSource, result);
		}
	}

	// Reading a block around N fields takes one pass over its tokens, and the read counts each token it takes.
	// A read that finds the next token of each token again from its list takes the tokens before it once more for each,
	// which is billions of steps at this size, where the pass takes a few for each field.
	[TestMethod]
	public void ABlockAroundManyFieldsIsReadInStepsLinearInItsSize()
	{
		const int fieldCount = 40_000;

		(bool isBetweenUnits, long steps) = ReadBlockAroundFields(fieldCount);

		Assert.IsTrue(isBetweenUnits);
		Assert.IsGreaterThan(fieldCount, steps, "the read counted no work.");
		Assert.IsLessThan((long)fieldCount * 20, steps, "the read took too many steps.");
	}

	// Touching blocks are judged as one run, so the run is read once and not once for each block in it.
	[TestMethod]
	public void ARunOfTouchingBlocksIsReadInBytesLinearInItsLength()
	{
		LinearCost.AssertLinear(
			TouchingBlocks(100),
			TouchingBlocks(400),
			static root => ReadEveryDirective(root)
		);
	}

	private static string TouchingBlocks(int blockCount)
	{
		StringBuilder builder = new("#define A\nclass C\n{\n\tint first;\n");

		for (int index = 0; index < blockCount; index++)
			builder.Append("#if A\n\tint _field").Append(index).Append(";\n#endif\n");

		return builder.Append("\tint last;\n}\n").ToString();
	}

	private static void ReadEveryDirective(SyntaxNode root)
	{
		foreach (
			DirectiveTriviaSyntax directive in root.DescendantNodes(descendIntoTrivia: true)
				.OfType<DirectiveTriviaSyntax>()
		)
		{
			if (directive is IfDirectiveTriviaSyntax or EndIfDirectiveTriviaSyntax)
				Assert.IsTrue(ConditionalBlocks.IsBetweenUnits(directive));
		}
	}

	private static (bool IsBetweenUnits, long Steps) ReadBlockAroundFields(int fieldCount)
	{
		StringBuilder builder = new("#define A\nclass C\n{\n\tint first;\n#if A\n");

		for (int index = 0; index < fieldCount; index++)
			builder.Append("\tint _field").Append(index).Append(";\n");

		builder.Append("#endif\n\tint last;\n}\n");

		SyntaxNode root = CSharpSyntaxTree.ParseText(builder.ToString()).GetRoot();

		DirectiveTriviaSyntax directive = root.DescendantNodes(descendIntoTrivia: true)
			.OfType<IfDirectiveTriviaSyntax>()
			.Single();

		ConditionalBlocks.StepsTaken = 0;

		return (ConditionalBlocks.IsBetweenUnits(directive), ConditionalBlocks.StepsTaken);
	}

	[TestMethod]
	public async Task ABlockHoldingTheTwoHalvesOfAnIfInNestedBlocksIsSeparatedInEveryView()
	{
		const string source = """
			internal sealed class Sample
			{
				private void M(bool value)
				{
					DoThing();
			#if A
			#if B
					if (value)
					{
			#endif
						DoThing();
			#if B
					}
			#endif
			#endif
					DoThing();
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private void M(bool value)
				{
					DoThing();

			#if A
			#if B
					if (value)
					{
			#endif
						DoThing();
			#if B
					}
			#endif
			#endif

					DoThing();
				}
			}
			""";

		foreach (
			string symbols in new[]
			{
				string.Empty,
				"#define A\n",
				"#define B\n",
				"#define A\n#define B\n",
			}
		)
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
					symbols + source,
					BlockRuleOnly
				);

			Assert.HasCount(
				2,
				result.Diagnostics,
				$"The outer block is judged with [{symbols.Trim()}]."
			);

			RoslynTestHarness.AssertFixedText(symbols + fixedSource, result);
		}
	}

	[TestMethod]
	public async Task ABlockOfACommaFirstEnumMemberIsSeparatedInEveryView()
	{
		const string source = """
			internal enum Sample
			{
				One
			#if A
				, Two
			#endif
			}
			""";

		const string fixedSource = """
			internal enum Sample
			{
				One

			#if A
				, Two
			#endif
			}
			""";

		foreach (string symbols in new[] { string.Empty, "#define A\n" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
					symbols + source,
					BlockRuleOnly
				);

			Assert.HasCount(1, result.Diagnostics, $"The block is judged with [{symbols.Trim()}].");
			RoslynTestHarness.AssertFixedText(symbols + fixedSource, result);
		}
	}

	[TestMethod]
	public async Task ABlockBesideAnotherBlockThatCompletesAnIfHeadKeepsItsGapsInEveryView()
	{
		const string source = """
			internal sealed class Sample
			{
				private void M(bool value)
				{
					if (value)
			#if A
						DoThing();
			#endif
			#if !A
						DoThing();
			#endif
					DoThing();
				}
			}
			""";

		foreach (string symbols in new[] { string.Empty, "#define A\n" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
					symbols + source,
					BlockRuleOnly
				);

			Assert.IsEmpty(
				result.Diagnostics,
				$"The blocks are left alone with [{symbols.Trim()}]."
			);
		}
	}

	// The second row reads as one section in the view without A, so a block that judged only the view with A would write a gap there alone.
	[TestMethod]
	[DataRow("return;")]
	[DataRow("")]
	public async Task ABlockThatHoldsTheTailOfOneSwitchSectionAndTheHeadOfTheNextKeepsItsGapsInEveryView(
		string firstStatement
	)
	{
		string source = $$"""
			internal sealed class Sample
			{
				private void M(int value)
				{
					switch (value)
					{
						case 1:
							{{firstStatement}}
			#if A
							break;
						case 2:
							DoThing();
							break;
			#endif
						case 3:
							break;
					}
				}
			}
			""";

		foreach (string symbols in new[] { string.Empty, "#define A\n" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
					symbols + source,
					BlockRuleOnly
				);

			Assert.IsEmpty(
				result.Diagnostics,
				$"The block keeps its gaps with [{symbols.Trim()}]."
			);
		}
	}
}
