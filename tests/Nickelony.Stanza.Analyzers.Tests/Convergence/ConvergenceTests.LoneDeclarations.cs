using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Convergence;

public sealed partial class ConvergenceTests
{
	// Whichever grouping rules are on, the fix order of a modifier run that a type splits writes one text.
	[TestMethod]
	public async Task AModifierRunThatATypeSplitsSettlesInEveryStateOfTheGroupingRules()
	{
		const string source = """
			class C
			{
				private static int _a;
				private int _b;
				private string _c;
				private string _d;
			}
			""";

		// Only these four rules form or own a gap among fields, as the others judge statements, so they are the states.
		// The invocation rule stays on in every state as a canary: it has no run here to write.
		DiagnosticDescriptor[] groupingRules =
		[
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateMemberGroups,
			DiagnosticDescriptors.SeparateModifierGroups,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
		];

		await RoslynTestHarness.AssertEveryStateSettlesAsync(
			source,
			toggled: groupingRules,
			alwaysOn: [DiagnosticDescriptors.SeparateInvocationRuns]
		);
	}

	// One fix of the gap below a lone declaration writes the gap above it too, as the blank line below ends the ask for that gap.
	public static IEnumerable<object[]> LoneDeclarationRows { get; } =
	[
		[
			"a const above a run of calls",

			"""
				class C
				{
					void M()
					{
						Baz();
						const int a = 1;
						Foo(a);
						Foo(a);
					}

					void Baz() { }

					void Foo(int value) { }
				}
				""",

			"""
				class C
				{
					void M()
					{
						Baz();

						const int a = 1;

						Foo(a);
						Foo(a);
					}

					void Baz() { }

					void Foo(int value) { }
				}
				""",
		],

		[
			"a var above a run of calls with a later declaration",

			"""
				class C
				{
					void M()
					{
						Foo(0);
						var a = 1;
						Foo(a);
						Foo(a);
						var b = 1;
					}

					void Foo(int value) { }
				}
				""",

			"""
				class C
				{
					void M()
					{
						Foo(0);

						var a = 1;

						Foo(a);
						Foo(a);

						var b = 1;
					}

					void Foo(int value) { }
				}
				""",
		],

		[
			"a const that a split leaves alone above a field run",

			"""
				class C
				{
					private string _a;
					private string _b;
					private int _c;
					private const int K = 1;
					public string D;
					private string _e;
				}
				""",

			"""
				class C
				{
					private string _a;
					private string _b;

					private int _c;

					private const int K = 1;

					public string D;
					private string _e;
				}
				""",
		],

		[
			"a const in a top-level program",

			"""
				DoThing();
				const int a = 1;
				Call(a, 1);
				Call(a, 2);
				""",

			"""
				DoThing();

				const int a = 1;

				Call(a, 1);
				Call(a, 2);
				""",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(LoneDeclarationRows),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ALoneDeclarationSettlesOnOneTextInEveryFixOrder(
		string name,
		string source,
		string fixedSource
	)
	{
		// The same-type rule splits a run of declarations that some rows hold, so it is on.
		var options = new RunOptions(RoslynTestHarness.AllAnalyzers)
		{
			DiagnosticOptions = TestRuleSets.Only(
				DiagnosticDescriptors.SeparateSameTypeDeclarationRuns
			),
		};

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			expected: fixedSource,
			source,
			options,
			name
		);
	}

	// The stanza a tight lone declaration reads is the same in every fix order, so the loose run rule gathers the same gaps.
	[TestMethod]
	public async Task ALoneDeclarationLeavesTheLooseRunRuleOneText()
	{
		const string source = """
			class C
			{
				void M()
				{
					Foo();

					Bar();
					var a = 1;
					int b = 2;
					int c = 3;
				}

				void Foo() { }

				void Bar() { }
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					Foo();
					Bar();

					var a = 1;

					int b = 2;
					int c = 3;
				}

				void Foo() { }

				void Bar() { }
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = OnSuppressingShippedStatementRules([
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateDeclarationRuns,
		]);

		Dictionary<string, string> options = new() { ["stanza_max_loose_run_size"] = "1" };

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			expected: fixedSource,
			source,

			options: new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = rules,
				AnalyzerConfigOptions = options,
			}
		);
	}

	// A pragma silences the gap above and a directive stands in it, so neither gap above can take a blank line from the fix below.
	public static IEnumerable<object[]> LoneDeclarationWithOneGapRows { get; } =
	[
		[
			"a pragma over the gap above",

			$$"""
				class C
				{
					void M()
					{
				#pragma warning disable {{DiagnosticDescriptors.SeparateInvocationRuns.Id}}
						Baz();
						int a =
				#pragma warning restore {{DiagnosticDescriptors.SeparateInvocationRuns.Id}}
							1;
						Foo(a);
						Foo(a);
					}

					void Baz() { }

					void Foo(int value) { }
				}
				""",

			$$"""
				class C
				{
					void M()
					{
				#pragma warning disable {{DiagnosticDescriptors.SeparateInvocationRuns.Id}}
						Baz();
						int a =
				#pragma warning restore {{DiagnosticDescriptors.SeparateInvocationRuns.Id}}
							1;

						Foo(a);
						Foo(a);
					}

					void Baz() { }

					void Foo(int value) { }
				}
				""",
		],

		[
			"a directive in the gap above",

			"""
				class C
				{
					void M()
					{
						Baz();
				#nullable enable
						var a = 1;
						Foo(a);
						Foo(a);
					}

					void Baz() { }

					void Foo(int value) { }
				}
				""",

			"""
				class C
				{
					void M()
					{
						Baz();
				#nullable enable
						var a = 1;

						Foo(a);
						Foo(a);
					}

					void Baz() { }

					void Foo(int value) { }
				}
				""",
		],

		[
			"a blank line that stands in the gap above",

			"""
				class C
				{
					void M()
					{
						Baz();

						var a = 1;
						Foo(a);
						Foo(a);
					}

					void Baz() { }

					void Foo(int value) { }
				}
				""",

			"""
				class C
				{
					void M()
					{
						Baz();

						var a = 1;

						Foo(a);
						Foo(a);
					}

					void Baz() { }

					void Foo(int value) { }
				}
				""",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(LoneDeclarationWithOneGapRows),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AFixBelowALoneDeclarationLeavesAGapAboveItThatItCannotWrite(
		string name,
		string source,
		string fixedSource
	)
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,

			new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = OnSuppressingShippedStatementRules([
					DiagnosticDescriptors.SeparateInvocationRuns,
				]),

				FixesBottomFirst = true,
			}
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result, $"{name}:");
	}

	// The unit above the declaration stands in one view only, so each view reads its own gaps and writes one text.
	[TestMethod]
	[DataRow("")]
	[DataRow("#define VIEW\n")]
	public async Task BothViewsOfALoneDeclarationWriteOneText(string prefix)
	{
		string source =
			prefix
			+ """
				class C
				{
					void M()
					{
				#if VIEW
						Baz();
				#endif
						var a = 1;
						Foo(a);
						Foo(a);
					}

					void Baz() { }

					void Foo(int value) { }
				}
				""";

		string fixedSource =
			prefix
			+ """
				class C
				{
					void M()
					{
				#if VIEW
						Baz();
				#endif
						var a = 1;

						Foo(a);
						Foo(a);
					}

					void Baz() { }

					void Foo(int value) { }
				}
				""";

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,

			new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = OnSuppressingShippedStatementRules([
					DiagnosticDescriptors.SeparateInvocationRuns,
				]),

				FixesBottomFirst = true,
			}
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
