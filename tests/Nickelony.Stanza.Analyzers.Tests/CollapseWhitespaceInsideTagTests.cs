using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Documentation;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class CollapseWhitespaceInsideTagTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> TagRuleOnly { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.CollapseWhitespaceInsideTag);

	// The indentation of a tag line in a comment without stars is the comment's marker, not space inside a tag.
	[TestMethod]
	public async Task TheIndentationOfAStarlessDelimitedCommentIsLeftAlone()
	{
		const string source = """
			class C
			{
				/**
				  <summary>Binds the options.</summary>
				  <returns>The options.</returns>
				 */
				int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<CollapseWhitespaceInsideTagAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.CollapseWhitespaceInsideTag)
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ARunInsideATagIsCollapsedToOneSpace()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Reads <see  langword="null"/> and <c>nothing  else</c></summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Reads <see langword="null"/> and <c>nothing  else</c></summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<CollapseWhitespaceInsideTagAnalyzer>(
				source,
				TagRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARunBeforeTheBracketBecomesOneSpace()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary  >Binds the options from the configuration section</summary  >
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary >Binds the options from the configuration section</summary >
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<CollapseWhitespaceInsideTagAnalyzer>(
				source,
				TagRuleOnly
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheGapBeforeTheSlashIsLeftToTheTagPair()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Reads <see langword="null"  /> when it is set</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<CollapseWhitespaceInsideTagAnalyzer>(
				source,
				TagRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ATagWrittenAcrossLinesIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Reads
				/// <see
				/// 	langword="null"
				/// 	/> when it is set</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<CollapseWhitespaceInsideTagAnalyzer>(
				source,
				TagRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnAttributeValueIsNotAGap()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Reads <see langword="null  "/> when it is set</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<CollapseWhitespaceInsideTagAnalyzer>(
				source,
				TagRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ATagInsideAnotherElementIsJudged()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <remarks>Reads <see  langword="null"/> when the host starts.</remarks>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <remarks>Reads <see langword="null"/> when the host starts.</remarks>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<CollapseWhitespaceInsideTagAnalyzer>(
				source,
				TagRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ATagInsideACodeBlockIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <code>Reads <see  langword="null"/> when the host starts.</code>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<CollapseWhitespaceInsideTagAnalyzer>(
				source,
				TagRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACommentThatSharesItsLineWithCodeIsJudged()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Bind(int value)
				{
					return value;
				} /// <summary>Reads <see  langword="null"/> when it is set</summary>
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				internal int Bind(int value)
				{
					return value;
				} /// <summary>Reads <see langword="null"/> when it is set</summary>
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<CollapseWhitespaceInsideTagAnalyzer>(
				source,
				TagRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task FixAllCollapsesEveryRunInOnePass()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Reads <see  langword="null"/> when it is set</summary>
				internal int Bind()
				{
					return 1;
				}

				/// <summary>Writes <see   langword="null"/> when it is unset</summary>
				internal int Release()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Reads <see langword="null"/> when it is set</summary>
				internal int Bind()
				{
					return 1;
				}

				/// <summary>Writes <see langword="null"/> when it is unset</summary>
				internal int Release()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunFixAllAsync(source, TagRuleOnly);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ATagWhitespaceFixWorksWithoutTheAdditionalLocation()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Reads <see  langword="null"/> when it is set</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Reads <see langword="null"/> when it is set</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<CollapseWhitespaceInsideTagAnalyzer>(
				source,
				TagRuleOnly
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
