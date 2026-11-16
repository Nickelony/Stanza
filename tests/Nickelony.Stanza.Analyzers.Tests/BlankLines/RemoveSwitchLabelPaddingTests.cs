using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed class RemoveSwitchLabelPaddingTests
{
	private static readonly IReadOnlyDictionary<string, ReportDiagnostic> s_on = TestRuleSets.Only(
		DiagnosticDescriptors.RemoveSwitchLabelPadding
	);

	[TestMethod]
	public async Task ABlankLineBelowALabelIsRemoved()
	{
		string source = Switch(
			"""
			case 1:

				DoThing();
				break;
			default:

				DoThing();
				break;
			"""
		);

		string fixedSource = Switch(
			"""
			case 1:
				DoThing();
				break;
			default:
				DoThing();
				break;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveSwitchLabelPaddingAnalyzer>(
			source,
			s_on
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABlankLineBetweenStackedLabelsIsRemoved()
	{
		string source = Switch(
			"""
			case 1:

			case 2:

				DoThing();
				break;
			"""
		);

		string fixedSource = Switch(
			"""
			case 1:
			case 2:
				DoThing();
				break;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveSwitchLabelPaddingAnalyzer>(
			source,
			s_on
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task EveryBlankLineOfAGapGoesAtOnce()
	{
		string source = Switch(
			"""
			case 1:


				DoThing();
				break;
			"""
		);

		string fixedSource = Switch(
			"""
			case 1:
				DoThing();
				break;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveSwitchLabelPaddingAnalyzer>(
			source,
			s_on
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task LabelsWithTightGapsAreNotReported()
	{
		string source = Switch(
			"""
			case 1:
			case 2:
				DoThing();
				break;

			default:
				DoThing();
				break;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync<RemoveSwitchLabelPaddingAnalyzer>(
			source,
			s_on
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheBlankLineBetweenSectionsIsNoLabelPadding()
	{
		string source = Switch(
			"""
			case 1:
				DoThing();
				break;

			case 2:
				DoThing();
				break;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync<RemoveSwitchLabelPaddingAnalyzer>(
			source,
			s_on
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task OnlyTheBlankLinesAboveTheFirstCommentGo()
	{
		string source = Switch(
			"""
			case 1:

				// Review the order.

				DoThing();
				break;
			"""
		);

		string fixedSource = Switch(
			"""
			case 1:
				// Review the order.

				DoThing();
				break;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveSwitchLabelPaddingAnalyzer>(
			source,
			s_on
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ADirectiveInTheGapLeavesItAlone()
	{
		string source = Switch(
			"""
			case 1:

			#if TRACE

				Trace();
			#endif
				DoThing();
				break;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync<RemoveSwitchLabelPaddingAnalyzer>(
			source,
			s_on
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACasePatternAndAWhenClauseAreLabelsToo()
	{
		string source = Switch(
			"""
			case int number when number > 1:

				DoThing();
				break;
			case string:

			case null:

				DoThing();
				break;
			"""
		);

		string fixedSource = Switch(
			"""
			case int number when number > 1:
				DoThing();
				break;
			case string:
			case null:
				DoThing();
				break;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveSwitchLabelPaddingAnalyzer>(
			source,
			s_on
		);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABlockThatOpensASectionLeavesItsGapToTheBracePaddingRuleWhereItIsOn()
	{
		string source = Switch(
			"""
			case 1:

				{
					DoThing();
					break;
				}
			"""
		);

		string fixedSource = Switch(
			"""
			case 1:
				{
					DoThing();
					break;
				}
			"""
		);

		AnalyzerRunResult owned = await RoslynTestHarness.ReportAsync<RemoveSwitchLabelPaddingAnalyzer>(
			source,
			s_on
		);

		Assert.IsEmpty(owned.Diagnostics);

		AnalyzerRunResult handedOver = await RoslynTestHarness.RunAsync<RemoveSwitchLabelPaddingAnalyzer>(
			source,

			TestRuleSets.WithOneOff(
				on: [DiagnosticDescriptors.RemoveSwitchLabelPadding],
				off: DiagnosticDescriptors.RemoveBracePadding
			)
		);

		Assert.HasCount(1, handedOver.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, handedOver);

		AnalyzerRunResult both = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.RemoveSwitchLabelPadding,
				DiagnosticDescriptors.RemoveBracePadding
			)
		);

		RoslynTestHarness.AssertFixedText(fixedSource, both);
	}

	[TestMethod]
	public async Task ThePaddingBeforeAFirstStatementOfAnotherKindIsRemoved()
	{
		string source = Switch(
			"""
			case 1:

				if (DoOther()) { }
				break;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync<RemoveSwitchLabelPaddingAnalyzer>(
			source,
			s_on
		);

		Assert.HasCount(1, result.Diagnostics);
	}

	[TestMethod]
	public async Task ThePaddingOfAGotoLabelIsNoSwitchLabelPadding()
	{
		string source = Switch(
			"""
			case 1:
				goto done;
			done:

				DoThing();
				break;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync<RemoveSwitchLabelPaddingAnalyzer>(
			source,
			s_on
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheRuleShipsOnAsASuggestion()
	{
		string source = Switch(
			"""
			case 1:

				DoThing();
				break;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync<RemoveSwitchLabelPaddingAnalyzer>(source);

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual(DiagnosticSeverity.Info, result.Diagnostics[0].Severity);
	}

	[TestMethod]
	public async Task APragmaThatSilencesTheRuleLeavesTheLabelAsWritten()
	{
		string source = Switch(
			$$"""
			#pragma warning disable {{DiagnosticDescriptors.RemoveSwitchLabelPadding.Id}}
			case 1:

				DoThing();
				break;
			#pragma warning restore {{DiagnosticDescriptors.RemoveSwitchLabelPadding.Id}}
			case 2:

				DoThing();
				break;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync<RemoveSwitchLabelPaddingAnalyzer>(
			source,
			s_on
		);

		Assert.HasCount(1, result.Diagnostics);

		Location location = result.Diagnostics[0].Location;
		int line = location.GetLineSpan().StartLinePosition.Line;

		Assert.AreEqual("case 2:", location.SourceTree!.GetText().Lines[line].ToString().Trim());
	}

	[TestMethod]
	public async Task EveryStateOfTheRulesBesideTheLabelsSettlesOnOneTextInEveryFixOrder()
	{
		string source = Switch(
			"""
			case 1:

			case 2:

				DoThing();
				break;
			case 3:

				{
					DoThing();
					break;
				}
			case 4:
				DoThing(
					1,
					2);
				break;
			default:

				// Last.
				DoThing();
				break;
			"""
		);

		await RoslynTestHarness.AssertEveryStateSettlesAsync(
			source,

			toggled:
			[
				DiagnosticDescriptors.RemoveSwitchLabelPadding,
				DiagnosticDescriptors.RemoveBracePadding,
				DiagnosticDescriptors.SeparateMultiLineSwitchSections,
				DiagnosticDescriptors.KeepSingleLineSwitchSectionsTogether,
				DiagnosticDescriptors.SeparateCommentedSibling,
			]
		);
	}

	// Wraps switch sections in a method of a class that declares what they call.
	private static string Switch(string sections)
	{
		string indented = SourceFixtures.Indent(sections);

		return SourceFixtures.WrapBody(
			$"switch (value)\n{{\n{indented}\n}}",
			signature: "void M(object value)",
			members: "\tvoid DoThing() { }\n\n\tvoid DoThing(int first, int second) { }\n\n\tvoid Trace() { }\n\n\tbool DoOther() => true;\n\n"
		);
	}
}
