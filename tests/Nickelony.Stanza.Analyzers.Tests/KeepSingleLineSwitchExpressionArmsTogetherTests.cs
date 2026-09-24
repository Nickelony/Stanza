using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class KeepSingleLineSwitchExpressionArmsTogetherTests
{
	// The pair rows assign the switch to a local, so a switch the method returns is pinned here.
	[TestMethod]
	public async Task TheBlankLineBetweenTheArmsOfAReturnedSwitchIsRemoved()
	{
		const string source = """
			class C
			{
				string M(int value)
				{
					return value switch
					{
						1 => "one",

						_ => "other",
					};
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				string M(int value)
				{
					return value switch
					{
						1 => "one",
						_ => "other",
					};
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepSingleLineSwitchExpressionArmsTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepSingleLineSwitchExpressionArmsTogether)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ArmsThatAlreadyStandTogetherAreLeftAlone()
	{
		const string source = """
			class C
			{
				string M(int value)
				{
					return value switch
					{
						1 => "one",
						_ => "other",
					};
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepSingleLineSwitchExpressionArmsTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepSingleLineSwitchExpressionArmsTogether)
			);

		Assert.IsEmpty(result.Diagnostics);
	}
}
