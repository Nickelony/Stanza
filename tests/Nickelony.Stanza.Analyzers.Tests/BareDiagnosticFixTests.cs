using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Arguments;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Analyzers.Bodies;
using Nickelony.Stanza.Analyzers.Braces;
using Nickelony.Stanza.Analyzers.Documentation;
using Nickelony.Stanza.Analyzers.MarkupLiterals;
using Nickelony.Stanza.Analyzers.Regions;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class BareDiagnosticFixTests
{
	private const bool OffersAFix = true;
	private const bool OffersNoFix = false;

	public static IEnumerable<object?[]> BareDiagnostics { get; } =
	[
		[
			"the layout rule",
			new LayOutDocumentationCommentAnalyzer(),
			OffersNoFix,
			null,

			"""
				internal sealed class Sample
				{
					/// <summary>Binds the options from the configuration section, validates them and lets the host fail at startup rather than at the first request.</summary>
					internal int Bind()
					{
						return 1;
					}
				}
				""",
		],

		[
			"the paragraph rule",
			new MarkDocumentationParagraphsAnalyzer(),
			OffersNoFix,
			null,

			"""
				internal sealed class Sample
				{
					/// <summary>
					/// The first thought.
					///
					/// The second thought.
					/// </summary>
					internal int Bind()
					{
						return 1;
					}
				}
				""",
		],

		[
			"the list layout rule",
			new LayOutItemsOfDocumentationListAnalyzer(),
			OffersNoFix,
			new Dictionary<string, string> { ["stanza_max_line_length"] = "46" },

			"""
				internal sealed class Sample
				{
					/// <list type="bullet">
					/// <item><description>One value.</description></item>
					/// </list>
					internal int Bind() => 1;
				}
				""",
		],

		[
			"the name-adding rule",
			new NameArgumentsOfCallAnalyzer(),
			OffersNoFix,
			null,

			"""
				internal static class Sample
				{
					internal static int Mix(int first, int second, int third, int fourth) => first;

					internal static int Run() => Mix(1, 2, 3, 4);
				}
				""",
		],

		[
			"the name-removal rule",
			new RemoveRedundantArgumentNameAnalyzer(),
			OffersAFix,
			null,

			"""
				internal static class Sample
				{
					internal static int Mix(int value) => value;

					internal static int Run(int value) => Mix(value: value);
				}
				""",
		],

		[
			"the multi-line tight markup rule",
			new KeepMultiLineMarkupElementsTogetherAnalyzer(),
			OffersAFix,
			null,

			""""
				class C
				{
					void M()
					{
						const string markup = """
							<root>
							  <a>
							    1
							  </a>

							  <b>2</b>
							</root>
							""";
					}
				}
				"""",
		],

		[
			"the single-line tight markup rule",
			new KeepSingleLineMarkupElementsTogetherAnalyzer(),
			OffersAFix,
			null,

			""""
				class C
				{
					void M()
					{
						const string markup = """
							<root>
							  <a>1</a>

							  <b>2</b>
							</root>
							""";
					}
				}
				"""",
		],

		[
			"the markup comment spacing rule",
			new SeparateMarkupCommentMarkerFromTextAnalyzer(),
			OffersAFix,
			null,

			""""
				class C
				{
					void M()
					{
						const string markup = """
							<root>
							  <!--note-->
							</root>
							""";
					}
				}
				"""",
		],

		[
			"the markup cap rule",
			new CollapseExtraBlankLinesBetweenMarkupElementsAnalyzer(),
			OffersNoFix,
			null,

			""""
				class C
				{
					void M()
					{
						const string markup = """
							<root>
							  <a>1</a>


							  <b>2</b>
							</root>
							""";
					}
				}
				"""",
		],

		[
			"the extra blank lines rule",
			new CollapseExtraBlankLinesAnalyzer(),
			OffersAFix,
			null,

			"""
				class C
				{
					int first;



					int second;
				}
				""",
		],

		[
			"the loose run rule",
			new GatherRunOfLooseStatementsAnalyzer(),
			OffersNoFix,
			null,

			"""
				class C
				{
					int first, second, third, fourth;

					void M()
					{
						first = 1;

						second = 2;

						third = 3;

						fourth = 4;
					}
				}
				""",
		],

		[
			"the loose member run rule",
			new GatherRunOfLooseMembersAnalyzer(),
			OffersNoFix,
			null,

			"""
				class C
				{
					int first;

					int second;

					int third;

					int fourth;
				}
				""",
		],

		[
			"the statement keep rule",
			new KeepRunOfStatementsTogetherAnalyzer(),
			OffersNoFix,
			null,

			"""
				class C
				{
					void M()
					{
						int first = 1;

						int second = 2;
					}
				}
				""",
		],

		[
			"the member keep rule",
			new KeepRunOfMembersTogetherAnalyzer(),
			OffersNoFix,
			null,

			"""
				class C
				{
					int first;

					int second;
				}
				""",
		],

		[
			"the leading arrow rule",
			new PutArrowOnLineAfterSignatureAnalyzer(),
			OffersAFix,
			null,

			"""
				class C
				{
					int Build(int value) =>
						value + 1;
				}
				""",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(BareDiagnostics),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ABareDiagnosticIsOfferedAFixOnlyWhenTheFixNeedsNoProperty(
		string name,
		DiagnosticAnalyzer analyzer,
		bool offersAFix,
		IReadOnlyDictionary<string, string>? analyzerConfigOptions,
		string source
	)
	{
		bool offered = await RoslynTestHarness.OffersFixWithoutPropertiesAsync(
			source,
			analyzer,
			diagnosticOptions: Only(analyzer.SupportedDiagnostics.Single()),
			analyzerConfigOptions
		);

		Assert.AreEqual(
			offersAFix,
			offered,
			$"{name} offered {(offered ? "a fix" : "no fix")} for a diagnostic with no property."
		);
	}

	[TestMethod]
	[CrossCutting]
	public async Task TheMarkupRuleFallsBackToTheTightShape()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Returns null when the section is missing.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Returns <see langword="null"/> when the section is missing.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<MarkLanguageKeywordsAnalyzer>(
				source,

				TestRuleSets.Only(
					DiagnosticDescriptors.MarkLanguageKeywords,
					DiagnosticDescriptors.SeparateSlashOfSelfClosingTag
				)
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task TheEmptyElementRuleFallsBackToTheTightShape()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the <c></c> options.</summary>
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Binds the <c/> options.</summary>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<WriteEmptyElementAsSelfClosingTagAnalyzer>(
				source,

				TestRuleSets.Only(
					DiagnosticDescriptors.WriteEmptyElementAsSelfClosingTag,
					DiagnosticDescriptors.SeparateSlashOfSelfClosingTag
				)
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task TheMemberBodyRuleWritesTheBlockWithoutTheStepColumns()
	{
		const string source = """
			class C
			{
				int Pick(bool first) =>
					first ? 1
					: 2;
			}
			""";

		const string fixedSource = """
			class C
			{
				int Pick(bool first)
				{
					return first ? 1
					: 2;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember)
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task TheValueBodyRuleWritesTheBlockWithoutTheStepColumns()
	{
		const string source = """
			class C
			{
				int this[
					int index
				] => throw new InvalidOperationException();
			}
			""";

		const string fixedSource = """
			class C
			{
				int this[
					int index
				]
				{
					get
					{
						throw new InvalidOperationException();
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<UseBlockBodyForMultiLineValueMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineValueMember)
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task TheBreakRuleFallsBackToTheTrailingPlacement()
	{
		const string source = """
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

		const string fixedSource = """
			class C
			{
				private static Kind PickTheOneThatComesFirstOfAll(bool firstOfTheTwoValues) =>
					firstOfTheTwoValues ? Kind.One : Kind.Two;
			}

			enum Kind
			{
				One,
				Two,
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<BreakExpressionOffSignatureLineAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.BreakExpressionOffSignatureLine)
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task TheTrailingRuleJoinsTheArrowWithoutThePlacement()
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
				int Build(int value) => value + 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<KeepArrowOnSignatureLineAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepArrowOnSignatureLine)
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task TheLeadingRuleMovesTheArrowWithoutThePlacement()
	{
		const string source = """
			class C
			{
				int Build(int value) =>
					value + 1;
			}
			""";

		const string fixedSource = """
			class C
			{
				int Build(int value)
					=> value + 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<PutArrowOnLineAfterSignatureAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.PutArrowOnLineAfterSignature)
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task TheBraceRuleWrapsABareBody()
	{
		const string source = """
			class C
			{
				void M(bool first, bool second)
				{
					if (first
						|| second)
						DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool first, bool second)
				{
					if (first
						|| second)
					{
						DoThing();
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<UseBracesConsistentlyAnalyzer>(
				source,
				Only(DiagnosticDescriptors.UseBracesConsistently)
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task TheMarkupSpacingRuleWritesTheBlankLineWithoutAProperty()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <child>
						    one
						  </child>
						  <child>
						    two
						  </child>
						</root>
						""";
				}
			}
			"""";

		const string fixedSource = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <child>
						    one
						  </child>

						  <child>
						    two
						  </child>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<SeparateMultiLineMarkupElementsAnalyzer>(
				source,
				Only(DiagnosticDescriptors.SeparateMultiLineMarkupElements)
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task TheTightMarkupRulesTakeEveryBlankLineAwayWithoutACount()
	{
		const string multiLine = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a>
						    1
						  </a>

						  <b>2</b>
						</root>
						""";
				}
			}
			"""";

		const string multiLineFixed = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a>
						    1
						  </a>
						  <b>2</b>
						</root>
						""";
				}
			}
			"""";

		const string singleLine = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a>1</a>

						  <b>2</b>
						</root>
						""";
				}
			}
			"""";

		const string singleLineFixed = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a>1</a>
						  <b>2</b>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult multi =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<KeepMultiLineMarkupElementsTogetherAnalyzer>(
				multiLine,
				Only(DiagnosticDescriptors.KeepMultiLineMarkupElementsTogether)
			);

		RoslynTestHarness.AssertFixedText(multiLineFixed, multi);

		AnalyzerRunResult single =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<KeepSingleLineMarkupElementsTogetherAnalyzer>(
				singleLine,
				Only(DiagnosticDescriptors.KeepSingleLineMarkupElementsTogether)
			);

		RoslynTestHarness.AssertFixedText(singleLineFixed, single);
	}

	[TestMethod]
	[CrossCutting]
	public async Task TheCommentSpacingRuleWritesTheSpaceWithoutAProperty()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <!-- a note-->
						</root>
						""";
				}
			}
			"""";

		const string fixedSource = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <!-- a note -->
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<SeparateMarkupCommentMarkerFromTextAnalyzer>(
				source,
				Only(DiagnosticDescriptors.SeparateMarkupCommentMarkerFromText)
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task TheSiblingRuleAddsTheBlankLineWithoutTheUnitBeforeIt()
	{
		const string source = """
			class C
			{
				void M()
				{
					DoThing();
				}
				void N()
				{
					DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					DoThing();
				}

				void N()
				{
					DoThing();
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<SeparateMultiLineConstructsAnalyzer>(
				source,
				Only(DiagnosticDescriptors.SeparateMultiLineConstructs)
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task TheAdjacentRuleTakesTheBlankLineAwayWithoutTheUnitBeforeIt()
	{
		const string source = """
			class C
			{
				private int _value;

				public int Value
				{
					get { return _value; }

					set
					{
						_value = value;
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _value;

				public int Value
				{
					get { return _value; }
					set
					{
						_value = value;
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<KeepMultiLineAccessorsTogetherAnalyzer>(
				source,
				Only(DiagnosticDescriptors.KeepMultiLineAccessorsTogether)
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task TheDeclarationGapRuleTakesTheBlankLineAwayWithoutTheComment()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options from the configuration section.</summary>

				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Binds the options from the configuration section.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<KeepDocumentationCommentWithDeclarationAnalyzer>(
				source,
				Only(DiagnosticDescriptors.KeepDocumentationCommentWithDeclaration)
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task TheRegionMarkerRuleAddsTheBlankLineBelowTheEndMarkerWithoutTheChange()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total;

				#region Fields

				private int _count;
				#endregion

				private int _sum;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private int _total;

				#region Fields

				private int _count;

				#endregion

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<SeparateRegionMarkerFromNeighborsAnalyzer>(
				source,
				Only(DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors)
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task TheConditionalBlockRuleAddsTheBlankLineBelowTheEndifWithoutTheChange()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total;

				#if TRACE
				private int _trace;
				#endif
				private int _sum;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private int _total;

				#if TRACE
				private int _trace;
				#endif

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
				source,
				Only(DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors)
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task TheRegionNameRuleWritesTheNameOfTheMarkerWithoutTheChange()
	{
		const string source = """
			internal sealed class Sample
			{
				#region Fields
				private int _count;
				#endregion
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				#region Fields
				private int _count;
				#endregion Fields
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<NameClosingRegionDirectiveAnalyzer>(
				source,
				Only(DiagnosticDescriptors.NameClosingRegionDirective)
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task TheConditionalNameRuleNamesOnlyTheEndifItReportsWithoutTheChange()
	{
		string fields = string.Concat(
			Enumerable.Range(0, 10).Select(static index => $"\tprivate int _field{index};\n")
		);

		string shortFields = string.Concat(
			Enumerable.Range(0, 9).Select(static index => $"\tprivate int _field{index};\n")
		);

		string source =
			$"internal sealed class Sample\n{{\n#if SHORT\n{shortFields}#endif\n\n#if LONG\n{fields}#endif\n}}\n";

		string fixedSource =
			$"internal sealed class Sample\n{{\n#if SHORT\n{shortFields}#endif\n\n#if LONG\n{fields}#endif // LONG\n}}\n";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<NameClosingConditionalDirectiveAnalyzer>(
				source,
				Only(DiagnosticDescriptors.NameClosingConditionalDirective)
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task TheEmptyRegionRuleRemovesTheRegionWithoutTheChange()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total;

				#region Fields
				#endregion

				private int _sum;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private int _total;

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<RemoveEmptyRegionsAnalyzer>(
				source,
				Only(DiagnosticDescriptors.RemoveEmptyRegions)
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("1", 1)]
	[DataRow("many", 0)]
	[DataRow("", 0)]
	[DataRow("-1", 0)]
	[DataRow("1.5", 0)]
	public async Task OnlyAKeptBlankLinesPropertyThatIsANumberOffersTheFix(string kept, int fixes)
	{
		const string source = "<a>\n\n\n\n<b />\n</a>";

		int offered = await RoslynTestHarness.CountOfferedFixesAsync(
			source,
			descriptor: DiagnosticDescriptors.KeepMultiLineMarkupElementsTogether,
			span: new TextSpan(source.IndexOf("<b", System.StringComparison.Ordinal), 2),
			properties: ImmutableDictionary<string, string?>.Empty.Add("keptBlankLines", kept)
		);

		Assert.AreEqual(fixes, offered, $"A kept count of '{kept}' offers {offered} fixes.");
	}

	private static IReadOnlyDictionary<string, ReportDiagnostic> Only(
		DiagnosticDescriptor descriptor
	) => TestRuleSets.Only(descriptor);
}
