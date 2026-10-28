using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class SeparateCommentedSiblingTests
{
	// Each row is one kind of container, with a comment directly above its second unit.
	public static IEnumerable<object[]> Containers { get; } =
	[
		[
			"statements",

			"""
			class C
			{
				void M()
				{
					Prepare();
					// Run the step.
					Run();
				}

				void Prepare() { }

				void Run() { }
			}
			""",

			"""
			class C
			{
				void M()
				{
					Prepare();

					// Run the step.
					Run();
				}

				void Prepare() { }

				void Run() { }
			}
			""",
		],

		[
			"members",

			"""
			class C
			{
				int first;
				// The second field.
				int second;
			}
			""",

			"""
			class C
			{
				int first;

				// The second field.
				int second;
			}
			""",
		],

		[
			"namespace members",

			"""
			namespace N
			{
				class First { }
				// The second type.
				class Second { }
			}
			""",

			"""
			namespace N
			{
				class First { }

				// The second type.
				class Second { }
			}
			""",
		],

		[
			"enum members",

			"""
			enum Mode
			{
				Off,
				// Not used yet.
				On,
			}
			""",

			"""
			enum Mode
			{
				Off,

				// Not used yet.
				On,
			}
			""",
		],

		[
			"initializer members with commented-out code",

			"""
			using System.Collections.Generic;

			class C
			{
				Dictionary<string, int> map = new Dictionary<string, int>
				{
					["a"] = 1,
					// ["b"] = 2,
					["c"] = 3,
				};
			}
			""",

			"""
			using System.Collections.Generic;

			class C
			{
				Dictionary<string, int> map = new Dictionary<string, int>
				{
					["a"] = 1,

					// ["b"] = 2,
					["c"] = 3,
				};
			}
			""",
		],

		[
			"collection elements",

			"""
			class C
			{
				int[] numbers = new[]
				{
					1,
					// Skip two.
					3,
				};
			}
			""",

			"""
			class C
			{
				int[] numbers = new[]
				{
					1,

					// Skip two.
					3,
				};
			}
			""",
		],

		[
			"arguments",

			"""
			class C
			{
				void M()
				{
					Call(
						1,
						// The second argument.
						2
					);
				}

				void Call(int first, int second) { }
			}
			""",

			"""
			class C
			{
				void M()
				{
					Call(
						1,

						// The second argument.
						2
					);
				}

				void Call(int first, int second) { }
			}
			""",
		],

		[
			"parameters",

			"""
			class C
			{
				void M(
					int first,
					// The second parameter.
					int second
				) { }
			}
			""",

			"""
			class C
			{
				void M(
					int first,

					// The second parameter.
					int second
				) { }
			}
			""",
		],

		[
			"switch sections",

			"""
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1: return;
						// The other value.
						case 2: return;
					}
				}
			}
			""",

			"""
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1: return;

						// The other value.
						case 2: return;
					}
				}
			}
			""",
		],

		[
			"switch expression arms",

			"""
			class C
			{
				string M(int value)
				{
					return value switch
					{
						1 => "one",
						// The other value.
						_ => "other",
					};
				}
			}
			""",

			"""
			class C
			{
				string M(int value)
				{
					return value switch
					{
						1 => "one",

						// The other value.
						_ => "other",
					};
				}
			}
			""",
		],

		[
			"accessors",

			"""
			class C
			{
				int _value;

				int Value
				{
					get { return _value; }
					// The setter.
					set { _value = value; }
				}
			}
			""",

			"""
			class C
			{
				int _value;

				int Value
				{
					get { return _value; }

					// The setter.
					set { _value = value; }
				}
			}
			""",
		],

		[
			"declarators",

			"""
			class C
			{
				void M()
				{
					int first = 1,
						// The second one.
						second = 2;
				}
			}
			""",

			"""
			class C
			{
				void M()
				{
					int first = 1,

						// The second one.
						second = 2;
				}
			}
			""",
		],

		[
			"import directives",

			"""
			using System;
			// The text helpers.
			using System.Text;

			class C { }
			""",

			"""
			using System;

			// The text helpers.
			using System.Text;

			class C { }
			""",
		],

		[
			"a delimited comment",

			"""
			class C
			{
				void M()
				{
					Prepare();
					/* Run the step. */
					Run();
				}

				void Prepare() { }

				void Run() { }
			}
			""",

			"""
			class C
			{
				void M()
				{
					Prepare();

					/* Run the step. */
					Run();
				}

				void Prepare() { }

				void Run() { }
			}
			""",
		],

		[
			"a documentation comment",

			"""
			class C
			{
				int first;
				/// <summary>The second field.</summary>
				int second;
			}
			""",

			"""
			class C
			{
				int first;

				/// <summary>The second field.</summary>
				int second;
			}
			""",
		],

		[
			"a delimited documentation comment",

			"""
			class C
			{
				int first;
				/** <summary>The second field.</summary> */
				int second;
			}
			""",

			"""
			class C
			{
				int first;

				/** <summary>The second field.</summary> */
				int second;
			}
			""",
		],

		[
			"a run of comments",

			"""
			class C
			{
				void M()
				{
					Prepare();
					// Run the step,
					// which takes a while.
					Run();
				}

				void Prepare() { }

				void Run() { }
			}
			""",

			"""
			class C
			{
				void M()
				{
					Prepare();

					// Run the step,
					// which takes a while.
					Run();
				}

				void Prepare() { }

				void Run() { }
			}
			""",
		],

		[
			"a comment after a trailing comment",

			"""
			class C
			{
				void M()
				{
					Prepare(); // Prepared.
					// Run the step.
					Run();
				}

				void Prepare() { }

				void Run() { }
			}
			""",

			"""
			class C
			{
				void M()
				{
					Prepare(); // Prepared.

					// Run the step.
					Run();
				}

				void Prepare() { }

				void Run() { }
			}
			""",
		],

		[
			"a comment after the separator comment of a list",

			"""
			class C
			{
				void M()
				{
					Call(
						1, // The first argument.
						// The second argument.
						2
					);
				}

				void Call(int first, int second) { }
			}
			""",

			"""
			class C
			{
				void M()
				{
					Call(
						1, // The first argument.

						// The second argument.
						2
					);
				}

				void Call(int first, int second) { }
			}
			""",
		],
	];

	// Each row is a gap the rule has to leave as written.
	public static IEnumerable<object[]> QuietGaps { get; } =
	[
		[
			"the first statement of a block",

			"""
			class C
			{
				void M()
				{
					// Prepare first.
					Prepare();
				}

				void Prepare() { }
			}
			""",
		],

		[
			"the first statement after a case label",

			"""
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1:
							// Prepare first.
							Prepare();
							break;
					}
				}

				void Prepare() { }
			}
			""",
		],

		[
			"the first member of a type",

			"""
			class C
			{
				// The first field.
				int first;
			}
			""",
		],

		[
			"the first initializer member",

			"""
			class C
			{
				object item = new
				{
					// The first member.
					First = 1,
					Second = 2,
				};
			}
			""",
		],

		[
			"a file header above the first import directive",

			"""
			// The header of this file.
			using System;

			class C { }
			""",
		],

		[
			"a gap that already has a blank line above the comment",

			"""
			class C
			{
				void M()
				{
					Prepare();

					// Run the step.
					Run();
				}

				void Prepare() { }

				void Run() { }
			}
			""",
		],

		[
			"a directive in the gap",

			"""
			class C
			{
				void M()
				{
					Prepare();
			#if DEBUG
					// Run the step.
					Run();
			#endif
				}

				void Prepare() { }

				void Run() { }
			}
			""",
		],

		[
			"a region marker in the gap",

			"""
			class C
			{
				int first;
			#region Second
				// The second field.
				int second;
			#endregion
			}
			""",
		],

		[
			"a multi-line unit before the comment",

			"""
			class C
			{
				void M()
				{
					Call(
						1);
					// Run the step.
					Run();
				}

				void Call(int value) { }

				void Run() { }
			}
			""",
		],

		[
			"a multi-line unit after the comment",

			"""
			class C
			{
				void M()
				{
					Run();
					// Call the step.
					Call(
						1);
				}

				void Call(int value) { }

				void Run() { }
			}
			""",
		],

		[
			"a comment that trails the previous unit",

			"""
			class C
			{
				void M()
				{
					Prepare(); // Prepared.
					Run();
				}

				void Prepare() { }

				void Run() { }
			}
			""",
		],

		[
			"a comment that trails the separator of the previous unit",

			"""
			class C
			{
				void M()
				{
					Call(
						1, // The first argument.
						2
					);
				}

				void Call(int first, int second) { }
			}
			""",
		],

		[
			"a comment with a blank line between it and the unit",

			"""
			class C
			{
				void M()
				{
					Prepare();
					// Not about the step below.

					Run();
				}

				void Prepare() { }

				void Run() { }
			}
			""",
		],

		[
			"a comment on the line of the unit",

			"""
			class C
			{
				void M()
				{
					Prepare();
					/* Run the step. */ Run();
				}

				void Prepare() { }

				void Run() { }
			}
			""",
		],

		[
			"a documentation comment with a blank line between it and the unit",

			"""
			class C
			{
				int first;
				/// <summary>The second field.</summary>

				int second;
			}
			""",
		],

		[
			"siblings with no comment",

			"""
			class C
			{
				void M()
				{
					Prepare();
					Run();
				}

				void Prepare() { }

				void Run() { }
			}
			""",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(Containers),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ABlankLineGoesAboveTheCommentOfASingleLineSibling(
		string name,
		string source,
		string fixedSource
	)
	{
		AnalyzerRunResult result = await RunAsync(source);

		Assert.HasCount(1, result.Diagnostics, name);
		RoslynTestHarness.AssertFixedText(fixedSource, result, name);
	}

	[TestMethod]
	[DynamicData(
		nameof(QuietGaps),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AGapThatIsNotAMissingBlankLineAboveACommentIsLeftAlone(
		string name,
		string source
	)
	{
		AnalyzerRunResult result = await RunAsync(source);
		Assert.IsEmpty(result.Diagnostics, name);
	}

	[TestMethod]
	public async Task EachCommentedSiblingOfAContainerIsReportedAndFixed()
	{
		const string source = """
			class C
			{
				void M()
				{
					Prepare();
					// Run the step.
					Run();
					// Finish the step.
					Finish();
				}

				void Prepare() { }

				void Run() { }

				void Finish() { }
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					Prepare();

					// Run the step.
					Run();

					// Finish the step.
					Finish();
				}

				void Prepare() { }

				void Run() { }

				void Finish() { }
			}
			""";

		AnalyzerRunResult result = await RunAsync(source);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheReportStandsOnTheFirstTokenOfTheCommentedSibling()
	{
		const string source = """
			class C
			{
				void M()
				{
					Prepare();
					// Run the step.
					Run();
				}

				void Prepare() { }

				void Run() { }
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateCommentedSiblingAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.SeparateCommentedSibling)
			);

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual(6, result.Diagnostics[0].Location.GetLineSpan().StartLinePosition.Line);

		Assert.AreEqual(
			"Prepare();",
			source.Substring(result.Diagnostics[0].AdditionalLocations[0].SourceSpan.Start, 10)
		);
	}

	// A grouping rule that asks for the same blank line reports the gap too, and either fix writes it once.
	[TestMethod]
	public async Task AGroupingRuleThatWantsTheSameBlankLineSettlesOnOne()
	{
		const string source = """
			class C
			{
				void M()
				{
					int first = 1;
					int second = 2;
					// Run the step.
					Run();
				}

				void Run() { }
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					int first = 1;
					int second = 2;

					// Run the step.
					Run();
				}

				void Run() { }
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns,
				DiagnosticDescriptors.SeparateCommentedSibling
			)
		);

		Assert.HasCount(1, result.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.SeparateCommentedSibling.Id,
			result.Diagnostics[0].Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AGroupingRuleReportsTheGapAsBeforeWhereThisRuleIsOff()
	{
		const string source = """
			class C
			{
				void M()
				{
					int first = 1;
					int second = 2;
					// Run the step.
					Run();
				}

				void Run() { }
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					int first = 1;
					int second = 2;

					// Run the step.
					Run();
				}

				void Run() { }
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns)
		);

		Assert.HasCount(1, result.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns.Id,
			result.Diagnostics[0].Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnImportRuleThatWantsTheSameBlankLineSettlesOnOne()
	{
		const string source = """
			using System;
			// Say hello.
			Helpers.DoThing();
			""";

		const string fixedSource = """
			using System;

			// Say hello.
			Helpers.DoThing();
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.SeparateImportSection,
				DiagnosticDescriptors.SeparateCommentedSibling
			)
		);

		Assert.HasCount(1, result.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.SeparateCommentedSibling.Id,
			result.Diagnostics[0].Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// Each row is a grouping rule that asks for the blank line above a commented sibling,
	// with the rule that would write the same line turned off, so only the rule in the row defers.
	public static IEnumerable<object?[]> DeferringRules { get; } =
	[
		[
			DiagnosticDescriptors.SeparateMemberGroups,
			null,

			"""
			class C
			{
				public void First() { }
				public void Second() { }
				// The property.
				public int Third { get; set; }
			}
			""",

			"""
			class C
			{
				public void First() { }
				public void Second() { }

				// The property.
				public int Third { get; set; }
			}
			""",
		],

		[
			DiagnosticDescriptors.SeparateModifierGroups,
			null,

			"""
			class C
			{
				public static void First() { }
				public static void Second() { }
				// The instance method.
				public void Third() { }
			}
			""",

			"""
			class C
			{
				public static void First() { }
				public static void Second() { }

				// The instance method.
				public void Third() { }
			}
			""",
		],

		[
			DiagnosticDescriptors.SeparateSameTypeDeclarationGroups,
			DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns,

			"""
			class C
			{
				void M()
				{
					int first = 1;
					int second = 2;
					// The text.
					string third = "x";
				}
			}
			""",

			"""
			class C
			{
				void M()
				{
					int first = 1;
					int second = 2;

					// The text.
					string third = "x";
				}
			}
			""",
		],

		[
			DiagnosticDescriptors.SeparateImportDirectiveGroups,
			null,

			"""
			using System;
			// The static imports.
			using static System.Math;

			class C { }
			""",

			"""
			using System;

			// The static imports.
			using static System.Math;

			class C { }
			""",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(DeferringRules),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task EachGroupingRuleLeavesTheGapAboveACommentedSiblingToThisRule(
		DiagnosticDescriptor grouping,
		DiagnosticDescriptor? turnedOff,
		string source,
		string fixedSource
	)
	{
		Dictionary<string, ReportDiagnostic> rules = new(
			TestRuleSets.Only(grouping, DiagnosticDescriptors.SeparateCommentedSibling),
			StringComparer.Ordinal
		);

		if (turnedOff is not null)
			rules[turnedOff.Id] = ReportDiagnostic.Suppress;

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source, rules);

		Assert.HasCount(1, result.Diagnostics, grouping.Id);

		Assert.AreEqual(
			DiagnosticDescriptors.SeparateCommentedSibling.Id,
			result.Diagnostics[0].Id,
			grouping.Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result, grouping.Id);
	}

	[TestMethod]
	public async Task APragmaOverThisRuleHandsTheGapBackToTheTightRule()
	{
		string source = $$"""
			class C
			{
				int[] numbers = new[]
				{
			#pragma warning disable {{DiagnosticDescriptors.SeparateCommentedSibling.Id}}
					1,

					// Skip two.
					2,
			#pragma warning restore {{DiagnosticDescriptors.SeparateCommentedSibling.Id}}
				};
			}
			""";

		string fixedSource = $$"""
			class C
			{
				int[] numbers = new[]
				{
			#pragma warning disable {{DiagnosticDescriptors.SeparateCommentedSibling.Id}}
					1,
					// Skip two.
					2,
			#pragma warning restore {{DiagnosticDescriptors.SeparateCommentedSibling.Id}}
				};
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.KeepSingleLineCollectionElementsTogether,
				DiagnosticDescriptors.SeparateCommentedSibling
			)
		);

		Assert.HasCount(1, result.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.KeepSingleLineCollectionElementsTogether.Id,
			result.Diagnostics[0].Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// A build that writes no documentation file parses a documentation comment as a plain one, and it counts all the same.
	[TestMethod]
	[DataRow(DocumentationMode.Parse)]
	[DataRow(DocumentationMode.None)]
	public async Task ADocumentationCommentCountsWhetherOrNotTheBuildParsesIt(
		DocumentationMode mode
	)
	{
		using IDisposable parse = RoslynTestHarness.AtDocumentationMode(mode);

		const string source = """
			class C
			{
				int first;
				/// <summary>The second field.</summary>
				int second;
			}
			""";

		const string fixedSource = """
			class C
			{
				int first;

				/// <summary>The second field.</summary>
				int second;
			}
			""";

		AnalyzerRunResult result = await RunAsync(source);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	private static Task<AnalyzerRunResult> RunAsync(string source)
	{
		return RoslynTestHarness.RunAsync<SeparateCommentedSiblingAnalyzer>(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.SeparateCommentedSibling)
		);
	}
}
