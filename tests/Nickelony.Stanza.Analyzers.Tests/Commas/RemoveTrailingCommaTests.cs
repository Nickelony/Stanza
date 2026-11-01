using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Commas;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Commas;

[TestClass]
public sealed class RemoveTrailingCommaTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> RemoveRuleOnly { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.RemoveTrailingComma);

	public static IEnumerable<object[]> ListsWithTheComma { get; } =
	[
		[
			"an enum",

			"""
				enum Level
				{
					Low,
					High,
				}
				""",

			"""
				enum Level
				{
					Low,
					High
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
						2,
					};
				}
				""",

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
		],

		[
			"a collection initializer",

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
		],

		[
			"an object initializer",

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
		],

		[
			"an anonymous object",

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
							Tags = [],
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
							Tags = []
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
						2,
					];
				}
				""",

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
							_ => "many",
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
							_ => "many"
						};
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
							2,
						},
						new[]
						{
							3,
						},
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
							2
						},
						new[]
						{
							3
						}
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
							Count: 2,
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
							Count: 2
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
							3,
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
							3
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
								Length: 1,
							},
							Count: 2,
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
								Length: 1
							},
							Count: 2
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
								2,
							],
							[
								3,
							],
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
								2
							],
							[
								3
							]
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
							Count: 2, // two
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
							Count: 2 // two
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
							2, // two
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
							2 // two
						];
				}
				""",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(ListsWithTheComma),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task TheCommaAfterTheLastElementOfAListThatClosesOnItsOwnLineIsRemoved(
		string name,
		string source,
		string fixedSource
	)
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveTrailingCommaAnalyzer>(
			source,
			RemoveRuleOnly
		);

		Assert.IsNotEmpty(result.Diagnostics, name);
		RoslynTestHarness.AssertFixedText(fixedSource, result, name);

		RoslynTestHarness.AssertChangeRidesDiagnostic(result, name);
	}

	public static IEnumerable<object[]> ListsThatStayAsWritten { get; } =
	[
		[
			"a list that ends without a comma",

			"""
				enum Level
				{
					Low,
					High
				}
				""",
		],

		[
			"a list on one line",

			"""
				class C
				{
					private int[] _values = new[] { 1, 2, };
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
						2, };
				}
				""",
		],

		[
			"a comma on a line of its own",

			"""
				class C
				{
					private int[] _values = new[]
					{
						1,
						2
						,
					};
				}
				""",
		],

		[
			"a comma-first list",

			"""
				class C
				{
					private int[] _values = new[]
					{
						1
						, 2
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
						item is { Name: "a", Count: 2, };
				}
				""",
		],

		[
			"a list pattern on one line",

			"""
				class C
				{
					private bool M(int[] values) =>
						values is [1, 2,];
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
							Count: 2, };
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
							2, ];
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
	public async Task AListWithNoCommaToRemoveIsLeftAlone(string name, string source)
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveTrailingCommaAnalyzer>(
			source,
			RemoveRuleOnly
		);

		Assert.IsEmpty(result.Diagnostics, name);
	}

	[TestMethod]
	public async Task ACommentAfterTheCommaStaysWithTheElement()
	{
		const string source = """
			enum Level
			{
				Low, // the floor
				High, // the ceiling
			}
			""";

		const string fixedSource = """
			enum Level
			{
				Low, // the floor
				High // the ceiling
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveTrailingCommaAnalyzer>(
			source,
			RemoveRuleOnly
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentBetweenTheElementAndTheCommaStays()
	{
		const string source = """
			enum Level
			{
				Low,
				High /* the ceiling */,
			}
			""";

		const string fixedSource = """
			enum Level
			{
				Low,
				High /* the ceiling */
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveTrailingCommaAnalyzer>(
			source,
			RemoveRuleOnly
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheBlanksBeforeTheCommaGoWithIt()
	{
		const string source = """
			enum Level
			{
				Low,
				High  ,
			}
			""";

		const string fixedSource = """
			enum Level
			{
				Low,
				High
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveTrailingCommaAnalyzer>(
			source,
			RemoveRuleOnly
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheBlanksBetweenACommentAndTheCommaGoWithTheComma()
	{
		const string source = """
			enum Level
			{
				Low,
				High /* the ceiling */ ,
			}
			""";

		const string fixedSource = """
			enum Level
			{
				Low,
				High /* the ceiling */
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveTrailingCommaAnalyzer>(
			source,
			RemoveRuleOnly
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentRightAfterTheCommaKeepsTheBlankBeforeIt()
	{
		const string source = """
			enum Level
			{
				Low,
				High ,// the ceiling
			}
			""";

		const string fixedSource = """
			enum Level
			{
				Low,
				High // the ceiling
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveTrailingCommaAnalyzer>(
			source,
			RemoveRuleOnly
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
				High,
				// More levels follow.
			}
			""";

		const string fixedSource = """
			enum Level
			{
				Low,
				High
				// More levels follow.
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveTrailingCommaAnalyzer>(
			source,
			RemoveRuleOnly
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
				High,
				#endregion
			}
			""";

		const string fixedSource = """
			enum Level
			{
				Low,
				#region Upper
				High
				#endregion
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveTrailingCommaAnalyzer>(
			source,
			RemoveRuleOnly
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
					High,
				#endif
				}
				""",
		],

		[
			"a comma that is a separator in another view",

			"""
				enum Level
				{
					Low,
					Middle,
				#if A
					High,
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
			"a directive directly above the closing brace",

			"""
				class C
				{
					private int[] _values = new[]
					{
						1,
						2,
				#if A
				#endif
					};
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
							Count: 2,
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
							2,
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
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<RemoveTrailingCommaAnalyzer>(
					symbols + source,
					RemoveRuleOnly
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
				High,
			}
			""";

		const string fixedSource = """
			enum Level
			{
				Low,
			#if A
				Middle,
			#endif
				High
			}
			""";

		foreach (string symbols in new[] { string.Empty, "#define A\n" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<RemoveTrailingCommaAnalyzer>(
					symbols + source,
					RemoveRuleOnly
				);

			Assert.HasCount(1, result.Diagnostics, $"The list is judged with [{symbols.Trim()}].");
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
				High,
			}
			""";

		const string fixedSource = """
			enum Level
			{
				Low,
				High
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<RemoveTrailingCommaAnalyzer>(
				source,
				RemoveRuleOnly
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
				High,
			""";

		ImmutableArray<Diagnostic> diagnostics = await RoslynTestHarness.RunWithoutCompilingAsync(
			source,
			RemoveRuleOnly
		);

		Assert.IsEmpty(diagnostics);
	}
}
