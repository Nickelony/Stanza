using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Documentation;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class DocumentationFamilyTests
{
	[TestMethod]
	public async Task AGapThatSpansALineIsLeftToTheLayoutRule()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <returns><see cref="System.String"
				/// />when the section is missing.</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult spaced =
			await RoslynTestHarness.RunAsync<SeparateSlashOfSelfClosingTagAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		AnalyzerRunResult tight =
			await RoslynTestHarness.RunAsync<KeepSlashOfSelfClosingTagTightAnalyzer>(
				source,
				TestRuleSets.GapRulesEnabled
			);

		Assert.IsEmpty(spaced.Diagnostics, "The spaced rule reported a wrapped gap.");
		Assert.IsEmpty(tight.Diagnostics, "The tight rule reported a wrapped gap.");
	}

	public static IEnumerable<object[]> OpaqueSamples { get; } =
	[
		[
			"a sample in a code block",

			"""
				internal sealed class Sample
				{
					/// <example>
					/// <code>
					/// <Item   Name="x"></Item>
					/// <Item Name="y" />
					/// <Item Name="z"/>
					/// </code>
					/// </example>
					internal int Bind() => 1;
				}
				""",
		],

		[
			"markup that preserves space",

			"""
				internal sealed class Sample
				{
					/// <summary xml:space="preserve">
					/// <Item   Name="x"></Item>
					/// <Item Name="y" />
					/// <Item Name="z"/>
					/// </summary>
					internal int Bind() => 1;
				}
				""",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(OpaqueSamples),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AnOpaqueSampleIsLeftAsWritten(string name, string source) =>
		await AssertNoTagRuleReportsAsync(name, source, ProseTags);

	// The tags of the opaque samples in plain prose, where each tag rule reports.
	private const string ProseTags = """
		internal sealed class Sample
		{
			/// <remarks>
			/// <Item   Name="x"></Item>
			/// <Item Name="y" />
			/// <Item Name="z"/>
			/// </remarks>
			internal int Bind() => 1;
		}
		""";

	private static async Task AssertNoTagRuleReportsAsync(
		string name,
		string source,
		string control
	)
	{
		await AssertLeftAloneAsync<CollapseWhitespaceInsideTagAnalyzer>(name, source, control);
		await AssertLeftAloneAsync<SeparateSlashOfSelfClosingTagAnalyzer>(name, source, control);
		await AssertLeftAloneAsync<KeepSlashOfSelfClosingTagTightAnalyzer>(name, source, control);

		await AssertLeftAloneAsync<WriteEmptyElementAsSelfClosingTagAnalyzer>(
			name,
			source,
			control
		);
	}

	// Each rule runs alone, because the tight rule takes the gap from the spaced one when both are enabled.
	private static async Task AssertLeftAloneAsync<TAnalyzer>(
		string name,
		string source,
		string control
	)
		where TAnalyzer : DiagnosticAnalyzer, new()
	{
		IReadOnlyDictionary<string, ReportDiagnostic> rule = TestRuleSets.Only(
			new TAnalyzer().SupportedDiagnostics.Single()
		);

		AnalyzerRunResult reported = await RoslynTestHarness.ReportAsync<TAnalyzer>(control, rule);

		Assert.IsNotEmpty(
			reported.Diagnostics,
			$"{typeof(TAnalyzer).Name} reported nothing on the control, so its silence proves nothing."
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync<TAnalyzer>(source, rule);

		Assert.IsEmpty(
			result.Diagnostics,
			$"{name}: {typeof(TAnalyzer).Name} reported inside markup that is left as written."
		);
	}

	// The well-formed tags of the broken and unquoted samples, where each slash rule reports.
	private const string QuotedTags = """
		internal sealed class Sample
		{
			/// <summary>Returns <see cref="Sample" /> or <see cref="Sample"/> at once.</summary>
			internal int Bind() => 1;
		}
		""";

	[TestMethod]
	[DataRow("a broken tag", "Returns a Task<List<int>> value.")]
	[DataRow(
		"a tag with an unquoted attribute",
		"Returns <see cref=Sample /> or <see cref=Sample/> at once."
	)]
	public async Task AMalformedTagInProseIsNotReportedByEitherRule(string name, string summary)
	{
		string source = $$"""
			internal sealed class Sample
			{
				/// <summary>{{summary}}</summary>
				internal int Bind() => 1;
			}
			""";

		await AssertLeftAloneAsync<SeparateSlashOfSelfClosingTagAnalyzer>(name, source, QuotedTags);

		await AssertLeftAloneAsync<KeepSlashOfSelfClosingTagTightAnalyzer>(
			name,
			source,
			QuotedTags
		);
	}

	private static IReadOnlyDictionary<string, ReportDiagnostic> LayoutAndEmptyElement { get; } =
		TestRuleSets.Only(
			DiagnosticDescriptors.LayOutDocumentationComment,
			DiagnosticDescriptors.WriteEmptyElementAsSelfClosingTag
		);

	[TestMethod]
	public async Task TheEmptyElementAndTheLayoutSettleInTurn()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				/// A sentence that is long enough to be laid out again by the width of a comment and holds
				/// an element <c></c> in the middle of it.
				/// </summary>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult first = await RoslynTestHarness.RunUnsettledAsync(
			source,
			LayoutAndEmptyElement
		);

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>
				/// A sentence that is long enough to be laid out again by the width of a comment and holds an
				/// element <c/> in the middle of it.
				/// </summary>
				internal int Bind() => 1;
			}
			""";

		RoslynTestHarness.AssertFixedText(
			fixedSource,
			first,
			"The empty element was not written as one tag."
		);

		AnalyzerRunResult second = await RoslynTestHarness.RunUnsettledAsync(
			first.FixedText,
			LayoutAndEmptyElement
		);

		RoslynTestHarness.AssertFixedText(fixedSource, second, "The empty element lost its shape.");

		AnalyzerRunResult third = await RoslynTestHarness.RunAllAsync(
			second.FixedText,
			LayoutAndEmptyElement
		);

		Assert.IsEmpty(third.Diagnostics, "The gap does not settle over an empty element.");
	}

	// The null rule turns the last word into an element, and the period still closes the sentence after it.
	[TestMethod]
	public async Task ThePeriodAndTheNullKeywordSettleInEitherOrder()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Returns null</summary>
				internal int Bind() => 1;
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.Only(
			DiagnosticDescriptors.EndDocumentationTextWithPeriod,
			DiagnosticDescriptors.MarkNullKeyword
		);

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Returns <see langword="null"/>.</summary>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source, rules);

		RoslynTestHarness.AssertFixedText(fixedSource, result, "The sentence lost its period.");

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			result.FixedText,
			source,
			new RunOptions(RoslynTestHarness.AllAnalyzers) { DiagnosticOptions = rules }
		);
	}

	[TestMethod]
	public async Task TheOrderRuleAndTheOtherRulesSettleTogether()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <remarks>Reads the value twice.</remarks>
				/// <summary>Binds the value.</summary>
				/// <param name="value">The value to bind.</param>
				internal int Bind(int value) => value + value;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Binds the value.</summary>
				/// <param name="value">The value to bind.</param>
				/// <remarks>
				/// Reads the value twice.
				/// </remarks>
				internal int Bind(int value) => value + value;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheListOrderAndTheListLayoutSettleOneFixAtATime()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <list type="table">
				///   <item>One</item>
				///   <listheader>Name</listheader>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <list type="table">
				/// <listheader>Name</listheader>
				/// <item>One</item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.LayOutItemsOfDocumentationList,
				DiagnosticDescriptors.OrderItemsOfDocumentationList
			)
		);

		string[] reported =
		[
			.. result
				.Diagnostics.Select(diagnostic => diagnostic.Id)
				.Distinct(StringComparer.Ordinal),
		];

		Assert.Contains(
			DiagnosticDescriptors.LayOutItemsOfDocumentationList.Id,
			reported,
			"the layout rule reported nothing, so this fixture proves nothing."
		);

		Assert.Contains(
			DiagnosticDescriptors.OrderItemsOfDocumentationList.Id,
			reported,
			"the order rule reported nothing, so this fixture proves nothing."
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheEmptyElementRuleAndTheLayoutRuleSettleTogether()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the <c></c> options, validates them and lets the host fail at startup rather than at the first request.</summary>
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>
				/// Binds the <c/> options, validates them and lets the host fail at startup rather than at the
				/// first request.
				/// </summary>
				internal int Bind() => 1;
			}
			""";

		// Each rule judges the comment as written, and the shorter tag can leave the layout rule a second pass.
		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.LayOutDocumentationComment,
				DiagnosticDescriptors.WriteEmptyElementAsSelfClosingTag
			)
		);

		Assert.AreSequenceEqual(
			[
				DiagnosticDescriptors.LayOutDocumentationComment.Id,
				DiagnosticDescriptors.WriteEmptyElementAsSelfClosingTag.Id,
			],

			result.Diagnostics.Select(diagnostic => diagnostic.Id),
			SequenceOrder.InAnyOrder
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
