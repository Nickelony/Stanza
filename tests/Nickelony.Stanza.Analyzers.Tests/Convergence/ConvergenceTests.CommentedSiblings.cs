using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Convergence;

public sealed partial class ConvergenceTests
{
	// A grouping rule reads a run across a gap that the commented sibling rule separates, unless the gap ends the run.
	// The blank line the rule writes would then depend on which fix came first.
	public static IEnumerable<object[]> CommentedSiblingRows { get; } =
	[
		[
			"a trailing comment ends the call run above the commented sibling",
			CommentedSiblingRuleSet(),

			"""
				class C
				{
					void M()
					{
						DoThing(); // Trailing.
						// Note.
						DoThing();
						Build(1);
					}

					void DoThing() { }

					int Build(int value) => value;
				}
				""",

			"""
				class C
				{
					void M()
					{
						DoThing(); // Trailing.

						// Note.
						DoThing();
						Build(1);
					}

					void DoThing() { }

					int Build(int value) => value;
				}
				""",
		],

		[
			"a commented sibling ends the call run that stands above the boundary",
			CommentedSiblingRuleSet(),

			"""
				class C
				{
					void M()
					{
						Build(0);
						DoThing();
						// Note.
						DoThing();
					}

					void DoThing() { }

					int Build(int value) => value;
				}
				""",

			"""
				class C
				{
					void M()
					{
						Build(0);
						DoThing();

						// Note.
						DoThing();
					}

					void DoThing() { }

					int Build(int value) => value;
				}
				""",
		],

		[
			"a commented sibling leaves a lone declaration with its neighbors",
			CommentedSiblingRuleSet(),

			"""
				class C
				{
					void M()
					{
						var a = 1;
						// Note.
						var b = 2;
						DoThing();
					}

					void DoThing() { }
				}
				""",

			"""
				class C
				{
					void M()
					{
						var a = 1;

						// Note.
						var b = 2;
						DoThing();
					}

					void DoThing() { }
				}
				""",
		],

		[
			"a commented sibling ends the run of the same-type rule",
			CommentedSiblingRuleSet(TestRuleSets.RunRules),

			"""
				class C
				{
					void M()
					{
						int a = 1;
						// Note.
						int b = 2;
						string c = "x";
					}
				}
				""",

			"""
				class C
				{
					void M()
					{
						int a = 1;

						// Note.
						int b = 2;
						string c = "x";
					}
				}
				""",
		],

		[
			"a commented sibling ends the run of the member rule",
			CommentedSiblingRuleSet(TestRuleSets.RunRules),

			"""
				class C
				{
					private int _a;
					// Note.
					private int _b;
					public void M() { }
				}
				""",

			"""
				class C
				{
					private int _a;

					// Note.
					private int _b;
					public void M() { }
				}
				""",
		],

		[
			"a single-line tight rule that is on takes the blank line above a comment out of its list",

			CommentedSiblingRuleSetWith(
				DiagnosticDescriptors.KeepSingleLineCollectionElementsTogether
			),

			"""
				class C
				{
					int[] numbers = new[]
					{
						1,

						// Note.
						2,
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
						// Note.
						2,
						3,
					};
				}
				""",
		],

		[
			"a single-line tight rule that is on leaves a commented list tight",
			CommentedSiblingRuleSetWith(DiagnosticDescriptors.KeepSingleLineArgumentsTogether),

			"""
				class C
				{
					void M()
					{
						Call(
							1,
							// Note.
							2,
							3
						);
					}

					void Call(int a, int b, int c) { }
				}
				""",

			"""
				class C
				{
					void M()
					{
						Call(
							1,
							// Note.
							2,
							3
						);
					}

					void Call(int a, int b, int c) { }
				}
				""",
		],

		[
			"the two-unit rule that is on leaves the comment of its container without a blank line",
			CommentedSiblingRuleSetWith(DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether),

			"""
				class C
				{
					void M()
					{
						DoThing();
						// Note.
						DoThing();
					}

					void DoThing() { }
				}
				""",

			"""
				class C
				{
					void M()
					{
						DoThing();
						// Note.
						DoThing();
					}

					void DoThing() { }
				}
				""",
		],

		[
			"the two-unit rule that is on keeps the blank line above a comment that stands there",
			CommentedSiblingRuleSetWith(DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether),

			"""
				class C
				{
					void M()
					{
						DoThing();

						// Note.
						DoThing();
					}

					void DoThing() { }
				}
				""",

			"""
				class C
				{
					void M()
					{
						DoThing();

						// Note.
						DoThing();
					}

					void DoThing() { }
				}
				""",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(CommentedSiblingRows),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ACommentedSiblingSettlesTheSameWayInEitherFixOrder(
		string name,
		IReadOnlyDictionary<string, ReportDiagnostic> rules,
		string source,
		string fixedSource
	)
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,
			diagnosticOptions: rules
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result, name);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			expected: fixedSource,
			source,
			options: new RunOptions(RoslynTestHarness.AllAnalyzers) { DiagnosticOptions = rules },
			message: $"{name}:"
		);
	}

	// The commented sibling rule on beside the rules that ship on, less the ones named.
	private static Dictionary<string, ReportDiagnostic> CommentedSiblingRuleSet(
		params DiagnosticDescriptor[] disabled
	)
	{
		Dictionary<string, ReportDiagnostic> rules = new(
			TestRuleSets.Only(DiagnosticDescriptors.SeparateCommentedSibling),
			StringComparer.Ordinal
		);

		foreach (DiagnosticDescriptor descriptor in disabled)
			rules[descriptor.Id] = ReportDiagnostic.Suppress;

		return rules;
	}

	// The commented sibling rule on beside the rules that ship on, and the one tight rule named.
	private static Dictionary<string, ReportDiagnostic> CommentedSiblingRuleSetWith(
		DiagnosticDescriptor tight
	)
	{
		Dictionary<string, ReportDiagnostic> rules = CommentedSiblingRuleSet();

		rules[tight.Id] = ReportDiagnostic.Warn;

		return rules;
	}

	// The commented sibling rule and the documentation comment rule on beside the rules that ship on, less the ones named.
	private static Dictionary<string, ReportDiagnostic> DocumentedCommentedSiblingRuleSet(
		params DiagnosticDescriptor[] disabled
	)
	{
		Dictionary<string, ReportDiagnostic> rules = CommentedSiblingRuleSet(disabled);

		rules[DiagnosticDescriptors.KeepDocumentationCommentWithDeclaration.Id] =
			ReportDiagnostic.Warn;

		return rules;
	}

	// The seeded statements reach these containers only by chance, so each template runs alone as well.
	public static IEnumerable<object[]> CommentedSiblingTemplates { get; } =
	[
		.. SourceGenerators.CommentedSiblings.Select(
			static (sibling, position) => new object[] { sibling.Name, position }
		),
	];

	[TestMethod]
	[DynamicData(
		nameof(CommentedSiblingTemplates),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ACommentedSiblingInEachKindOfContainerIsReportedAndSettles(
		string name,
		int position
	)
	{
		string source = SourceGenerators.WrapStatements(
			SourceGenerators.IndentStatement(
				SourceGenerators.CommentedSiblings[position].Template(0)
			)
		);

		// The rules it defers to ship on and would take the gap, so they stay off.
		AnalyzerRunResult reported = await RoslynTestHarness.ReportAllAsync(
			source,

			TestRuleSets.OnSuppressingOthersOf(
				[DiagnosticDescriptors.SeparateCommentedSibling],
				[.. StanzaRules.SeparateCommentedSibling.DefersTo]
			)
		);

		Assert.IsTrue(
			reported.Diagnostics.Any(static diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.SeparateCommentedSibling.Id
			),

			$"{name}: the commented sibling rule reported nothing."
		);

		_ = await AssertSourceSettlesAsync(source, limit: 1, label: $"{name}:");
	}
}
