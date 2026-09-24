using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class KeepMultiLineParametersTogetherTests
{
	// The pair rows hold a method, so the parameters of a constructor are pinned here.
	[TestMethod]
	public async Task TheBlankLineBetweenTheParametersOfAConstructorIsRemoved()
	{
		const string source = """
			using System.Collections.Generic;

			class C
			{
				C(
					IReadOnlyDictionary<
						string,
						int> lookup,

					IReadOnlyDictionary<
						string,
						int> other)
				{
					return;
				}
			}
			""";

		const string fixedSource = """
			using System.Collections.Generic;

			class C
			{
				C(
					IReadOnlyDictionary<
						string,
						int> lookup,
					IReadOnlyDictionary<
						string,
						int> other)
				{
					return;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepMultiLineParametersTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepMultiLineParametersTogether)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ParametersThatAlreadyStandTogetherAreLeftAlone()
	{
		const string source = """
			using System.Collections.Generic;

			class C
			{
				C(
					IReadOnlyDictionary<
						string,
						int> lookup,
					IReadOnlyDictionary<
						string,
						int> other)
				{
					return;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepMultiLineParametersTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepMultiLineParametersTogether)
			);

		Assert.IsEmpty(result.Diagnostics);
	}
}
