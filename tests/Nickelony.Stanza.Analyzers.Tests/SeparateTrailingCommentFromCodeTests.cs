using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Comments;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class SeparateTrailingCommentFromCodeTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> TrailingRuleOnly { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.SeparateTrailingCommentFromCode);

	[TestMethod]
	public async Task ACommentThatTrailsCodeIsSeparated()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Bind(int seed)
				{
					int value = seed;// the option
					value += 1;/* the step */
					return value;// the result
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				internal int Bind(int seed)
				{
					int value = seed; // the option
					value += 1; /* the step */
					return value; // the result
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateTrailingCommentFromCodeAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentWithWhitespaceBeforeItIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Bind(int seed)
				{
					int value = seed; // the option
					value += 1;   /* the step */
					return value;	// the result
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateTrailingCommentFromCodeAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACommentOnItsOwnLineIsLeftAlone()
	{
		const string source = """
			// The sample of this file.

			internal sealed class Sample
			{
				/// Binds the options of the sample.
				internal int Bind(int seed)
				{
					/* The step of the sample. */
					int value = seed;

					// The result of the sample.
					return value;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateTrailingCommentFromCodeAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	// A build that writes no documentation file parses the comment as a plain one that trails the code.
	[TestMethod]
	[DataRow(DocumentationMode.Parse)]
	[DataRow(DocumentationMode.None)]
	public async Task ADocumentationCommentAfterCodeIsLeftAlone(DocumentationMode mode)
	{
		using IDisposable parse = RoslynTestHarness.AtDocumentationMode(mode);

		const string source = """
			internal sealed class Sample
			{
				internal int Bind(int seed)
				{
					int value = seed;/// the option of the sample.
					value += 1;/** the step of the sample. */
					return value;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateTrailingCommentFromCodeAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACommentedOutRunIsSeparatedFromTheCode()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Bind(int seed)
				{
					int value = seed;//// var value = seed;
					return value;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				internal int Bind(int seed)
				{
					int value = seed; //// var value = seed;
					return value;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateTrailingCommentFromCodeAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABlockCommentBetweenTwoStatementsIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Bind(int seed)
				{
					int value = seed;/* the option */value += 1;
					return value;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateTrailingCommentFromCodeAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACommentAfterADirectiveIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
			#pragma warning disable CS0169// the field warning
			#nullable enable// the annotations
				internal int Bind(int seed)
				{
			#if DEBUG// the debug path
					return seed;
			#else// the release path
					return 0;
			#endif// the debug path
				}
			#pragma warning restore CS0169// the end of the field warning
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateTrailingCommentFromCodeAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACommentThatCodeFollowsOnItsLineIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				internal void Bind()
				{
					Call(/* first: */ 1, 2);
					Call(1/* the second */, 2);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateTrailingCommentFromCodeAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ABlockCommentAtTheEndOfTheFileIsSeparated()
	{
		const string source = """
			internal sealed class Sample
			{
			}/* the end */
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
			} /* the end */
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateTrailingCommentFromCodeAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ATrailingCommentFixWorksWithoutTheAdditionalLocation()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Bind(int seed)
				{
					int value = seed;// the option
					return value;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				internal int Bind(int seed)
				{
					int value = seed; // the option
					return value;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<SeparateTrailingCommentFromCodeAnalyzer>(
				source,
				TrailingRuleOnly
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
