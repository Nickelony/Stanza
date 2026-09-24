using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class SeparateMultiLineDeclaratorsTests
{
	[TestMethod]
	public async Task MultiLineDeclaratorsGetSeparated()
	{
		const string source = """
			class C
			{
				void M()
				{
					int lookup = Build(
						1),
						capacity = 4;
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
				void M()
				{
					int lookup = Build(
						1),

						capacity = 4;
				}

				int Build(int value)
				{
					return value;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineDeclaratorsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task SingleLineDeclaratorsAndWrappedTypesStayTight()
	{
		const string source = """
			class C
			{
				void M()
				{
					int first = 1, second = 2;
					Dictionary<
						int,
						string> lookup = new(), cache = new();
				}
			}
			""";

		// A wrapped type belongs to the declaration rather than to a declarator,
		// so it never separates the declarators after it.
		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineDeclaratorsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task FieldDeclaratorsAreSeparated()
	{
		const string source = """
			class C
			{
				private int first = Build(
						1),
					second = Build(2);
			}
			""";

		const string fixedSource = """
			class C
			{
				private int first = Build(
						1),

					second = Build(2);
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineDeclaratorsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
