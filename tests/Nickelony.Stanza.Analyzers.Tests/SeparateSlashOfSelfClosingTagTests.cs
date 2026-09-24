using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Documentation;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class SeparateSlashOfSelfClosingTagTests
{
	[TestMethod]
	public async Task TheSpacedRulePutsOneSpaceBeforeTheSlash()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <returns><see langword="null"/> when the section is missing.</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <returns><see langword="null" /> when the section is missing.</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSlashOfSelfClosingTagAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheSpacedRuleNormalizesARunOfWhitespace()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <returns><see langword="null"  /> when the section is missing.</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <returns><see langword="null" /> when the section is missing.</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSlashOfSelfClosingTagAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task APragmaThatSilencesTheTightRuleLeavesTheGapToTheSpacedOne()
	{
		string source = $$"""
			internal sealed class Sample
			{
			#pragma warning disable {{DiagnosticDescriptors.KeepSlashOfSelfClosingTagTight.Id}}
				/// <returns><see langword="null"  /> when the section is missing.</returns>
				internal int Bind() => 1;
			#pragma warning restore {{DiagnosticDescriptors.KeepSlashOfSelfClosingTagTight.Id}}
			}
			""";

		string fixedSource = $$"""
			internal sealed class Sample
			{
			#pragma warning disable {{DiagnosticDescriptors.KeepSlashOfSelfClosingTagTight.Id}}
				/// <returns><see langword="null" /> when the section is missing.</returns>
				internal int Bind() => 1;
			#pragma warning restore {{DiagnosticDescriptors.KeepSlashOfSelfClosingTagTight.Id}}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,
			TestRuleSets.TagRulesEnabled
		);

		Assert.HasCount(1, result.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.SeparateSlashOfSelfClosingTag.Id,
			result.Diagnostics[0].Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The slash gap needs no body of the comment, so an XML comment or a CDATA section does not hide it.
	[TestMethod]
	public async Task AMarkupCommentOrACdataSectionDoesNotHideAMissingGapBeforeTheSlash()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Reads <see cref="Sample"/> here.</summary>
				/// <!-- note -->
				internal int A() => 1;

				/// <summary>
				/// Text with <see cref="Sample"/> and a
				/// <![CDATA[ raw ]]>
				/// </summary>
				internal int D() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Reads <see cref="Sample" /> here.</summary>
				/// <!-- note -->
				internal int A() => 1;

				/// <summary>
				/// Text with <see cref="Sample" /> and a
				/// <![CDATA[ raw ]]>
				/// </summary>
				internal int D() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSlashOfSelfClosingTagAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ASlashThatAlreadyHasItsSpaceIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <returns><see langword="null" /> when the section is missing.</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSlashOfSelfClosingTagAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}
}
