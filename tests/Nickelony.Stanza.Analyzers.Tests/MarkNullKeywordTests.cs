using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Documentation;
using Nickelony.Stanza.Tests.Shared;

using static Nickelony.Stanza.Analyzers.Tests.DocumentationFamilyFixtures;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class MarkNullKeywordTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> TightSlashRules { get; } =
		TestRuleSets.Only(
			DiagnosticDescriptors.MarkNullKeyword,
			DiagnosticDescriptors.KeepSlashOfSelfClosingTagTight
		);

	private static IReadOnlyDictionary<string, ReportDiagnostic> MarkupRuleOnly { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.MarkNullKeyword);

	private static IReadOnlyDictionary<string, ReportDiagnostic> BothSlashRules { get; } =
		TestRuleSets.Only(
			DiagnosticDescriptors.MarkNullKeyword,
			DiagnosticDescriptors.SeparateSlashOfSelfClosingTag,
			DiagnosticDescriptors.KeepSlashOfSelfClosingTagTight
		);

	[TestMethod]
	public async Task AKeywordInAHyphenatedWordIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Returns a null-terminated value that is non-null.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkNullKeywordAnalyzer>(
			source,
			MarkupRuleOnly
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	[DataRow(
		"See https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null for it."
	)]
	[DataRow("Reads the file at data/null/settings.json first.")]
	[DataRow("Returns `null` when the section is missing.")]
	[DataRow("Returns `a &lt; null` when the section is missing.")]
	[DataRow("See https://example.com/?a=1&amp;b=null for it.")]
	[DataRow("Returns the string \"null\" when the section is missing.")]
	public async Task AKeywordInALinkAPathACodeSpanOrQuotesIsLeftAlone(string text)
	{
		string source = $$"""
			internal sealed class Sample
			{
				/// <summary>{{text}}</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkNullKeywordAnalyzer>(
			source,
			MarkupRuleOnly
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ANullKeywordInTheProseBecomesAnElement()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Returns null when the section is missing.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Returns <see langword="null" /> when the section is missing.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkNullKeywordAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The keyword starts its text right after the marker, so the replacement starts after the marker too.
	[TestMethod]
	public async Task AKeywordRightAfterTheLineMarkerKeepsTheMarker()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				///null when the section is missing.
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkNullKeywordAnalyzer>(
			source,
			MarkupRuleOnly
		);

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>
				///<see langword="null"/> when the section is missing.
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AKeywordInACodeSpanAcrossALineIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Returns `value ??
				/// null` when the section is missing.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkNullKeywordAnalyzer>(
			source,
			MarkupRuleOnly
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	// An element written in a namespace belongs to the prefix of the element that holds it.
	[TestMethod]
	public async Task AKeywordInAPrefixedElementTakesThePrefixOfThatElement()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <x:summary xmlns:x="urn:stanza">Returns null when the section is missing.</x:summary>
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <x:summary xmlns:x="urn:stanza">Returns <x:see langword="null"/> when the section is missing.</x:summary>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkNullKeywordAnalyzer>(
			source,
			MarkupRuleOnly
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ANullKeywordInANestedElementBecomesAnElement()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options. <note>Returns null when they are missing.</note></summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Binds the options. <note>Returns <see langword="null" /> when they are missing.</note></summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkNullKeywordAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentWhoseElementPreservesItsSpaceKeepsTheProseAsItIs()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options.</summary>
				/// <remarks xml:space="preserve">Returns null as  written.</remarks>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkNullKeywordAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheKeywordTakesTheShapeOfTheTagGapTheFileOwns()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Returns null when the section is missing.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Returns <see langword="null"/> when the section is missing.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkNullKeywordAnalyzer>(
			source,
			BothSlashRules
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task APragmaThatSilencesTheSpacedRuleTakesTheKeywordToTheTightShape()
	{
		string source = $$"""
			internal sealed class Sample
			{
			#pragma warning disable {{DiagnosticDescriptors.SeparateSlashOfSelfClosingTag.Id}}
				/// <summary>Returns null when the section is missing.</summary>
				internal int Bind() => 1;
			#pragma warning restore {{DiagnosticDescriptors.SeparateSlashOfSelfClosingTag.Id}}

				/// <summary>Returns null when the section is missing.</summary>
				internal int Other() => 1;
			}
			""";

		string fixedSource = $$"""
			internal sealed class Sample
			{
			#pragma warning disable {{DiagnosticDescriptors.SeparateSlashOfSelfClosingTag.Id}}
				/// <summary>Returns <see langword="null"/> when the section is missing.</summary>
				internal int Bind() => 1;
			#pragma warning restore {{DiagnosticDescriptors.SeparateSlashOfSelfClosingTag.Id}}

				/// <summary>Returns <see langword="null" /> when the section is missing.</summary>
				internal int Other() => 1;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkNullKeywordAnalyzer>(
			source,
			SpacedSlashRules
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ThePunctuationAroundTheKeywordStaysWhereItIs()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>The bound options, or null, when the section is missing.</summary>
				/// <returns>The options, or (null) when they could not be bound.</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>The bound options, or <see langword="null" />, when the section is missing.</summary>
				/// <returns>The options, or (<see langword="null" />) when they could not be bound.</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkNullKeywordAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AWordThatMentionsTheKeywordIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Nullifies the value, and the nullability of the field is announced.</summary>
				/// <returns>The value, or SomeNullValue when the field holds one.</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkNullKeywordAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AKeywordThatIsAlreadyMarkedUpIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Returns <see langword="null"/> when the section is missing.</summary>
				/// <returns>The value, or <c>null</c> when it could not be bound.</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkNullKeywordAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnInlineElementAtTheTopLevelOfACommentIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <c>null</c> means unset.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkNullKeywordAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheProseOfAParagraphIsMarkedUpToo()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <remarks>
				/// <para>Returns null when the section is missing.</para>
				/// <para>Otherwise the options are bound.</para>
				/// </remarks>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <remarks>
				/// <para>Returns <see langword="null" /> when the section is missing.</para>
				/// <para>Otherwise the options are bound.</para>
				/// </remarks>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkNullKeywordAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheKeywordOfAPlainCommentIsMarkedUpToo()
	{
		const string source = """
			internal sealed class Sample
			{
				/// Returns null when the section is missing.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// Returns <see langword="null" /> when the section is missing.
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkNullKeywordAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentThatHoldsACodeBlockIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Returns null when the section is missing.</summary>
				/// <code>
				/// if (value == null)
				/// {
				///     return null;
				/// }
				/// </code>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkNullKeywordAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task EveryKeywordOfACommentGoesInOneFixAllPass()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Returns null when the section is missing.</summary>
				/// <returns>The options, or null when they could not be bound.</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Returns <see langword="null"/> when the section is missing.</summary>
				/// <returns>The options, or <see langword="null"/> when they could not be bound.</returns>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunFixAllAsync(source, MarkupRuleOnly);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentThatSharesItsLineWithCodeIsMarkedUp()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					return 1;
				} /// <summary>Returns null when the section is missing.</summary>
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					return 1;
				} /// <summary>Returns <see langword="null"/> when the section is missing.</summary>
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkNullKeywordAnalyzer>(
			source,
			MarkupRuleOnly
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	public static IEnumerable<object[]> SlashRules { get; } =
	[
		["the spaced rule on", SpacedSlashRules, "<see langword=\"null\" />"],
		["the tight rule on", TightSlashRules, "<see langword=\"null\"/>"],
	];

	[TestMethod]
	[DynamicData(
		nameof(SlashRules),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task TheWrittenElementTakesTheShapeTheSlashRuleAsksFor(
		string name,
		IReadOnlyDictionary<string, ReportDiagnostic> rules,
		string element
	)
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Returns null when the section is missing.</summary>
				internal int Bind()
				{
					int value = 1;

					value += 1;

					return value;
				}
			}
			""";

		string fixedSource = $$"""
			internal sealed class Sample
			{
				/// <summary>Returns {{element}} when the section is missing.</summary>
				internal int Bind()
				{
					int value = 1;

					value += 1;

					return value;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source, rules);

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.MarkNullKeyword.Id, result.Diagnostics[0].Id);

		RoslynTestHarness.AssertFixedText(fixedSource, result, name);
	}
}
