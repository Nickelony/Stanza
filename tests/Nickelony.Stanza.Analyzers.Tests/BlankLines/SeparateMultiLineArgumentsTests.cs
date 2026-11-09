using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed class SeparateMultiLineArgumentsTests
{
	// A comment that trails the comma after the argument stands in the gap, so the blank line goes below it.
	[TestMethod]
	public async Task TheBlankLineGoesBelowAMultiLineCommentAfterTheComma()
	{
		const string source = """
			class C
			{
				void M()
				{
					Call(
						Compute(
							1), /* a
						b */
						2);
				}

				static void Call(int first, int second) { }

				static int Compute(int value) => value;
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					Call(
						Compute(
							1), /* a
						b */

						2);
				}

				static void Call(int first, int second) { }

				static int Compute(int value) => value;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineArgumentsAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.SeparateMultiLineArguments)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// A comma that opens its line belongs to the argument after it, so the blank line goes above the comma.
	[TestMethod]
	public async Task TheBlankLineGoesAboveACommaThatOpensItsLine()
	{
		const string source = """
			class C
			{
				void M()
				{
					Call(
						Compute(
							1)
						, 2
						, 3);
				}

				static void Call(int first, int second, int third) { }

				static int Compute(int value) => value;
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					Call(
						Compute(
							1)

						, 2
						, 3);
				}

				static void Call(int first, int second, int third) { }

				static int Compute(int value) => value;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineArgumentsAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.SeparateMultiLineArguments)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AMultiLineArgumentOnTheLineOfThePreviousOneStartsItsOwnLine()
	{
		const string source =
			"class C\r\n{\r\n\tvoid M()\r\n\t{\r\n\t\tCall(1, Build(\r\n\t\t\t2));\r\n\t}\r\n\r\n\tvoid Call(int first, int second)\r\n\t{\r\n\t}\r\n\r\n\tint Build(int value)\r\n\t{\r\n\t\treturn value;\r\n\t}\r\n}\r\n";

		const string fixedSource =
			"class C\r\n{\r\n\tvoid M()\r\n\t{\r\n\t\tCall(1,\r\n\r\n\t\tBuild(\r\n\t\t\t2));\r\n\t}\r\n\r\n\tvoid Call(int first, int second)\r\n\t{\r\n\t}\r\n\r\n\tint Build(int value)\r\n\t{\r\n\t\treturn value;\r\n\t}\r\n}\r\n";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineArgumentsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheArgumentSpacingFixFindsTheGapWithoutAnAdditionalLocation()
	{
		const string source = """
			class C
			{
				void M()
				{
					Call(
						Build(
							1
							),
						2
						);
				}

				void Call(int first, int second)
				{
				}

				int Build(int value)
				{
					return value;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					Call(
						Build(
							1
							),

						2
						);
				}

				void Call(int first, int second)
				{
				}

				int Build(int value)
				{
					return value;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<SeparateMultiLineArgumentsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task SingleLineArgumentsStayTight()
	{
		const string source = """
			class C
			{
				void M()
				{
					Call(1, 2);

					Call(
						1,
						2
					);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineArgumentsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task MultiLineArgumentsGetSeparated()
	{
		const string source = """
			class C
			{
				void M()
				{
					Call(
						Build(
							1
						),
						Build(
							2
						)
					);
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					Call(
						Build(
							1
						),

						Build(
							2
						)
					);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineArgumentsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task SingleLineArgumentAfterMultiLineArgumentGetsSeparated()
	{
		const string source = """
			class C
			{
				void M()
				{
					Call(
						Build(
							1
						),
						2
					);
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					Call(
						Build(
							1
						),

						2
					);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineArgumentsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task SameLineArgumentsAreSeparated()
	{
		const string source = """
			class C
			{
				void M()
				{
					Call(
						1, Build(
							2
						)
					);
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					Call(
						1,

						Build(
							2
						)
					);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineArgumentsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task IndexerArgumentsGetSeparated()
	{
		const string source = """
			class C
			{
				int M(int[,] cube)
				{
					return cube[
						1, Build(
							2)];
				}

				int Build(int value)
				{
					return value;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				int M(int[,] cube)
				{
					return cube[
						1,

						Build(
							2)];
				}

				int Build(int value)
				{
					return value;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineArgumentsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task MultiLineAttributeArgumentsGetSeparated()
	{
		const string source = """
			class C
			{
				[InlineData(
					1, "a" +
						"b")]
				void M()
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				[InlineData(
					1,

					"a" +
						"b")]
				void M()
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineArgumentsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task CommentBetweenSameLineArgumentsIsPreserved()
	{
		const string source = """
			class C
			{
				void M()
				{
					Call(
						1, /* gap */ Build(
							2)
					);
				}

				void Call(int first, int second)
				{
				}

				int Build(int value)
				{
					return value;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					Call(
						1, /* gap */

						Build(
							2)
					);
				}

				void Call(int first, int second)
				{
				}

				int Build(int value)
				{
					return value;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineArgumentsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task MultiLineTupleElementsGetSeparated()
	{
		const string source = """
			class C
			{
				void M()
				{
					var pair = (
						Build(
							1
						),
						Build(
							2
						)
					);
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					var pair = (
						Build(
							1
						),

						Build(
							2
						)
					);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineArgumentsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);

		// The report stands on the first token of the second element.
		Assert.AreEqual(8, result.Diagnostics[0].Location.GetLineSpan().StartLinePosition.Line);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ATupleElementOnTheLineOfThePreviousOneStartsItsOwnLine()
	{
		const string source = """
			class C
			{
				void M()
				{
					var pair = (1, Build(
						2));
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					var pair = (1,

					Build(
						2));
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineArgumentsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ATupleThatDeconstructsIsSeparatedLikeAnyOther()
	{
		const string source = """
			class C
			{
				void M()
				{
					(int first, int second) = (
						Build(
							1
						),
						2
					);
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					(int first, int second) = (
						Build(
							1
						),

						2
					);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineArgumentsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// A tuple is a list of its own, so a call and the tuples among its arguments each judge their own gaps.
	[TestMethod]
	public async Task ATupleInsideACallIsJudgedByItsOwnElements()
	{
		const string source = """
			class C
			{
				void M()
				{
					Take(
						(
							Build(
								1
							),
							2
						),
						(3, 4)
					);
				}

				void Take((int, int) first, (int, int) second) { }
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					Take(
						(
							Build(
								1
							),

							2
						),

						(3, 4)
					);
				}

				void Take((int, int) first, (int, int) second) { }
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineArgumentsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(2, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// A tuple that opens the second element begins with a parenthesis, as no argument does.
	[TestMethod]
	public async Task ATupleThatOpensTheSecondElementStartsItsOwnGap()
	{
		const string source = """
			class C
			{
				void M()
				{
					var pair = (
						Build(
							1
						),
						(2, 3)
					);
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					var pair = (
						Build(
							1
						),

						(2, 3)
					);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineArgumentsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);

		AnalyzerRunResult withoutLocations =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<SeparateMultiLineArgumentsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, withoutLocations.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, withoutLocations);
	}

	[TestMethod]
	public async Task SingleLineTupleElementsStayTight()
	{
		const string source = """
			class C
			{
				void M()
				{
					var pair = (1, 2);

					var wrapped = (
						1,
						2
					);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineArgumentsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}
}
