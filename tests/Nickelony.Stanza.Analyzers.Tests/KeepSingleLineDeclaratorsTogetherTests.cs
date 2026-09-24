using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class KeepSingleLineDeclaratorsTogetherTests
{
	// The pair rows declare locals, so the declarators of a field are pinned here.
	[TestMethod]
	public async Task TheBlankLineBetweenTheDeclaratorsOfAFieldIsRemoved()
	{
		const string source = """
			class C
			{
				int first = 1,

					second = 2;
			}
			""";

		const string fixedSource = """
			class C
			{
				int first = 1,
					second = 2;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepSingleLineDeclaratorsTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepSingleLineDeclaratorsTogether)
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
				int first = 1,
					second = 2;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepSingleLineDeclaratorsTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepSingleLineDeclaratorsTogether)
			);

		Assert.IsEmpty(result.Diagnostics);
	}
}
