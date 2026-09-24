using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class KeepMultiLineCollectionElementsTogetherTests
{
	// The pair rows hold an array initializer, so the elements of a collection expression are pinned here.
	[TestMethod]
	public async Task TheBlankLineBetweenTheElementsOfACollectionExpressionIsRemoved()
	{
		const string source = """
			class C
			{
				int[] M()
				{
					return
					[
						Build(
							1),

						Build(
							2),
					];
				}

				static int Build(int value) => value;
			}
			""";

		const string fixedSource = """
			class C
			{
				int[] M()
				{
					return
					[
						Build(
							1),
						Build(
							2),
					];
				}

				static int Build(int value) => value;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepMultiLineCollectionElementsTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepMultiLineCollectionElementsTogether)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ElementsThatAlreadyStandTogetherAreLeftAlone()
	{
		const string source = """
			class C
			{
				int[] M()
				{
					return
					[
						Build(
							1),
						Build(
							2),
					];
				}

				static int Build(int value) => value;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepMultiLineCollectionElementsTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepMultiLineCollectionElementsTogether)
			);

		Assert.IsEmpty(result.Diagnostics);
	}
}
