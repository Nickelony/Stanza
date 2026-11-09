using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Braces;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Braces;

[TestClass]
public sealed class RemoveElseAfterJumpTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> Rule { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.RemoveElseAfterJump);

	// The rule waits while the brace rule has braces to add, so a fixture with an unbraced branch beside a multi-line one runs with that rule off.
	// The multi-line rule is off too, so the fixture pins the move and none of the gaps the fix writes for that rule.
	private static IReadOnlyDictionary<string, ReportDiagnostic> RuleWithoutBraces { get; } =
		new Dictionary<string, ReportDiagnostic>(Rule, StringComparer.Ordinal)
		{
			[DiagnosticDescriptors.UseBracesConsistently.Id] = ReportDiagnostic.Suppress,
			[DiagnosticDescriptors.SeparateMultiLineConstructs.Id] = ReportDiagnostic.Suppress,
		};

	// The bodies of an else that a fix has to keep apart from the ifs around it: each condition names its if, so a moved else shows in IfBindings.
	public static IEnumerable<object[]> ElseBodies { get; } =
	[
		["a block of one call", "{ DoThing(); }"],
		["a block ending in an if without an else", "{ if (c1) DoThing(); }"],
		["a block ending in an if with an else", "{ if (c1) DoThing(); else Call(1, 2); }"],
		["a block ending in a braced if", "{ if (c1) { DoThing(); } }"],

		[
			"a block ending in a nested dangling if",
			"{ if (c1) if (c2) DoThing(); else Call(1, 2); }",
		],

		["a block holding a call and an if", "{ DoThing(); if (c1) DoThing(); }"],
		["a statement", "DoThing();"],
		["a loop around an if without an else", "for (;;) if (c1) DoThing();"],
		["a loop around a dangling if", "while (c1) if (c2) DoThing(); else Call(1, 2);"],
	];

	// Where the chain stands: in the method, in the block of a branch that has an else of its own, and in a loop.
	private static readonly (string Name, string Format)[] s_wrappers =
	[
		("a method", "{0}"),
		("the block of a branch with an else", "if (c3)\n{\n{0}\n}\nelse\n\tCall(0, 0);"),
		("a loop", "while (c3)\n{\n{0}\n}"),
	];

	private static readonly string[] s_branches =
	[
		"{ return; }",
		"return;",
		"{ DoThing(); return; }",
	];

	public static IEnumerable<object[]> Jumps { get; } =
	[
		["a return", "return;"],
		["a throw", "throw new InvalidOperationException();"],
		["a break", "break;"],
		["a continue", "continue;"],
		["a goto", "goto done;"],
		["a goto case", "goto case 1;"],
	];

	// A conditional that stands near the statement, or picks what it holds in another view.
	public static IEnumerable<object[]> DirectiveShapes { get; } =
	[
		[
			"a conditional inside the else",
			"if (c0)\n{\n\treturn;\n}\nelse\n{\n#if B\n\tDoThing();\n#endif\n\tCall(1, 2);\n}",
		],

		[
			"a conditional around the else",
			"if (c0)\n{\n\treturn;\n}\n#if B\nelse\n{\n\tDoThing();\n}\n#endif",
		],

		[
			"a conditional around the statement",
			"#if B\nif (c0)\n{\n\treturn;\n}\nelse\n{\n\tDoThing();\n}\n#endif",
		],

		[
			"a conditional below the statement",
			"if (c0)\n{\n\treturn;\n}\nelse\n{\n\tDoThing();\n}\n#if B\nCall(1, 2);\n#endif",
		],

		[
			"a conditional that picks the branch",
			"if (c0)\n{\n#if B\n\treturn;\n#else\n\tDoThing();\n\treturn;\n#endif\n}\nelse\n{\n\tCall(1, 2);\n}",
		],

		[
			"a conditional that picks the else",
			"if (c0)\n{\n\treturn;\n}\nelse\n#if B\n\tDoThing();\n#else\n\tCall(1, 2);\n#endif",
		],

		[
			"a region around the statement",
			"#region Parts\nif (c0)\n{\n\treturn;\n}\nelse\n{\n\tDoThing();\n}\n#endregion",
		],

		[
			"a pragma above the statement",
			"#pragma warning disable CS0162\nif (c0)\n{\n\treturn;\n}\nelse\n{\n\tDoThing();\n}",
		],
	];

	// A name the moved statements declare joins the block around the chain, so a use of it elsewhere in that block may bind to another symbol.
	public static IEnumerable<object[]> DeclaredNames { get; } =
	[
		["a local", "int x = 1;"],
		["a var local", "var x = 1;"],
		["an out variable", "int.TryParse(\"1\", out var x);"],
		["a pattern variable", "_ = other is string x;"],
		["a deconstruction", "var (x, y) = Read();"],
		["a local function", "int x() => 1;"],
		["a label", "x: DoThing();"],
	];

	// Each clash is a statement above or below the chain, in a fixture that already compiles with the name in the else alone.
	private static readonly (string Name, string Above, string Below)[] s_nameClashes =
	[
		(
			"a loop variable below the chain",
			string.Empty,
			"foreach (var x in new[] { 1 })\n\tDoThing();"
		),

		("a use of the field below the chain", string.Empty, "Call(x, 2);"),
		("an assignment of the field below the chain", string.Empty, "x = 5;"),
		("a use of the field above the chain", "Call(x, 0);\n", string.Empty),
		("a lambda parameter below the chain", string.Empty, "Func<int, int> twice = x => x * 2;"),
	];

	[TestMethod]
	public async Task AnElseAfterAReturnMovesItsStatementsOut()
	{
		const string source = """
			class C
			{
				void M(bool flag)
				{
					if (flag)
					{
						return;
					}
					else
					{
						DoThing();
						DoThing();
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool flag)
				{
					if (flag)
					{
						return;
					}
					DoThing();
					DoThing();
				}
			}
			""";

		AnalyzerRunResult result = await RunAsync(source);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnUnbracedElseMovesItsStatementOut()
	{
		const string source = """
			class C
			{
				void M(bool flag)
				{
					if (flag)
						return;
					else
						DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool flag)
				{
					if (flag)
						return;
					DoThing();
				}
			}
			""";

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	public async Task AnElseOnTheLineOfTheClosingBraceMovesItsStatementsOut()
	{
		const string source = """
			class C
			{
				void M(bool flag)
				{
					if (flag) {
						return;
					} else {
						DoThing();
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool flag)
				{
					if (flag) {
						return;
					}
					DoThing();
				}
			}
			""";

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	public async Task AnElseOnOneLineMovesItsStatementOntoALineOfItsOwn()
	{
		const string source = """
			class C
			{
				void M(bool flag)
				{
					if (flag) { return; } else { DoThing(); }
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool flag)
				{
					if (flag) { return; }
					DoThing();
				}
			}
			""";

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	public async Task StatementsIndentedWithSpacesMoveOutByOneStep()
	{
		const string source =
			"class C\n{\n  void M(bool flag)\n  {\n    if (flag)\n      return;\n    else\n    {\n      DoThing();\n      if (flag)\n        DoThing();\n    }\n  }\n}\n";

		const string fixedSource =
			"class C\n{\n  void M(bool flag)\n  {\n    if (flag)\n      return;\n    DoThing();\n    if (flag)\n      DoThing();\n  }\n}\n";

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	[DynamicData(
		nameof(Jumps),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task EachKindOfJumpEndsABranch(string name, string jump)
	{
		AnalyzerRunResult result = await RunAsync(Jumping(jump, moved: false));

		Assert.HasCount(1, result.Diagnostics, name);
		RoslynTestHarness.AssertFixedText(Jumping(jump, moved: true), result, name);
	}

	[TestMethod]
	public async Task AnIteratorEndsABranchWithAYieldBreak()
	{
		const string source = """
			using System.Collections.Generic;

			class C
			{
				IEnumerable<int> M(bool flag)
				{
					if (flag)
					{
						yield break;
					}
					else
					{
						yield return 1;
					}
				}
			}
			""";

		const string fixedSource = """
			using System.Collections.Generic;

			class C
			{
				IEnumerable<int> M(bool flag)
				{
					if (flag)
					{
						yield break;
					}
					yield return 1;
				}
			}
			""";

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	public async Task ABranchThatDoesNotEndInAJumpKeepsItsElse()
	{
		string[] bodies =
		[
			"if (c0)\n{\n\tDoThing();\n}\nelse\n{\n\tCall(1, 2);\n}",
			"if (c0)\n{\n\treturn;\n\tDoThing();\n}\nelse\n{\n\tCall(1, 2);\n}",
			"if (c0)\n{\n\tif (c1)\n\t\treturn;\n}\nelse\n{\n\tCall(1, 2);\n}",
			"if (c0)\n{\n}\nelse\n{\n\tCall(1, 2);\n}",
		];

		foreach (string body in bodies)
			Assert.IsEmpty((await RunAsync(Method(body))).Diagnostics, body);
	}

	[TestMethod]
	public async Task AnElseIfStaysAndSoDoesTheChainAroundIt()
	{
		string[] bodies =
		[
			"if (c0)\n\treturn;\nelse if (c1)\n\treturn;\nelse\n\tCall(1, 2);",
			"if (c0)\n\tDoThing();\nelse if (c1)\n\treturn;\nelse\n{\n\tCall(1, 2);\n}",
			"if (c0)\n{\n\tDoThing();\n}\nelse if (c1)\n{\n\tDoThing();\n}\nelse if (c2)\n{\n\treturn;\n}\nelse\n{\n\tCall(1, 2);\n}",
		];

		foreach (string body in bodies)
			Assert.IsEmpty((await RunAsync(Method(body))).Diagnostics, body);
	}

	[TestMethod]
	public async Task AnIfOutsideABlockStays()
	{
		string[] bodies =
		[
			"while (c3)\n\tif (c0)\n\t\tbreak;\n\telse\n\t\tDoThing();",
			"switch (x)\n{\n\tcase 1:\n\t\tif (c0)\n\t\t\tbreak;\n\t\telse\n\t\t\tDoThing();\n\n\t\tbreak;\n}",
			"again:\nif (c0)\n\treturn;\nelse\n\tCall(1, 2);",
			"if (c3)\n\tCall(0, 0);\nelse\n\tif (c0)\n\t\treturn;\n\telse\n\t\tCall(1, 2);",
		];

		foreach (string body in bodies)
			Assert.IsEmpty((await RunAsync(Method(body))).Diagnostics, body);
	}

	[TestMethod]
	public async Task AnEmptyElseStays()
	{
		string[] bodies =
		[
			"if (c0)\n\treturn;\nelse\n{\n}",
			"if (c0)\n\treturn;\nelse\n{\n\t// Nothing to do.\n}",
			"if (c0)\n\treturn;\nelse\n\t;",
		];

		foreach (string body in bodies)
			Assert.IsEmpty((await RunAsync(Method(body))).Diagnostics, body);
	}

	[TestMethod]
	public async Task TheReportStandsOnTheElseKeyword()
	{
		const string source = """
			class C
			{
				void M(bool flag)
				{
					if (flag)
						return;
					else
						DoThing();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync<RemoveElseAfterJumpAnalyzer>(
			source,
			Rule
		);

		Assert.HasCount(1, result.Diagnostics);

		Assert.AreEqual(
			"else",

			source[
				result.Diagnostics[0].Location.SourceSpan.Start..result
					.Diagnostics[0]
					.Location
					.SourceSpan
					.End
			]
		);
	}

	[TestMethod]
	public async Task CommentsOfTheRemovedTokensStayAndTheBlankLineGoes()
	{
		const string source = """
			class C
			{
				void M(bool flag)
				{
					if (flag)
					{
						return;
					} // Done.

					// Otherwise carry on.
					else // The rest.
					{
						// Before the call.
						DoThing(); // After the call.
						// Before the brace.
					} // The end.
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool flag)
				{
					if (flag)
					{
						return;
					} // Done.
					// Otherwise carry on.
					// The rest.
					// Before the call.
					DoThing(); // After the call.
					// Before the brace.
					// The end.
				}
			}
			""";

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	public async Task ABlockCommentBeforeTheStatementsStaysWithThem()
	{
		const string source = """
			class C
			{
				void M(bool flag)
				{
					if (flag)
						return;
					else /* Last. */
					{
						DoThing();
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool flag)
				{
					if (flag)
						return;
					/* Last. */
					DoThing();
				}
			}
			""";

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	public async Task AMultiLineBlockCommentComesOutByTheStepTheStatementsDo()
	{
		const string source = """
			class C
			{
				void M(bool flag)
				{
					if (flag)
						return;
					else
					{
						/* Before
						   the call. */
						DoThing();
						/* After
						   the call. */
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool flag)
				{
					if (flag)
						return;
					/* Before
					   the call. */
					DoThing();
					/* After
					   the call. */
				}
			}
			""";

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	public async Task ACommentOnTheLineOfTheBranchStaysOnIt()
	{
		const string source = """
			class C
			{
				void M(bool flag)
				{
					if (flag) {
						return;
					} else { // Otherwise.
						DoThing();
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool flag)
				{
					if (flag) {
						return;
					} // Otherwise.
					DoThing();
				}
			}
			""";

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	public async Task ATokenAfterTheClosingBraceStaysOutsideTheCommentThatEndsTheBody()
	{
		string[] tails =
		[
			"Call(1, 2); // Note.",
			"Call(1, 2); /* Note. */",
			"Call(1, 2); /* Note. */ // More.",
			"Call(1, 2);\n// Note.",
			"Call(1, 2); // Note.\n// More.",
		];

		string[] followers =
		[
			"DoThing();",
			"Build(1); Build(2);",
			"/* Next. */ DoThing();",
			"if (c1) DoThing();",
		];

		foreach (string tail in tails)
		{
			foreach (string follower in followers)
			{
				string source = Method(
					$"if (c0)\n{{\n\treturn;\n}}\nelse\n{{\n\t{tail}\n}} {follower}"
				);

				AnalyzerRunResult result = await RunAsync(source);
				string row = $"{tail} then {follower}";

				Assert.HasCount(1, result.Diagnostics, row);
				CodeTokens.AssertNamesKept(source, result.FixedText, row);
			}
		}
	}

	[TestMethod]
	public async Task ATokenAfterTheClosingBraceStartsItsLineWithoutTheSpaceThatSeparatedItFromTheBrace()
	{
		const string source =
			"if (c0)\n{\n\treturn;\n}\nelse\n{\n\tCall(1, 2); // Note.\n} DoThing();";

		const string fixedSource = "if (c0)\n{\n\treturn;\n}\nCall(1, 2); // Note.\nDoThing();";

		RoslynTestHarness.AssertFixedText(Method(fixedSource), await RunAsync(Method(source)));
	}

	[TestMethod]
	[DynamicData(
		nameof(DirectiveShapes),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ADirectiveNearTheStatementLeavesItAlone(string name, string body)
	{
		foreach (string view in new[] { string.Empty, "#define B\n" })
		{
			AnalyzerRunResult result = await RunAsync(view + Method(body));
			Assert.IsEmpty(result.Diagnostics, $"{name}, view '{view.Trim()}'");
		}
	}

	[TestMethod]
	public async Task ADirectiveFarFromTheStatementDoesNotStopTheFix()
	{
		const string body =
			"#if B\nCall(0, 0);\n#endif\n\nDoThing();\n\nif (c0)\n\treturn;\nelse\n\tCall(1, 2);\n\nDoThing();";

		const string fixedSource =
			"#if B\nCall(0, 0);\n#endif\n\nDoThing();\n\nif (c0)\n\treturn;\nCall(1, 2);\n\nDoThing();";

		foreach (string view in new[] { string.Empty, "#define B\n" })
		{
			AnalyzerRunResult result = await RunAsync(view + Method(body));

			Assert.HasCount(1, result.Diagnostics, view);
			RoslynTestHarness.AssertFixedText(view + Method(fixedSource), result, view);
		}
	}

	[TestMethod]
	[DynamicData(
		nameof(DeclaredNames),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ANameTheElseDeclaresMovesOutWhenNothingElseUsesIt(
		string name,
		string declaration
	)
	{
		string source = Method($"if (c0)\n\treturn;\nelse\n{{\n\t{declaration}\n\tCall(1, 2);\n}}");
		string fixedSource = Method($"if (c0)\n\treturn;\n{declaration}\nCall(1, 2);");

		AnalyzerRunResult result = await RunAsync(source);

		Assert.HasCount(1, result.Diagnostics, name);
		RoslynTestHarness.AssertFixedText(fixedSource, result, name);
	}

	[TestMethod]
	public async Task ANameTheElseDeclaresStaysInItsBlockWhenTheBlockAroundUsesIt()
	{
		List<string> moved = [];

		foreach (object[] declared in DeclaredNames)
		{
			foreach ((string clashName, string above, string below) in s_nameClashes)
			{
				string source = Method(
					$"{above}if (c0)\n\treturn;\nelse\n{{\n\t{declared[1]}\n\tCall(1, 2);\n}}\n\n{below}"
				);

				AnalyzerRunResult result = await RunAsync(source);

				if (result.Diagnostics.Length > 0)
					moved.Add($"{declared[0]} with {clashName}");
			}
		}

		Assert.IsEmpty(moved, "The chain was fixed though the block around it uses the name.");
	}

	[TestMethod]
	public async Task ANameTheElseDeclaresStaysInItsBlockWhenDisabledTextUsesIt()
	{
		const string chain =
			"if (c0)\n\treturn;\nelse\n{\n\tvar x = 1;\n\tCall(x, 2);\n}\n\nDoThing();\n\n";

		foreach (string view in new[] { string.Empty, "#define B\n" })
		{
			AnalyzerRunResult result = await RunAsync(
				view + Method(chain + "#if B\nCall(x, 3);\n#endif")
			);

			Assert.IsEmpty(result.Diagnostics, $"view '{view.Trim()}'");
		}
	}

	[TestMethod]
	public async Task ANameTheElseDeclaresMovesOutWhenDisabledTextOnlyHoldsALongerName()
	{
		const string source =
			"if (c0)\n\treturn;\nelse\n{\n\tvar x = 1;\n\tCall(x, 2);\n}\n\nDoThing();\n\n#if B\nCall(xx, 3);\n#endif";

		const string fixedSource =
			"if (c0)\n\treturn;\nvar x = 1;\nCall(x, 2);\n\nDoThing();\n\n#if B\nCall(xx, 3);\n#endif";

		AnalyzerRunResult result = await RunAsync(Method(source));

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(Method(fixedSource), result);
	}

	[TestMethod]
	public async Task ANameTheBranchBeforeTheElseUsesStaysInItsBlock()
	{
		string[] bodies =
		[
			"if (c0)\n{\n\tvar x = 2;\n\tCall(x, 1);\n\treturn;\n}\nelse\n{\n\tvar x = 1;\n\tCall(x, 2);\n}",
			"if (c0)\n{\n\tCall(x, 1);\n\treturn;\n}\nelse\n{\n\tvar x = 1;\n\tCall(x, 2);\n}",
			"if (c0)\n{\n\tFunc<int, int> twice = x => x * 2;\n\treturn;\n}\nelse\n{\n\tvar x = 1;\n\tCall(x, 2);\n}",
		];

		foreach (string body in bodies)
			Assert.IsEmpty((await RunAsync(Method(body))).Diagnostics, body);
	}

	[TestMethod]
	public async Task ANameTheConditionDeclaresStaysInScopeForTheMovedStatements()
	{
		const string source = """
			class C
			{
				int M(object other)
				{
					if (other is not string text)
					{
						return 0;
					}
					else
					{
						return text.Length;
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				int M(object other)
				{
					if (other is not string text)
					{
						return 0;
					}
					return text.Length;
				}
			}
			""";

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	public async Task SiblingChainsThatDeclareOneNameStayInTheirBlocks()
	{
		string body =
			"if (c0)\n\treturn;\nelse\n{\n\tvar y = 1;\n\tCall(y, 1);\n}\n\nif (c1)\n\treturn;\nelse\n{\n\tvar y = 2;\n\tCall(y, 2);\n}";

		AnalyzerRunResult result = await RunAsync(Method(body));

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task SiblingChainsThatDeclareDifferentNamesFixTogether()
	{
		string source = Method(
			"if (c0)\n\treturn;\nelse\n{\n\tvar y = 1;\n\tCall(y, 1);\n}\n\nif (c1)\n\treturn;\nelse\n{\n\tvar z = 2;\n\tCall(z, 2);\n}"
		);

		string fixedSource = Method(
			"if (c0)\n\treturn;\nvar y = 1;\nCall(y, 1);\n\nif (c1)\n\treturn;\nvar z = 2;\nCall(z, 2);"
		);

		AnalyzerRunResult result = await RunAsync(source);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AUsingDeclarationOfTheElseStaysSoItDisposesWhereItDid()
	{
		string[] bodies =
		[
			"if (c0)\n\treturn;\nelse\n{\n\tusing var resource = Open();\n\tCall(1, 2);\n}\n\nDoThing();",
			"if (c0)\n\treturn;\nelse\n{\n\tCall(1, 2);\n\tusing var resource = Open();\n}",
			"if (c0)\n\treturn;\nelse\n{\n\tdone: using var resource = Open();\n\tCall(1, 2);\n}\n\nDoThing();",
			"if (c0)\n\treturn;\nelse\n{\n\tfirst: second: using var resource = Open();\n\tCall(1, 2);\n}\n\nDoThing();",
		];

		foreach (string body in bodies)
			Assert.IsEmpty((await RunAsync(Method(body))).Diagnostics, body);
	}

	[TestMethod]
	public async Task AUsingStatementOfTheElseMovesWithItsOwnBlock()
	{
		string source = Method(
			"if (c0)\n\treturn;\nelse\n{\n\tusing (Open())\n\t{\n\t\tCall(1, 2);\n\t}\n}"
		);

		string fixedSource = Method("if (c0)\n\treturn;\nusing (Open())\n{\n\tCall(1, 2);\n}");

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	public async Task AMultiLineStringKeepsItsLinesAndTheStatementsKeepTheirIndentation()
	{
		const string source = """
			class C
			{
				void M(bool flag)
				{
					if (flag)
						return;
					else
					{
						var text = @"first
			second";
						DoThing();
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool flag)
				{
					if (flag)
						return;
						var text = @"first
			second";
						DoThing();
				}
			}
			""";

		AnalyzerRunResult result = await RunAsync(source);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ALineThatDoesNotShareTheIndentationKeepsTheStatementsWhereTheyStand()
	{
		const string source =
			"class C\n{\n\tvoid M(bool flag)\n\t{\n\t\tif (flag)\n\t\t\treturn;\n\t\telse\n\t\t{\n\t\t\tCall(\n\t\t\t\t1,\n2);\n\t\t}\n\t}\n}\n";

		const string fixedSource =
			"class C\n{\n\tvoid M(bool flag)\n\t{\n\t\tif (flag)\n\t\t\treturn;\n\t\t\tCall(\n\t\t\t\t1,\n2);\n\t}\n}\n";

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	public async Task ABlankLineAroundTheElseGoesWithIt()
	{
		const string source = """
			class C
			{
				void M(bool flag)
				{
					if (flag)
					{
						return;
					}

					else

					{

						DoThing();

						Call(1, 2);

					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool flag)
				{
					if (flag)
					{
						return;
					}
					DoThing();

					Call(1, 2);
				}
			}
			""";

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	[DynamicData(
		nameof(ElseBodies),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task NoShapeOfTheElseMovesAnotherElse(string name, string elseBody)
	{
		foreach ((string wrapperName, string format) in s_wrappers)
		{
			foreach (string branch in s_branches)
			{
				string chain = $"if (c0)\n{branch}\nelse\n{elseBody}\n\nDoThing();";

				string wrapped =
					wrapperName == "a method"
						? chain
						: format.Replace(
							"{0}",
							SourceFixtures.Indent(chain),
							StringComparison.Ordinal
						);

				string source = Method(wrapped);

				AnalyzerRunResult result = await RunAsync(source);
				string row = $"{name} in {wrapperName} after {branch}";

				Assert.HasCount(1, result.Diagnostics, row);
				IfBindings.AssertKept(source, result.FixedText, "c0");
			}
		}
	}

	[TestMethod]
	public async Task FixAllKeepsEveryElseWithItsIfAndEveryNameInItsBlock()
	{
		string[] bodies =
		[
			"{ if (c1) DoThing(); }",
			"{ if (c1) DoThing(); else Call(1, 2); }",
			"for (;;) if (c1) DoThing();",
		];

		foreach (string body in bodies)
		{
			foreach ((string wrapperName, string format) in s_wrappers)
			{
				string chain = $"if (c0)\n{{ return; }}\nelse\n{body}\n\nDoThing();";

				string wrapped =
					wrapperName == "a method"
						? chain
						: format.Replace(
							"{0}",
							SourceFixtures.Indent(chain),
							StringComparison.Ordinal
						);

				string source = Method(wrapped);

				string fixedText = await RoslynTestHarness.AssertFixesAgreeAsync(source, Rule);

				IfBindings.AssertKept(source, fixedText, "c0");
			}
		}

		string sameName = Method(
			"if (c0)\n\treturn;\nelse\n{\n\tvar y = 1;\n\tCall(y, 1);\n}\n\nif (c1)\n\treturn;\nelse\n{\n\tvar y = 2;\n\tCall(y, 2);\n}"
		);

		string otherNames = Method(
			"if (c0)\n\treturn;\nelse\n{\n\tvar y = 1;\n\tCall(y, 1);\n}\n\nif (c1)\n\treturn;\nelse\n{\n\tvar z = 2;\n\tCall(z, 2);\n}"
		);

		Assert.HasCount(
			2,
			IfBindings.ReadElses(await RoslynTestHarness.AssertFixesAgreeAsync(sameName, Rule))
		);

		Assert.IsEmpty(
			IfBindings.ReadElses(await RoslynTestHarness.AssertFixesAgreeAsync(otherNames, Rule))
		);
	}

	[TestMethod]
	public async Task ANestedChainWaitsForTheOuterOne()
	{
		string source = Method(
			"if (c0)\n{\n\treturn;\n}\nelse\n{\n\tif (c1)\n\t{\n\t\treturn;\n\t}\n\telse\n\t{\n\t\tCall(1, 2);\n\t}\n}"
		);

		AnalyzerRunResult reported =
			await RoslynTestHarness.ReportAsync<RemoveElseAfterJumpAnalyzer>(source, Rule);

		Assert.HasCount(1, reported.Diagnostics);

		Assert.DoesNotContain(
			"if (c1)",
			source[..reported.Diagnostics[0].Location.SourceSpan.Start]
		);

		string fixedText = await RoslynTestHarness.AssertFixesAgreeAsync(source, Rule);

		Assert.DoesNotContain("else", fixedText);
		IfBindings.AssertKept(source, fixedText, "c0", "c1");
	}

	[TestMethod]
	public async Task ANestedChainKeepsItsElseWhenTheOuterBlockCannotTakeItsName()
	{
		string source = Method(
			"if (c0)\n{\n\treturn;\n}\nelse\n{\n\tif (c1)\n\t{\n\t\treturn;\n\t}\n\telse\n\t{\n\t\tvar y = 1;\n\t\tCall(y, 2);\n\t}\n}\n\nforeach (var y in new[] { 1 })\n\tDoThing();"
		);

		string fixedText = await RoslynTestHarness.AssertFixesAgreeAsync(source, Rule);

		// The outer else goes, and the inner one stays, as its name could only join the block that the foreach below declares it in.
		IfBindings.AssertKept(source, fixedText, "c0");
	}

	[TestMethod]
	public async Task SeveralChainsFixTogetherAndOneAtATime()
	{
		string source = Method(
			"if (c0)\n\treturn;\nelse\n\tDoThing();\n\nif (c1)\n{\n\treturn;\n}\nelse\n{\n\tCall(1, 2);\n\tCall(3, 4);\n}\n\nif (c2)\n\treturn;\nelse\n\tCall(5, 6);"
		);

		string fixedText = await RoslynTestHarness.AssertFixesAgreeAsync(source, Rule);

		Assert.DoesNotContain("else", fixedText);
		IfBindings.AssertKept(source, fixedText, "c0", "c1", "c2");
	}

	[TestMethod]
	public async Task AChainTightAgainstItsSiblingsKeepsTheBlankLinesTheMultiLineRuleAsks()
	{
		string source = Method(
			"DoThing();\nif (c0)\n{\n\treturn;\n}\nelse\n{\n\tCall(1, 2);\n}\nBuild(1);"
		);

		string fixedSource = Method(
			"DoThing();\n\nif (c0)\n{\n\treturn;\n}\n\nCall(1, 2);\n\nBuild(1);"
		);

		using IDisposable skipped = RoslynTestHarness.SkippingDefaultRules();

		RoslynTestHarness.AssertFixedText(
			fixedSource,
			await RoslynTestHarness.RunAsync<RemoveElseAfterJumpAnalyzer>(source, Rule)
		);
	}

	[TestMethod]
	public async Task AChainTightAgainstItsSiblingsGetsNoBlankLineWhenTheMultiLineRuleIsOff()
	{
		string source = Method(
			"DoThing();\nif (c0)\n{\n\treturn;\n}\nelse\n{\n\tCall(1, 2);\n}\nBuild(1);"
		);

		string fixedSource = Method("DoThing();\nif (c0)\n{\n\treturn;\n}\nCall(1, 2);\nBuild(1);");

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	public async Task AChainOnOneLineGetsNoBlankLineAroundIt()
	{
		string source = Method("DoThing();\nif (c0) return; else Call(1, 2);\nBuild(1);");
		string fixedSource = Method("DoThing();\nif (c0) return;\nCall(1, 2);\nBuild(1);");

		RoslynTestHarness.AssertFixedText(fixedSource, await RunAsync(source));
	}

	[TestMethod]
	public async Task NeighborsThatTwoChainsShareGetOneBlankLineBetweenThem()
	{
		string source = Method(
			"if (c0)\n{\n\treturn;\n}\nelse\n{\n\tCall(1, 2);\n}\nif (c1)\n{\n\treturn;\n}\nelse\n{\n\tCall(3, 4);\n}"
		);

		string fixedText = await RoslynTestHarness.AssertFixesAgreeAsync(source, Rule);

		Assert.DoesNotContain("\n\n\n", fixedText);
		Assert.DoesNotContain("else", fixedText);
	}

	[TestMethod]
	public async Task TheBraceRuleBracesAChainBeforeTheElseGoes()
	{
		string source = Method("if (c0)\n\treturn;\nelse\n{\n\tCall(1, 2);\n\tCall(3, 4);\n}");

		// ZA0205 braces the chain, then this rule moves the else body out and writes the gap ZA0001 asks for.
		string fixedSource = Method("if (c0)\n{\n\treturn;\n}\n\nCall(1, 2);\nCall(3, 4);");

		Assert.AreEqual(fixedSource, await RoslynTestHarness.AssertFixesAgreeAsync(source, Rule));
	}

	[TestMethod]
	public async Task TheElseGoesWithoutBracesWhenTheBraceRuleIsOff()
	{
		string source = Method("if (c0)\n\treturn;\nelse\n{\n\tCall(1, 2);\n\tCall(3, 4);\n}");
		string fixedSource = Method("if (c0)\n\treturn;\n\nCall(1, 2);\nCall(3, 4);");

		var rules = new Dictionary<string, ReportDiagnostic>(Rule, StringComparer.Ordinal)
		{
			[DiagnosticDescriptors.UseBracesConsistently.Id] = ReportDiagnostic.Suppress,
		};

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source, rules);

		Assert.AreEqual(fixedSource, result.FixedText);
	}

	[TestMethod]
	public async Task TheElseGoesWithoutBracesWhenBracesAreNotPreferred()
	{
		string source = Method("if (c0)\n\treturn;\nelse\n{\n\tCall(1, 2);\n\tCall(3, 4);\n}");
		string fixedSource = Method("if (c0)\n\treturn;\n\nCall(1, 2);\nCall(3, 4);");

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,
			Rule,
			new Dictionary<string, string> { ["csharp_prefer_braces"] = "false" }
		);

		Assert.AreEqual(fixedSource, result.FixedText);
	}

	// Each chain declares a name of its own, so each one asks whether the block around it reuses the name.
	[TestMethod]
	public void TheCostGrowsLinearlyWithTheNumberOfChainsInABlock()
	{
		static string Chains(int count)
		{
			return Method(
				string.Concat(
					Enumerable
						.Range(0, count)
						.Select(static index =>
							$"if (c0)\n\treturn;\nelse\n{{\n\tvar v{index} = {index};\n\tCall(v{index}, 1);\n}}\n\n"
						)
				)
			);
		}

		LinearCost.AssertLinear(
			Chains(250),
			Chains(1_000),

			static root =>
			{
				SourceText sourceText = root.SyntaxTree.GetText();

				foreach (
					IfStatementSyntax ifStatement in root.DescendantNodes()
						.OfType<IfStatementSyntax>()
				)
				{
					ElseAfterJump.CreateRemoval(sourceText, ifStatement);
				}
			}
		);
	}

	public static IEnumerable<object[]> FreedStatementGaps { get; } =
	[
		[
			"a braced branch",
			"if (c0)\n{\n\treturn;\n}\nelse\n{\n\tDoThing();\n}",
			"if (c0)\n{\n\treturn;\n}\n\nDoThing();",
		],

		[
			"a branch without braces",
			"if (c0)\n\treturn;\nelse\n\tDoThing();",
			"if (c0)\n\treturn;\n\nDoThing();",
		],

		[
			"a comment above the statement",
			"if (c0)\n{\n\treturn;\n}\nelse\n{\n\t// Note.\n\tDoThing();\n}",
			"if (c0)\n{\n\treturn;\n}\n\n// Note.\nDoThing();",
		],

		[
			"a comment beside the branch",
			"if (c0)\n{\n\treturn;\n} // Note.\nelse\n{\n\tDoThing();\n}",
			"if (c0)\n{\n\treturn;\n} // Note.\n\nDoThing();",
		],

		[
			"a multi-line statement below one line",
			"if (c0) return;\nelse\n{\n\tCall(\n\t\t1,\n\t\t2);\n}",
			"if (c0) return;\n\nCall(\n\t1,\n\t2);",
		],

		["a single-line chain", "if (c0) return; else DoThing();", "if (c0) return;\nDoThing();"],

		[
			"two lines that stay single-line units",
			"if (c0) return;\nelse DoThing();",
			"if (c0) return;\nDoThing();",
		],

		[
			"a statement on the line of the branch",
			"if (c0)\n\treturn; else DoThing();",
			"if (c0)\n\treturn;\n\tDoThing();",
		],
	];

	// One fix ends on the text ZA0001 would ask for, so no report of that rule follows it.
	[TestMethod]
	[DynamicData(
		nameof(FreedStatementGaps),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task OneFixWritesTheGapBelowTheBranchThatTheMultiLineRuleAsksFor(
		string name,
		string statement,
		string fixedStatement
	)
	{
		string[] texts = await RoslynTestHarness.ApplyEachFixAloneAsync(
			Method(statement),
			DiagnosticDescriptors.RemoveElseAfterJump,
			Rule
		);

		Assert.AreEqual(Method(fixedStatement), texts.Single(), name);
	}

	[TestMethod]
	public async Task TheFreedStatementsStayTightWhereAPragmaSilencesTheMultiLineRule()
	{
		string pragma =
			$"#pragma warning disable {DiagnosticDescriptors.SeparateMultiLineConstructs.Id}\n";

		string source = pragma + Method("if (c0)\n{\n\treturn;\n}\nelse\n{\n\tDoThing();\n}");
		string fixedSource = pragma + Method("if (c0)\n{\n\treturn;\n}\nDoThing();");

		string[] texts = await RoslynTestHarness.ApplyEachFixAloneAsync(
			source,
			DiagnosticDescriptors.RemoveElseAfterJump,
			Rule
		);

		Assert.AreEqual(fixedSource, texts.Single());
	}

	// A read of the comments that starts at the root passes every statement of the block for each chain, which allocates nothing but takes quadratic time.
	[TestMethod]
	[DoNotParallelize]
	public void AHugeBlockOfChainsIsWalkedInUnderThreeSeconds()
	{
		string chains = Method(
			string.Concat(
				Enumerable
					.Range(0, 16_000)
					.Select(static index =>
						$"if (c0)\n\treturn;\nelse\n{{\n\tvar v{index} = {index};\n\tCall(v{index}, 1);\n}}\n\n"
					)
			)
		);

		LinearCost.AssertWalkEndsWithin(
			milliseconds: 3_000,
			chains,

			static root =>
			{
				SourceText sourceText = root.SyntaxTree.GetText();

				foreach (
					IfStatementSyntax ifStatement in root.DescendantNodes()
						.OfType<IfStatementSyntax>()
				)
				{
					ElseAfterJump.CreateRemoval(sourceText, ifStatement);
				}
			}
		);
	}

	private static async Task<AnalyzerRunResult> RunAsync(string source)
	{
		using IDisposable skipped = RoslynTestHarness.SkippingDefaultRules();

		return await RoslynTestHarness.RunAsync<RemoveElseAfterJumpAnalyzer>(
			source,
			RuleWithoutBraces
		);
	}

	// The chain stands in a block of a section, since a goto case needs a switch and an if of the section's own list is not in a block.
	private static string Jumping(string jump, bool moved)
	{
		string elseText = moved
			? "\t\t\t\t\tDoThing();"
			: "\t\t\t\t\telse\n\t\t\t\t\t{\n\t\t\t\t\t\tDoThing();\n\t\t\t\t\t}";

		return "class C\n{\n\tbool c0;\n\n\tvoid M(int value, bool again)\n\t{\n\t\twhile (again)\n\t\t{\n\t\t\tswitch (value)\n\t\t\t{\n\t\t\t\tcase 1:\n\t\t\t\t{\n\t\t\t\t\tif (c0)\n\t\t\t\t\t{\n\t\t\t\t\t\t"
			+ jump
			+ "\n\t\t\t\t\t}\n"
			+ elseText
			+ "\n\n\t\t\t\t\tbreak;\n\t\t\t\t}\n\t\t\t}\n\t\t}\n\n\tdone:\n\t\tDoThing();\n\t}\n}\n";
	}

	// The members every fixture shares, so each if names itself and no snippet needs a symbol of its own.
	private static string Method(string statements)
	{
		string body = string.Join(
			"\n",

			statements
				.Split('\n')
				.Select(static line => line.Length == 0 || line[0] == '#' ? line : "\t\t" + line)
		);

		return $"class C\n{{\n\tbool c0, c1, c2, c3;\n\tint x;\n\n\tvoid M(object other)\n\t{{\n{body}\n\t}}\n}}\n";
	}
}
