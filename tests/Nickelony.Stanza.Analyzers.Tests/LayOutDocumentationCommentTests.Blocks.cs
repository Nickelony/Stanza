using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Documentation;

namespace Nickelony.Stanza.Analyzers.Tests;

public sealed partial class LayOutDocumentationCommentTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> BlockRuleOnly { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.LayOutDocumentationComment);

	[TestMethod]
	public async Task ABlockIsLaidOutAtTheWidth()
	{
		const string source = """
			internal sealed class Sample
			{
				/** Binds the options from the configuration section, validates them and lets the host fail at startup rather than at the first request. */
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/**
				 * Binds the options from the configuration section, validates them and lets the host fail at
				 * startup rather than at the first request.
				 */
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AOneLineBlockOpensItsMarkers()
	{
		const string source = """
			internal sealed class Sample
			{
				/** Binds the options. */
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/**
				 * Binds the options.
				 */
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABlockKeepsTheBlankLineOfAParagraphBreak()
	{
		const string source = """
			internal sealed class Sample
			{
				/**
				 * Binds the options from the configuration section, validates them and lets the host fail at startup.
				 *
				 * The second paragraph is laid out the same way rather than joined to the first one.
				 */
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/**
				 * Binds the options from the configuration section, validates them and lets the host fail
				 * at startup.
				 *
				 * The second paragraph is laid out the same way rather than joined to the first one.
				 */
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABlockWithAnElementIsLaidOut()
	{
		const string source = """
			internal sealed class Sample
			{
				/**
				 * <summary>Binds the options from the configuration section, validates them and lets the host fail at startup rather than at the first request.</summary>
				 * <param name="value">The options to bind.</param>
				 */
				internal int Bind(int value)
				{
					return value;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/**
				 * <summary>
				 * Binds the options from the configuration section, validates them and lets the host fail at
				 * startup rather than at the first request.
				 * </summary>
				 * <param name="value">The options to bind.</param>
				 */
				internal int Bind(int value)
				{
					return value;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABlockExpandsAnElementTheScopeNames()
	{
		const string source = """
			internal sealed class Sample
			{
				/**
				 * <param name="value">The options to bind.</param>
				 */
				internal int Bind(int value)
				{
					return value;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/**
				 * <param name="value">
				 * The options to bind.
				 * </param>
				 */
				internal int Bind(int value)
				{
					return value;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				BlockRuleOnly,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_doc_expand"] = "sections",
				}
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnEmptyBlockIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/** */
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ABlockThatHoldsACodeBlockIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/**
				 * <code>
				 * int value = 1;
				 * </code>
				 */
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ABlockWhoseElementHoldsABlankLineIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/**
				 * <remarks>
				 *
				 * Binds the options from the configuration section.
				 * </remarks>
				 */
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task BothFormsOfACommentAreLaidOutByOnePass()
	{
		const string source = """
			internal sealed class Sample
			{
				/// Binds the options from the configuration section, validates them and lets the host fail at startup rather than at the first request.
				internal int First()
				{
					return 1;
				}

				/** Binds the options from the configuration section, validates them and lets the host fail at startup rather than at the first request. */
				internal int Second()
				{
					return 2;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// Binds the options from the configuration section, validates them and lets the host fail at
				/// startup rather than at the first request.
				internal int First()
				{
					return 1;
				}

				/**
				 * Binds the options from the configuration section, validates them and lets the host fail at
				 * startup rather than at the first request.
				 */
				internal int Second()
				{
					return 2;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ANestedBlockIsLaidOutAtItsOwnIndentation()
	{
		const string source = """
			internal sealed class Sample
			{
				internal sealed class Nested
				{
					/** Binds the options from the configuration section, validates them and lets the host fail at startup rather than at the first request. */
					internal int Bind()
					{
						return 1;
					}
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				internal sealed class Nested
				{
					/**
					 * Binds the options from the configuration section, validates them and lets the host fail
					 * at startup rather than at the first request.
					 */
					internal int Bind()
					{
						return 1;
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABlockThatSharesItsLineWithCodeIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int First() { return 1; } /** Binds the options from the configuration section, validates them and lets the host fail at startup rather than at the first request. */
				internal int Second()
				{
					return 2;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task EveryBlockGoesInOneFixAllPass()
	{
		const string source = """
			internal sealed class Sample
			{
				/** Binds the options. */
				internal int First()
				{
					return 1;
				}

				/** Keeps the options. */
				internal int Second()
				{
					return 2;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/**
				 * Binds the options.
				 */
				internal int First()
				{
					return 1;
				}

				/**
				 * Keeps the options.
				 */
				internal int Second()
				{
					return 2;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunFixAllAsync(source, BlockRuleOnly);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ATextThatTouchesItsStarIsSeparatedFromIt()
	{
		const string source = """
			internal sealed class Sample
			{
				/**
				 *Binds the options from the configuration section, validates them and lets the host fail at
				 *startup rather than at the first request.
				 */
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/**
				 * Binds the options from the configuration section, validates them and lets the host fail at
				 * startup rather than at the first request.
				 */
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The compiler strips stars only when every line carries one at one column,
	// so a block whose lines carry them unevenly holds the stars as text, and a layout would change that text.
	[TestMethod]
	[DataRow(
		" * Binds the options from the configuration section, validates them and lets the host fail at startup.",
		" Continued here without a star."
	)]
	[DataRow(
		" * Binds the options from the configuration section, validates them and lets the host fail at startup.",
		"   * Continued here at another column."
	)]
	public async Task ABlockWhoseLinesCarryStarsUnevenlyIsLeftAlone(string first, string second)
	{
		string source = $$"""
			internal sealed class Sample
			{
				/**
			{{first}}
			{{second}}
				 */
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				BlockRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheLinesOfABlockAreIndentedAfterTheirStar()
	{
		const string source = """
			internal sealed class Sample
			{
				/** <summary>Binds the options from the configuration section, validates them and lets the host fail at startup rather than at the first request.</summary> */
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/**
				 * <summary>
				 *   Binds the options from the
				 *   configuration section, validates them
				 *   and lets the host fail at startup
				 *   rather than at the first request.
				 * </summary>
				 */
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		var options = new Dictionary<string, string>
		{
			["stanza_max_line_length"] = "46",
			["stanza_doc_indent_size"] = "2",
		};

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				BlockRuleOnly,
				analyzerConfigOptions: options
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
