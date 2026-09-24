using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class SeparateClauseKeywordFromBraceBeforeItTests
{
	[TestMethod]
	public async Task AClauseKeywordCanBeSeparatedFromItsBranch()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					if (value > 0)
					{
						DoThing();
					}
					else
					{
						DoThing();
					}
				}

				void N()
				{
					try
					{
						DoThing();
					}
					catch (Exception)
					{
						DoThing();
					}
					finally
					{
						DoThing();
					}
				}

				void P()
				{
					do
					{
						DoThing();
					}
					while (true);
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int value)
				{
					if (value > 0)
					{
						DoThing();
					}

					else
					{
						DoThing();
					}
				}

				void N()
				{
					try
					{
						DoThing();
					}

					catch (Exception)
					{
						DoThing();
					}

					finally
					{
						DoThing();
					}
				}

				void P()
				{
					do
					{
						DoThing();
					}

					while (true);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateClauseKeywordFromBraceBeforeItAnalyzer>(
				source,
				TestRuleSets.GapRulesEnabled
			);

		Assert.HasCount(4, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheSeparatedClauseKeepsACommentWithTheKeyword()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					if (value > 0)
					{
						DoThing();
					}
					// why
					else
					{
						DoThing();
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int value)
				{
					if (value > 0)
					{
						DoThing();
					}

					// why
					else
					{
						DoThing();
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateClauseKeywordFromBraceBeforeItAnalyzer>(
				source,
				TestRuleSets.GapRulesEnabled
			);

		// The comment travels with the keyword,
		// so the blank line lands before the annotation rather than between it and the keyword it explains.
		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ClauseSeparationLeavesBraceLessBodiesAndInlineClausesAlone()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					if (value > 0)
						DoThing();
					else
						DoThing();
				}

				void N(int value)
				{
					if (value > 0) { DoThing(); } else { DoThing(); }
				}

				void P(int value)
				{
					if (value > 0)
					{
						DoThing();
					}

					else
					{
						DoThing();
					}
				}
			}
			""";

		// A branch without braces has no brace to measure,
		// and a clause that shares its line with the brace belongs to the new line options of the formatter.
		// A blank line above an `else` on its own line is not reported by this rule.
		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateClauseKeywordFromBraceBeforeItAnalyzer>(
				source,
				TestRuleSets.GapRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AClauseKeywordAfterATrailingCommentGetsNoBlankLineInsideIt()
	{
		const string source = """
			class C
			{
				void M(int x)
				{
					if (x > 0)
					{
						Helpers.DoThing();
					} /* a
					b */ else
					{
						Helpers.DoThing();
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateClauseKeywordFromBraceBeforeItAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.SeparateClauseKeywordFromBraceBeforeIt)
			);

		Assert.IsEmpty(result.Diagnostics);
	}
}
