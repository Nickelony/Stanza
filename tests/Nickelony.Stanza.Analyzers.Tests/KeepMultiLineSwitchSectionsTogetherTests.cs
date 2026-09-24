using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class KeepMultiLineSwitchSectionsTogetherTests
{
	[TestMethod]
	public async Task APragmaOverOneSectionBoundaryHandsOnlyThatBoundaryToTheTightRule()
	{
		string source = $$"""
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1:
							return;

			#pragma warning disable {{DiagnosticDescriptors.SeparateMultiLineSwitchSections.Id}}
						case 2:
							return;

						case 3:
							return;
			#pragma warning restore {{DiagnosticDescriptors.SeparateMultiLineSwitchSections.Id}}

						case 4:
							return;

						case 5:
							return;
					}
				}
			}
			""";

		string fixedSource = $$"""
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1:
							return;

			#pragma warning disable {{DiagnosticDescriptors.SeparateMultiLineSwitchSections.Id}}
						case 2:
							return;
						case 3:
							return;
			#pragma warning restore {{DiagnosticDescriptors.SeparateMultiLineSwitchSections.Id}}

						case 4:
							return;

						case 5:
							return;
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepMultiLineSwitchSectionsTogetherAnalyzer>(
				source,

				TestRuleSets.Only(
					DiagnosticDescriptors.SeparateMultiLineSwitchSections,
					DiagnosticDescriptors.KeepMultiLineSwitchSectionsTogether
				)
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
						case 1:
							return;
						case 2:
							return;
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepMultiLineSwitchSectionsTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepMultiLineSwitchSectionsTogether)
			);

		Assert.IsEmpty(result.Diagnostics);
	}
}
