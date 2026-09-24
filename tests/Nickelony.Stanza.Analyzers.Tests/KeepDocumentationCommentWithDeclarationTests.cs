using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Documentation;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class KeepDocumentationCommentWithDeclarationTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> GapRuleOnly { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.KeepDocumentationCommentWithDeclaration);

	[TestMethod]
	public async Task ABlankLineBetweenACommentAndItsDeclarationIsRemoved()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options from the configuration section.</summary>

				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Binds the options from the configuration section.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepDocumentationCommentWithDeclarationAnalyzer>(
				source,
				GapRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);

		Assert.IsLessThanOrEqualTo(
			result.Diagnostics[0].Location.SourceSpan.Start,
			result.Diagnostics[0].AdditionalLocations[0].SourceSpan.End,
			"The comment does not travel with the declaration it documents."
		);
	}

	[TestMethod]
	public async Task TwoBlankLinesAreTakenAwayAtOnce()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options from the configuration section.</summary>


				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Binds the options from the configuration section.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepDocumentationCommentWithDeclarationAnalyzer>(
				source,
				GapRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentBeforeAnAttributeListBelongsToTheDeclaration()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options from the configuration section.</summary>

				[System.Obsolete]
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Binds the options from the configuration section.</summary>
				[System.Obsolete]
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepDocumentationCommentWithDeclarationAnalyzer>(
				source,
				GapRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentAfterTheAttributeListIsLeftToTheCompiler()
	{
		const string source = """
			internal sealed class Sample
			{
				[System.Obsolete]

				/// <summary>Binds the options from the configuration section.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepDocumentationCommentWithDeclarationAnalyzer>(
				source,
				GapRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnEnumMemberKeepsItsComment()
	{
		const string source = """
			internal enum Kind
			{
				/// <summary>The first kind.</summary>

				First,
			}
			""";

		const string fixedSource = """
			internal enum Kind
			{
				/// <summary>The first kind.</summary>
				First,
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepDocumentationCommentWithDeclarationAnalyzer>(
				source,
				GapRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ALocalFunctionKeepsItsComment()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					/// <summary>Builds the value.</summary>

					int Build() => 1;

					return Build();
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					/// <summary>Builds the value.</summary>
					int Build() => 1;

					return Build();
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepDocumentationCommentWithDeclarationAnalyzer>(
				source,
				GapRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentBeforeAStatementIsLeftToTheCompiler()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					/// <summary>Builds the value.</summary>

					int value = 1;

					return value;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepDocumentationCommentWithDeclarationAnalyzer>(
				source,
				GapRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ASecondCommentInTheGapLeavesItAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>The first thought.</summary>

				/// <summary>The second thought.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepDocumentationCommentWithDeclarationAnalyzer>(
				source,
				GapRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ABlockCommentIsKeptWithItsDeclarationToo()
	{
		const string source = """
			internal sealed class Sample
			{
				/** <summary>Binds the options from the configuration section.</summary> */

				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/** <summary>Binds the options from the configuration section.</summary> */
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepDocumentationCommentWithDeclarationAnalyzer>(
				source,
				GapRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task EveryGapGoesInOneFixAllPass()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options from the configuration section.</summary>

				internal int Bind()
				{
					return 1;
				}

				/// <summary>Reads the options from the configuration section.</summary>

				internal int Read()
				{
					return 2;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Binds the options from the configuration section.</summary>
				internal int Bind()
				{
					return 1;
				}

				/// <summary>Reads the options from the configuration section.</summary>
				internal int Read()
				{
					return 2;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunFixAllAsync(source, GapRuleOnly);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheGapRuleAndTheOtherRulesSettleTogether()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options from the configuration section.</summary>

				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Binds the options from the configuration section.</summary>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
