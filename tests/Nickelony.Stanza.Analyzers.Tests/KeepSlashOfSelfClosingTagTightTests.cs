using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Documentation;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class KeepSlashOfSelfClosingTagTightTests
{
	[TestMethod]
	public async Task TheTightRuleTakesTheSpaceAway()
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

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <returns><see langword="null"/> when the section is missing.</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepSlashOfSelfClosingTagTightAnalyzer>(
				source,
				TestRuleSets.GapRulesEnabled
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ASlashInsideAListIsLeftAsItIs()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <list type="bullet">
				/// <item><description>A <see langword="null" /> value.</description></item>
				/// </list>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepSlashOfSelfClosingTagTightAnalyzer>(
				source,
				TestRuleSets.GapRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheTwoRulesLeaveTheGapToTheTightOne()
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
				/// <returns><see langword="null"/> when the section is missing.</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,
			TestRuleSets.TagRulesEnabled
		);

		string reportedIds = string.Join(
			",",
			result.Diagnostics.Select(diagnostic => diagnostic.Id).Distinct().OrderBy(id => id)
		);

		Assert.AreEqual(
			TestRuleSets.JoinSortedIds([DiagnosticDescriptors.KeepSlashOfSelfClosingTagTight]),
			reportedIds
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The slash gap needs no body of the comment, so an XML comment or a CDATA section does not hide it.
	[TestMethod]
	public async Task AMarkupCommentOrACdataSectionDoesNotHideAGapBeforeTheSlash()
	{
		const string source = """
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

		const string fixedSource = """
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

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepSlashOfSelfClosingTagTightAnalyzer>(
				source,
				TestRuleSets.GapRulesEnabled
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
