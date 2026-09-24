using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class KeepSingleLineSwitchSectionsTogetherTests
{
	// The pair rows hold `case` labels alone, so the section a `default` label opens is pinned here.
	[TestMethod]
	public async Task TheBlankLineBeforeADefaultSectionIsRemoved()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1: return;

						default: return;
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1: return;
						default: return;
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepSingleLineSwitchSectionsTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepSingleLineSwitchSectionsTogether)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task SectionsThatAlreadyStandTogetherAreLeftAlone()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1: return;
						default: return;
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepSingleLineSwitchSectionsTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepSingleLineSwitchSectionsTogether)
			);

		Assert.IsEmpty(result.Diagnostics);
	}
}
