using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.MarkupLiterals;

namespace Nickelony.Stanza.Analyzers.Tests.MarkupLiterals;

[TestClass]
public sealed class SeparateSlashOfSelfClosingMarkupElementTests
{
	[TestMethod]
	public async Task TheSpacedRulePutsOneSpaceBeforeEverySlash()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<ItemGroup>
						  <Compile Include="a.cs"/>
						  <Compile Include="b.cs"   />
						  <Compile Include="c.cs"	/>
						  <None/>
						  <Compile Include="d.cs" />
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
						  <Compile Include="a.cs" />
						  <Compile Include="b.cs" />
						  <Compile Include="c.cs" />
						  <None />
						  <Compile Include="d.cs" />
						</ItemGroup>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSlashOfSelfClosingMarkupElementAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.SeparateSlashOfSelfClosingMarkupElement)
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
			await RoslynTestHarness.RunAsync<SeparateSlashOfSelfClosingMarkupElementAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.SeparateSlashOfSelfClosingMarkupElement)
			);

		Assert.IsEmpty(result.Diagnostics);
		RoslynTestHarness.AssertFixedText(source, result);
	}

	[TestMethod]
	public async Task PreservedSpaceTextAndAttributesStayAsWritten()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root note="a/>b">
						  <pre xml:space="preserve"><b/></pre>
						  text <b/> and more
						  <a x="1"/>
						</root>
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
						<root note="a/>b">
						  <pre xml:space="preserve"><b/></pre>
						  text <b /> and more
						  <a x="1" />
						</root>
						""";
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSlashOfSelfClosingMarkupElementAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.SeparateSlashOfSelfClosingMarkupElement)
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AUtf8LiteralIsSeparatedToo()
	{
		const string source = """"
			class C
			{
				void M()
				{
					var markup = """
						<root>
						  <a/>
						</root>
						"""u8;
				}
			}
			"""";

		const string fixedSource = """"
			class C
			{
				void M()
				{
					var markup = """
						<root>
						  <a />
						</root>
						"""u8;
				}
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSlashOfSelfClosingMarkupElementAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.SeparateSlashOfSelfClosingMarkupElement)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AFileEnablingBothHalvesGetsTheSpacedShapeFromThisRule()
	{
		const string source = """"
			class C
			{
				void M()
				{
					const string markup = """
						<root>
						  <a/>
						</root>
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

		Assert.HasCount(1, result.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.SeparateSlashOfSelfClosingMarkupElement.Id,
			result.Diagnostics[0].Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task APragmaThatSilencesTheTightRuleLeavesTheGapToTheSpacedOne()
	{
		string source = $$""""
			class C
			{
				void M()
				{
					#pragma warning disable {{DiagnosticDescriptors.KeepSlashOfSelfClosingMarkupElementTight.Id}}
					const string markup = """
						<root>
						  <a/>
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
					#pragma warning disable {{DiagnosticDescriptors.KeepSlashOfSelfClosingMarkupElementTight.Id}}
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

		Assert.HasCount(1, result.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.SeparateSlashOfSelfClosingMarkupElement.Id,
			result.Diagnostics[0].Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
