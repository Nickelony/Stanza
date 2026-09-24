using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class KeepMultiLineArgumentsTogetherTests
{
	// The gap starts below the comment that trails the argument, so the blank line under that comment is the one to remove.
	[TestMethod]
	public async Task TheBlankLineBelowAMultiLineTrailingCommentIsRemoved()
	{
		const string source = """
			class C
			{
				void M()
				{
					Call(
						Compute(
							1), /* a
						b */

						2);
				}

				static void Call(int first, int second) { }

				static int Compute(int value) => value;
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					Call(
						Compute(
							1), /* a
						b */
						2);
				}

				static void Call(int first, int second) { }

				static int Compute(int value) => value;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepMultiLineArgumentsTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepMultiLineArgumentsTogether)
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
				void M()
				{
					Call(
						Compute(
							1), /* a
						b */
						2);
				}

				static void Call(int first, int second) { }

				static int Compute(int value) => value;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepMultiLineArgumentsTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepMultiLineArgumentsTogether)
			);

		Assert.IsEmpty(result.Diagnostics);
	}
}
