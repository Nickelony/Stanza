using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Bodies;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class KeepArrowOnSignatureLineTests
{
	[TestMethod]
	public async Task AWrappedExpressionIsLeftToTheBodyShapeRules()
	{
		const string source = """
			class C
			{
				int Build(int value)
					=> value
					+ 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepArrowOnSignatureLineAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepArrowOnSignatureLine)
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnArrowOnItsOwnLineLeavesTheExpressionOnItsLineWhileTheBreakRuleIsOn()
	{
		const string source = """
			class C
			{
				int Build(int value)
					=> value + 1;
			}
			""";

		const string fixedSource = """
			class C
			{
				int Build(int value) =>
					value + 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepArrowOnSignatureLineAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepArrowOnSignatureLine)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnArrowOnItsOwnLineIsJoinedToTheSignatureWhileTheBreakRuleIsOff()
	{
		const string source = """
			class C
			{
				int Build(int value)
					=>
					value + 1;
			}
			""";

		const string fixedSource = """
			class C
			{
				int Build(int value) => value + 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepArrowOnSignatureLineAnalyzer>(
				source,

				new Dictionary<string, ReportDiagnostic>
				{
					[DiagnosticDescriptors.KeepArrowOnSignatureLine.Id] = ReportDiagnostic.Warn,

					[DiagnosticDescriptors.BreakExpressionOffSignatureLine.Id] =
						ReportDiagnostic.Suppress,
				}
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnExpressionOnItsOwnLineWaitsForTheArrow()
	{
		const string source = """
			class C
			{
				int Build(int value)
					=>
					value + 1;
			}
			""";

		const string fixedSource = """
			class C
			{
				int Build(int value) =>
					value + 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepArrowOnSignatureLineAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepArrowOnSignatureLine)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("/* keeps the arrow */ => value + 1;", DisplayName = "before the arrow")]
	[DataRow("=> // keeps the arrow\n\t\tvalue + 1;", DisplayName = "after the arrow")]
	public async Task ACommentBesideTheArrowKeepsIt(string body)
	{
		string source = $$"""
			class C
			{
				int Build(int value)
					{{body}}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepArrowOnSignatureLineAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepArrowOnSignatureLine)
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task APragmaThatSilencesTheLeadingRuleLeavesTheTieToThisOne()
	{
		const string source = """
			class C
			{
				int Build(int value)
					=>
					value + 1;
			}
			""";

		string withPragma = $$"""
			class C
			{
			#pragma warning disable {{DiagnosticDescriptors.PutArrowOnLineAfterSignature.Id}}
				int Build(int value)
					=>
					value + 1;
			#pragma warning restore {{DiagnosticDescriptors.PutArrowOnLineAfterSignature.Id}}
			}
			""";

		string fixedSource = $$"""
			class C
			{
			#pragma warning disable {{DiagnosticDescriptors.PutArrowOnLineAfterSignature.Id}}
				int Build(int value) =>
					value + 1;
			#pragma warning restore {{DiagnosticDescriptors.PutArrowOnLineAfterSignature.Id}}
			}
			""";

		// The control: while the leading rule owns the gap, the trailing one says nothing.
		AnalyzerRunResult steppedAside =
			await RoslynTestHarness.RunAsync<KeepArrowOnSignatureLineAnalyzer>(
				source,
				TestRuleSets.ArrowRulesEnabled
			);

		Assert.IsEmpty(steppedAside.Diagnostics);

		AnalyzerRunResult reported =
			await RoslynTestHarness.RunAsync<KeepArrowOnSignatureLineAnalyzer>(
				withPragma,
				TestRuleSets.ArrowRulesEnabled
			);

		Assert.HasCount(1, reported.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, reported);
	}

	// Each source holds an arrow this rule would move, so a report shows the rule took the gap.
	public static IEnumerable<object[]> FileKeysForTheLeadingRule { get; } =
	[
		[
			"the leading key alone",

			"""
				class C
				{
					int Build(int value)
						=> value + 1;
				}
				""",

			new Dictionary<string, ReportDiagnostic>
			{
				[DiagnosticDescriptors.PutArrowOnLineAfterSignature.Id] = ReportDiagnostic.Warn,
			},
		],

		[
			"the keys of both rules",

			"""
				class C
				{
					int Build(int value)
						=>
						value + 1;
				}
				""",

			new Dictionary<string, ReportDiagnostic>
			{
				[DiagnosticDescriptors.KeepArrowOnSignatureLine.Id] = ReportDiagnostic.Warn,
				[DiagnosticDescriptors.PutArrowOnLineAfterSignature.Id] = ReportDiagnostic.Warn,
			},
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(FileKeysForTheLeadingRule),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AFileThatEnablesTheLeadingRuleLeavesTheTrailingRuleAlone(
		string name,
		string source,
		IReadOnlyDictionary<string, ReportDiagnostic> treeOptions
	)
	{
		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepArrowOnSignatureLineAnalyzer>(
				source,
				analyzerConfigOptions: null,

				diagnosticOptions: TestRuleSets.Only(
					DiagnosticDescriptors.KeepArrowOnSignatureLine
				),

				treeOptions: treeOptions
			);

		Assert.IsEmpty(result.Diagnostics, name);
	}
}
