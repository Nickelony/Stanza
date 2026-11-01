using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed class SeparateJumpStatementTests
{
	// Each row is a jump of one kind below two statements, and the jump with its blank line.
	public static IEnumerable<object[]> Jumps { get; } =
	[
		[
			"return",
			"void M(int x)",

			"""
				a = 1;
				b = 2;
				return;
				""",

			"""
				a = 1;
				b = 2;

				return;
				""",
		],

		[
			"throw",
			"void M(int x)",

			"""
				a = 1;
				b = 2;
				throw new System.InvalidOperationException();
				""",

			"""
				a = 1;
				b = 2;

				throw new System.InvalidOperationException();
				""",
		],

		[
			"break",
			"void M(int x)",

			"""
				while (x > 0)
				{
					a = 1;
					b = 2;
					break;
				}
				""",

			"""
				while (x > 0)
				{
					a = 1;
					b = 2;

					break;
				}
				""",
		],

		[
			"continue",
			"void M(int x)",

			"""
				while (x > 0)
				{
					a = 1;
					b = 2;
					continue;
				}
				""",

			"""
				while (x > 0)
				{
					a = 1;
					b = 2;

					continue;
				}
				""",
		],

		[
			"goto",
			"void M(int x)",

			"""
				a = 1;
				b = 2;
				goto done;
				done:
				Do();
				""",

			"""
				a = 1;
				b = 2;

				goto done;
				done:
				Do();
				""",
		],

		[
			"goto case",
			"void M(int x)",

			"""
				switch (x)
				{
					case 1:
						a = 1;
						b = 2;
						goto case 2;
					case 2:
						break;
				}
				""",

			"""
				switch (x)
				{
					case 1:
						a = 1;
						b = 2;

						goto case 2;
					case 2:
						break;
				}
				""",
		],

		[
			"goto default",
			"void M(int x)",

			"""
				switch (x)
				{
					case 1:
						a = 1;
						b = 2;
						goto default;
					default:
						break;
				}
				""",

			"""
				switch (x)
				{
					case 1:
						a = 1;
						b = 2;

						goto default;
					default:
						break;
				}
				""",
		],

		[
			"yield break",
			"System.Collections.Generic.IEnumerable<int> M(int x)",

			"""
				a = 1;
				b = 2;
				yield break;
				""",

			"""
				a = 1;
				b = 2;

				yield break;
				""",
		],
	];

	// Each row is a jump the rule leaves tight, in a method of its own.
	public static IEnumerable<object[]> QuietBodies { get; } =
	[
		["one single-line statement above", "a = 1;\nreturn;"],
		["a jump that opens its block", "if (x > 0)\n{\n\treturn;\n}"],
		["a jump that is an embedded statement", "a = 1;\nb = 2;\nif (x > 0) return;"],
		["a labeled jump", "a = 1;\nb = 2;\ndone: return;"],
		["a statement that is not a jump", "a = 1;\nb = 2;\nc = 3;"],
		["a trailing comment on the one statement above", "a = 1; // First.\nreturn;"],
		["a comment above the one statement above", "// First.\na = 1;\nreturn;"],
		["two statements on the line above", "a = 1; b = 2;\nreturn;"],
		["a stanza already set apart", "a = 1;\nb = 2;\n\nreturn;"],

		[
			"a stanza that a directive ends",
			"a = 1;\nb = 2;\n#pragma warning disable CS0162\nreturn;",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(Jumps),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task EachKindOfJumpIsSetApartFromAStanzaOfTwoLines(
		string name,
		string signature,
		string body,
		string fixedBody
	)
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateJumpStatementAnalyzer>(
			Method(body, signature),
			Alone()
		);

		Assert.HasCount(1, result.Diagnostics, name);
		RoslynTestHarness.AssertFixedText(Method(fixedBody, signature), result, name);
	}

	[TestMethod]
	[DynamicData(
		nameof(QuietBodies),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AJumpTightAgainstASingleLineStanzaIsLeftAlone(string name, string body)
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateJumpStatementAnalyzer>(
			Method(body),
			Alone()
		);

		Assert.IsEmpty(result.Diagnostics, name);
	}

	[TestMethod]
	public async Task AYieldReturnIsNoJump()
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateJumpStatementAnalyzer>(
			Method(
				"a = 1;\nb = 2;\nyield return 1;",
				"System.Collections.Generic.IEnumerable<int> M(int x)"
			),

			Alone()
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AMultiLineStatementAboveAJumpIsSetApartWhereTheMultiLineRuleIsOff()
	{
		string source = Method(
			"""
			if (x > 0)
			{
				Do();
			}
			return;
			"""
		);

		string fixedSource = Method(
			"""
			if (x > 0)
			{
				Do();
			}

			return;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateJumpStatementAnalyzer>(
			source,
			Alone()
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AMultiLineStatementAboveAJumpIsLeftToTheMultiLineRuleWhereItIsOn()
	{
		string source = Method(
			"""
			if (x > 0)
			{
				Do();
			}
			return;
			"""
		);

		AnalyzerRunResult jump = await RoslynTestHarness.ReportAsync<SeparateJumpStatementAnalyzer>(
			source,
			Alone(DiagnosticDescriptors.SeparateMultiLineConstructs)
		);

		AnalyzerRunResult multiLine =
			await RoslynTestHarness.ReportAsync<SeparateMultiLineConstructsAnalyzer>(
				source,
				Alone(DiagnosticDescriptors.SeparateMultiLineConstructs)
			);

		Assert.IsEmpty(jump.Diagnostics, "ZA0001 owns the gap beside a multi-line statement.");
		Assert.HasCount(1, multiLine.Diagnostics);
	}

	[TestMethod]
	public async Task TwoStatementsOnTheLineAboveAJumpMakeOneLineButThreeStatementsMakeTwo()
	{
		string source = Method(
			"""
			a = 1; b = 2;
			c = 3;
			return;
			"""
		);

		string fixedSource = Method(
			"""
			a = 1; b = 2;
			c = 3;

			return;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateJumpStatementAnalyzer>(
			source,
			Alone()
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AJumpOnTheLineOfTheStatementAboveItIsPushedDownBeneathABlankLine()
	{
		string source = Method(
			"""
			a = 1;
			b = 2; return;
			"""
		);

		string fixedSource = Method(
			"""
			a = 1;
			b = 2;

			return;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateJumpStatementAnalyzer>(
			source,
			Alone()
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AJumpOnTheLineOfTheClosingBraceAboveItIsPushedDownBeneathABlankLine()
	{
		string source = Method(
			"""
			if (x > 0)
			{
				Do();
			} return;
			"""
		);

		string fixedSource = Method(
			"""
			if (x > 0)
			{
				Do();
			}

			return;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateJumpStatementAnalyzer>(
			source,
			Alone()
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentLineAmongTheStatementsAboveAJumpCountsTowardTheStanza()
	{
		string source = Method(
			"""
			a = 1;
			// The second.
			b = 2;
			return;
			"""
		);

		string fixedSource = Method(
			"""
			a = 1;
			// The second.
			b = 2;

			return;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateJumpStatementAnalyzer>(
			source,
			Alone()
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentAboveAJumpMovesWithItSoTheBlankLineGoesAboveTheComment()
	{
		string source = Method(
			"""
			a = 1;
			b = 2;
			// Done.
			return;
			"""
		);

		string fixedSource = Method(
			"""
			a = 1;
			b = 2;

			// Done.
			return;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateJumpStatementAnalyzer>(
			source,
			Alone()
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ATrailingCommentOnTheStatementAboveAJumpStaysOnItsLine()
	{
		string source = Method(
			"""
			a = 1;
			b = 2; // The second.
			return;
			"""
		);

		string fixedSource = Method(
			"""
			a = 1;
			b = 2; // The second.

			return;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateJumpStatementAnalyzer>(
			source,
			Alone()
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AJumpIsJudgedByTheStanzaDirectlyAboveItWhateverStandsAbove()
	{
		string source = Method(
			"""
			a = 1;

			b = 2;
			c = 3;
			return;
			"""
		);

		string fixedSource = Method(
			"""
			a = 1;

			b = 2;
			c = 3;

			return;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateJumpStatementAnalyzer>(
			source,
			Alone()
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AStatementThatBranchesToALabelBelongsToTheStanzaAboveTheJump()
	{
		string source = Method(
			"""
			a = 1;
			if (x > 0) goto done;
			return;
			done:
			Do();
			"""
		);

		string fixedSource = Method(
			"""
			a = 1;
			if (x > 0) goto done;

			return;
			done:
			Do();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateJumpStatementAnalyzer>(
			source,
			Alone()
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TwoJumpsOneAfterTheOtherAskForOneBlankLineBecauseTheFirstEndsTheStanza()
	{
		string source = Method(
			"""
			a = 1;
			b = 2;
			return;
			throw new System.InvalidOperationException();
			"""
		);

		string fixedSource = Method(
			"""
			a = 1;
			b = 2;

			return;
			throw new System.InvalidOperationException();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateJumpStatementAnalyzer>(
			source,
			Alone()
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// A section's closing break ends the section rather than a stanza of its logic.
	public static IEnumerable<object[]> ExemptBreaks { get; } =
	[
		[
			"a break that stands in the section",

			"""
				switch (x)
				{
					case 1:
						a = 1;
						b = 2;
						break;
				}
				""",
		],

		[
			"a break that ends the only block of the section",

			"""
				switch (x)
				{
					case 1:
					{
						a = 1;
						b = 2;
						break;
					}
				}
				""",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(ExemptBreaks),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task TheBreakThatClosesASwitchSectionIsNotJudged(string name, string body)
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateJumpStatementAnalyzer>(
			Method(body),
			Alone()
		);

		Assert.IsEmpty(result.Diagnostics, name);
	}

	// Every other jump at the end of a section, and a break that closes no section, is judged.
	public static IEnumerable<object[]> JudgedSectionEnds { get; } =
	[
		[
			"a return at the end of a section",

			"""
				switch (x)
				{
					case 1:
						a = 1;
						b = 2;
						return;
				}
				""",

			"""
				switch (x)
				{
					case 1:
						a = 1;
						b = 2;

						return;
				}
				""",
		],

		[
			"a break that ends a block beside another statement of the section",

			"""
				switch (x)
				{
					case 1:
						Do();
						{
							a = 1;
							b = 2;
							break;
						}
				}
				""",

			"""
				switch (x)
				{
					case 1:
						Do();
						{
							a = 1;
							b = 2;

							break;
						}
				}
				""",
		],

		[
			"a break inside a conditional block of the only block of a section",

			"""
				switch (x)
				{
					case 1:
					{
						if (x > 1)
						{
							a = 1;
							b = 2;
							break;
						}

						break;
					}
				}
				""",

			"""
				switch (x)
				{
					case 1:
					{
						if (x > 1)
						{
							a = 1;
							b = 2;

							break;
						}

						break;
					}
				}
				""",
		],

		[
			"a break that ends a loop body",

			"""
				switch (x)
				{
					case 1:
						while (x > 0)
						{
							a = 1;
							b = 2;
							break;
						}

						break;
				}
				""",

			"""
				switch (x)
				{
					case 1:
						while (x > 0)
						{
							a = 1;
							b = 2;

							break;
						}

						break;
				}
				""",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(JudgedSectionEnds),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AJumpThatIsNotTheClosingBreakOfASectionIsJudged(
		string name,
		string body,
		string fixedBody
	)
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateJumpStatementAnalyzer>(
			Method(body),
			Alone()
		);

		Assert.HasCount(1, result.Diagnostics, name);
		RoslynTestHarness.AssertFixedText(Method(fixedBody), result, name);
	}

	[TestMethod]
	public async Task AStanzaThatADirectiveCrossesKeepsItsJumpTightInBothViews()
	{
		const string body = """
			a = 1;
			#if B
			b = 2;
			#endif
			c = 3;
			return;
			""";

		foreach (string view in new[] { string.Empty, "#define B\n" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<SeparateJumpStatementAnalyzer>(
					view + Method(body),
					Alone()
				);

			Assert.IsEmpty(result.Diagnostics, $"[{view.Trim()}] The directive ends the stanza.");
		}
	}

	[TestMethod]
	public async Task AStanzaOfTwoLinesBelowADirectiveIsSetApartInBothViews()
	{
		const string body = """
			a = 1;
			#if B
			b = 2;
			#endif
			c = 3;
			d = 4;
			return;
			""";

		const string fixedSource = """
			a = 1;
			#if B
			b = 2;
			#endif
			c = 3;
			d = 4;

			return;
			""";

		foreach (string view in new[] { string.Empty, "#define B\n" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<SeparateJumpStatementAnalyzer>(
					view + Method(body),
					Alone()
				);

			Assert.HasCount(1, result.Diagnostics, $"[{view.Trim()}]");
			RoslynTestHarness.AssertFixedText(view + Method(fixedSource), result, view.Trim());
		}
	}

	[TestMethod]
	public async Task AJumpInsideAConditionalBlockKeepsTheDirectiveGapAsWritten()
	{
		const string body = """
			a = 1;
			b = 2;
			#if B
			return;
			#endif
			""";

		foreach (string view in new[] { string.Empty, "#define B\n" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<SeparateJumpStatementAnalyzer>(
					view + Method(body),
					Alone()
				);

			Assert.IsEmpty(result.Diagnostics, $"[{view.Trim()}] The directive keeps the gap.");
		}
	}

	[TestMethod]
	public async Task ARegionMarkerBetweenTheStatementsAndTheJumpKeepsTheGapAsWritten()
	{
		const string body = """
			a = 1;
			b = 2;
			#region Done
			return;
			#endregion
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateJumpStatementAnalyzer>(
			Method(body),
			Alone()
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task APragmaOverTheJumpHandsNothingBackAndSilencesTheRule()
	{
		const string body = """
			#pragma warning disable ZA0009
			a = 1;
			b = 2;
			return;
			#pragma warning restore ZA0009
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateJumpStatementAnalyzer>(
			Method(body),
			Alone()
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ARunOfInvocationsAboveAJumpIsLeftToTheInvocationRuleWhereItIsOn()
	{
		string source = Method(
			"""
			Do();
			Do();
			return;
			"""
		);

		AnalyzerRunResult jump = await RoslynTestHarness.ReportAsync<SeparateJumpStatementAnalyzer>(
			source,
			Alone(DiagnosticDescriptors.SeparateInvocationRuns)
		);

		AnalyzerRunResult runs =
			await RoslynTestHarness.ReportAsync<SeparateInvocationRunsAnalyzer>(
				source,
				Alone(DiagnosticDescriptors.SeparateInvocationRuns)
			);

		Assert.IsEmpty(jump.Diagnostics, "ZA0004 reports the gap, so ZA0009 stays quiet.");
		Assert.HasCount(1, runs.Diagnostics);
	}

	[TestMethod]
	public async Task ARunOfInvocationsAboveAJumpIsSetApartByTheJumpRuleWhereTheInvocationRuleIsOff()
	{
		string source = Method(
			"""
			Do();
			Do();
			return;
			"""
		);

		string fixedSource = Method(
			"""
			Do();
			Do();

			return;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateJumpStatementAnalyzer>(
			source,
			Alone()
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARunOfAssignmentsAboveAJumpIsLeftToTheAssignmentRuleWhereItIsOn()
	{
		string source = Method(
			"""
			a = 1;
			b = 2;
			return;
			"""
		);

		AnalyzerRunResult jump = await RoslynTestHarness.ReportAsync<SeparateJumpStatementAnalyzer>(
			source,
			Alone(DiagnosticDescriptors.SeparateAssignmentRuns)
		);

		AnalyzerRunResult runs =
			await RoslynTestHarness.ReportAsync<SeparateAssignmentRunsAnalyzer>(
				source,
				Alone(DiagnosticDescriptors.SeparateAssignmentRuns)
			);

		Assert.IsEmpty(jump.Diagnostics, "ZA0008 reports the gap, so ZA0009 stays quiet.");
		Assert.HasCount(1, runs.Diagnostics);
	}

	[TestMethod]
	public async Task ARunOfAssignmentsAboveAJumpIsSetApartByTheJumpRuleWhereTheAssignmentRuleIsOff()
	{
		string source = Method(
			"""
			a = 1;
			b = 2;
			return;
			"""
		);

		string fixedSource = Method(
			"""
			a = 1;
			b = 2;

			return;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateJumpStatementAnalyzer>(
			source,
			Alone()
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnotherRulesBlankLineEndsTheStanzaSoTheJumpStaysTight()
	{
		// ZA0002 sets the two declarations apart from the call, which leaves the call alone above the return.
		string source = Method(
			"""
			var first = 1;
			var second = 2;
			Do();
			return;
			"""
		);

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateJumpStatementAnalyzer>(
				source,
				Alone(DiagnosticDescriptors.SeparateDeclarationRuns)
			);

		Assert.IsEmpty(result.Diagnostics, "The call stands alone once the run is set apart.");
	}

	[TestMethod]
	public async Task AStanzaThatAGroupBlankLineWouldNotEndStillSetsTheJumpApartWhereTheGroupRuleIsOff()
	{
		string source = Method(
			"""
			var first = 1;
			var second = 2;
			Do();
			return;
			"""
		);

		string fixedSource = Method(
			"""
			var first = 1;
			var second = 2;
			Do();

			return;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateJumpStatementAnalyzer>(
			source,
			Alone()
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentedSiblingRuleThatIsOnEndsTheStanzaAtTheCommentedStatement()
	{
		string source = Method(
			"""
			a = 1;
			// The second.
			b = 2;
			return;
			"""
		);

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateJumpStatementAnalyzer>(
				source,
				Alone(DiagnosticDescriptors.SeparateCommentedSibling)
			);

		Assert.IsEmpty(result.Diagnostics, "ZA0012 writes a blank line above the comment.");
	}

	[TestMethod]
	public async Task ACommentedSiblingRuleThatIsOffLeavesTheCommentInsideTheStanza()
	{
		string source = Method(
			"""
			a = 1;
			// The second.
			b = 2;
			return;
			"""
		);

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateJumpStatementAnalyzer>(source, Alone());

		Assert.HasCount(1, result.Diagnostics);
	}

	[TestMethod]
	public async Task TheGuardRuleEndsTheStanzaAboveAJumpWhereItIsOn()
	{
		string source = Method(
			"""
			if (x > 0) return;
			a = 1;
			return;
			"""
		);

		AnalyzerRunResult withoutGuards =
			await RoslynTestHarness.ReportAsync<SeparateJumpStatementAnalyzer>(source, Alone());

		AnalyzerRunResult withGuards =
			await RoslynTestHarness.ReportAsync<SeparateJumpStatementAnalyzer>(
				source,
				Alone(DiagnosticDescriptors.SeparateGuardClauses)
			);

		Assert.HasCount(
			1,
			withoutGuards.Diagnostics,
			"The guard and the assignment are a stanza of two lines."
		);

		Assert.IsEmpty(
			withGuards.Diagnostics,
			"ZA0011 writes a blank line below the guard, which ends the stanza."
		);
	}

	[TestMethod]
	public async Task AGuardChainAboveAJumpIsAStanzaOfTwoLinesWhateverTheGuardRuleDoes()
	{
		string source = Method(
			"""
			if (x > 0) return;
			if (x > 1) return;
			return;
			"""
		);

		AnalyzerRunResult withoutGuards =
			await RoslynTestHarness.ReportAsync<SeparateJumpStatementAnalyzer>(source, Alone());

		AnalyzerRunResult withGuards =
			await RoslynTestHarness.ReportAsync<SeparateJumpStatementAnalyzer>(
				source,
				Alone(DiagnosticDescriptors.SeparateGuardClauses)
			);

		Assert.HasCount(1, withoutGuards.Diagnostics);

		Assert.HasCount(
			1,
			withGuards.Diagnostics,
			"The gap above the jump is this rule's, not the guard rule's."
		);
	}

	[TestMethod]
	public async Task ATopLevelJumpBelowAStanzaOfTwoLinesIsSetApart()
	{
		const string source = """
			int a = 1;
			int b = 2;
			return;
			""";

		const string fixedSource = """
			int a = 1;
			int b = 2;

			return;
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateJumpStatementAnalyzer>(
			source,
			Alone()
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ATopLevelThrowAndGotoAreJumpsToo()
	{
		foreach (
			string jump in new[] { "throw new System.InvalidOperationException();", "goto done;" }
		)
		{
			string source = $"int a = 1;\nint b = 2;\n{jump}\ndone:\nreturn;\n";
			string fixedSource = $"int a = 1;\nint b = 2;\n\n{jump}\ndone:\nreturn;\n";

			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<SeparateJumpStatementAnalyzer>(source, Alone());

			Assert.HasCount(1, result.Diagnostics, jump);
			RoslynTestHarness.AssertFixedText(fixedSource, result, jump);
		}
	}

	[TestMethod]
	public async Task ATopLevelJumpTightAgainstASingleLineStanzaIsLeftAlone()
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateJumpStatementAnalyzer>(
			"int a = 1;\nreturn;\n",
			Alone()
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ATopLevelJumpIsJudgedByTheStanzaDirectlyAboveItInBothViews()
	{
		const string source = """
			int a = 1;
			#if B
			int b = 2;
			#endif
			int c = 3;
			int d = 4;
			return;
			""";

		const string fixedSource = """
			int a = 1;
			#if B
			int b = 2;
			#endif
			int c = 3;
			int d = 4;

			return;
			""";

		foreach (string view in new[] { string.Empty, "#define B\n" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<SeparateJumpStatementAnalyzer>(
					view + source,
					Alone()
				);

			Assert.HasCount(1, result.Diagnostics, $"[{view.Trim()}]");
			RoslynTestHarness.AssertFixedText(view + fixedSource, result, view.Trim());
		}
	}

	[TestMethod]
	public async Task TheGapBelowTheImportsOfAFileIsLeftToTheImportRuleWhereItIsOn()
	{
		const string source = """
			using System;
			using System.Linq;
			return;
			""";

		AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(
			source,
			Alone(DiagnosticDescriptors.SeparateImportSection)
		);

		RoslynTestHarness.AssertEachGapIsClaimedOnce(result);
		Assert.HasCount(1, result.Diagnostics);
	}

	[TestMethod]
	public async Task TheContainerOfAJumpIsABlockASwitchSectionOrTheFileButNoOtherList()
	{
		const string source = """
			class C
			{
				int a, b;

				int M(int x)
				{
					return x switch
					{
						1 => 2,
						_ => 3,
					};
				}

				void N()
				{
					var pair = (a, b);
					M(1);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateJumpStatementAnalyzer>(
			source,
			Alone()
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	// ZA0009 alone: the rules that share its gaps are off unless a test names them.
	private static Dictionary<string, ReportDiagnostic> Alone(params DiagnosticDescriptor[] alsoOn)
	{
		return TestRuleSets.OnSuppressingOthersOf(
			[DiagnosticDescriptors.SeparateJumpStatement, .. alsoOn],
			TestRuleSets.SharedGapRules
		);
	}

	// Each jump of the run asks by the stanza above it, which holds the jumps before it.
	[TestMethod]
	public void TheCostGrowsLinearlyWithTheNumberOfJumpsInARun()
	{
		static string Jumps(int count) =>
			$"class C\n{{\n\tvoid M()\n\t{{\n{string.Concat(Enumerable.Repeat("\t\tCall();\n", count))}{string.Concat(Enumerable.Repeat("\t\treturn;\n", count))}\t}}\n}}\n";

		LinearCost.AssertLinear(
			Jumps(250),
			Jumps(1_000),

			static root =>
			{
				ImmutableArray<SyntaxNode> siblings = StanzaSyntax.ReadSiblings(
					root.DescendantNodes().OfType<BlockSyntax>().First()
				);

				BlankLineJumps.ReadAskedJumpBoundaries(
					siblings,
					jumps: BlankLineJumps.FindJumps(siblings),
					gapOwners: default,
					standing: [],
					cancellationToken: default
				);
			}
		);
	}

	// Two calls and a jump, again and again with no blank line, make one stanza that every jump asks by.
	// A read of the stanza above each jump that walks it again for every jump grows with the square of the statements.
	private static string TightBlock(int count) =>
		$"class C\n{{\n\tvoid M()\n\t{{\n{string.Concat(Enumerable.Repeat("\t\tCall();\n\t\tCall();\n\t\treturn;\n", count / 3))}\t}}\n}}\n";

	private static void ReadStanzas(SyntaxNode root)
	{
		ImmutableArray<SyntaxNode> siblings = StanzaSyntax.ReadSiblings(
			root.DescendantNodes().OfType<BlockSyntax>().First()
		);

		ImmutableArray<int> jumps = BlankLineJumps.FindJumps(siblings);
		ImmutableHashSet<int> gathered = [.. Enumerable.Range(1, siblings.Length - 1)];

		// The first read stands on the text as written, and the second gathers every gap, so each stanza reaches back to the block's start.
		BlankLineJumps.ReadAskedJumpBoundaries(
			siblings,
			jumps,
			gapOwners: default,
			standing: [],
			cancellationToken: default
		);

		BlankLineJumps.ReadStayingJumpBoundaries(
			siblings,
			jumps,
			gapOwners: default,
			standing: [],

			gathered: new BlankLineGrouping.GatheredStretch(
				Boundaries: gathered,
				Start: 0,
				End: siblings.Length
			),

			cancellationToken: default
		);
	}

	// The stanza reader answers for every jump of a block from the stanzas it has read, so four times the statements cost about four times the bytes.
	[TestMethod]
	public void TheCostOfReadingStanzasGrowsLinearlyWithTheStatementsOfABlock()
	{
		LinearCost.AssertLinear(
			TightBlock(300),
			TightBlock(1_200),
			static root => ReadStanzas(root)
		);
	}

	// The bytes would not tell a stanza read by a pass over the block per jump from a linear one, so the time does.
	[TestMethod]
	[DoNotParallelize]
	public void TheStanzasOfAHugeBlockAreReadInUnderTwoSeconds()
	{
		LinearCost.AssertWalkEndsWithin(
			milliseconds: 2_000,
			TightBlock(10_000),
			static root => ReadStanzas(root)
		);
	}

	private static string Method(string body, string signature = "void M(int x)")
	{
		return SourceFixtures.WrapBody(
			body,
			signature,
			members: "\tint a, b, c, d;\n\n",
			after: "\n\tvoid Do() { }\n"
		);
	}
}
