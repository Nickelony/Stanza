using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.MarkupLiterals;

namespace Nickelony.Stanza.Analyzers.Tests.MarkupLiterals;

[TestClass]
public sealed class KeepSlashOfSelfClosingMarkupElementTightTests
{
	[TestMethod]
	public async Task TheTightRuleTakesTheWhitespaceBeforeTheSlashAway()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<ItemGroup>
						  <Compile Include="a.cs" />
						  <Compile Include="b.cs"   />
						  <Compile Include="c.cs"	/>
						  <None />
						  <Compile Include="d.cs"/>
						</ItemGroup>
						""";
				}
			}
			"""";

		const string fixedSource = """"
			class C
			{
				void M()
				{
					const string markup = """
						<ItemGroup>
						  <Compile Include="a.cs"/>
						  <Compile Include="b.cs"/>
						  <Compile Include="c.cs"/>
						  <None/>
						  <Compile Include="d.cs"/>
						</ItemGroup>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepSlashOfSelfClosingMarkupElementTightAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepSlashOfSelfClosingMarkupElementTight)
			);

		Assert.HasCount(4, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AWrappedTagAndASlashOnItsOwnLineStayAsWritten()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<ItemGroup>
						  <Compile
						    Include="a.cs"
						    />
						  <Compile Include="b.cs"
						  />
						</ItemGroup>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepSlashOfSelfClosingMarkupElementTightAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepSlashOfSelfClosingMarkupElementTight)
			);

		Assert.IsEmpty(result.Diagnostics);
		RoslynTestHarness.AssertFixedText(source, result);
	}

	[TestMethod]
	public async Task AFileEnablingBothHalvesLeavesTheGapToTheSpacedRule()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a />
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,
			TestRuleSets.MarkupSlashRulesEnabled
		);

		Assert.IsEmpty(result.Diagnostics);
		RoslynTestHarness.AssertFixedText(source, result);
	}

	[TestMethod]
	public async Task APragmaThatSilencesTheSpacedRuleHandsTheGapToTheTightOne()
	{
		string source = $$""""
			class C
			{
				void M()
				{
					#pragma warning disable {{DiagnosticDescriptors.SeparateSlashOfSelfClosingMarkupElement.Id}}
					const string markup = """
						<root>
						  <a />
						</root>
						""";
				}
			}
			"""";

		string fixedSource = $$""""
			class C
			{
				void M()
				{
					#pragma warning disable {{DiagnosticDescriptors.SeparateSlashOfSelfClosingMarkupElement.Id}}
					const string markup = """
						<root>
						  <a/>
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,
			TestRuleSets.MarkupSlashRulesEnabled
		);

		Assert.HasCount(1, result.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.KeepSlashOfSelfClosingMarkupElementTight.Id,
			result.Diagnostics[0].Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
