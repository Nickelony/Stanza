using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class KeepSingleLineArgumentsTogetherTests
{
	// The pair rows hold a call, so the arguments of an attribute and of an element access are pinned here.
	[TestMethod]
	public async Task TheBlankLinesInAttributeAndElementAccessArgumentsAreRemoved()
	{
		const string source = """
			class C
			{
				[System.Obsolete("Use Run instead.",

					false)]
				int M(int[,] values)
				{
					return values[0,

						1];
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				[System.Obsolete("Use Run instead.",
					false)]
				int M(int[,] values)
				{
					return values[0,
						1];
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepSingleLineArgumentsTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepSingleLineArgumentsTogether)
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ArgumentsThatAlreadyStandTogetherAreLeftAlone()
	{
		const string source = """
			class C
			{
				[System.Obsolete("Use Run instead.",
					false)]
				int M(int[,] values)
				{
					return values[0,
						1];
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepSingleLineArgumentsTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepSingleLineArgumentsTogether)
			);

		Assert.IsEmpty(result.Diagnostics);
	}
}
