using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Comments;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class SeparateSingleLineCommentMarkerFromTextTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> MarkerRuleOnly { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.SeparateSingleLineCommentMarkerFromText);

	[TestMethod]
	public async Task AMarkerThatTouchesItsTextIsSeparated()
	{
		const string source = """
			internal sealed class Sample
			{
				//Binds the options of an item
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				// Binds the options of an item
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSingleLineCommentMarkerFromTextAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ATrailingCommentMarkerIsSeparated()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					int value = 1; //the value the host reads
					return value;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					int value = 1; // the value the host reads
					return value;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSingleLineCommentMarkerFromTextAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AMarkerOnADirectiveLineIsSeparated()
	{
		const string source = """
			#define TRACING //the trace switch

			internal sealed class Sample
			{
			#pragma warning disable CS0169 //the field warning
			#pragma warning restore CS0169//the end of the field warning
			#nullable enable //the annotations
				internal int Bind(int seed)
				{
			#if DEBUG //the debug path
					return seed;
			#elif TRACING //the trace path
					return seed + 1;
			#else //the release path
					return 0;
			#endif //DEBUG
				}
			#nullable restore ///the project's annotations
			}
			""";

		const string fixedSource = """
			#define TRACING // the trace switch

			internal sealed class Sample
			{
			#pragma warning disable CS0169 // the field warning
			#pragma warning restore CS0169// the end of the field warning
			#nullable enable // the annotations
				internal int Bind(int seed)
				{
			#if DEBUG // the debug path
					return seed;
			#elif TRACING // the trace path
					return seed + 1;
			#else // the release path
					return 0;
			#endif // DEBUG
				}
			#nullable restore /// the project's annotations
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunFixAllAsync(source, MarkerRuleOnly);

		Assert.HasCount(9, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AMarkerOnADirectiveLineIsSeparatedWithoutTheAdditionalLocation()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Bind(int seed)
				{
			#if DEBUG
					return seed;
			#else
					return 0;
			#endif //DEBUG
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				internal int Bind(int seed)
				{
			#if DEBUG
					return seed;
			#else
					return 0;
			#endif // DEBUG
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<SeparateSingleLineCommentMarkerFromTextAnalyzer>(
				source,
				MarkerRuleOnly
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The text after the keyword of a region marker or a diagnostic directive is the message it carries.
	[TestMethod]
	public async Task TheMessageOfADirectiveIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
			#region //Options
			#region Binding //the bound values
				internal int Bind(int seed)
				{
			#warning //Check the seed
					return seed;
				}
			#endregion //Binding
			#endregion//Options
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSingleLineCommentMarkerFromTextAnalyzer>(
				source,
				MarkerRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ADocumentationCommentMarkerIsSeparated()
	{
		const string source = """
			internal sealed class Sample
			{
				///Binds the options from the configuration section.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// Binds the options from the configuration section.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSingleLineCommentMarkerFromTextAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task CommentedOutCodeIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					////int value = 1;
					////---- the boundary of the host
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSingleLineCommentMarkerFromTextAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ASpacedOrEmptyCommentIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					//  Binds the options of an item
					//
					// Binds the options of an item
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSingleLineCommentMarkerFromTextAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ABlockCommentIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					/*Binds the options of an item*/
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSingleLineCommentMarkerFromTextAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task EveryTouchingMarkerIsFixedInOnePass()
	{
		const string source = """
			internal sealed class Sample
			{
				//Binds the options of an item
				internal int Bind()
				{
					int value = 1; //the value the host reads
					return value;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				// Binds the options of an item
				internal int Bind()
				{
					int value = 1; // the value the host reads
					return value;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunFixAllAsync(source, MarkerRuleOnly);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AMarkerFixWorksWithoutTheAdditionalLocation()
	{
		const string source = """
			internal sealed class Sample
			{
				//Binds the options of an item
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				// Binds the options of an item
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<SeparateSingleLineCommentMarkerFromTextAnalyzer>(
				source,
				MarkerRuleOnly
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task EveryLineOfADocumentationBlockIsFixedInOnePass()
	{
		const string source = """
			internal sealed class Sample
			{
				///<summary>
				///Binds the options of an item.
				/// </summary>
				///<returns>The bound value.</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>
				/// Binds the options of an item.
				/// </summary>
				/// <returns>The bound value.</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunFixAllAsync(source, MarkerRuleOnly);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task EveryLineOfAConsecutiveCommentRunIsFixedInOnePass()
	{
		const string source = """
			internal sealed class Sample
			{
				//Binds the options
				// of an item.
				//Returns the bound value.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				// Binds the options
				// of an item.
				// Returns the bound value.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunFixAllAsync(source, MarkerRuleOnly);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnEmptyOrSpacedLineInsideADocumentationBlockIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				///
				///	Binds the options of an item.
				///   Keeps the extra indent.
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSingleLineCommentMarkerFromTextAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ALaterLineOfADocumentationBlockThatTouchesItsTextIsSeparated()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				///Binds the options of an item.
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>
				/// Binds the options of an item.
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSingleLineCommentMarkerFromTextAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task CommentedOutCodeAfterADocumentationBlockIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				///<summary>Binds the options of an item.</summary>
				////int value = 1;
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Binds the options of an item.</summary>
				////int value = 1;
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSingleLineCommentMarkerFromTextAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// Roslyn ends a line at U+2028, U+2029 and U+0085, so a marker after one is a marker of its own.
	[TestMethod]
	[DataRow(0x2028)]
	[DataRow(0x2029)]
	[DataRow(0x85)]
	public async Task AMarkerAfterAnotherLineBreakInADocumentationCommentIsSeparated(int codePoint)
	{
		string source = """
			internal sealed class Sample
			{
				/// <summary>{break}///Binds the options.</summary>
				internal int Bind() => 1;
			}
			""".Replace("{break}", (char)codePoint + "\t", StringComparison.Ordinal);

		string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>{break}/// Binds the options.</summary>
				internal int Bind() => 1;
			}
			""".Replace("{break}", (char)codePoint + "\t", StringComparison.Ordinal);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateSingleLineCommentMarkerFromTextAnalyzer>(
				source,
				MarkerRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
