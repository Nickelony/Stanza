using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class KeepMultiLineDeclaratorsTogetherTests
{
	// The pair rows declare locals, so the declarators of a field are pinned here.
	[TestMethod]
	public async Task TheBlankLineBetweenTheDeclaratorsOfAFieldIsRemoved()
	{
		const string source = """
			class C
			{
				int lookup = 1 +
					2,

					capacity = 3 +
					4;
			}
			""";

		const string fixedSource = """
			class C
			{
				int lookup = 1 +
					2,
					capacity = 3 +
					4;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepMultiLineDeclaratorsTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepMultiLineDeclaratorsTogether)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task DeclaratorsThatAlreadyStandTogetherAreLeftAlone()
	{
		const string source = """
			class C
			{
				int lookup = 1 +
					2,
					capacity = 3 +
					4;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepMultiLineDeclaratorsTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepMultiLineDeclaratorsTogether)
			);

		Assert.IsEmpty(result.Diagnostics);
	}
}
