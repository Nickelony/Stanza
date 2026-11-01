using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

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

	// A tuple literal is a list of arguments of its own, in a deconstruction as much as in a value.
	[TestMethod]
	public async Task TheBlankLinesBetweenTheElementsOfATupleAreRemoved()
	{
		const string source = """
			class C
			{
				void M()
				{
					var pair = (1,

						2);

					(int first, int second) = (3,

						4);
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					var pair = (1,
						2);

					(int first, int second) = (3,
						4);
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
	public async Task ATrailingCommentAfterATupleElementStaysWhileTheBlankLineGoes()
	{
		const string source = """
			class C
			{
				void M()
				{
					var pair = (1, // The first element.

						(2, 3));
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					var pair = (1, // The first element.
						(2, 3));
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepSingleLineArgumentsTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepSingleLineArgumentsTogether)
			);

		Assert.HasCount(1, result.Diagnostics);
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
