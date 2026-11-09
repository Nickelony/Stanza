using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Regions;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Regions;

[TestClass]
public sealed class NameClosingConditionalDirectiveTests
{
	private const string LinesKey = "stanza_min_lines_to_name_endif";

	private static IReadOnlyDictionary<string, ReportDiagnostic> NameRuleOnly { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.NameClosingConditionalDirective);

	private static IReadOnlyDictionary<string, string> EveryBlock { get; } =
		new Dictionary<string, string> { [LinesKey] = "0" };

	[TestMethod]
	public async Task ABareEndifTakesTheConditionOfItsIf()
	{
		const string source = """
			internal sealed class Sample
			{
			#if DEBUG
				private int _count;
			#endif
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
			#if DEBUG
				private int _count;
			#endif // DEBUG
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameClosingConditionalDirectiveAnalyzer>(
				source,
				NameRuleOnly,
				EveryBlock
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);

		RoslynTestHarness.AssertChangeRidesDiagnostic(result);
	}

	[TestMethod]
	public async Task AShortBlockStaysUnnamedAtTheDefaultAndALongOneIsNamed()
	{
		string shortBlock = Wrap("#if DEBUG\n" + Fields(9) + "#endif\n");
		string longBlock = Wrap("#if DEBUG\n" + Fields(10) + "#endif\n");
		string fixedSource = Wrap("#if DEBUG\n" + Fields(10) + "#endif // DEBUG\n");

		AnalyzerRunResult quiet =
			await RoslynTestHarness.RunAsync<NameClosingConditionalDirectiveAnalyzer>(
				shortBlock,
				NameRuleOnly
			);

		Assert.IsEmpty(
			quiet.Diagnostics,
			"Nine lines between the directives fall short of the default."
		);

		AnalyzerRunResult named =
			await RoslynTestHarness.RunAsync<NameClosingConditionalDirectiveAnalyzer>(
				longBlock,
				NameRuleOnly
			);

		Assert.HasCount(
			1,
			named.Diagnostics,
			"Ten lines between the directives reach the default."
		);

		RoslynTestHarness.AssertFixedText(fixedSource, named);
	}

	[TestMethod]
	public async Task TheThresholdCountsTheLinesBetweenTheDirectives()
	{
		Dictionary<string, string> atThree = new() { [LinesKey] = "3" };
		string fixedSource = Wrap("#if DEBUG\n" + Fields(3) + "#endif // DEBUG\n");

		AnalyzerRunResult quiet =
			await RoslynTestHarness.RunAsync<NameClosingConditionalDirectiveAnalyzer>(
				Wrap("#if DEBUG\n" + Fields(2) + "#endif\n"),
				NameRuleOnly,
				atThree
			);

		Assert.IsEmpty(quiet.Diagnostics);

		AnalyzerRunResult named =
			await RoslynTestHarness.RunAsync<NameClosingConditionalDirectiveAnalyzer>(
				Wrap("#if DEBUG\n" + Fields(3) + "#endif\n"),
				NameRuleOnly,
				atThree
			);

		Assert.HasCount(1, named.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, named);
	}

	[TestMethod]
	public async Task AZeroThresholdNamesABlockWithNothingBetweenItsDirectives()
	{
		const string source = """
			internal sealed class Sample
			{
			#if DEBUG
			#endif
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
			#if DEBUG
			#endif // DEBUG
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameClosingConditionalDirectiveAnalyzer>(
				source,
				NameRuleOnly,
				EveryBlock
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	public static IEnumerable<object[]> ConditionsAndTheirNames { get; } =
	[
		["a negation", "!DEBUG", "!DEBUG"],
		["a conjunction", "A && !B", "A && !B"],
		["a grouped condition", "(A || B) && C", "(A || B) && C"],
		["an equality", "A == true", "A == true"],
		["a condition with irregular spacing", "A   &&\t!B", "A && !B"],
		["a condition with a note on its line", "DEBUG // the debug build", "DEBUG"],
	];

	[TestMethod]
	[DynamicData(
		nameof(ConditionsAndTheirNames),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task TheConditionIsWrittenInFull(string name, string condition, string written)
	{
		string source = Wrap($"#if {condition}\n\tprivate int _count;\n#endif\n");

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameClosingConditionalDirectiveAnalyzer>(
				source,
				NameRuleOnly,
				EveryBlock
			);

		Assert.HasCount(1, result.Diagnostics, name);

		RoslynTestHarness.AssertFixedText(
			Wrap($"#if {condition}\n\tprivate int _count;\n#endif // {written}\n"),
			result
		);
	}

	public static IEnumerable<object[]> NamesAlreadyInPlace { get; } =
	[
		["the exact name", "#endif // DEBUG"],
		["a name with no space after the marker", "#endif //DEBUG"],
		["a name with its own spacing", "#endif    //    DEBUG"],
	];

	[TestMethod]
	[DynamicData(
		nameof(NamesAlreadyInPlace),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ANameThatMatchesTheConditionIsLeftAlone(string name, string closing)
	{
		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameClosingConditionalDirectiveAnalyzer>(
				Wrap($"#if DEBUG\n\tprivate int _count;\n{closing}\n"),
				NameRuleOnly,
				EveryBlock
			);

		Assert.IsEmpty(result.Diagnostics, name);
	}

	[TestMethod]
	public async Task TheSpacingOfAnOperatorDoesNotMakeAMatchDisagree()
	{
		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameClosingConditionalDirectiveAnalyzer>(
				Wrap("#if A && !B\n\tprivate int _count;\n#endif // A&&!B\n"),
				NameRuleOnly,
				EveryBlock
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	public static IEnumerable<object[]> NotesThatNameNothing { get; } =
	[
		["a sentence", "#endif // end of the debug code"],
		["a note with a label", "#endif // TODO: remove"],
		["a note with two comments", "#endif // DEBUG // see the build file"],
		["a one-word note", "#endif // done"],
		["a word that is a symbol", "#endif // TODO"],
		["a platform", "#endif // Windows"],
		["a different symbol", "#endif // RELEASE"],
		["a negated symbol", "#endif // !DEBUG"],
		["a symbol with a stray operator", "#endif // DEBUG && TRACE"],
		["a different symbol with no space after the marker", "#endif //RELEASE"],
		["a different symbol with trailing blanks", "#endif // RELEASE   "],
	];

	[TestMethod]
	[DynamicData(
		nameof(NotesThatNameNothing),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ACommentThatIsNoNameOfTheConditionStays(string name, string closing)
	{
		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameClosingConditionalDirectiveAnalyzer>(
				Wrap($"#if DEBUG\n\tprivate int _count;\n{closing}\n"),
				NameRuleOnly,
				EveryBlock
			);

		Assert.IsEmpty(result.Diagnostics, name);
	}

	[TestMethod]
	public async Task OnlyTheEndifTakesTheNameOfAChain()
	{
		const string source = """
			internal sealed class Sample
			{
			#if A
				private int _first;
			#elif B
				private int _second;
			#else
				private int _third;
			#endif
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
			#if A
				private int _first;
			#elif B
				private int _second;
			#else
				private int _third;
			#endif // A
			}
			""";

		foreach (string symbols in new[] { string.Empty, "#define A\n", "#define B\n" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<NameClosingConditionalDirectiveAnalyzer>(
					symbols + source,
					NameRuleOnly,
					EveryBlock
				);

			Assert.HasCount(1, result.Diagnostics, $"The chain is judged with [{symbols.Trim()}].");
			RoslynTestHarness.AssertFixedText(symbols + fixedSource, result);
		}
	}

	[TestMethod]
	public async Task EachNestedBlockTakesItsOwnConditionInEveryView()
	{
		string source = """
			internal sealed class Sample
			{
			#if A
				private int _a;
			#if B
				private int _b;
			#if C
				private int _c;
			#endif
			#endif
			#endif
			}
			""".ReplaceLineEndings("\n");

		// A view names the blocks its symbols make active and leaves the directives of disabled text as written.
		(string Symbols, string EndB, string EndC)[] views =
		[
			(string.Empty, "#endif", "#endif"),
			("#define A\n", "#endif // B", "#endif"),
			("#define A\n#define B\n", "#endif // B", "#endif // C"),
			("#define A\n#define B\n#define C\n", "#endif // B", "#endif // C"),
		];

		foreach ((string symbols, string endB, string endC) in views)
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<NameClosingConditionalDirectiveAnalyzer>(
					symbols + source,
					NameRuleOnly,
					EveryBlock
				);

			string expected =
				"internal sealed class Sample\n{\n#if A\n\tprivate int _a;\n#if B\n\tprivate int _b;\n#if C\n\tprivate int _c;\n"
				+ $"{endC}\n{endB}\n#endif // A\n}}";

			RoslynTestHarness.AssertFixedText(
				symbols + expected,
				result,
				$"The blocks are judged with [{symbols.Trim()}]."
			);
		}
	}

	[TestMethod]
	public async Task ALabelWrittenInOneViewIsNeverRewrittenInAnother()
	{
		const string source = """
			internal sealed class Sample
			{
			#if A
				private int _a;
			#if B
				private int _b;
			#endif
			#endif
			}
			""";

		string[] views = [string.Empty, "#define A\n", "#define A\n#define B\n", "#define B\n"];

		foreach (string first in views)
		{
			AnalyzerRunResult written =
				await RoslynTestHarness.RunAsync<NameClosingConditionalDirectiveAnalyzer>(
					first + source,
					NameRuleOnly,
					EveryBlock
				);

			string body = written.FixedText[first.Length..];
			string[] bodyLines = body.ReplaceLineEndings("\n").Split('\n');

			foreach (string second in views)
			{
				AnalyzerRunResult again =
					await RoslynTestHarness.RunAsync<NameClosingConditionalDirectiveAnalyzer>(
						second + body,
						NameRuleOnly,
						EveryBlock
					);

				string[] againLines = again
					.FixedText[second.Length..]
					.ReplaceLineEndings("\n")
					.Split('\n');

				Assert.HasCount(bodyLines.Length, againLines);

				AssertLabelsStay(
					bodyLines,
					againLines,
					$"The label written with [{first.Trim()}] was rewritten with [{second.Trim()}]."
				);
			}
		}
	}

	// A second view may name a bare directive that its symbols make active, but it never rewrites a label.
	private static void AssertLabelsStay(string[] lines, string[] againLines, string message)
	{
		for (int index = 0; index < lines.Length; index++)
		{
			if (lines[index] != againLines[index])
				Assert.AreEqual("#endif", lines[index], message);
		}
	}

	[TestMethod]
	public async Task ABlockInDisabledTextStaysAsWritten()
	{
		const string source = """
			internal sealed class Sample
			{
			#if false
			#if B
				private int _b;
			#endif
			#endif
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
			#if false
			#if B
				private int _b;
			#endif
			#endif // false
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameClosingConditionalDirectiveAnalyzer>(
				source,
				NameRuleOnly,
				EveryBlock
			);

		Assert.HasCount(1, result.Diagnostics, "Only the block that the file reads is named.");
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABlockWhoseConditionIsFalseIsNamedInEveryView()
	{
		const string source = """
			internal sealed class Sample
			{
			#if A
				private int _a;
			#endif
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
			#if A
				private int _a;
			#endif // A
			}
			""";

		foreach (string symbols in new[] { string.Empty, "#define A\n" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<NameClosingConditionalDirectiveAnalyzer>(
					symbols + source,
					NameRuleOnly,
					EveryBlock
				);

			Assert.HasCount(1, result.Diagnostics, $"The block is judged with [{symbols.Trim()}].");
			RoslynTestHarness.AssertFixedText(symbols + fixedSource, result);
		}
	}

	[TestMethod]
	public async Task ABlockAroundWholeUnitsAndOneInsideAnExpressionAreBothNamed()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total =
			#if A
					1
			#else
					2
			#endif
				;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private int _total =
			#if A
					1
			#else
					2
			#endif // A
				;
			}
			""";

		foreach (string symbols in new[] { string.Empty, "#define A\n" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<NameClosingConditionalDirectiveAnalyzer>(
					symbols + source,
					NameRuleOnly,
					EveryBlock
				);

			Assert.HasCount(1, result.Diagnostics, $"The block is judged with [{symbols.Trim()}].");
			RoslynTestHarness.AssertFixedText(symbols + fixedSource, result);
		}
	}

	[TestMethod]
	public async Task AnEndifThatClosesNoBlockIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _first;
			#endif
				private int _second;
			}
			""";

		// The directive is no source the compiler accepts, and a run over every analyzer fails if one throws on it.
		ImmutableArray<Diagnostic> diagnostics = await RoslynTestHarness.RunWithoutCompilingAsync(
			source,
			NameRuleOnly
		);

		Assert.IsEmpty(diagnostics);
	}

	[TestMethod]
	public async Task AnIfWithoutAConditionIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
			#if
				private int _first;
			#endif
			}
			""";

		ImmutableArray<Diagnostic> diagnostics = await RoslynTestHarness.RunWithoutCompilingAsync(
			source,
			NameRuleOnly
		);

		Assert.IsEmpty(diagnostics);
	}

	[TestMethod]
	public async Task ARegionInsideTheBlockIsNotTouched()
	{
		const string source = """
			internal sealed class Sample
			{
			#if DEBUG
				#region Fields
				private int _count;
				#endregion
			#endif
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
			#if DEBUG
				#region Fields
				private int _count;
				#endregion
			#endif // DEBUG
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameClosingConditionalDirectiveAnalyzer>(
				source,
				NameRuleOnly,
				EveryBlock
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnEndifAtTheEndOfTheFileTakesTheName()
	{
		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameClosingConditionalDirectiveAnalyzer>(
				"#if DEBUG\ninternal sealed class Sample\n{\n}\n#endif",
				NameRuleOnly,
				EveryBlock
			);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(
			"#if DEBUG\ninternal sealed class Sample\n{\n}\n#endif // DEBUG",
			result
		);
	}

	[TestMethod]
	public async Task AConditionNameFixWorksWithoutTheAdditionalLocation()
	{
		string source = Wrap("#if DEBUG\n" + Fields(10) + "#endif\n");
		string fixedSource = Wrap("#if DEBUG\n" + Fields(10) + "#endif // DEBUG\n");

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<NameClosingConditionalDirectiveAnalyzer>(
				source,
				NameRuleOnly
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnEndifPaddedWithBlanksLosesThemWithTheName()
	{
		string source = Wrap("#if DEBUG\n\tprivate int _count;\n#endif   \n");
		string fixedSource = Wrap("#if DEBUG\n\tprivate int _count;\n#endif // DEBUG\n");

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameClosingConditionalDirectiveAnalyzer>(
				source,
				NameRuleOnly,
				EveryBlock
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheLineBreakAfterTheDirectiveIsKept()
	{
		string source = Wrap("#if DEBUG\n\tprivate int _count;\n#endif\n")
			.ReplaceLineEndings("\r\n");

		string fixedSource = Wrap("#if DEBUG\n\tprivate int _count;\n#endif // DEBUG\n")
			.ReplaceLineEndings("\r\n");

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<NameClosingConditionalDirectiveAnalyzer>(
				source,
				NameRuleOnly,
				EveryBlock
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnUnreadableThresholdFallsBackToTheDefault()
	{
		string source = Wrap("#if DEBUG\n" + Fields(9) + "#endif\n");

		foreach (string value in new[] { "-1", "many", "1001" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<NameClosingConditionalDirectiveAnalyzer>(
					source,
					NameRuleOnly,
					new Dictionary<string, string> { [LinesKey] = value }
				);

			Assert.IsEmpty(result.Diagnostics, $"A threshold of '{value}' reads as the default.");
		}
	}

	private static string Wrap(string members) =>
		"internal sealed class Sample\n{\n" + members + "}\n";

	private static string Fields(int count)
	{
		return string.Concat(
			Enumerable.Range(0, count).Select(index => $"\tprivate int _field{index};\n")
		);
	}
}
