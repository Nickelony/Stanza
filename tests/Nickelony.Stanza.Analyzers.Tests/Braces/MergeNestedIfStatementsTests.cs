using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Braces;
using Nickelony.Stanza.Analyzers.Limits;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Braces;

[TestClass]
public sealed class MergeNestedIfStatementsTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> Rule { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.MergeNestedIfStatements);

	// The rule waits while the brace rule has braces to add, so a fixture with an unbraced branch beside a multi-line one runs with that rule off.
	private static IReadOnlyDictionary<string, ReportDiagnostic> RuleWithoutBraces { get; } =
		new Dictionary<string, ReportDiagnostic>(Rule, StringComparer.Ordinal)
		{
			[DiagnosticDescriptors.UseBracesConsistently.Id] = ReportDiagnostic.Suppress,
		};

	// An operand the merged condition has to parenthesize, as `&&` binds tighter than each of them.
	public static IEnumerable<object[]> LooseOperands { get; } =
	[
		["an or", "c0 || c1", "(c0 || c1)"],
		["a conditional", "c0 ? c1 : c2", "(c0 ? c1 : c2)"],
		["an assignment", "c3 = c1", "(c3 = c1)"],
		["a coalesce", "maybe ?? c1", "(maybe ?? c1)"],
	];

	// An operand that binds as tightly as `&&` or tighter, so the merge adds no parentheses.
	public static IEnumerable<object[]> TightOperands { get; } =
	[
		["an and", "c0 && c1", "c0 && c1"],
		["a comparison", "x > 1", "x > 1"],
		["a negation", "!c1", "!c1"],
		["a call", "Flag(c0, c1)", "Flag(c0, c1)"],
		["a pattern", "x is 1 or 2", "x is 1 or 2"],
		["a parenthesis", "(c0 || c1)", "(c0 || c1)"],
		["a member", "other is null", "other is null"],
	];

	// A conditional that stands near the statements, or picks what they hold in another view.
	public static IEnumerable<object[]> DirectiveShapes { get; } =
	[
		[
			"a conditional between the heads",
			"if (c0)\n{\n#if B\n\tDoThing();\n#endif\n\tif (c1)\n\t{\n\t\tDoThing();\n\t}\n}",
		],

		[
			"a conditional inside the body",
			"if (c0)\n{\n\tif (c1)\n\t{\n#if B\n\t\tDoThing();\n#endif\n\t\tCall(1, 2);\n\t}\n}",
		],

		[
			"a conditional around the statement",
			"#if B\nif (c0)\n{\n\tif (c1)\n\t{\n\t\tDoThing();\n\t}\n}\n#endif",
		],

		[
			"a conditional below the statement",
			"if (c0)\n{\n\tif (c1)\n\t{\n\t\tDoThing();\n\t}\n}\n#if B\nCall(1, 2);\n#endif",
		],

		[
			"a conditional around an else",
			"if (c0)\n{\n\tif (c1)\n\t{\n\t\tDoThing();\n\t}\n}\n#if B\nelse\n{\n\tCall(1, 2);\n}\n#endif",
		],

		[
			"a conditional around the inner else",
			"if (c0)\n{\n\tif (c1)\n\t{\n\t\tDoThing();\n\t}\n#if B\n\telse\n\t{\n\t\tCall(1, 2);\n\t}\n#endif\n}",
		],

		[
			"a conditional that picks the inner condition",
			"if (c0)\n{\n\tif (\n#if B\n\t\tc1\n#else\n\t\tc2\n#endif\n\t)\n\t{\n\t\tDoThing();\n\t}\n}",
		],

		[
			"a region around the statement",
			"#region Parts\nif (c0)\n{\n\tif (c1)\n\t{\n\t\tDoThing();\n\t}\n}\n#endregion",
		],

		[
			"a pragma above the statement",
			"#pragma warning disable CS0162\nif (c0)\n{\n\tif (c1)\n\t{\n\t\tDoThing();\n\t}\n}",
		],
	];

	// A comment between the two heads would be left without the line it belongs to.
	public static IEnumerable<object[]> HeadComments { get; } =
	[
		[
			"a comment after the outer condition",
			"if (c0) // Outer.\n{\n\tif (c1)\n\t{\n\t\tDoThing();\n\t}\n}",
		],

		[
			"a comment above the inner if",
			"if (c0)\n{\n\t// Inner.\n\tif (c1)\n\t{\n\t\tDoThing();\n\t}\n}",
		],

		[
			"a comment after the opening brace",
			"if (c0)\n{ // Outer.\n\tif (c1)\n\t{\n\t\tDoThing();\n\t}\n}",
		],

		[
			"a comment inside the inner head",
			"if (c0)\n{\n\tif (/* Inner. */ c1)\n\t{\n\t\tDoThing();\n\t}\n}",
		],

		[
			"a comment after the inner condition",
			"if (c0)\n{\n\tif (c1) // Inner.\n\t{\n\t\tDoThing();\n\t}\n}",
		],

		[
			"a comment between an unbraced head and its body",
			"if (c0)\n\tif (c1)\n\t\t// Body.\n\t\tDoThing();",
		],

		["a delimited comment between the heads", "if (c0) /* Outer. */ if (c1) DoThing();"],
	];

	// The bodies the merged if keeps: each condition names its if, so a moved else shows in IfBindings.
	public static IEnumerable<object[]> InnerBodies { get; } =
	[
		["a block of one call", "{ DoThing(); }"],
		["a block ending in an if without an else", "{ if (c2) DoThing(); }"],
		["a block ending in an if with an else", "{ if (c2) DoThing(); else Call(1, 2); }"],
		["a block holding a call and an if", "{ DoThing(); if (c2) DoThing(); }"],
		["a statement", "DoThing();"],
		["an if with an else", "if (c2) DoThing(); else Call(1, 2);"],
		["a loop around a dangling if", "while (c2) if (c3) DoThing(); else Call(1, 2);"],
	];

	// Where the outer if stands: in a method, in the block of a branch with an else, in an else if, and in a loop.
	private static readonly (string Name, string Format)[] s_wrappers =
	[
		("a method", "{0}"),
		("the block of a branch with an else", "if (c3)\n{\n{0}\n}\nelse\n\tCall(0, 0);"),
		("an else if", "if (c3)\n\tCall(0, 0);\nelse {0}"),
		("a loop", "while (c3)\n{\n{0}\n}"),
	];

	// How the outer and the inner if hold their bodies.
	private static readonly (string Name, string Format)[] s_nestings =
	[
		("both braced", "if (c0)\n{\n\tif (c1)\n\t{0}\n}"),
		("an unbraced inner body", "if (c0)\n{\n\tif (c1)\n\t\t{0}\n}"),
		("an unbraced outer body", "if (c0)\n\tif (c1)\n\t\t{0}"),
		("one line", "if (c0) { if (c1) {0} }"),
	];

	// A name the inner condition declares joins the block around the outer if, so another use of it there may bind to another symbol.
	public static IEnumerable<object[]> NameClashes { get; } =
	[
		["a loop variable below", string.Empty, "foreach (var y in new[] { 1 })\n\tDoThing();"],
		["a use of the field below", string.Empty, "Call(y, 2);"],
		["a use of the field above", "Call(y, 0);\n", string.Empty],
		["a lambda parameter below", string.Empty, "Func<int, int> twice = y => y * 2;"],
		["a use of the field in the outer condition", string.Empty, string.Empty],
	];

	[TestMethod]
	public async Task ABracedIfInsideABracedIfMerges()
	{
		const string source = """
			class C
			{
				void M(bool a, bool b)
				{
					if (a)
					{
						if (b)
						{
							DoThing();
							DoThing();
						}
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool a, bool b)
				{
					if (a && b)
					{
						DoThing();
						DoThing();
					}
				}
			}
			""";

		AnalyzerRunResult result = await RunAsync(source);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnUnbracedInnerBodyMergesAndStaysUnbraced()
	{
		const string source = """
			class C
			{
				void M(bool a, bool b)
				{
					if (a)
					{
						if (b)
							DoThing();
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool a, bool b)
				{
					if (a && b)
						DoThing();
				}
			}
			""";

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	public async Task AnIfAloneInAnUnbracedBodyMerges()
	{
		const string source = """
			class C
			{
				void M(bool a, bool b)
				{
					if (a)
						if (b)
							DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool a, bool b)
				{
					if (a && b)
						DoThing();
				}
			}
			""";

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	public async Task IfsOnOneLineMergeOnOneLine()
	{
		const string source = """
			class C
			{
				void M(bool a, bool b)
				{
					if (a) { if (b) { DoThing(); } }
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool a, bool b)
				{
					if (a && b) { DoThing(); }
				}
			}
			""";

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	public async Task BracesOnTheLineOfTheHeadsMergeAndKeepTheirLayout()
	{
		const string source = """
			class C
			{
				void M(bool a, bool b)
				{
					if (a) {
						if (b) {
							DoThing();
							DoThing();
						}
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool a, bool b)
				{
					if (a && b) {
						DoThing();
						DoThing();
					}
				}
			}
			""";

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	public async Task StatementsIndentedWithSpacesMoveOutByOneStep()
	{
		const string source =
			"class C\n{\n  void M(bool a, bool b)\n  {\n    if (a)\n    {\n      if (b)\n      {\n        DoThing();\n        DoThing();\n      }\n    }\n  }\n}\n";

		const string fixedSource =
			"class C\n{\n  void M(bool a, bool b)\n  {\n    if (a && b)\n    {\n      DoThing();\n      DoThing();\n    }\n  }\n}\n";

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	public async Task AnElseIfMerges()
	{
		const string source = """
			class C
			{
				void M(bool a, bool b, bool c)
				{
					if (c)
						DoThing();
					else if (a)
					{
						if (b)
							Call(1, 2);
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool a, bool b, bool c)
				{
					if (c)
						DoThing();
					else if (a && b)
						Call(1, 2);
				}
			}
			""";

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	public async Task AMultiLineInnerConditionMergesAndKeepsItsContinuationIndentation()
	{
		const string source = """
			class C
			{
				void M(bool a, bool b, bool c)
				{
					if (a)
					{
						if (b
							&& c)
						{
							DoThing();
						}
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool a, bool b, bool c)
				{
					if (a && b
						&& c)
					{
						DoThing();
					}
				}
			}
			""";

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	public async Task TheReportStandsOnTheOuterIfKeyword()
	{
		const string source = """
			class C
			{
				void M(bool a, bool b)
				{
					if (a)
						if (b)
							DoThing();
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<MergeNestedIfStatementsAnalyzer>(
				source,
				RuleWithoutBraces
			);

		Assert.HasCount(1, result.Diagnostics);

		Assert.AreEqual(
			"if",

			source[
				result.Diagnostics[0].Location.SourceSpan.Start..result
					.Diagnostics[0]
					.Location
					.SourceSpan
					.End
			]
		);

		Assert.AreEqual(4, result.Diagnostics[0].Location.GetLineSpan().StartLinePosition.Line);
	}

	[TestMethod]
	[DynamicData(
		nameof(LooseOperands),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AnOperandThatBindsLooserThanAndTakesParentheses(
		string name,
		string operand,
		string written
	)
	{
		string source = Method($"if ({operand})\n{{\n\tif (c2)\n\t\tDoThing();\n}}");
		string fixedSource = Method($"if ({written} && c2)\n\tDoThing();");

		AnalyzerRunResult first = await RunAsync(source);

		Assert.HasCount(1, first.Diagnostics, name);
		RoslynTestHarness.AssertFixedText(fixedSource, first, name);

		string innerSource = Method($"if (c2)\n{{\n\tif ({operand})\n\t\tDoThing();\n}}");
		string innerFixed = Method($"if (c2 && {written})\n\tDoThing();");

		AnalyzerRunResult second = await RunAsync(innerSource);

		Assert.HasCount(1, second.Diagnostics, name);
		RoslynTestHarness.AssertFixedText(innerFixed, second, name);
	}

	[TestMethod]
	[DynamicData(
		nameof(TightOperands),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AnOperandThatBindsAsTightlyAsAndNeedsNoParentheses(
		string name,
		string operand,
		string written
	)
	{
		string source = Method($"if (c2)\n{{\n\tif ({operand})\n\t\tDoThing();\n}}");
		string fixedSource = Method($"if (c2 && {written})\n\tDoThing();");

		AnalyzerRunResult result = await RunAsync(source);

		Assert.HasCount(1, result.Diagnostics, name);
		RoslynTestHarness.AssertFixedText(fixedSource, result, name);
	}

	[TestMethod]
	public async Task AChainOfIfsMergesIntoOneCondition()
	{
		string source = Method(
			"if (c0)\n{\n\tif (c1)\n\t{\n\t\tif (c2)\n\t\t{\n\t\t\tDoThing();\n\t\t}\n\t}\n}"
		);

		string fixedSource = Method("if (c0 && c1 && c2)\n{\n\tDoThing();\n}");

		AnalyzerRunResult result = await RunAsync(source);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AChainThatMixesBracedAndUnbracedBodiesMergesIntoOneCondition()
	{
		string source = Method("if (c0)\n\tif (c1)\n\t{\n\t\tif (c2)\n\t\t\tDoThing();\n\t}");
		string fixedSource = Method("if (c0 && c1 && c2)\n\tDoThing();");

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	public async Task AnIfWithAnElseStaysAndSoDoesOneWithAnElseInside()
	{
		string[] bodies =
		[
			"if (c0)\n{\n\tif (c1)\n\t{\n\t\tDoThing();\n\t}\n}\nelse\n{\n\tCall(1, 2);\n}",
			"if (c0)\n{\n\tif (c1)\n\t{\n\t\tDoThing();\n\t}\n\telse\n\t{\n\t\tCall(1, 2);\n\t}\n}",
			"if (c0)\n\tif (c1)\n\t\tDoThing();\n\telse\n\t\tCall(1, 2);",
			"if (c0)\n{\n\tif (c1)\n\t\tDoThing();\n\n\tCall(1, 2);\n}",
			"if (c0)\n{\n\tCall(1, 2);\n\n\tif (c1)\n\t\tDoThing();\n}",
			"if (c0)\n{\n\tCall(1, 2);\n}",
			"if (c0)\n{\n\twhile (c1)\n\t\tDoThing();\n}",
		];

		foreach (string body in bodies)
			Assert.IsEmpty((await RunAsync(Method(body))).Diagnostics, body);
	}

	[TestMethod]
	[DynamicData(
		nameof(HeadComments),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ACommentThatStandsBetweenTheHeadsLeavesThemAlone(string name, string body) =>
		Assert.IsEmpty((await RunAsync(Method(body))).Diagnostics, name);

	[TestMethod]
	public async Task CommentsOutsideTheHeadsStay()
	{
		const string source = """
			class C
			{
				void M(bool a, bool b)
				{
					// Both hold.
					if (a)
					{
						if (b)
						{
							// Do it.
							DoThing(); // Now.
						} // Inner end.
					} // Outer end.
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool a, bool b)
				{
					// Both hold.
					if (a && b)
					{
						// Do it.
						DoThing(); // Now.
					} // Inner end.
					// Outer end.
				}
			}
			""";

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	public async Task ATokenAfterTheClosingBraceStaysOutsideTheCommentThatEndsTheBody()
	{
		string[] bodies =
		[
			"{\n\t\tCall(1, 2);\n\t} // Note.",
			"{\n\t\tCall(1, 2);\n\t} /* Note. */",
			"{\n\t\tCall(1, 2);\n\t} // Note.\n\t// More.",
			"{\n\t\tCall(1, 2);\n\t}\n\t// Note.",
			"\n\t\tCall(1, 2); // Note.",
			"\n\t\tCall(1, 2); /* Note. */ // More.",
		];

		(string Name, string Format)[] shapes =
		[
			("a statement", "{0}\nDoThing();"),
			("a statement on the line", "{0} DoThing(); Build(1);"),
			("a do loop", "do\n{0} while (c2);"),
			("a block", "{\n\t{0} }"),
		];

		foreach (string body in bodies)
		{
			foreach ((string shapeName, string shapeFormat) in shapes)
			{
				string outer =
					$"if (c0)\n{{\n\tif (c1){(body.StartsWith('{') ? "\n\t" : string.Empty)}{body}\n}}";

				string source = Method(shapeFormat.Replace("{0}", outer, StringComparison.Ordinal));

				AnalyzerRunResult result = await RunAsync(source);
				string row = $"{body} in {shapeName}";

				Assert.HasCount(1, result.Diagnostics, row);
				CodeTokens.AssertNamesKept(source, result.FixedText, row);
			}
		}
	}

	[TestMethod]
	public async Task ATokenAfterTheClosingBraceStartsItsLineWithoutTheSpaceThatSeparatedItFromTheBrace()
	{
		const string source =
			"if (c0)\n{\n\tif (c1)\n\t{\n\t\tCall(1, 2);\n\t} // Inner.\n} DoThing();";

		const string fixedSource = "if (c0 && c1)\n{\n\tCall(1, 2);\n} // Inner.\nDoThing();";

		RoslynTestHarness.AssertFixedText(Method(fixedSource), await RunAsync(Method(source)));
	}

	[TestMethod]
	[DynamicData(
		nameof(DirectiveShapes),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ADirectiveNearTheIfsLeavesThemAlone(string name, string body)
	{
		foreach (string view in new[] { string.Empty, "#define B\n" })
		{
			AnalyzerRunResult result = await RunAsync(view + Method(body));
			Assert.IsEmpty(result.Diagnostics, $"{name}, view '{view.Trim()}'");
		}
	}

	[TestMethod]
	public async Task ADirectiveFarFromTheIfsDoesNotStopTheMerge()
	{
		const string body =
			"#if B\nCall(0, 0);\n#endif\n\nDoThing();\n\nif (c0)\n{\n\tif (c1)\n\t\tCall(1, 2);\n}\n\nDoThing();";

		const string fixedSource =
			"#if B\nCall(0, 0);\n#endif\n\nDoThing();\n\nif (c0 && c1)\n\tCall(1, 2);\n\nDoThing();";

		foreach (string view in new[] { string.Empty, "#define B\n" })
		{
			AnalyzerRunResult result = await RunAsync(view + Method(body));

			Assert.HasCount(1, result.Diagnostics, view);
			RoslynTestHarness.AssertFixedText(view + Method(fixedSource), result, view);
		}
	}

	[TestMethod]
	public async Task AMergeThatStaysWithinTheLimitIsMadeAndOneThatPassesItIsNot()
	{
		// Two operators beside two, plus the one the merge adds, make five, one past the default of four.
		string source = Method("if (c0 && c1 && c2)\n{\n\tif (c1 && c2 && c3)\n\t\tDoThing();\n}");

		Assert.IsEmpty((await RunAsync(source)).Diagnostics);

		string[] fitting =
		[
			"if (c0 && c1)\n{\n\tif (c1 && c2 && c3)\n\t\tDoThing();\n}",
			"if (c0 && c1 && c2)\n{\n\tif (c1 && c2)\n\t\tDoThing();\n}",
			"if (c0 || c1 || c2 || c3)\n{\n\tif (c1)\n\t\tDoThing();\n}",
		];

		foreach (string body in fitting)
		{
			AnalyzerRunResult result = await RunAsync(Method(body));

			Assert.HasCount(1, result.Diagnostics, body);
			Assert.IsEmpty((await ReportLongConditionsAsync(result.FixedText)).Diagnostics, body);
		}
	}

	[TestMethod]
	public async Task TheLimitComesFromTheLongConditionRule()
	{
		string source = Method("if (c0 && c1 && c2)\n{\n\tif (c1 && c2 && c3)\n\t\tDoThing();\n}");

		Assert.AreEqual(0, await CountMergesAsync(source, "4"));
		Assert.AreEqual(1, await CountMergesAsync(source, "5"));
		Assert.AreEqual(1, await CountMergesAsync(source, "0"));

		string pair = Method("if (c0 && c1)\n{\n\tif (c2)\n\t\tDoThing();\n}");

		Assert.AreEqual(1, await CountMergesAsync(pair, "2"));
		Assert.AreEqual(0, await CountMergesAsync(pair, "1"));
	}

	[TestMethod]
	public async Task OperatorsOfArgumentsAndLambdasDoNotCountTowardsTheLimit()
	{
		string[] bodies =
		[
			"if (c0 && c1 && c2 && c3)\n{\n\tif (Flag(c0 && c1 && c2 && c3, c0 || c1 || c2))\n\t\tDoThing();\n}",
			"if (c0 && c1 && c2)\n{\n\tif (Run(() => c0 && c1 && c2 && c3))\n\t\tDoThing();\n}",
		];

		// The operators of the arguments and the lambda stay out, so each merge stands within the limit of four.
		foreach (string body in bodies)
			Assert.AreEqual(1, await CountMergesAsync(Method(body), "4"), body);
	}

	[TestMethod]
	public async Task ALimitedChainMergesAsFarAsTheLimitAllowsThenSettles()
	{
		string source = Method(
			"if (c0)\n{\n\tif (c1)\n\t{\n\t\tif (c2)\n\t\t{\n\t\t\tif (c3)\n\t\t\t\tDoThing();\n\t\t}\n\t}\n}"
		);

		string fixedSource = Method("if (c0 && c1 && c2)\n{\n\tif (c3)\n\t\tDoThing();\n}");

		AnalyzerRunResult result = await RunAsync(source, "2");

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnInnerPairReportsWhenTheOuterPairWouldPassTheLimit()
	{
		string source = Method(
			"if (c0 && c1 && c2 && c3 && c0)\n{\n\tif (c1)\n\t{\n\t\tif (c2)\n\t\t\tDoThing();\n\t}\n}"
		);

		AnalyzerRunResult reported =
			await RoslynTestHarness.ReportAsync<MergeNestedIfStatementsAnalyzer>(source, Rule);

		Assert.HasCount(1, reported.Diagnostics);

		Assert.AreEqual(
			source.IndexOf("if (c1)", StringComparison.Ordinal),
			reported.Diagnostics[0].Location.SourceSpan.Start
		);
	}

	[TestMethod]
	public async Task AMergeInAnEqualsMethodIgnoresTheLimitAsTheLongConditionRuleDoes()
	{
		const string source = """
			class C
			{
				bool a, b, c, d, e;

				public override bool Equals(object other)
				{
					if (a && b && c && d)
					{
						if (a && b && c && d && e)
							return true;
					}

					return false;
				}

				public override int GetHashCode() => 0;
			}
			""";

		Assert.AreEqual(1, await CountMergesAsync(source));
	}

	[TestMethod]
	public async Task ANameTheInnerConditionDeclaresJoinsTheOuterBlockWhenNothingElseUsesIt()
	{
		string source = Method(
			"if (other is string text)\n{\n\tif (other is string moreText)\n\t\tCall(text.Length, moreText.Length);\n}"
		);

		string fixedSource = Method(
			"if (other is string text && other is string moreText)\n\tCall(text.Length, moreText.Length);"
		);

		AnalyzerRunResult result = await RunAsync(source);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ANameTheInnerConditionDeclaresStaysInItsBlockWhenTheOuterBlockUsesIt()
	{
		List<string> merged = [];

		foreach (object[] clash in NameClashes)
		{
			string above = (string)clash[1];
			string below = (string)clash[2];

			string condition =
				(string)clash[0] == "a use of the field in the outer condition" ? "y > 0" : "c0";

			string source = Method(
				$"{above}if ({condition})\n{{\n\tif (other is string y)\n\t\tCall(y.Length, 1);\n}}\n\n{below}"
			);

			AnalyzerRunResult result = await RunAsync(source);

			if (result.Diagnostics.Length > 0)
				merged.Add((string)clash[0]);
		}

		Assert.IsEmpty(merged, "The ifs were merged though the block around them uses the name.");
	}

	[TestMethod]
	public async Task ANameTheInnerConditionDeclaresStaysInItsBlockWhenDisabledTextUsesIt()
	{
		const string chain =
			"if (c0)\n{\n\tif (other is string y)\n\t\tCall(y.Length, 1);\n}\n\nDoThing();\n\n";

		foreach (string view in new[] { string.Empty, "#define B\n" })
		{
			AnalyzerRunResult result = await RunAsync(
				view + Method(chain + "#if B\nCall(y, 2);\n#endif")
			);

			Assert.IsEmpty(result.Diagnostics, $"view '{view.Trim()}'");
		}
	}

	[TestMethod]
	public async Task ANameTheInnerConditionDeclaresMergesWhenDisabledTextOnlyHoldsALongerName()
	{
		const string tail = "\n\nDoThing();\n\n#if B\nCall(yy, 2);\n#endif";

		string source = Method(
			"if (c0)\n{\n\tif (other is string y)\n\t\tCall(y.Length, 1);\n}" + tail
		);

		string fixedSource = Method("if (c0 && other is string y)\n\tCall(y.Length, 1);" + tail);

		AnalyzerRunResult result = await RunAsync(source);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ALabelOnTheOuterIfDoesNotHideTheBlockAroundIt()
	{
		string[] clashes =
		[
			"L: if (c0)\n{\n\tif (other is string y)\n\t\tCall(y.Length, 1);\n}\n\nCall(y, 2);",
			"L: if (c0)\n{\n\tif (other is string y)\n\t\tCall(y.Length, 1);\n}\n\n{\n\tint y = 1;\n}",
			"first: second: if (c0)\n{\n\tif (other is string y)\n\t\tCall(y.Length, 1);\n}\n\nCall(y, 2);",
		];

		foreach (string body in clashes)
			Assert.IsEmpty((await RunAsync(Method(body))).Diagnostics, body);

		string clear = "L: if (c0)\n{\n\tif (other is string y)\n\t\tCall(y.Length, 1);\n}";
		string merged = "L: if (c0 && other is string y)\n\tCall(y.Length, 1);";

		AnalyzerRunResult result = await RunAsync(Method(clear));

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(Method(merged), result);
	}

	[TestMethod]
	public async Task ANameTheOuterConditionDeclaresAndTheInnerOneUsesIsNotAClash()
	{
		string source = Method(
			"if (other is string text)\n{\n\tif (text.Length > 0)\n\t\tCall(text.Length, 1);\n}\n\nCall(x, 2);"
		);

		string fixedSource = Method(
			"if (other is string text && text.Length > 0)\n\tCall(text.Length, 1);\n\nCall(x, 2);"
		);

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	[DynamicData(
		nameof(InnerBodies),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task NoShapeOfTheMergeMovesAnElse(string name, string body)
	{
		foreach ((string wrapperName, string wrapperFormat) in s_wrappers)
		{
			foreach ((string nestingName, string nestingFormat) in s_nestings)
			{
				string nested = nestingFormat.Replace("{0}", body, StringComparison.Ordinal);

				string wrapped =
					wrapperName == "a method"
						? nested
						: wrapperFormat.Replace(
							"{0}",
							wrapperName == "an else if" ? nested : SourceFixtures.Indent(nested),
							StringComparison.Ordinal
						);

				string source = Method(wrapped);

				AnalyzerRunResult result = await RunAsync(source, "0");
				string row = $"{name} in {wrapperName} with {nestingName}";

				Assert.HasCount(1, result.Diagnostics, row);
				IfBindings.AssertKept(source, result.FixedText);
			}
		}
	}

	[TestMethod]
	public async Task FixAllKeepsEveryElseWithItsIf()
	{
		string[] bodies =
		[
			"{ if (c2) DoThing(); else Call(1, 2); }",
			"if (c2) DoThing(); else Call(1, 2);",
			"while (c2) if (c3) DoThing(); else Call(1, 2);",
		];

		foreach (string body in bodies)
		{
			foreach ((string wrapperName, string wrapperFormat) in s_wrappers)
			{
				foreach ((string nestingName, string nestingFormat) in s_nestings)
				{
					string nested = nestingFormat.Replace("{0}", body, StringComparison.Ordinal);

					string wrapped =
						wrapperName == "a method"
							? nested
							: wrapperFormat.Replace(
								"{0}",

								wrapperName == "an else if"
									? nested
									: SourceFixtures.Indent(nested),

								StringComparison.Ordinal
							);

					string source = Method(wrapped);

					IfBindings.AssertKept(
						source,
						await RoslynTestHarness.AssertFixesAgreeAsync(source, Rule)
					);
				}
			}
		}
	}

	[TestMethod]
	public async Task NestedChainsInsideABodyFixTogetherAndOneAtATime()
	{
		string source = Method(
			"if (c0)\n{\n\tif (c1)\n\t{\n\t\tif (c2)\n\t\t{\n\t\t\tDoThing();\n\n\t\t\tif (c3)\n\t\t\t{\n\t\t\t\tif (c2)\n\t\t\t\t\tCall(1, 2);\n\t\t\t}\n\t\t}\n\t}\n}"
		);

		string fixedText = await RoslynTestHarness.AssertFixesAgreeAsync(source, Rule);

		Assert.AreEqual(2, CountIfs(fixedText));
		IfBindings.AssertKept(source, fixedText);
	}

	[TestMethod]
	public async Task APairTightAgainstItsSiblingsKeepsTheBlankLinesTheMultiLineRuleAsks()
	{
		string source = Method("DoThing();\nif (c0)\n{\n\tif (c1)\n\t\tDoThing();\n}\nBuild(1);");
		string fixedSource = Method("DoThing();\n\nif (c0 && c1)\n\tDoThing();\n\nBuild(1);");

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	public async Task APairTightAgainstItsSiblingsGetsNoBlankLineWhenTheMultiLineRuleIsOff()
	{
		string source = Method("DoThing();\nif (c0)\n{\n\tif (c1)\n\t\tDoThing();\n}\nBuild(1);");
		string fixedSource = Method("DoThing();\nif (c0 && c1)\n\tDoThing();\nBuild(1);");

		var rules = new Dictionary<string, ReportDiagnostic>(
			RuleWithoutBraces,
			StringComparer.Ordinal
		)
		{
			[DiagnosticDescriptors.SeparateMultiLineConstructs.Id] = ReportDiagnostic.Suppress,
		};

		RoslynTestHarness.AssertFixedText(
			fixedSource,
			await RoslynTestHarness.RunAsync<MergeNestedIfStatementsAnalyzer>(source, rules)
		);
	}

	[TestMethod]
	public async Task AConditionOfAnotherTypeKeepsTheIfsApart()
	{
		const string source = """
			struct Flag
			{
				public static bool operator true(Flag flag) => true;

				public static bool operator false(Flag flag) => false;
			}

			class C
			{
				void M(Flag first, Flag second)
				{
					if (first)
					{
						if (second)
							DoThing();
					}
				}
			}
			""";

		Assert.IsEmpty((await RunAsync(source)).Diagnostics);
	}

	[TestMethod]
	public async Task AnInnerPairReportsWhenTheIfAroundItHasAConditionOfAnotherType()
	{
		const string source = """
			struct Flag
			{
				public static bool operator true(Flag flag) => true;

				public static bool operator false(Flag flag) => false;
			}

			class C
			{
				void M(Flag first, bool second, bool third)
				{
					if (first)
					{
						if (second)
						{
							if (third)
								DoThing();
						}
					}
				}
			}
			""";

		AnalyzerRunResult reported =
			await RoslynTestHarness.ReportAsync<MergeNestedIfStatementsAnalyzer>(source, Rule);

		Assert.HasCount(1, reported.Diagnostics);

		Assert.AreEqual(
			source.IndexOf("if (second)", StringComparison.Ordinal),
			reported.Diagnostics[0].Location.SourceSpan.Start
		);
	}

	[TestMethod]
	public async Task AChainMergesUpToTheFirstConditionOfAnotherType()
	{
		const string source = """
			struct Flag
			{
				public static bool operator true(Flag flag) => true;

				public static bool operator false(Flag flag) => false;
			}

			class C
			{
				void M(bool first, bool second, Flag third)
				{
					if (first)
					{
						if (second)
						{
							if (third)
								DoThing();
						}
					}
				}
			}
			""";

		const string fixedSource = """
			struct Flag
			{
				public static bool operator true(Flag flag) => true;

				public static bool operator false(Flag flag) => false;
			}

			class C
			{
				void M(bool first, bool second, Flag third)
				{
					if (first && second)
					{
						if (third)
							DoThing();
					}
				}
			}
			""";

		AnalyzerRunResult result = await RunAsync(source);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The merge around the stack dedents its lines, so a Fix All would keep only one of the two changes.
	[TestMethod]
	public async Task AStackInsideAConditionOfAnotherTypeWaitsForTheMergeAroundIt()
	{
		const string source = """
			struct Flag
			{
				public static bool operator true(Flag flag) => true;

				public static bool operator false(Flag flag) => false;
			}

			class C
			{
				void M(bool a, bool b, Flag maybe, bool c, bool d)
				{
					if (a)
					{
						if (b)
						{
							if (maybe)
							{
								if (c)
								{
									if (d)
										DoThing();
								}
							}
						}
					}
				}
			}
			""";

		const string fixedSource = """
			struct Flag
			{
				public static bool operator true(Flag flag) => true;

				public static bool operator false(Flag flag) => false;
			}

			class C
			{
				void M(bool a, bool b, Flag maybe, bool c, bool d)
				{
					if (a && b)
					{
						if (maybe)
						{
							if (c && d)
								DoThing();
						}
					}
				}
			}
			""";

		AnalyzerRunResult reported =
			await RoslynTestHarness.ReportAsync<MergeNestedIfStatementsAnalyzer>(source, Rule);

		Assert.HasCount(1, reported.Diagnostics);

		Assert.AreEqual(
			source.IndexOf("if (a)", StringComparison.Ordinal),
			reported.Diagnostics[0].Location.SourceSpan.Start
		);

		Assert.AreEqual(fixedSource, await RoslynTestHarness.AssertFixesAgreeAsync(source, Rule));
	}

	[TestMethod]
	public async Task TheBraceRuleBracesTheInnerIfBeforeTheMerge()
	{
		string source = Method("if (c0)\n{\n\tif (c1\n\t\t&& c2)\n\t\tDoThing();\n}");

		// ZA0205 braces the inner body, which its head spans lines, then this rule merges, with the head left over two lines.
		string fixedSource = Method("if (c0 && c1\n\t&& c2)\n{\n\tDoThing();\n}");

		Assert.AreEqual(fixedSource, await RoslynTestHarness.AssertFixesAgreeAsync(source, Rule));
	}

	[TestMethod]
	public async Task TheMergeWorksOnTheTextTheOtherBraceRulesLeave()
	{
		string source = Method("if (c0)\n{\n\tif (c1)\n\t{\n\t\tDoThing();\n\t}\n}");
		string fixedSource = Method("if (c0 && c1)\n\tDoThing();");

		var rules = new Dictionary<string, ReportDiagnostic>(Rule, StringComparer.Ordinal)
		{
			[DiagnosticDescriptors.RemoveRedundantBraces.Id] = ReportDiagnostic.Warn,
		};

		Assert.AreEqual(fixedSource, await RoslynTestHarness.AssertFixesAgreeAsync(source, rules));
	}

	// The report stands on one if of a chain, so a Fix All has one set of changes for it, which settles in a single pass.
	[TestMethod]
	public async Task SeveralPairsFixTogetherAndOneAtATime()
	{
		string source = Method(
			"if (c0)\n{\n\tif (c1)\n\t\tDoThing();\n}\n\nif (c2)\n\tif (c3)\n\t\tCall(1, 2);\n\nif (c0)\n{\n\tif (c3)\n\t{\n\t\tif (c1)\n\t\t\tCall(3, 4);\n\t}\n}"
		);

		string fixedText = await RoslynTestHarness.AssertFixesAgreeAsync(source, Rule);

		Assert.AreEqual(3, CountIfs(fixedText));
		IfBindings.AssertKept(source, fixedText);
	}

	// Each pair declares a name of its own, so each one asks whether the block around it reuses the name.
	[TestMethod]
	public void TheCostGrowsLinearlyWithTheNumberOfPairsInABlock()
	{
		static string Pairs(int count)
		{
			return Method(
				string.Concat(
					Enumerable
						.Range(0, count)
						.Select(static index =>
							$"if (c0)\n{{\n\tif (other is string v{index})\n\t\tCall(v{index}.Length, 1);\n}}\n\n"
						)
				)
			);
		}

		LinearCost.AssertLinear(
			Pairs(250),
			Pairs(1_000),

			static root =>
			{
				foreach (
					IfStatementSyntax ifStatement in root.DescendantNodes()
						.OfType<IfStatementSyntax>()
				)
				{
					NestedIfs.FindMerges(ifStatement, limit: 0);
				}
			}
		);
	}

	// A read of the comments that starts at the root passes every statement of the block for each pair, which allocates nothing but takes quadratic time.
	[TestMethod]
	[DoNotParallelize]
	public void AHugeBlockOfPairsIsWalkedInUnderThreeSeconds()
	{
		string pairs = Method(
			string.Concat(
				Enumerable.Repeat("if (c0)\n{\n\tif (c1)\n\t{\n\t\tCall(1, 1);\n\t}\n}\n\n", 16_000)
			)
		);

		LinearCost.AssertWalkEndsWithin(
			milliseconds: 3_000,
			pairs,

			static root =>
			{
				SourceText sourceText = root.SyntaxTree.GetText();

				foreach (
					IfStatementSyntax ifStatement in root.DescendantNodes()
						.OfType<IfStatementSyntax>()
				)
				{
					NestedIfs.CreateMerge(sourceText, ifStatement, count: 1);
				}
			}
		);
	}

	// The body comes out one step, so the later lines of a comment that trails it come out with it.
	[TestMethod]
	public async Task AMultiLineCommentThatTrailsTheBodyMovesOutWithIt()
	{
		string source = Method(
			"if (c0)\n{\n\tif (c1)\n\t{\n\t\tDoThing();\n\t} /* one\n\t   two */\n}"
		);

		string fixedSource = Method("if (c0 && c1)\n{\n\tDoThing();\n} /* one\n   two */");

		AnalyzerRunResult result = await RunAsync(source);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// Counts the merges a run reports, and reads the text it fixes to, which differs from the source when it merged.
	private static async Task<int> CountMergesAsync(string source, string? limit = null)
	{
		AnalyzerRunResult result = await RunAsync(source, limit);

		Assert.AreEqual(result.Diagnostics.Length > 0, result.FixedText != source);

		return result.Diagnostics.Length;
	}

	private static async Task<AnalyzerRunResult> RunAsync(string source, string? limit = null)
	{
		return await RoslynTestHarness.RunAsync<MergeNestedIfStatementsAnalyzer>(
			source,
			RuleWithoutBraces,

			limit is null
				? null
				: new Dictionary<string, string> { ["stanza_max_logical_operators"] = limit }
		);
	}

	// A condition the merge wrote is judged by the long condition rule at the default limit.
	private static Task<AnalyzerRunResult> ReportLongConditionsAsync(string source)
	{
		return RoslynTestHarness.ReportAsync<ShortenLongConditionAnalyzer>(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.ShortenLongCondition)
		);
	}

	private static int CountIfs(string source) =>
		source.Split(["if ("], StringSplitOptions.None).Length - 1;

	// The members every fixture shares, so each if names itself and no snippet needs a symbol of its own.
	private static string Method(string statements)
	{
		string body = string.Join(
			"\n",

			statements
				.Split('\n')
				.Select(static line => line.Length == 0 || line[0] == '#' ? line : "\t\t" + line)
		);

		return $"class C\n{{\n\tbool c0, c1, c2, c3;\n\tbool? maybe;\n\tint x, y;\n\n\tstatic bool Flag(bool first, bool second) => first;\n\n\tstatic bool Run(Func<bool> test) => test();\n\n\tvoid M(object other)\n\t{{\n{body}\n\t}}\n}}\n";
	}
}
