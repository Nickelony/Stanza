using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class KeepMultiLineSwitchExpressionArmsTogetherTests
{
	// The pair rows hold constant patterns alone, so an arm with the discard pattern is pinned here.
	[TestMethod]
	public async Task TheBlankLineBeforeADiscardArmIsRemoved()
	{
		const string source = """
			class C
			{
				string M(int value)
				{
					return value switch
					{
						1 => Build(
							value
						),

						_ => Build(
							value
						),
					};
				}

				string Build(int value)
				{
					return value.ToString();
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
						1 => Build(
							value
						),
						_ => Build(
							value
						),
					};
				}

				string Build(int value)
				{
					return value.ToString();
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepMultiLineSwitchExpressionArmsTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepMultiLineSwitchExpressionArmsTogether)
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
						1 => Build(
							value
						),
						_ => Build(
							value
						),
					};
				}

				string Build(int value)
				{
					return value.ToString();
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepMultiLineSwitchExpressionArmsTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepMultiLineSwitchExpressionArmsTogether)
			);

		Assert.IsEmpty(result.Diagnostics);
	}
}
