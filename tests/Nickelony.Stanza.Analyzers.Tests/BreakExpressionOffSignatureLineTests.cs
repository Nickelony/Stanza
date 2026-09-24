using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Bodies;

using static Nickelony.Stanza.Analyzers.Tests.BodyFamilyFixtures;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class BreakExpressionOffSignatureLineTests
{
	private const string PickSource = """
		class C
		{
			private static Kind Pick(bool first) => first ? Kind.One : Kind.Two;
		}

		enum Kind
		{
			One,
			Two,
		}
		""";

	private const string PickFixedSource = """
		class C
		{
			private static Kind Pick(bool first) =>
				first ? Kind.One : Kind.Two;
		}

		enum Kind
		{
			One,
			Two,
		}
		""";

	private const string WideSource = """
		class C
		{
			private static Kind PickTheOneThatComesFirstOfAll(bool firstOfTheTwoValues) => firstOfTheTwoValues ? Kind.One : Kind.Two;
		}

		enum Kind
		{
			One,
			Two,
		}
		""";

	[TestMethod]
	public async Task ALongMemberBreaksAfterTheArrow()
	{
		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<BreakExpressionOffSignatureLineAnalyzer>(
				PickSource,
				analyzerConfigOptions: Width(60)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(PickFixedSource, result);
	}

	// The tab after `int` ends at column 8, so the line is 29 columns wide and fits the limit of 30.
	[TestMethod]
	public async Task ATabInsideTheSignatureReachesTheNextTabStop()
	{
		const string source = "class C\n{\n\tint _value;\n\n\tint\tGetValue() => _value;\n}\n";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<BreakExpressionOffSignatureLineAnalyzer>(
				source,
				analyzerConfigOptions: Width(30)
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheContentPaysForNothingInTheWidth()
	{
		AnalyzerRunResult atTheContentWidth =
			await RoslynTestHarness.ReportAsync<BreakExpressionOffSignatureLineAnalyzer>(
				PickSource,
				analyzerConfigOptions: Width(72)
			);

		Assert.IsEmpty(atTheContentWidth.Diagnostics);

		AnalyzerRunResult oneColumnShort =
			await RoslynTestHarness.ReportAsync<BreakExpressionOffSignatureLineAnalyzer>(
				PickSource,
				analyzerConfigOptions: Width(71)
			);

		Assert.HasCount(1, oneColumnShort.Diagnostics);
	}

	[TestMethod]
	public async Task AMemberThatFitsOneLineIsLeftAlone()
	{
		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<BreakExpressionOffSignatureLineAnalyzer>(PickSource);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ZeroWidthSwitchesTheCheckOff()
	{
		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<BreakExpressionOffSignatureLineAnalyzer>(
				WideSource,
				analyzerConfigOptions: Width(0)
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AWidthThatIsNotANumberLeavesTheDefault()
	{
		foreach (string value in new[] { "wide", "-1", "" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.ReportAsync<BreakExpressionOffSignatureLineAnalyzer>(
					WideSource,

					analyzerConfigOptions: new Dictionary<string, string>
					{
						["stanza_max_line_length"] = value,
					}
				);

			Assert.HasCount(1, result.Diagnostics, $"'{value}' must read as the default width.");
		}

		// The control: the same member, at the width the default names.
		AnalyzerRunResult atTheDefault =
			await RoslynTestHarness.ReportAsync<BreakExpressionOffSignatureLineAnalyzer>(
				WideSource,
				analyzerConfigOptions: Width(100)
			);

		Assert.HasCount(1, atTheDefault.Diagnostics);
	}

	[TestMethod]
	public async Task TheLeadingTasteOpensTheArrowLine()
	{
		const string fixedSource = """
			class C
			{
				private static Kind Pick(bool first)
					=> first ? Kind.One : Kind.Two;
			}

			enum Kind
			{
				One,
				Two,
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<BreakExpressionOffSignatureLineAnalyzer>(
				PickSource,
				TestRuleSets.GapRulesEnabled,
				analyzerConfigOptions: Width(60)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AMemberAtColumnZeroTakesTheIndentationOfItsNeighborLines()
	{
		const string source = """
			Kind Pick(bool first) => first ? Kind.One : Kind.Two;

			enum Kind
			{
				One,
				Two,
			}
			""";

		const string fixedSource = """
			Kind Pick(bool first) =>
				first ? Kind.One : Kind.Two;

			enum Kind
			{
				One,
				Two,
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<BreakExpressionOffSignatureLineAnalyzer>(
				source,
				analyzerConfigOptions: Width(40)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABrokenMemberKeepsItsLine()
	{
		const string source = """
			class C
			{
				int Pick(int value) =>
					value + 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<BreakExpressionOffSignatureLineAnalyzer>(
				source,
				analyzerConfigOptions: Width(10)
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnArrowThatOpensItsOwnLineIsLeftAlone()
	{
		const string source = """
			class C
			{
				int Pick(int value)
					=> value + 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<BreakExpressionOffSignatureLineAnalyzer>(
				source,
				analyzerConfigOptions: Width(10)
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACommentBeforeTheArrowBlocksOnlyTheLeadingBreak()
	{
		const string source = """
			class C
			{
				int Pick(int value) /* stays */ => value + 1;
			}
			""";

		const string fixedSource = """
			class C
			{
				int Pick(int value) /* stays */ =>
					value + 1;
			}
			""";

		AnalyzerRunResult trailing =
			await RoslynTestHarness.RunAsync<BreakExpressionOffSignatureLineAnalyzer>(
				source,
				analyzerConfigOptions: Width(40)
			);

		AnalyzerRunResult leading =
			await RoslynTestHarness.RunAsync<BreakExpressionOffSignatureLineAnalyzer>(
				source,
				TestRuleSets.GapRulesEnabled,
				analyzerConfigOptions: Width(40)
			);

		Assert.HasCount(1, trailing.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, trailing);
		Assert.IsEmpty(leading.Diagnostics);
	}

	[TestMethod]
	public async Task AWrappedExpressionIsNotThisRulesBusiness()
	{
		const string source = """
			class C
			{
				int Pick(int value) => value
					+ value;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<BreakExpressionOffSignatureLineAnalyzer>(
				source,
				analyzerConfigOptions: Width(10)
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACommentAfterTheArrowKeepsTheMember()
	{
		const string source = """
			class C
			{
				int Pick(int value) => /* stays */ value + 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<BreakExpressionOffSignatureLineAnalyzer>(
				source,
				analyzerConfigOptions: Width(20)
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ASignatureThatCannotFitItsLineIsLeftAlone()
	{
		const string source = """
			class C
			{
				private static int BuildTheValueFromTheGivenNumber(int value) => value + 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<BreakExpressionOffSignatureLineAnalyzer>(
				source,
				analyzerConfigOptions: Width(50)
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ABodyThatCannotFitItsOwnLineIsLeftAlone()
	{
		const string source = """
			class C
			{
				int Pick(int value) => value + value + value + value + value + value + value + value;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<BreakExpressionOffSignatureLineAnalyzer>(
				source,
				analyzerConfigOptions: Width(40)
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AValueMemberBreaksAfterItsArrow()
	{
		const string source = """
			class C
			{
				private int _value;

				int Value => _value + _value + _value;
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _value;

				int Value =>
					_value + _value + _value;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<BreakExpressionOffSignatureLineAnalyzer>(
				source,
				analyzerConfigOptions: Width(35)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);

		AnalyzerRunResult narrow =
			await RoslynTestHarness.RunAsync<BreakExpressionOffSignatureLineAnalyzer>(
				source,
				analyzerConfigOptions: Width(30)
			);

		Assert.IsEmpty(narrow.Diagnostics, "The body does not fit a line of its own at 30.");
	}

	[TestMethod]
	public async Task TheTriggerDecidesWhetherTheArrowPaysForTheLineItEnds()
	{
		const string source = """
			class C
			{
				string Describe(int value) => value.ToString();
			}
			""";

		const string fixedSource = """
			class C
			{
				string Describe(int value) =>
					value.ToString();
			}
			""";

		// The signature line is 30 columns, its line with the arrow 33, and the statement's 25,
		// so 31 misses the arrow while either line still fits on its own.
		foreach (string charged in new[] { "either", "signature" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<BreakExpressionOffSignatureLineAnalyzer>(
					source,
					analyzerConfigOptions: WidthAndTrigger(31, charged)
				);

			Assert.IsEmpty(
				result.Diagnostics,
				$"'{charged}' charges the arrow for the line it ends."
			);
		}

		foreach (string free in new[] { "body", "both" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<BreakExpressionOffSignatureLineAnalyzer>(
					source,
					analyzerConfigOptions: WidthAndTrigger(31, free)
				);

			Assert.HasCount(1, result.Diagnostics, $"'{free}' frees the arrow from the width.");
			RoslynTestHarness.AssertFixedText(fixedSource, result);
		}
	}

	private static Dictionary<string, string> WidthAndTrigger(int columns, string trigger)
	{
		return new()
		{
			["stanza_max_line_length"] = $"{columns}",
			["stanza_multiline_body_trigger"] = trigger,
		};
	}

	[TestMethod]
	public async Task TheExpressionLineBreakRuleReadsThePragmaHandOverTheArrowPairHonors()
	{
		string source = $$"""
			class C
			{
			#pragma warning disable {{DiagnosticDescriptors.PutArrowOnLineAfterSignature.Id}}
				private static Kind Pick(bool first) => first ? Kind.One : Kind.Two;
			#pragma warning restore {{DiagnosticDescriptors.PutArrowOnLineAfterSignature.Id}}
			}

			enum Kind
			{
				One,
				Two,
			}
			""";

		string fixedSource = $$"""
			class C
			{
			#pragma warning disable {{DiagnosticDescriptors.PutArrowOnLineAfterSignature.Id}}
				private static Kind Pick(bool first) =>
					first ? Kind.One : Kind.Two;
			#pragma warning restore {{DiagnosticDescriptors.PutArrowOnLineAfterSignature.Id}}
			}

			enum Kind
			{
				One,
				Two,
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,
			TestRuleSets.ArrowRulesEnabled,

			analyzerConfigOptions: new Dictionary<string, string>
			{
				["stanza_max_line_length"] = "60",
			}
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// A pragma over ZA0044 hands its gap to ZA0045 only while the file enables ZA0045, which ships off.
	[TestMethod]
	public async Task APragmaOverTheTrailingArrowRuleHandsNothingToARuleThatShipsOff()
	{
		string source = $$"""
			class C
			{
			#pragma warning disable {{DiagnosticDescriptors.KeepArrowOnSignatureLine.Id}}
				private static Kind Pick(bool first) => first ? Kind.One : Kind.Two;
			#pragma warning restore {{DiagnosticDescriptors.KeepArrowOnSignatureLine.Id}}
			}

			enum Kind
			{
				One,
				Two,
			}
			""";

		string fixedSource = $$"""
			class C
			{
			#pragma warning disable {{DiagnosticDescriptors.KeepArrowOnSignatureLine.Id}}
				private static Kind Pick(bool first) =>
					first ? Kind.One : Kind.Two;
			#pragma warning restore {{DiagnosticDescriptors.KeepArrowOnSignatureLine.Id}}
			}

			enum Kind
			{
				One,
				Two,
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.KeepArrowOnSignatureLine),

			analyzerConfigOptions: new Dictionary<string, string>
			{
				["stanza_max_line_length"] = "60",
			}
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
