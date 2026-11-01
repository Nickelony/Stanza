using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Commas;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Commas;

[TestClass]
public sealed class AddTrailingCommaTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> AddRuleOnly { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.AddTrailingComma);

	public static IEnumerable<object[]> ListsWithoutTheComma { get; } =
	[
		[
			"an enum",

			"""
				enum Level
				{
					Low,
					High
				}
				""",

			"""
				enum Level
				{
					Low,
					High,
				}
				""",
		],

		[
			"an array initializer",

			"""
				class C
				{
					private int[] _values = new[]
					{
						1,
						2
					};
				}
				""",

			"""
				class C
				{
					private int[] _values = new[]
					{
						1,
						2,
					};
				}
				""",
		],

		[
			"a collection initializer",

			"""
				class C
				{
					private List<int> _values = new List<int>
					{
						1,
						2
					};
				}
				""",

			"""
				class C
				{
					private List<int> _values = new List<int>
					{
						1,
						2,
					};
				}
				""",
		],

		[
			"an object initializer",

			"""
				class C
				{
					private Item _item = new Item
					{
						Name = "a",
						Count = 2
					};
				}
				""",

			"""
				class C
				{
					private Item _item = new Item
					{
						Name = "a",
						Count = 2,
					};
				}
				""",
		],

		[
			"an anonymous object",

			"""
				class C
				{
					private object _item = new
					{
						Name = "a",
						Size = 2
					};
				}
				""",

			"""
				class C
				{
					private object _item = new
					{
						Name = "a",
						Size = 2,
					};
				}
				""",
		],

		[
			"a with initializer",

			"""
				class C
				{
					private Rec Copy(Rec item) =>
						item with
						{
							Name = "a",
							Tags = []
						};
				}
				""",

			"""
				class C
				{
					private Rec Copy(Rec item) =>
						item with
						{
							Name = "a",
							Tags = [],
						};
				}
				""",
		],

		[
			"a collection expression",

			"""
				class C
				{
					private int[] _values =
					[
						1,
						2
					];
				}
				""",

			"""
				class C
				{
					private int[] _values =
					[
						1,
						2,
					];
				}
				""",
		],

		[
			"a switch expression",

			"""
				class C
				{
					private string Name(int value) =>
						value switch
						{
							1 => "one",
							_ => "many"
						};
				}
				""",

			"""
				class C
				{
					private string Name(int value) =>
						value switch
						{
							1 => "one",
							_ => "many",
						};
				}
				""",
		],

		[
			"an index initializer",

			"""
				class C
				{
					private Dictionary<int, int> _map = new Dictionary<int, int>
					{
						[1] = 2,
						[3] = 4
					};
				}
				""",

			"""
				class C
				{
					private Dictionary<int, int> _map = new Dictionary<int, int>
					{
						[1] = 2,
						[3] = 4,
					};
				}
				""",
		],

		[
			"a list with one element",

			"""
				enum Level
				{
					Low
				}
				""",

			"""
				enum Level
				{
					Low,
				}
				""",
		],

		[
			"a nested list",

			"""
				class C
				{
					private int[][] _values = new[]
					{
						new[]
						{
							1,
							2
						},
						new[]
						{
							3
						}
					};
				}
				""",

			"""
				class C
				{
					private int[][] _values = new[]
					{
						new[]
						{
							1,
							2,
						},
						new[]
						{
							3,
						},
					};
				}
				""",
		],

		[
			"a property pattern",

			"""
				class C
				{
					private bool M(Item item) =>
						item is
						{
							Name: "a",
							Count: 2
						};
				}
				""",

			"""
				class C
				{
					private bool M(Item item) =>
						item is
						{
							Name: "a",
							Count: 2,
						};
				}
				""",
		],

		[
			"a list pattern",

			"""
				class C
				{
					private bool M(int[] values) =>
						values is
						[
							1,
							2,
							3
						];
				}
				""",

			"""
				class C
				{
					private bool M(int[] values) =>
						values is
						[
							1,
							2,
							3,
						];
				}
				""",
		],

		[
			"a nested property pattern",

			"""
				class C
				{
					private bool M(Item item) =>
						item is
						{
							Name:
							{
								Length: 1
							},
							Count: 2
						};
				}
				""",

			"""
				class C
				{
					private bool M(Item item) =>
						item is
						{
							Name:
							{
								Length: 1,
							},
							Count: 2,
						};
				}
				""",
		],

		[
			"a nested list pattern",

			"""
				class C
				{
					private bool M(int[][] values) =>
						values is
						[
							[
								1,
								2
							],
							[
								3
							]
						];
				}
				""",

			"""
				class C
				{
					private bool M(int[][] values) =>
						values is
						[
							[
								1,
								2,
							],
							[
								3,
							],
						];
				}
				""",
		],

		[
			"a property pattern with a comment after the last element",

			"""
				class C
				{
					private bool M(Item item) =>
						item is
						{
							Name: "a",
							Count: 2 // two
						};
				}
				""",

			"""
				class C
				{
					private bool M(Item item) =>
						item is
						{
							Name: "a",
							Count: 2, // two
						};
				}
				""",
		],

		[
			"a list pattern with a comment after the last element",

			"""
				class C
				{
					private bool M(int[] values) =>
						values is
						[
							1,
							2 // two
						];
				}
				""",

			"""
				class C
				{
					private bool M(int[] values) =>
						values is
						[
							1,
							2, // two
						];
				}
				""",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(ListsWithoutTheComma),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task TheLastElementOfAListThatClosesOnItsOwnLineTakesTheComma(
		string name,
		string source,
		string fixedSource
	)
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<AddTrailingCommaAnalyzer>(
			source,
			AddRuleOnly
		);

		Assert.IsNotEmpty(result.Diagnostics, name);
		RoslynTestHarness.AssertFixedText(fixedSource, result, name);

		RoslynTestHarness.AssertChangeRidesDiagnostic(result, name);
	}

	public static IEnumerable<object[]> ListsThatStayAsWritten { get; } =
	[
		[
			"a list that ends in a comma",

			"""
				enum Level
				{
					Low,
					High,
				}
				""",
		],

		[
			"a list on one line",

			"""
				class C
				{
					private int[] _values = new[] { 1, 2 };
				}
				""",
		],

		[
			"a list whose closing brace follows the last element",

			"""
				class C
				{
					private int[] _values = new[] {
						1,
						2 };
				}
				""",
		],

		[
			"an empty list",

			"""
				class C
				{
					private int[] _values = new int[]
					{
					};
				}
				""",
		],

		[
			"an argument list",

			"""
				class C
				{
					private void M()
					{
						Call(
							1,
							2
						);
					}
				}
				""",
		],

		[
			"an attribute list",

			"""
				class C
				{
					[InlineData(
						1,
						"a"
					)]
					private void M()
					{
					}
				}
				""",
		],

		[
			"a complex element of a collection initializer",

			"""
				class C
				{
					private Dictionary<int, int> _map = new Dictionary<int, int>
					{
						{
							1,
							2
						},
					};
				}
				""",
		],

		[
			"a positional pattern",

			"""
				class C
				{
					private bool M((int, int) pair) =>
						pair is
						(
							1,
							2
						);
				}
				""",
		],

		[
			"a property pattern on one line",

			"""
				class C
				{
					private bool M(Item item) =>
						item is { Name: "a", Count: 2 };
				}
				""",
		],

		[
			"a list pattern on one line",

			"""
				class C
				{
					private bool M(int[] values) =>
						values is [1, 2];
				}
				""",
		],

		[
			"a property pattern whose closing brace follows the last element",

			"""
				class C
				{
					private bool M(Item item) =>
						item is {
							Name: "a",
							Count: 2 };
				}
				""",
		],

		[
			"a list pattern whose closing bracket follows the last element",

			"""
				class C
				{
					private bool M(int[] values) =>
						values is [
							1,
							2 ];
				}
				""",
		],

		[
			"an empty property pattern",

			"""
				class C
				{
					private bool M(Item item) =>
						item is
						{
						};
				}
				""",
		],

		[
			"an empty list pattern",

			"""
				class C
				{
					private bool M(int[] values) =>
						values is
						[
						];
				}
				""",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(ListsThatStayAsWritten),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AListThatNeedsNoCommaIsLeftAlone(string name, string source)
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<AddTrailingCommaAnalyzer>(
			source,
			AddRuleOnly
		);

		Assert.IsEmpty(result.Diagnostics, name);
	}

	[TestMethod]
	public async Task TheCommaGoesBeforeACommentOnTheLastElement()
	{
		const string source = """
			enum Level
			{
				Low, // the floor
				High // the ceiling
			}
			""";

		const string fixedSource = """
			enum Level
			{
				Low, // the floor
				High, // the ceiling
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<AddTrailingCommaAnalyzer>(
			source,
			AddRuleOnly
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheCommaGoesBeforeADelimitedCommentOnTheLastElement()
	{
		const string source = """
			enum Level
			{
				Low,
				High /* the ceiling */
			}
			""";

		const string fixedSource = """
			enum Level
			{
				Low,
				High, /* the ceiling */
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<AddTrailingCommaAnalyzer>(
			source,
			AddRuleOnly
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentBeforeTheClosingBraceStaysWhereItIs()
	{
		const string source = """
			enum Level
			{
				Low,
				High
				// More levels follow.
			}
			""";

		const string fixedSource = """
			enum Level
			{
				Low,
				High,
				// More levels follow.
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<AddTrailingCommaAnalyzer>(
			source,
			AddRuleOnly
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARegionMarkerInTheListStaysWhereItIs()
	{
		const string source = """
			enum Level
			{
				Low,
				#region Upper
				High
				#endregion
			}
			""";

		const string fixedSource = """
			enum Level
			{
				Low,
				#region Upper
				High,
				#endregion
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<AddTrailingCommaAnalyzer>(
			source,
			AddRuleOnly
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommaAfterTheLastElementOfAnEnumWithAnExplicitValueIsWritten()
	{
		const string source = """
			enum Level
			{
				Low = 1,
				High = Low + 1
			}
			""";

		const string fixedSource = """
			enum Level
			{
				Low = 1,
				High = Low + 1,
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<AddTrailingCommaAnalyzer>(
			source,
			AddRuleOnly
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	public static IEnumerable<object[]> ListsWhoseLastElementDependsOnTheView { get; } =
	[
		[
			"an element behind a directive",

			"""
				enum Level
				{
					Low,
				#if A
					High
				#endif
				}
				""",
		],

		[
			"a comma behind a directive",

			"""
				enum Level
				{
					Low
				#if A
					,
					High
				#endif
				}
				""",
		],

		[
			"a branch that ends each way",

			"""
				enum Level
				{
					Low,
				#if A
					Middle,
				#else
					High
				#endif
				}
				""",
		],

		[
			"an element before a block that ends the list",

			"""
				enum Level
				{
					Low,
					Middle
				#if A
					, High
				#endif
				}
				""",
		],

		[
			"a directive directly above the closing brace",

			"""
				class C
				{
					private int[] _values = new[]
					{
						1,
						2
				#if A
				#endif
					};
				}
				""",
		],

		[
			"a comma behind a directive in a property pattern",

			"""
				class C
				{
					private bool M(Item item) =>
						item is
						{
							Name: "a"
				#if A
							,
							Count: 2
				#endif
						};
				}
				""",
		],

		[
			"a comma behind a directive in a list pattern",

			"""
				class C
				{
					private bool M(int[] values) =>
						values is
						[
							1
				#if A
							,
							2
				#endif
						];
				}
				""",
		],

		[
			"an element behind a directive in a property pattern",

			"""
				class C
				{
					private bool M(Item item) =>
						item is
						{
							Name: "a",
				#if A
							Count: 2
				#endif
						};
				}
				""",
		],

		[
			"an element behind a directive in a list pattern",

			"""
				class C
				{
					private bool M(int[] values) =>
						values is
						[
							1,
				#if A
							2
				#endif
						];
				}
				""",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(ListsWhoseLastElementDependsOnTheView),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AListWhoseLastElementDependsOnTheViewStaysAsWrittenInEveryView(
		string name,
		string source
	)
	{
		foreach (string symbols in new[] { string.Empty, "#define A\n" })
		{
			AnalyzerRunResult result = await RoslynTestHarness.RunAsync<AddTrailingCommaAnalyzer>(
				symbols + source,
				AddRuleOnly
			);

			Assert.IsEmpty(result.Diagnostics, $"{name} is judged with [{symbols.Trim()}].");
		}
	}

	[TestMethod]
	public async Task ADirectiveAboveTheLastElementLeavesTheCommaToEveryView()
	{
		const string source = """
			enum Level
			{
				Low,
			#if A
				Middle,
			#endif
				High
			}
			""";

		const string fixedSource = """
			enum Level
			{
				Low,
			#if A
				Middle,
			#endif
				High,
			}
			""";

		foreach (string symbols in new[] { string.Empty, "#define A\n" })
		{
			AnalyzerRunResult result = await RoslynTestHarness.RunAsync<AddTrailingCommaAnalyzer>(
				symbols + source,
				AddRuleOnly
			);

			Assert.HasCount(1, result.Diagnostics, $"The list is judged with [{symbols.Trim()}].");
			RoslynTestHarness.AssertFixedText(symbols + fixedSource, result);
		}
	}

	[TestMethod]
	public async Task AListInsideAConditionalBlockIsJudgedByItself()
	{
		const string source = """
			#if A
			enum Level
			{
				Low,
				High
			}
			#endif
			""";

		const string fixedSource = """
			#if A
			enum Level
			{
				Low,
				High,
			}
			#endif
			""";

		foreach (string symbols in new[] { string.Empty, "#define A\n" })
		{
			AnalyzerRunResult result = await RoslynTestHarness.RunAsync<AddTrailingCommaAnalyzer>(
				symbols + source,
				AddRuleOnly
			);

			// A view that skips the block never parses the list, so it reports nothing.
			if (symbols.Length == 0)
			{
				Assert.IsEmpty(result.Diagnostics);
				continue;
			}

			Assert.HasCount(1, result.Diagnostics);
			RoslynTestHarness.AssertFixedText(symbols + fixedSource, result);
		}
	}

	[TestMethod]
	public async Task ACommaFixWorksWithoutTheChangeOfTheDiagnostic()
	{
		const string source = """
			enum Level
			{
				Low,
				High
			}
			""";

		const string fixedSource = """
			enum Level
			{
				Low,
				High,
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<AddTrailingCommaAnalyzer>(
				source,
				AddRuleOnly
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AListWithASyntaxErrorIsLeftAlone()
	{
		const string source = """
			enum Level
			{
				Low,
				High
			""";

		ImmutableArray<Diagnostic> diagnostics = await RoslynTestHarness.RunWithoutCompilingAsync(
			source,
			AddRuleOnly
		);

		Assert.IsEmpty(diagnostics);
	}
}
