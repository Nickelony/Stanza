using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.MarkupLiterals;

namespace Nickelony.Stanza.Analyzers.Tests.MarkupLiterals;

/// <summary>Pins the literals <c>stanza_markup_literals</c> picks, through the cap.</summary>
[TestClass]
public sealed class MarkupLiteralMarkingTests
{
	private const string Key = "stanza_markup_literals";

	private static readonly IReadOnlyDictionary<string, ReportDiagnostic> s_cap = TestRuleSets.Only(
		DiagnosticDescriptors.CollapseExtraBlankLinesBetweenMarkupElements
	);

	[TestMethod]
	[CrossCutting]
	public async Task TheDefaultLeavesAnUnmarkedLiteralAlone()
	{
		const string source = """"
			class C
			{
				const string Markup = """
					<root>
					  <a>1</a>



					  <b>2</b>
					</root>
					""";
			}
			"""";

		using IDisposable scope = RoslynTestHarness.AtMarkupLiteralDefault();

		Assert.IsEmpty((await ReportCapAsync(source)).Diagnostics);
	}

	[TestMethod]
	[CrossCutting]
	public async Task TheDefaultReadsALiteralALanguageCommentMarks()
	{
		const string source = """"
			class C
			{
				// language=xml
				const string Markup = """
					<root>
					  <a>1</a>



					  <b>2</b>
					</root>
					""";
			}
			"""";

		using IDisposable scope = RoslynTestHarness.AtMarkupLiteralDefault();

		Assert.HasCount(1, (await ReportCapAsync(source)).Diagnostics);
	}

	[TestMethod]
	[CrossCutting]
	public async Task EachFormOfTheLanguageCommentMarksTheLiteral()
	{
		string[] comments =
		[
			"// language=xml",
			"// lang=xml",
			"//LANGUAGE = Xml",
			"/* language=xml */",
			"// language=xml prefix=<root>",
		];

		foreach (string comment in comments)
		{
			string source = $$""""
				class C
				{
					{{comment}}
					const string Markup = """
						<root>
						  <a>1</a>



						  <b>2</b>
						</root>
						""";
				}
				"""";

			Assert.HasCount(1, (await ReportCapAsync(source, "marked")).Diagnostics, comment);
		}
	}

	[TestMethod]
	[CrossCutting]
	public async Task ACommentThatNamesAnotherLanguageOrNoneLeavesTheLiteralAlone()
	{
		string[] comments =
		[
			"// language=json",
			"// language=xmlish",
			"// languages=xml",
			"// xml",
		];

		foreach (string comment in comments)
		{
			string source = $$""""
				class C
				{
					{{comment}}
					const string Markup = """
						<root>
						  <a>1</a>



						  <b>2</b>
						</root>
						""";
				}
				"""";

			Assert.IsEmpty((await ReportCapAsync(source, "marked")).Diagnostics, comment);
		}
	}

	[TestMethod]
	[CrossCutting]
	public async Task ACommentOnTheStatementOrRightBeforeTheLiteralMarksIt()
	{
		const string onTheStatement = """"
			class C
			{
				void M()
				{
					// language=xml
					Load("""
						<root>
						  <a>1</a>



						  <b>2</b>
						</root>
						""");
				}

				static void Load(string markup)
				{
					return;
				}
			}
			"""";

		const string beforeTheLiteral = """"
			class C
			{
				void M()
				{
					Load(
						/* language=xml */ """
						<root>
						  <a>1</a>



						  <b>2</b>
						</root>
						""");
				}

				static void Load(string markup)
				{
					return;
				}
			}
			"""";

		const string atTheEndOfTheLineBefore = """"
			class C
			{
				void M()
				{
					var markup = // language=xml
						"""
						<root>
						  <a>1</a>



						  <b>2</b>
						</root>
						""";
				}
			}
			"""";

		Assert.HasCount(1, (await ReportCapAsync(onTheStatement, "marked")).Diagnostics);
		Assert.HasCount(1, (await ReportCapAsync(beforeTheLiteral, "marked")).Diagnostics);
		Assert.HasCount(1, (await ReportCapAsync(atTheEndOfTheLineBefore, "marked")).Diagnostics);
	}

	[TestMethod]
	[CrossCutting]
	public async Task ACommentAboveTheMethodOrAnotherStatementLeavesTheLiteralAlone()
	{
		const string aboveTheMethod = """"
			class C
			{
				// language=xml
				void M()
				{
					var markup = """
						<root>
						  <a>1</a>



						  <b>2</b>
						</root>
						""";
				}
			}
			"""";

		const string aboveAnotherStatement = """"
			class C
			{
				void M()
				{
					// language=xml
					var first = "<root />";

					var markup = """
						<root>
						  <a>1</a>



						  <b>2</b>
						</root>
						""";
				}
			}
			"""";

		Assert.IsEmpty((await ReportCapAsync(aboveTheMethod, "marked")).Diagnostics);
		Assert.IsEmpty((await ReportCapAsync(aboveAnotherStatement, "marked")).Diagnostics);
	}

	[TestMethod]
	[CrossCutting]
	public async Task StringSyntaxXmlOnAFieldOrAPropertyMarksItsInitializerAndItsAssignments()
	{
		const string source = """"
			using System.Diagnostics.CodeAnalysis;

			class C
			{
				[StringSyntax(StringSyntaxAttribute.Xml)]
				static readonly string Field = """
					<root>
					  <a>1</a>



					  <b>2</b>
					</root>
					""";

				[StringSyntax("xml")]
				string Property { get; set; } = """
					<root>
					  <a>1</a>



					  <b>2</b>
					</root>
					""";

				void M()
				{
					Property = """
						<root>
						  <a>1</a>



						  <b>2</b>
						</root>
						""";
				}
			}
			"""";

		Assert.HasCount(3, (await ReportCapAsync(source, "marked")).Diagnostics);
	}

	[TestMethod]
	[CrossCutting]
	public async Task StringSyntaxXmlOnAParameterMarksItsPositionalNamedAndParamsArguments()
	{
		const string source = """"
			using System.Diagnostics.CodeAnalysis;

			class C
			{
				void M()
				{
					Load(1, """
						<root>
						  <a>1</a>



						  <b>2</b>
						</root>
						""");

					Load(markup: """
						<root>
						  <a>1</a>



						  <b>2</b>
						</root>
						""", count: 1);

					LoadAll("<root />", """
						<root>
						  <a>1</a>



						  <b>2</b>
						</root>
						""");
				}

				static void Load(int count, [StringSyntax("Xml")] string markup)
				{
					return;
				}

				static void LoadAll([StringSyntax("Xml")] params string[] documents)
				{
					return;
				}
			}
			"""";

		Assert.HasCount(3, (await ReportCapAsync(source, "marked")).Diagnostics);
	}

	[TestMethod]
	[CrossCutting]
	public async Task StringSyntaxXmlOnAParamsCollectionMarksItsArguments()
	{
		const string source = """"
			using System.Collections.Generic;
			using System.Diagnostics.CodeAnalysis;

			class C
			{
				void M()
				{
					LoadAll("<a />", """
						<root>
						  <a>1</a>



						  <b>2</b>
						</root>
						""");
				}

				static void LoadAll([StringSyntax("Xml")] params IEnumerable<string> documents)
				{
					return;
				}
			}
			"""";

		// A params collection is a preview feature of C# 13.
		using IDisposable preview = RoslynTestHarness.AtLanguageVersion(LanguageVersion.Preview);

		Assert.HasCount(1, (await ReportCapAsync(source, "marked")).Diagnostics);
	}

	[TestMethod]
	[CrossCutting]
	public async Task StringSyntaxXmlOnARecordParameterOrItsPropertyMarksBoth()
	{
		const string source = """"
			using System.Diagnostics.CodeAnalysis;

			record Document([StringSyntax(StringSyntaxAttribute.Xml)] string Markup);

			record Page([property: StringSyntax(StringSyntaxAttribute.Xml)] string Markup);

			class C
			{
				Document M()
				{
					var document = new Document("""
						<root>
						  <a>1</a>



						  <b>2</b>
						</root>
						""");

					return document with
					{
						Markup = """
							<root>
							  <a>1</a>



							  <b>2</b>
							</root>
							""",
					};
				}

				Page N()
				{
					return new Page("""
						<root>
						  <a>1</a>



						  <b>2</b>
						</root>
						""");
				}
			}
			"""";

		Assert.HasCount(3, (await ReportCapAsync(source, "marked")).Diagnostics);
	}

	[TestMethod]
	[CrossCutting]
	public async Task StringSyntaxThatNamesAnotherSyntaxLeavesTheLiteralAlone()
	{
		const string source = """"
			using System.Diagnostics.CodeAnalysis;

			class C
			{
				[StringSyntax(StringSyntaxAttribute.Json)]
				const string Markup = """
					<root>
					  <a>1</a>



					  <b>2</b>
					</root>
					""";
			}
			"""";

		Assert.IsEmpty((await ReportCapAsync(source, "marked")).Diagnostics);
	}

	[TestMethod]
	[CrossCutting]
	public async Task OffReadsNoLiteralEvenAMarkedOne()
	{
		const string source = """"
			class C
			{
				// language=xml
				const string Markup = """
					<root>
					  <a>1</a>



					  <b>2</b>
					</root>
					""";
			}
			"""";

		Assert.IsEmpty((await ReportCapAsync(source, "off")).Diagnostics);
	}

	[TestMethod]
	[CrossCutting]
	public async Task AllReadsAnUnmarkedLiteral()
	{
		const string source = """"
			class C
			{
				const string Markup = """
					<root>
					  <a>1</a>



					  <b>2</b>
					</root>
					""";
			}
			"""";

		Assert.HasCount(1, (await ReportCapAsync(source, "ALL")).Diagnostics);
	}

	[TestMethod]
	[CrossCutting]
	public async Task AValueTheKeyDoesNotNameReadsAsMarked()
	{
		const string source = """"
			class C
			{
				// language=xml
				const string Marked = """
					<root>
					  <a>1</a>



					  <b>2</b>
					</root>
					""";

				const string Plain = """
					<root>
					  <a>1</a>



					  <b>2</b>
					</root>
					""";
			}
			"""";

		AnalyzerRunResult result = await ReportCapAsync(source, "everything");

		Assert.HasCount(1, result.Diagnostics);

		Assert.IsLessThan(
			source.IndexOf("Plain", StringComparison.Ordinal),
			result.Diagnostics[0].Location.SourceSpan.Start
		);
	}

	[TestMethod]
	[CrossCutting]
	public async Task TheFixChangesTheMarkedLiteralAndLeavesTheUnmarkedOneAsWritten()
	{
		const string source = """"
			class C
			{
				// language=xml
				const string Marked = """
					<root>
					  <a>1</a>



					  <b>2</b>
					</root>
					""";

				const string Plain = """
					<root>
					  <a>1</a>



					  <b>2</b>
					</root>
					""";
			}
			"""";

		const string fixedSource = """"
			class C
			{
				// language=xml
				const string Marked = """
					<root>
					  <a>1</a>

					  <b>2</b>
					</root>
					""";

				const string Plain = """
					<root>
					  <a>1</a>



					  <b>2</b>
					</root>
					""";
			}
			"""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<CollapseExtraBlankLinesBetweenMarkupElementsAnalyzer>(
				source,
				s_cap,
				analyzerConfigOptions: new Dictionary<string, string> { [Key] = "marked" }
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	private static Task<AnalyzerRunResult> ReportCapAsync(string source, string? literals = null)
	{
		Dictionary<string, string>? keys = literals is null
			? null
			: new Dictionary<string, string> { [Key] = literals };

		return RoslynTestHarness.ReportAsync<CollapseExtraBlankLinesBetweenMarkupElementsAnalyzer>(
			source,
			s_cap,
			analyzerConfigOptions: keys
		);
	}
}
