using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Documentation;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class WriteEmptyElementAsSelfClosingTagTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> EmptyElementRuleOnly { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.WriteEmptyElementAsSelfClosingTag);

	private static IReadOnlyDictionary<string, ReportDiagnostic> SpacedTagFile { get; } =
		TestRuleSets.Only(
			DiagnosticDescriptors.WriteEmptyElementAsSelfClosingTag,
			DiagnosticDescriptors.SeparateSlashOfSelfClosingTag
		);

	public static IEnumerable<object[]> SlashShapes { get; } =
	[
		["an empty element in the tight shape", "<c></c>", "<c/>", EmptyElementRuleOnly],
		["an empty element in the spaced shape", "<c></c>", "<c />", SpacedTagFile],
		["a space before the bracket in the tight shape", "<c ></c>", "<c/>", EmptyElementRuleOnly],
		["a space before the bracket in the spaced shape", "<c ></c>", "<c />", SpacedTagFile],
	];

	[TestMethod]
	[DynamicData(
		nameof(SlashShapes),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AnEmptyElementIsWrittenAsOneTagInTheShapeTheFileNames(
		string name,
		string element,
		string tag,
		IReadOnlyDictionary<string, ReportDiagnostic> options
	)
	{
		string source = $$"""
			internal sealed class Sample
			{
				/// <summary>Binds the {{element}} options.</summary>
				internal int Bind() => 1;
			}
			""";

		string fixedSource = $$"""
			internal sealed class Sample
			{
				/// <summary>Binds the {{tag}} options.</summary>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<WriteEmptyElementAsSelfClosingTagAnalyzer>(
				source,
				options
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result, name);
	}

	[TestMethod]
	public async Task AnEmptyElementInsideAListIsLeftAsItIs()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <list type="bullet">
				/// <item><description>A <c></c> value.</description></item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<WriteEmptyElementAsSelfClosingTagAnalyzer>(
				source,
				EmptyElementRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task EveryEmptyElementIsWrittenAsOneTag()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				/// Reads <see cref="Item"></see> and a break <br></br> and a <c></c>.
				/// </summary>
				/// <remarks></remarks>
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>
				/// Reads <see cref="Item"/> and a break <br/> and a <c/>.
				/// </summary>
				/// <remarks/>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<WriteEmptyElementAsSelfClosingTagAnalyzer>(
				source,
				EmptyElementRuleOnly
			);

		Assert.HasCount(4, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnElementThatHoldsWhitespaceIsWrittenAsOneTag()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the <c> </c> options.</summary>
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Binds the <c/> options.</summary>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<WriteEmptyElementAsSelfClosingTagAnalyzer>(
				source,
				EmptyElementRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The space of an element that preserves it, and of a code sample, is content, so the element keeps its pair.
	[TestMethod]
	[DataRow("<c xml:space=\"preserve\">   </c>")]
	[DataRow("<code>   </code>")]
	public async Task AnElementThatKeepsItsWhitespaceIsLeftAsItIs(string element)
	{
		string source = $$"""
			internal sealed class Sample
			{
				/// <summary>Binds the {{element}} options.</summary>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<WriteEmptyElementAsSelfClosingTagAnalyzer>(
				source,
				EmptyElementRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnElementThatHoldsANonBreakingSpaceIsLeftAlone()
	{
		const char space = (char)0xA0;

		string source =
			"internal sealed class Sample\n{\n\t/// <summary>Binds the <c>"
			+ space
			+ "</c> options.</summary>\n\tinternal int Bind() => 1;\n}\n";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<WriteEmptyElementAsSelfClosingTagAnalyzer>(
				source,
				EmptyElementRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	// A character reference is content, though it reads as a space, so the element is not empty.
	[TestMethod]
	public async Task AnElementThatHoldsOnlyACharacterReferenceIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the <c>&#32;</c> options.</summary>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<WriteEmptyElementAsSelfClosingTagAnalyzer>(
				source,
				EmptyElementRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	// The comment holds a CDATA section, so the rule leaves every element of it as written, an empty one too.
	[TestMethod]
	public async Task AnElementThatHoldsOnlyACdataSectionLeavesTheWholeCommentAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the <c><![CDATA[ ]]></c> and the <b></b> options.</summary>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<WriteEmptyElementAsSelfClosingTagAnalyzer>(
				source,
				EmptyElementRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnElementOverTwoLinesIsJoined()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				/// Binds the options of the <c>
				/// </c> section.
				/// </summary>
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>
				/// Binds the options of the <c/> section.
				/// </summary>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<WriteEmptyElementAsSelfClosingTagAnalyzer>(
				source,
				EmptyElementRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnEmptyElementWithAWrappedStartTagIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the <c
				///     name="value"></c> options.</summary>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<WriteEmptyElementAsSelfClosingTagAnalyzer>(
				source,
				EmptyElementRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics, "A wrapped start tag is line structure.");
	}

	[TestMethod]
	public async Task ABulkSeverityLeavesTheSlashToTheRulesOwnKey()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the <c></c> options.</summary>
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Binds the <c/> options.</summary>
				internal int Bind() => 1;
			}
			""";

		var file = new Dictionary<string, string>
		{
			["dotnet_analyzer_diagnostic.severity"] = "warning",
		};

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<WriteEmptyElementAsSelfClosingTagAnalyzer>(
				source,
				analyzerConfigOptions: file,
				diagnosticOptions: EmptyElementRuleOnly,

				treeOptions: new Dictionary<string, ReportDiagnostic>
				{
					[DiagnosticDescriptors.KeepSlashOfSelfClosingTagTight.Id] =
						ReportDiagnostic.Warn,
				}
			);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(
			fixedSource,
			result,
			"The bulk key decided the slash shape."
		);
	}

	[TestMethod]
	public async Task AnElementThatHoldsSomethingIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the <c>null</c> options and the <c/> form.</summary>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<WriteEmptyElementAsSelfClosingTagAnalyzer>(
				source,
				EmptyElementRuleOnly
			);

		Assert.IsEmpty(
			result.Diagnostics,
			"An element that holds something is written as one tag."
		);
	}

	[TestMethod]
	public async Task AnElementThatHoldsAChildIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the <c><see cref="Item"/></c> options.</summary>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<WriteEmptyElementAsSelfClosingTagAnalyzer>(
				source,
				EmptyElementRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics, "An element that holds a child is written as one tag.");
	}

	[TestMethod]
	public async Task AnEmptyElementOfABrokenCommentIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>
				/// Binds the <c></c> options of a break <br> here.
				/// </summary>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<WriteEmptyElementAsSelfClosingTagAnalyzer>(
				source,
				EmptyElementRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics, "A comment the parser had to repair is written.");
	}

	[TestMethod]
	public async Task ACommentThatSharesItsLineWithCodeIsWrittenAsOneTag()
	{
		const string source = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					return 1;
				} /// <summary>Binds the <c></c> options.</summary>
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				internal int Bind()
				{
					return 1;
				} /// <summary>Binds the <c/> options.</summary>
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<WriteEmptyElementAsSelfClosingTagAnalyzer>(
				source,
				EmptyElementRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task APragmaThatSilencesTheSpacedRuleTakesTheEmptyElementToTheTightShape()
	{
		string source = $$"""
			internal sealed class Sample
			{
			#pragma warning disable {{DiagnosticDescriptors.SeparateSlashOfSelfClosingTag.Id}}
				/// <summary>Binds the <c></c> options.</summary>
				internal int Bind() => 1;
			#pragma warning restore {{DiagnosticDescriptors.SeparateSlashOfSelfClosingTag.Id}}

				/// <summary>Binds the <c></c> options.</summary>
				internal int Other() => 1;
			}
			""";

		string fixedSource = $$"""
			internal sealed class Sample
			{
			#pragma warning disable {{DiagnosticDescriptors.SeparateSlashOfSelfClosingTag.Id}}
				/// <summary>Binds the <c/> options.</summary>
				internal int Bind() => 1;
			#pragma warning restore {{DiagnosticDescriptors.SeparateSlashOfSelfClosingTag.Id}}

				/// <summary>Binds the <c /> options.</summary>
				internal int Other() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<WriteEmptyElementAsSelfClosingTagAnalyzer>(
				source,
				SpacedTagFile
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
