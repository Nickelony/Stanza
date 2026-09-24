using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Documentation;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>Runs the documentation rules over entities, markup and non-ASCII text.</summary>
[TestClass]
public sealed class DocCommentContentTests
{
	[TestMethod]
	[CrossCutting]
	public async Task AnEntityIsNeverSplitWhenTheLayoutWraps()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the &lt;T&gt; options and the &amp; copies from the configuration section, validates them and lets the host fail at startup.</summary>
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
				/// Binds the &lt;T&gt; options and the &amp; copies from the configuration section,
				/// validates them and lets the host fail at startup.
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task NonAsciiLettersCountAsOneColumnEachWhenTheLayoutWraps()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Überprüft die Größe der Zuordnung, bindet die Optionen aus dem Abschnitt der Konfiguration und lässt den Host früh scheitern.</summary>
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
				/// Überprüft die Größe der Zuordnung, bindet die Optionen aus dem Abschnitt der Konfiguration
				/// und lässt den Host früh scheitern.
				/// </summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task AnEmojiCountsTwoColumnsAndACjkCharacterOne()
	{
		// A one-line summary is 27 columns of tab, markers and tags around its text, and the width is 100.
		(string Glyph, int Columns, int Over)[] cases =
		[
			("\U0001F600", 2, 0),
			("\U0001F600", 2, 1),
			("漢", 1, 0),
			("漢", 1, 1),
		];

		foreach ((string glyph, int columns, int over) in cases)
		{
			// The text fills the line to column 100 when the glyph counts as given, and one past it where Over is 1.
			int fill = 100 - 27 - 1 - columns + over;

			string words = string.Concat(
				Enumerable.Range(0, fill).Select(index => index % 10 == 4 ? ' ' : 'a')
			);

			string source = $$"""
				internal sealed class Sample
				{
					/// <summary>{{words}} {{glyph}}</summary>
					internal int Bind()
					{
						return 1;
					}
				}
				""";

			AnalyzerRunResult result =
				await RoslynTestHarness.ReportAsync<LayOutDocumentationCommentAnalyzer>(
					source,
					TestRuleSets.StandaloneRulesEnabled
				);

			Assert.HasCount(
				over,
				result.Diagnostics,
				$"U+{char.ConvertToUtf32(glyph, 0):X4} at {fill} filler columns."
			);
		}
	}

	[TestMethod]
	[CrossCutting]
	public async Task ACdataSectionIsLeftAsWritten()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options.</summary>
				/// <remarks>
				/// <![CDATA[ if (a < b && c > d) { Run(); } ]]>
				/// </remarks>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		RoslynTestHarness.AssertFixedText(source, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task AnXmlCommentInsideTheTextIsLeftAsWritten()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Binds the options <!-- not for the first release --> from the section.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		RoslynTestHarness.AssertFixedText(source, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task TextThatEndsInAnAnchorGetsNoPeriod()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Reads <a href="https://example.com/spec">the specification</a></summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<EndDocumentationTextWithPeriodAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	[CrossCutting]
	public async Task NonAsciiTextEndsWithAPeriod()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Öffnet das Café</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Öffnet das Café.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<EndDocumentationTextWithPeriodAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task AnEscapedNullIsMarkedLikeAnyNullThatPunctuationTouches()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>Returns &lt;null&gt; when the section is missing.</summary>
				internal int Bind()
				{
					return 1;
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <summary>Returns &lt;<see langword="null" />&gt; when the section is missing.</summary>
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

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
