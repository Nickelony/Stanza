using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class KeepSingleLineCollectionElementsTogetherTests
{
	// The pair rows hold an array initializer, so the elements of a collection initializer are pinned here.
	[TestMethod]
	public async Task TheBlankLineBetweenTheElementsOfACollectionInitializerIsRemoved()
	{
		const string source = """
			class C
			{
				System.Collections.Generic.List<int> M()
				{
					return new System.Collections.Generic.List<int>
					{
						1,

						2,
					};
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				System.Collections.Generic.List<int> M()
				{
					return new System.Collections.Generic.List<int>
					{
						1,
						2,
					};
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepSingleLineCollectionElementsTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepSingleLineCollectionElementsTogether)
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
				System.Collections.Generic.List<int> M()
				{
					return new System.Collections.Generic.List<int>
					{
						1,
						2,
					};
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepSingleLineCollectionElementsTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepSingleLineCollectionElementsTogether)
			);

		Assert.IsEmpty(result.Diagnostics);
	}
}
