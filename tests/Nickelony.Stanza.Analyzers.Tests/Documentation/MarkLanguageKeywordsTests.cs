using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Documentation;
using Nickelony.Stanza.Tests.Shared;

using static Nickelony.Stanza.Analyzers.Tests.Documentation.DocumentationFamilyFixtures;

namespace Nickelony.Stanza.Analyzers.Tests.Documentation;

[TestClass]
public sealed class MarkLanguageKeywordsTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> TightSlashRules { get; } =
		TestRuleSets.Only(
			DiagnosticDescriptors.MarkLanguageKeywords,
			DiagnosticDescriptors.KeepSlashOfSelfClosingTagTight
		);

	private static IReadOnlyDictionary<string, ReportDiagnostic> MarkupRuleOnly { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.MarkLanguageKeywords);

	private static IReadOnlyDictionary<string, ReportDiagnostic> BothSlashRules { get; } =
		TestRuleSets.Only(
			DiagnosticDescriptors.MarkLanguageKeywords,
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

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkLanguageKeywordsAnalyzer>(
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

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkLanguageKeywordsAnalyzer>(
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

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkLanguageKeywordsAnalyzer>(
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

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkLanguageKeywordsAnalyzer>(
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

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkLanguageKeywordsAnalyzer>(
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

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkLanguageKeywordsAnalyzer>(
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

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkLanguageKeywordsAnalyzer>(
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

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkLanguageKeywordsAnalyzer>(
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

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkLanguageKeywordsAnalyzer>(
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

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkLanguageKeywordsAnalyzer>(
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

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkLanguageKeywordsAnalyzer>(
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

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkLanguageKeywordsAnalyzer>(
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

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkLanguageKeywordsAnalyzer>(
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

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkLanguageKeywordsAnalyzer>(
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

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkLanguageKeywordsAnalyzer>(
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

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkLanguageKeywordsAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACodeBlockIsLeftAloneWhileTheRestOfTheCommentIsMarked()
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
				/// <remarks>Reads null from the host.</remarks>
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
				/// <code>
				/// if (value == null)
				/// {
				///     return null;
				/// }
				/// </code>
				/// <remarks>Reads <see langword="null"/> from the host.</remarks>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkLanguageKeywordsAnalyzer>(
			source,
			MarkupRuleOnly
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnElementThatHoldsACodeBlockOrAnInheritdocIsLeftAloneWhileItsSiblingsAreMarked()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Returns null when the section is missing.</summary>
				/// <remarks>Reads null from the host.
				/// <code>return null;</code>
				/// </remarks>
				/// <returns>The value, or null.<inheritdoc cref="Other.Bind(string)"/> More null.</returns>
				/// <exception cref="System.ArgumentException">Thrown for null.</exception>
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
				/// <remarks>Reads null from the host.
				/// <code>return null;</code>
				/// </remarks>
				/// <returns>The value, or null.<inheritdoc cref="Other.Bind(string)"/> More null.</returns>
				/// <exception cref="System.ArgumentException">Thrown for <see langword="null"/>.</exception>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkLanguageKeywordsAnalyzer>(
			source,
			MarkupRuleOnly
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACodeBlockAloneHasNoKeywordToMark()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <code>
				/// return null;
				/// </code>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkLanguageKeywordsAnalyzer>(
			source,
			MarkupRuleOnly
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

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkLanguageKeywordsAnalyzer>(
			source,
			MarkupRuleOnly
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	private const string AllKeywords = "null, true, false, default";

	// A phrase proves the keyword; the same word amid other words is the language's own English.
	public static IEnumerable<object?[]> KeywordPhrases { get; } =
	[
		[
			"a sentence that opens on true and if",
			"True if the section exists.",
			"<see langword=\"true\"/> if the section exists.",
		],

		[
			"an element that opens on false and when",
			"false when the section is missing.",
			"<see langword=\"false\"/> when the section is missing.",
		],

		[
			"a sentence that opens after a period",
			"Binds the value. True if it was bound.",
			"Binds the value. <see langword=\"true\"/> if it was bound.",
		],

		[
			"true after returns",
			"Returns true if bound.",
			"Returns <see langword=\"true\"/> if bound.",
		],

		[
			"false after returns in capitals",
			"Returns FALSE when missing.",
			"Returns <see langword=\"false\"/> when missing.",
		],

		[
			"a keyword that punctuation touches after returns",
			"Returns (true) if bound.",
			"Returns (<see langword=\"true\"/>) if bound.",
		],

		[
			"false after otherwise and a comma",
			"True if bound; otherwise, false.",
			"<see langword=\"true\"/> if bound; otherwise, <see langword=\"false\"/>.",
		],

		[
			"false after otherwise and a colon",
			"Returns true if bound; otherwise: false.",
			"Returns <see langword=\"true\"/> if bound; otherwise: <see langword=\"false\"/>.",
		],

		[
			"false after otherwise and a space",
			"Returns true if bound, otherwise false.",
			"Returns <see langword=\"true\"/> if bound, otherwise <see langword=\"false\"/>.",
		],

		[
			"false before otherwise",
			"True if bound, false otherwise.",
			"<see langword=\"true\"/> if bound, <see langword=\"false\"/> otherwise.",
		],

		[
			"true before otherwise and a comma",
			"Reads the flag, true, otherwise the default.",
			"Reads the flag, <see langword=\"true\"/>, otherwise the default.",
		],

		[
			"a null in the same list",
			"Returns true or null if bound.",
			"Returns <see langword=\"true\"/> or <see langword=\"null\"/> if bound.",
		],

		[
			"code elements of each listed keyword",
			"Is <c>true</c> or <c>false</c>, else <c>default</c>.",
			"Is <see langword=\"true\"/> or <see langword=\"false\"/>, else <see langword=\"default\"/>.",
		],

		["a code element with another case", "Is <c>True</c> here.", null],
		["a code element with padding", "Is <c> true </c> here.", null],
		["a code element of null", "Is <c>null</c> here.", null],
		["a code element of another word", "Is <c>trueish</c> here.", null],
		["a phrase that opens on this is", "This is true if bound.", null],
		["a true amid other words", "Holds a true value, not a false one.", null],
		["a true after a plain verb", "Is true when bound.", null],
		["a default amid words", "Returns the default value, or default when missing.", null],
		["a default after returns", "Returns default if missing.", null],
		["true in a code span", "Returns `true` if bound.", null],
		["true in a word", "Returns trueish if bound.", null],
		["true in quotes", "Returns \"true\" if bound.", null],
		["true in a path", "Returns data/true if bound.", null],
		["true in a hyphenated word", "Returns true-or-false if bound.", null],

		[
			"false closing a sentence after returns",
			"Returns false.",
			"Returns <see langword=\"false\"/>.",
		],

		["a noun after true after returns", "Returns true positives.", null],
		["a noun after false after returns", "Returns false alarms only.", null],
		["a noun after false after otherwise", "Otherwise false positives are dropped.", null],
		["a true after returns as a noun", "Tax returns, true to form.", null],
		["a default as a noun after otherwise", "Otherwise default values apply.", null],
	];

	[TestMethod]
	[DynamicData(
		nameof(KeywordPhrases),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AListedKeywordIsMarkedWhereItsPhraseProvesIt(
		string name,
		string text,
		string? expected
	)
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkLanguageKeywordsAnalyzer>(
			BuildSummary(text),
			MarkupRuleOnly,

			analyzerConfigOptions: new Dictionary<string, string>
			{
				["stanza_doc_langwords"] = AllKeywords,
			}
		);

		if (expected is null)
		{
			Assert.IsEmpty(result.Diagnostics, name);
			return;
		}

		Assert.IsNotEmpty(result.Diagnostics, name);
		RoslynTestHarness.AssertFixedText(BuildSummary(expected), result, name);
	}

	private static string BuildSummary(string body)
	{
		return $$"""
			internal sealed class Sample
			{
				/// <summary>{{body}}</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";
	}

	[TestMethod]
	[DataRow(null, "Returns true or <see langword=\"null\"/> if bound.")]
	[DataRow("null", "Returns true or <see langword=\"null\"/> if bound.")]
	[DataRow("true, false", "Returns <see langword=\"true\"/> or null if bound.")]
	[DataRow("nothing, maybe", "Returns true or <see langword=\"null\"/> if bound.")]
	[DataRow("null,true", "Returns <see langword=\"true\"/> or <see langword=\"null\"/> if bound.")]
	public async Task TheKeywordKeyListsTheWordsTheRuleMarks(string? value, string expected)
	{
		Dictionary<string, string> config = value is null
			? []
			: new() { ["stanza_doc_langwords"] = value };

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<MarkLanguageKeywordsAnalyzer>(
			BuildSummary("Returns true or null if bound."),
			MarkupRuleOnly,
			analyzerConfigOptions: config
		);

		RoslynTestHarness.AssertFixedText(BuildSummary(expected), result);
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
		Assert.AreEqual(DiagnosticDescriptors.MarkLanguageKeywords.Id, result.Diagnostics[0].Id);

		RoslynTestHarness.AssertFixedText(fixedSource, result, name);
	}
}
