using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.MarkupLiterals;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class MarkupLiteralFamilyTests
{
	[TestMethod]
	public async Task TheCapStaysSilentOverARunAnEnabledTightRuleOwns()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a>
						    1
						  </a>



						  <b>2</b>
						</root>
						""";
				}
			}
			"""";

		IReadOnlyDictionary<string, ReportDiagnostic> both = TestRuleSets.Only(
			DiagnosticDescriptors.CollapseExtraBlankLinesBetweenMarkupElements,
			DiagnosticDescriptors.KeepMultiLineMarkupElementsTogether
		);

		AnalyzerRunResult cap =
			await RoslynTestHarness.ReportAsync<CollapseExtraBlankLinesBetweenMarkupElementsAnalyzer>(
				source,
				both
			);

		AnalyzerRunResult tight =
			await RoslynTestHarness.ReportAsync<KeepMultiLineMarkupElementsTogetherAnalyzer>(
				source,
				both
			);

		Assert.IsEmpty(cap.Diagnostics);
		Assert.HasCount(1, tight.Diagnostics);
	}
}
