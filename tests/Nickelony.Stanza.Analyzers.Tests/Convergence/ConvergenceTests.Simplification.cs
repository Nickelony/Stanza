using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Convergence;

public sealed partial class ConvergenceTests
{
	// Each template is one statement, written without its indent: an else after a jump or an if inside an if.
	// They stand in the shapes that meet the rules beside them: comments, blank lines, directives and nested chains.
	private static readonly Func<int, string[]>[] s_simplifyingStatements =
	[
		index =>
			[
				"if (loose > 0)",
				"{",
				"\treturn;",
				"}",
				"else",
				"{",
				"\tDoThing();",
				$"\tBuild({index});",
				"}",
			],

		index => [$"if (loose > {index})", "\treturn;", "else", "\tDoThing();"],
		index => ["if (loose > 1) return;", "else DoThing();"],
		index => ["if (loose > 2) {", "\treturn;", "} else {", "\tDoThing();", "}"],

		index =>
			[
				"if (loose > 3)",
				"{",
				"\tthrow new System.InvalidOperationException();",
				"}",
				"// Otherwise.",
				"else",
				"{",
				$"\t// Do {index}.",
				"\tDoThing(); // Now.",
				"}",
			],

		index =>
			[
				"if (loose > 5)",
				"{",
				"\treturn;",
				"}",
				"else",
				"{",
				$"\tint local{index} = {index};",
				$"\tBuild(local{index});",
				"}",
			],

		index =>
			[
				"if (loose > 6)",
				"{",
				"\treturn;",
				"}",
				"else",
				"{",
				"\tint shared = 1;",
				"\tBuild(shared);",
				"}",
			],

		index =>
			[
				"if (loose > 7)",
				"{",
				"\treturn;",
				"}",
				"else",
				"{",
				"\tif (loose > 8)",
				"\t{",
				"\t\treturn;",
				"\t}",
				"\telse",
				"\t{",
				"\t\tDoThing();",
				"\t}",
				"}",
			],

		index =>
			[
				"#if !STANZA_SOAK",
				"if (loose > 9)",
				"{",
				"\treturn;",
				"}",
				"else",
				"{",
				"\tDoThing();",
				"}",
				"#endif",
			],

		index =>
			[
				"while (loose > 10)",
				"{",
				"\tif (loose > 11)",
				"\t{",
				"\t\tcontinue;",
				"\t}",
				"\telse",
				"\t{",
				"\t\tloose--;",
				"\t\tDoThing();",
				"\t}",
				"}",
			],

		index =>
			[
				"if (loose > 12)",
				"{",
				"\tif (loose > 13)",
				"\t\tDoThing();",
				"}",
				"else",
				$"\tBuild({index});",
			],

		index =>
			[
				"if (loose > 13)",
				"{",
				"\treturn;",
				"}",
				"else",
				"{",
				$"\tusing var resource{index} = Open();",
				"\tDoThing();",
				"}",
			],

		index =>
			[
				"if (loose > 0)",
				"{",
				"\tif (loose < 100)",
				"\t{",
				"\t\tDoThing();",
				$"\t\tBuild({index});",
				"\t}",
				"}",
			],

		index => ["if (loose > 1)", "\tif (loose < 99)", "\t\tDoThing();"],
		index => ["if (loose > 2) { if (loose < 98) { DoThing(); } }"],

		index =>
			[
				"if (loose > 3)",
				"{",
				"\tif (loose < 97)",
				"\t{",
				"\t\tif (loose != 50)",
				"\t\t{",
				"\t\t\tDoThing();",
				"\t\t}",
				"\t}",
				"}",
			],

		index =>
			[
				"if (loose > 4)",
				"{",
				"\tif (loose < 96)",
				"\t\tDoThing();",
				"\telse",
				$"\t\tBuild({index});",
				"}",
			],

		index =>
			[
				"if (loose > 5)",
				"{",
				"\t// Both hold.",
				"\tif (loose < 95)",
				"\t{",
				"\t\tDoThing();",
				"\t}",
				"}",
			],

		index =>
			[
				$"if (loose is int number{index})",
				"{",
				$"\tif (number{index} > 0)",
				$"\t\tBuild(number{index});",
				"}",
			],

		index =>
			[
				"if (loose > 0 && loose > 1 && loose > 2)",
				"{",
				"\tif (loose > 3 && loose > 4 && loose > 5)",
				"\t\tDoThing();",
				"}",
			],

		index =>
			[
				"if (loose > 6)",
				"{",
				"\tif (loose < 94)",
				"\t{",
				"\t\tif (loose == 1)",
				"\t\t\treturn;",
				"\t\telse",
				"\t\t\tDoThing();",
				"\t}",
				"}",
			],

		// Braces that go leave an else and a while beside their statement, with or without the blank line the clause rules ask about.
		index =>
			["if (loose > 14)", "{", $"\tBuild({index});", "}", "else", "{", "\tDoThing();", "}"],

		index =>
			[
				"if (loose > 15)",
				"{",
				$"\tBuild({index});",
				"}",
				"",
				"else",
				"{",
				"\tDoThing();",
				"}",
			],

		index => ["do", "{", $"\tBuild({index});", "}", "while (loose-- > 16);"],
		index => ["do", "{", $"\tBuild({index});", "}", "", "while (loose-- > 17);"],
	];

	// The rules that rewrite a chain, alone and beside the brace and the stanza rules that judge the same lines.
	private static readonly DiagnosticDescriptor[][] s_simplifyingRuleSets =
	[
		[DiagnosticDescriptors.RemoveElseAfterJump, DiagnosticDescriptors.MergeNestedIfStatements],

		[
			DiagnosticDescriptors.RemoveElseAfterJump,
			DiagnosticDescriptors.MergeNestedIfStatements,
			DiagnosticDescriptors.RemoveRedundantBraces,
		],

		[
			DiagnosticDescriptors.RemoveElseAfterJump,
			DiagnosticDescriptors.MergeNestedIfStatements,
			DiagnosticDescriptors.SeparateClauseKeywordFromBraceBeforeIt,
		],

		[
			DiagnosticDescriptors.RemoveElseAfterJump,
			DiagnosticDescriptors.MergeNestedIfStatements,
			DiagnosticDescriptors.RemoveRedundantBraces,
			DiagnosticDescriptors.SeparateClauseKeywordFromBraceBeforeIt,
		],

		[
			DiagnosticDescriptors.RemoveElseAfterJump,
			DiagnosticDescriptors.MergeNestedIfStatements,
			DiagnosticDescriptors.RemoveRedundantBraces,
			DiagnosticDescriptors.KeepClauseKeywordsAdjacent,
		],

		[
			DiagnosticDescriptors.RemoveElseAfterJump,
			DiagnosticDescriptors.MergeNestedIfStatements,
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateJumpStatement,
			DiagnosticDescriptors.SeparateGuardClauses,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
		],

		[
			DiagnosticDescriptors.RemoveElseAfterJump,
			DiagnosticDescriptors.MergeNestedIfStatements,
			DiagnosticDescriptors.RemoveRedundantBraces,
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateJumpStatement,
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
			DiagnosticDescriptors.SeparateCommentedSibling,
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(Seeds),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ChainsRewrittenBesideTheStanzaRulesSettleInEitherFixOrder(int seed)
	{
		int findings = await AssertSimplifyingRunsSettleAsync(seed);

		Assert.IsGreaterThan(
			0,
			findings,
			"The generated file needed no fix, so it proves nothing."
		);
	}

	// The rules that rewrite a chain remove tokens, so the check is that the text compiles, settles and keeps its comments.
	private static async Task<int> AssertSimplifyingRunsSettleAsync(int seed)
	{
		string source = CreateSimplifyingSource(new Random(seed));

		Dictionary<string, string> options = new()
		{
			["stanza_max_loose_run_size"] = "3",
			["stanza_guard_roots"] = "Guard",
		};

		int findings = 0;

		for (int set = 0; set < s_simplifyingRuleSets.Length; set++)
		{
			DiagnosticDescriptor[] rules = s_simplifyingRuleSets[set];
			IReadOnlyDictionary<string, ReportDiagnostic> enabled = TestRuleSets.Only(rules);
			string label = $"seed {seed}, rule set {set}:";

			AnalyzerRunResult result = await RunAsync(source, rules: enabled, options, label);

			// The sets that pair the braces rule with a clause rule drop the braces that the clause rules judge, so a Fix All per ID has to match too.
			bool dropsBracesBesideClauseRule =
				Array.IndexOf(rules, DiagnosticDescriptors.RemoveRedundantBraces) >= 0
				&& (
					Array.IndexOf(
						rules,
						DiagnosticDescriptors.SeparateClauseKeywordFromBraceBeforeIt
					) >= 0
					|| Array.IndexOf(rules, DiagnosticDescriptors.KeepClauseKeywordsAdjacent) >= 0
				);

			// The first set, the last and the clause sets contest the most gaps, and a second order for each set would outlast the soak's test timeout.
			if (
				rules == s_simplifyingRuleSets[0]
				|| rules == s_simplifyingRuleSets[^1]
				|| dropsBracesBesideClauseRule
			)
			{
				AnalyzerRunResult bottomFirst = await RoslynTestHarness.RunIterativelyAsync(
					source,

					new RunOptions(RoslynTestHarness.AllAnalyzers)
					{
						DiagnosticOptions = enabled,
						AnalyzerConfigOptions = options,
						FixesBottomFirst = true,
					}
				);

				Assert.AreEqual(
					result.FixedText,
					bottomFirst.FixedText,
					$"{label} fixing the last gap first wrote another text than fixing the first gap first."
				);
			}

			if (dropsBracesBesideClauseRule)
			{
				await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
					expected: result.FixedText,
					source,

					options: new RunOptions(RoslynTestHarness.AllAnalyzers)
					{
						DiagnosticOptions = enabled,
						AnalyzerConfigOptions = options,
					},

					message: label
				);
			}

			Assert.AreSequenceEqual(
				ReadComments(source),
				ReadComments(result.FixedText),
				$"{label} a fix changed a comment."
			);

			findings += result.Diagnostics.Length;
		}

		return findings;
	}

	internal static string CreateSimplifyingSource(Random random)
	{
		List<string> lines = [];
		int count = random.Next(6, 13);

		for (int index = 0; index < count; index++)
		{
			if (index > 0)
				lines.AddRange(SourceGenerators.StatementGap(random));

			// Most statements are the chains, with the loose and jump statements of the other corpus among them.
			Func<int, string[]>[] pool =
				random.Next(4) == 0 ? SourceGenerators.Statements : s_simplifyingStatements;

			lines.AddRange(SourceGenerators.IndentStatement(pool[random.Next(pool.Length)](index)));
		}

		return SourceGenerators.WrapStatements(lines);
	}

	// A chain that a rule rewrites meets the gap rules and the brace rules in either order, and has to end in one text.
	// Each row gives the statements before and after, as the statements of a method that opens with `int loose = 0;`.
	public static IEnumerable<object[]> SimplifyingFixOrderRows { get; } =
	[
		[
			"an else after a jump takes the blank line before it along, and the multi-line rule separates what it freed",
			TestRuleSets.Only(DiagnosticDescriptors.RemoveElseAfterJump),
			"if (loose > 0)\n{\n\treturn;\n}\n\nelse\n{\n\tDoThing();\n}",
			"if (loose > 0)\n{\n\treturn;\n}\n\nDoThing();",
		],

		[
			"an else after a jump goes whether the clause rule asks for a blank line before it",

			TestRuleSets.Only(
				DiagnosticDescriptors.RemoveElseAfterJump,
				DiagnosticDescriptors.SeparateClauseKeywordFromBraceBeforeIt
			),

			"if (loose > 0)\n{\n\treturn;\n}\nelse\n{\n\tDoThing();\n}",
			"if (loose > 0)\n{\n\treturn;\n}\n\nDoThing();",
		],

		[
			"an else after a jump and the braces rule that drops braces end in one text",

			TestRuleSets.Only(
				DiagnosticDescriptors.RemoveElseAfterJump,
				DiagnosticDescriptors.RemoveRedundantBraces
			),

			"if (loose > 0)\n{\n\treturn;\n}\nelse\n{\n\tDoThing();\n}",
			"if (loose > 0)\n\treturn;\n\nDoThing();",
		],

		[
			"an else after a jump waits for the braces rule that wraps the branch before it",
			TestRuleSets.Only(DiagnosticDescriptors.RemoveElseAfterJump),
			"if (loose > 0)\n\treturn;\nelse\n{\n\tDoThing();\n\tBuild(1);\n}",
			"if (loose > 0)\n{\n\treturn;\n}\n\nDoThing();\nBuild(1);",
		],

		[
			"the statements an else frees meet the jump rule beside them",

			TestRuleSets.Only(
				DiagnosticDescriptors.RemoveElseAfterJump,
				DiagnosticDescriptors.SeparateJumpStatement
			),

			"if (loose > 0)\n{\n\treturn;\n}\nelse\n{\n\tloose = 1;\n\tloose = 2;\n\treturn;\n}",
			"if (loose > 0)\n{\n\treturn;\n}\n\nloose = 1;\nloose = 2;\n\nreturn;",
		],

		[
			"the statements an else frees are gathered with the loose run rule",

			TestRuleSets.Only(
				DiagnosticDescriptors.RemoveElseAfterJump,
				DiagnosticDescriptors.GatherRunOfLooseStatements
			),

			"if (loose > 0)\n{\n\treturn;\n}\nelse\n{\n\tloose = 1;\n\n\tloose = 2;\n\n\tloose = 3;\n\n\tloose = 4;\n}",
			"if (loose > 0)\n{\n\treturn;\n}\n\nloose = 1;\nloose = 2;\nloose = 3;\nloose = 4;",
		],

		[
			"a chain of two elses after jumps leaves both blank lines before the elses gone",
			TestRuleSets.Only(DiagnosticDescriptors.RemoveElseAfterJump),
			"if (loose > 0)\n{\n\treturn;\n}\n\nelse\n{\n\tif (loose > 1)\n\t{\n\t\treturn;\n\t}\n\n\telse\n\t{\n\t\tDoThing();\n\t}\n}",
			"if (loose > 0)\n{\n\treturn;\n}\n\nif (loose > 1)\n{\n\treturn;\n}\n\nDoThing();",
		],

		[
			"braces dropped from a chain leave no blank line before the else that the separating clause rule would ask for",

			TestRuleSets.Only(
				DiagnosticDescriptors.RemoveRedundantBraces,
				DiagnosticDescriptors.SeparateClauseKeywordFromBraceBeforeIt
			),

			"if (loose > 0)\n{\n\tDoThing();\n}\nelse\n{\n\tBuild(1);\n}",
			"if (loose > 0)\n\tDoThing();\nelse\n\tBuild(1);",
		],

		[
			"braces dropped from a chain take the blank line before the else along, as the adjacent clause rule would",

			TestRuleSets.Only(
				DiagnosticDescriptors.RemoveRedundantBraces,
				DiagnosticDescriptors.KeepClauseKeywordsAdjacent
			),

			"if (loose > 0)\n{\n\tDoThing();\n}\n\nelse\n{\n\tBuild(1);\n}",
			"if (loose > 0)\n\tDoThing();\nelse\n\tBuild(1);",
		],

		[
			"braces dropped from a chain take the blank line before the else along, whichever clause rule is on",

			TestRuleSets.Only(
				DiagnosticDescriptors.RemoveRedundantBraces,
				DiagnosticDescriptors.SeparateClauseKeywordFromBraceBeforeIt
			),

			"if (loose > 0)\n{\n\tDoThing();\n}\n\nelse\n{\n\tBuild(1);\n}",
			"if (loose > 0)\n\tDoThing();\nelse\n\tBuild(1);",
		],

		[
			"braces dropped from a do loop take the blank line before the while along",

			TestRuleSets.Only(
				DiagnosticDescriptors.RemoveRedundantBraces,
				DiagnosticDescriptors.SeparateClauseKeywordFromBraceBeforeIt
			),

			"do\n{\n\tDoThing();\n}\nwhile (loose > 0);",
			"do\n\tDoThing();\nwhile (loose > 0);",
		],

		[
			"braces dropped from a chain take the blank lines above a comment before the else along, as the adjacent clause rule would",

			TestRuleSets.Only(
				DiagnosticDescriptors.RemoveRedundantBraces,
				DiagnosticDescriptors.KeepClauseKeywordsAdjacent
			),

			"if (loose > 0)\n{\n\tDoThing();\n}\n\n// Otherwise.\nelse\n{\n\tBuild(1);\n}",
			"if (loose > 0)\n\tDoThing();\n// Otherwise.\nelse\n\tBuild(1);",
		],

		[
			"braces dropped from a chain leave no blank line above a comment before the else that the separating clause rule would ask for",

			TestRuleSets.Only(
				DiagnosticDescriptors.RemoveRedundantBraces,
				DiagnosticDescriptors.SeparateClauseKeywordFromBraceBeforeIt
			),

			"if (loose > 0)\n{\n\tDoThing();\n}\n// Otherwise.\nelse\n{\n\tBuild(1);\n}",
			"if (loose > 0)\n\tDoThing();\n// Otherwise.\nelse\n\tBuild(1);",
		],

		[
			"braces dropped from a do loop take the blank line before the while along, as the adjacent clause rule would",

			TestRuleSets.Only(
				DiagnosticDescriptors.RemoveRedundantBraces,
				DiagnosticDescriptors.KeepClauseKeywordsAdjacent
			),

			"do\n{\n\tDoThing();\n}\n\nwhile (loose > 0);",
			"do\n\tDoThing();\nwhile (loose > 0);",
		],

		[
			"merged ifs and the braces rule that drops braces end in one text",

			TestRuleSets.Only(
				DiagnosticDescriptors.MergeNestedIfStatements,
				DiagnosticDescriptors.RemoveRedundantBraces
			),

			"if (loose > 0)\n{\n\tif (loose < 100)\n\t{\n\t\tDoThing();\n\t}\n}",
			"if (loose > 0 && loose < 100)\n\tDoThing();",
		],

		[
			"merged ifs wait for the braces rule that wraps the inner body",
			TestRuleSets.Only(DiagnosticDescriptors.MergeNestedIfStatements),
			"if (loose > 0)\n{\n\tif (loose < 100\n\t\t&& loose != 5)\n\t\tDoThing();\n}",
			"if (loose > 0 && loose < 100\n\t&& loose != 5)\n{\n\tDoThing();\n}",
		],

		[
			"merged ifs keep the blank lines that the multi-line rule asks around the pair",
			TestRuleSets.Only(DiagnosticDescriptors.MergeNestedIfStatements),
			"DoThing();\nif (loose > 0)\n\tif (loose < 100) DoThing();\nBuild(1);",
			"DoThing();\n\nif (loose > 0 && loose < 100) DoThing();\n\nBuild(1);",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(SimplifyingFixOrderRows),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ARewrittenChainSettlesTheSameWayWhicheverFixComesFirst(
		string name,
		IReadOnlyDictionary<string, ReportDiagnostic> rules,
		string statements,
		string fixedStatements
	)
	{
		string source = SourceGenerators.WrapStatements(
			SourceGenerators.IndentStatement(statements.Split('\n'))
		);

		string fixedSource = SourceGenerators.WrapStatements(
			SourceGenerators.IndentStatement(fixedStatements.Split('\n'))
		);

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			expected: fixedSource,
			source,
			options: new RunOptions(RoslynTestHarness.AllAnalyzers) { DiagnosticOptions = rules },
			name
		);
	}
}
