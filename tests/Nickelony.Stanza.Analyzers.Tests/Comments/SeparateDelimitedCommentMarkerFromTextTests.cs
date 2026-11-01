using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Comments;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Comments;

[TestClass]
public sealed class SeparateDelimitedCommentMarkerFromTextTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> DelimitedRuleOnly { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.SeparateDelimitedCommentMarkerFromText);

	// An unterminated comment runs to the end of the file, so it has no closing marker to set its text apart from.
	[TestMethod]
	public async Task AnUnterminatedCommentIsLeftAlone()
	{
		const string source = "internal sealed class Sample { }\n/*Binds the options";

		ImmutableArray<Diagnostic> diagnostics = await RoslynTestHarness.RunWithoutCompilingAsync(
			source,
			DelimitedRuleOnly
		);

		Assert.IsEmpty(
			diagnostics.Where(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.SeparateDelimitedCommentMarkerFromText.Id
			)
		);
	}

	public static IEnumerable<object[]> TouchingMarkers { get; } =
	[
		["a sentence", "/*Binds the options of an item*/", "/* Binds the options of an item */"],
		["a run of two marks", "/*** Options ***/", "/* ** Options ** */"],
		["a run that touches a word", "/*--Options--*/", "/* --Options-- */"],
	];

	[TestMethod]
	[DynamicData(
		nameof(TouchingMarkers),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AMarkerThatTouchesItsTextIsSeparated(
		string name,
		string comment,
		string written
	)
	{
		string source = $$"""
			internal sealed class Sample
			{
				internal int Bind()
				{
					{{comment}}
					return 1;
				}
			}
			""";

		string fixedSource = $$"""
			internal sealed class Sample
			{
				internal int Bind()
				{
					{{written}}
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDelimitedCommentMarkerFromTextAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result, name);
	}

	[TestMethod]
	public async Task OnlyTheSideThatMissesTheSpaceIsWritten()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					/* Keeps the options of an item*/
					/*Binds the options of an item */
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					/* Keeps the options of an item */
					/* Binds the options of an item */
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDelimitedCommentMarkerFromTextAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AMarkerWithWhitespaceAfterItIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					/*  Keeps the options of an item  */
					/*	Reads the options of an item	*/
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDelimitedCommentMarkerFromTextAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnEmptyCommentIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					/**/
					/* */
					/***/
					/*****/
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDelimitedCommentMarkerFromTextAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ABannerOrABoxIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					/***** Options *****/
					/*--------*/
					/*=== Options ===*/
					/*************
					 * Options
					 *************/
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDelimitedCommentMarkerFromTextAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task OnlyTheEndThatARunTouchesIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					/***** Options*/
					/*Options *****/
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					/***** Options */
					/* Options *****/
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDelimitedCommentMarkerFromTextAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("0", 4)]
	[DataRow("1", 0)]
	[DataRow("3", 2)]
	[DataRow("5", 4)]
	[DataRow("three", 2)]
	public async Task TheBannerKeyDecidesTheShortestRun(string value, int expected)
	{
		const string source = """
			internal sealed class Sample
			{
				/*-- Options --*/
				/*---- Options ----*/
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateDelimitedCommentMarkerFromTextAnalyzer>(
				source,
				DelimitedRuleOnly,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_comment_banner_min_run"] = value,
				}
			);

		Assert.HasCount(expected, result.Diagnostics);
	}

	[TestMethod]
	public async Task TheFixSeparatesABannerTheKeyTurnsOff()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					/***** Options *****/
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					/* **** Options **** */
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDelimitedCommentMarkerFromTextAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_comment_banner_min_run"] = "0",
				}
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AMultiLineCommentIsJudgedAtItsTwoEnds()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					/*
					 * Binds the options of an item.*/
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					/*
					 * Binds the options of an item. */
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDelimitedCommentMarkerFromTextAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheIndentationInsideACommentIsNeverTouched()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					/*
						Binds the options of an item.
					*/
					/*Binds the options of an item.
					*/
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					/*
						Binds the options of an item.
					*/
					/* Binds the options of an item.
					*/
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDelimitedCommentMarkerFromTextAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentThatSharesItsLineWithCodeIsJudged()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					int value = 1; /*the value the host reads*/
					return value;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					int value = 1; /* the value the host reads */
					return value;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDelimitedCommentMarkerFromTextAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// A build that writes no documentation file parses the block as a plain delimited comment.
	[TestMethod]
	[DataRow(DocumentationMode.Parse)]
	[DataRow(DocumentationMode.None)]
	public async Task ADocumentationBlockIsNotThisRulesBusiness(DocumentationMode mode)
	{
		using IDisposable parse = RoslynTestHarness.AtDocumentationMode(mode);

		const string source = """
			internal sealed class Sample
			{
				/**Binds the options of an item*/
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDelimitedCommentMarkerFromTextAnalyzer>(
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
				/*Binds the options of an item*/
				internal int Bind()
				{
					/*Keeps the value the host reads*/
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/* Binds the options of an item */
				internal int Bind()
				{
					/* Keeps the value the host reads */
					return 1;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunFixAllAsync(
			source,
			DelimitedRuleOnly
		);

		Assert.HasCount(4, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ADelimitedCommentFixWorksWithoutTheAdditionalLocation()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					/* Binds the options of an item*/
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					/* Binds the options of an item */
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<SeparateDelimitedCommentMarkerFromTextAnalyzer>(
				source,
				DelimitedRuleOnly
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
