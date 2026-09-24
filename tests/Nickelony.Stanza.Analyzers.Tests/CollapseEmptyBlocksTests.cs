using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Braces;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class CollapseEmptyBlocksTests
{
	public static IEnumerable<object[]> EmptyBodies { get; } =
	[
		[
			"a method body",

			"""
				class C
				{
					void M()
					{
					}

					void N()
					{
						DoThing();
					}
				}
				""",

			"""
				class C
				{
					void M()
					{ }

					void N()
					{
						DoThing();
					}
				}
				""",
		],

		[
			"a constructor body",

			"""
				class C
				{
					public C()
					{
					}
				}
				""",

			"""
				class C
				{
					public C()
					{ }
				}
				""",
		],

		[
			"an accessor body",

			"""
				class C
				{
					int Value
					{
						set
						{
						}
					}
				}
				""",

			"""
				class C
				{
					int Value
					{
						set
						{ }
					}
				}
				""",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(EmptyBodies),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task EmptyBodiesAreJoined(string name, string source, string fixedSource)
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<CollapseEmptyBlocksAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result, name);
	}

	[TestMethod]
	public async Task BodiesThatAreNotEmptyAreLeftAlone()
	{
		const string source = """
			class C
			{
				void WithStatement()
				{
					DoThing();
				}

				void WithComment()
				{
					// nothing to do here
				}

				void WithDirective()
				{
			#if DEBUG
			#endif
				}

				void Joined()
				{ }

				void Tight()
				{}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<CollapseEmptyBlocksAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnEmptyAnonymousObjectIsJoined()
	{
		const string source = """
			class C
			{
				object M() => new {
				};
			}
			""";

		const string fixedSource = """
			class C
			{
				object M() => new { };
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<CollapseEmptyBlocksAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheEmptyBlockFixFindsTheBracesWithoutAnAdditionalLocation()
	{
		const string source = """
			class C
			{
				void M()
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{ }
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<CollapseEmptyBlocksAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheEmptyBlockRuleIsOffWhenSingleLineBlocksAreNotPreserved()
	{
		const string source = """
			class C
			{
				void M()
				{
				}
			}
			""";

		AnalyzerRunResult expandsSingleLineBlocks =
			await RoslynTestHarness.ReportAsync<CollapseEmptyBlocksAnalyzer>(
				source,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["csharp_preserve_single_line_blocks"] = "false",
				}
			);

		Assert.IsEmpty(expandsSingleLineBlocks.Diagnostics);

		AnalyzerRunResult preservesSingleLineBlocks =
			await RoslynTestHarness.ReportAsync<CollapseEmptyBlocksAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, preservesSingleLineBlocks.Diagnostics);
	}

	[TestMethod]
	public async Task AnEmptyCollectionExpressionIsNotPeeled()
	{
		const string source = """
			class C
			{
				void M()
				{
					int[] items = [
					];

					var other = new List<int>
					{
					};
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					int[] items = [
					];

					var other = new List<int>
					{ };
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<CollapseEmptyBlocksAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		// Only the collection initializer is joined, and it is the closing brace that anchors the finding.
		Assert.HasCount(1, result.Diagnostics);

		TextSpan span = result.Diagnostics[0].Location.SourceSpan;

		Assert.AreEqual('}', source[span.Start]);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnEmptyNamespaceBodyAndAnEmptySwitchAreLeftAlone()
	{
		const string source = """
			namespace N
			{
			}

			internal sealed class Sample
			{
				internal void Bind(int value)
				{
					switch (value)
					{
					}
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<CollapseEmptyBlocksAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task EmptyTypeBodiesAndInitializersAreJoined()
	{
		const string source = """
			class Empty
			{
			}

			record Marker
			{
			}

			enum None
			{
			}

			class C
			{
				void M()
				{
					var items = new List<int>
					{
					};

					var item = new Item
					{
					};
				}
			}
			""";

		const string fixedSource = """
			class Empty
			{ }

			record Marker
			{ }

			enum None
			{ }

			class C
			{
				void M()
				{
					var items = new List<int>
					{ };

					var item = new Item
					{ };
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunFixAllAsync(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(5, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task EmptyArrayAndWithInitializersAreJoined()
	{
		const string source = """
			record Pair(int Value);

			class C
			{
				void M(Pair item)
				{
					int[] values = new int[]
					{
					};

					var copy = item with
					{
					};
				}
			}
			""";

		const string fixedSource = """
			record Pair(int Value);

			class C
			{
				void M(Pair item)
				{
					int[] values = new int[]
					{ };

					var copy = item with
					{ };
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunFixAllAsync(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.CollapseEmptyBlocks)
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// An empty element initializer does not compile, but an editor shows the rule while the code is being written.
	[TestMethod]
	public async Task AnEmptyElementInitializerIsReported()
	{
		const string source = """
			using System.Collections.Generic;

			class C
			{
				Dictionary<int, int> M() => new()
				{
					{
					},
				};
			}
			""";

		ImmutableArray<Diagnostic> diagnostics = await RoslynTestHarness.RunWithoutCompilingAsync(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.CollapseEmptyBlocks)
		);

		Assert.HasCount(1, diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.CollapseEmptyBlocks.Id, diagnostics[0].Id);
	}
}
