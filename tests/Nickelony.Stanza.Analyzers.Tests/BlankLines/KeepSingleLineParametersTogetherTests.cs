using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed class KeepSingleLineParametersTogetherTests
{
	// The pair rows hold a method, so the bracketed parameters of an indexer are pinned here.
	[TestMethod]
	public async Task TheBlankLineBetweenTheParametersOfAnIndexerIsRemoved()
	{
		const string source = """
			class C
			{
				int this[int first,

					int second] => first + second;
			}
			""";

		const string fixedSource = """
			class C
			{
				int this[int first,
					int second] => first + second;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepSingleLineParametersTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepSingleLineParametersTogether)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The elements of a tuple type are a list of their own, in the type of a parameter as much as in a field.
	[TestMethod]
	public async Task TheBlankLinesBetweenTheElementsOfATupleTypeAreRemoved()
	{
		const string source = """
			class C
			{
				(int First,

					string Second) Pair;

				void M((int First,

					int Second) pair,

					int other)
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				(int First,
					string Second) Pair;

				void M((int First,
					int Second) pair,

					int other)
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepSingleLineParametersTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepSingleLineParametersTogether)
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ParametersThatAlreadyStandTogetherAreLeftAlone()
	{
		const string source = """
			class C
			{
				int this[int first,
					int second] => first + second;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepSingleLineParametersTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepSingleLineParametersTogether)
			);

		Assert.IsEmpty(result.Diagnostics);
	}
}
