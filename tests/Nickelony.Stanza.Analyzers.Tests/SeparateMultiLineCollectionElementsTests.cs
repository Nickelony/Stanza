using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class SeparateMultiLineCollectionElementsTests
{
	[TestMethod]
	public async Task SingleLineCollectionElementsStayTight()
	{
		const string source = """
			class C
			{
				void M()
				{
					var numbers = new[] { 1, 2, 3 };

					var items = new List<string>
					{
						"a",
						"b",
					};
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineCollectionElementsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task CollectionInitializerElementsGetSeparated()
	{
		const string source = """
			class C
			{
				void M()
				{
					var items = new List<Item>
					{
						new Item
						{
							Name = "a",
						},
						new Item
						{
							Name = "b",
						},
					};
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					var items = new List<Item>
					{
						new Item
						{
							Name = "a",
						},

						new Item
						{
							Name = "b",
						},
					};
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineCollectionElementsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task MultiLineCollectionExpressionElementGetsSeparated()
	{
		const string source = """
			class C
			{
				List<string> M()
				{
					List<string> items =
					[
						"a",
						Build(
							"b"),
					];

					return items;
				}

				string Build(string value)
				{
					return value;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				List<string> M()
				{
					List<string> items =
					[
						"a",

						Build(
							"b"),
					];

					return items;
				}

				string Build(string value)
				{
					return value;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineCollectionElementsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task SingleLineCollectionExpressionsStayTight()
	{
		const string source = """
			class C
			{
				int[] M(int[] values)
				{
					int[] merged = [1, 2, .. values];

					return merged;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineCollectionElementsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ArrayInitializerElementsGetSeparated()
	{
		const string source = """
			class C
			{
				int[] M()
				{
					return new[]
					{
						Build(
							1),
						2,
					};
				}

				int Build(int value)
				{
					return value;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				int[] M()
				{
					return new[]
					{
						Build(
							1),

						2,
					};
				}

				int Build(int value)
				{
					return value;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineCollectionElementsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ComplexElementInitializerElementsGetSeparated()
	{
		const string source = """
			class C
			{
				Dictionary<int, string> M()
				{
					return new Dictionary<int, string>
					{
						{
							1, "one"
						},
						{
							2, "two"
						},
					};
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				Dictionary<int, string> M()
				{
					return new Dictionary<int, string>
					{
						{
							1, "one"
						},

						{
							2, "two"
						},
					};
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineCollectionElementsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
