using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class KeepSingleLineAccessorsTogetherTests
{
	[TestMethod]
	public async Task TheTightAccessorRuleLeavesTheOtherAccessorShapesAlone()
	{
		const string source = """
			class C
			{
				private int _value;

				public int Tight { get; set; }

				public int Multi
				{
					get
					{
						return _value;
					}

					set
					{
						_value = value;
					}
				}

				public int Commented
				{
					get { return _value; }

					// why

					set { _value = value; }
				}
			}
			""";

		// None of the three reports: the multi-line accessors are the multi-line rules' to judge,
		// the single-line ones have nothing to take away, and the comment's gap cannot be taken away.
		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepSingleLineAccessorsTogetherAnalyzer>(
				source,
				TestRuleSets.GapRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TwoCommentBlocksBetweenSingleLineAccessorsKeepTheGap()
	{
		const string source = """
			class C
			{
				private int _value;

				public int Value
				{
					get { return _value; }

					// first note

					// second note
					set { _value = value; }
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepSingleLineAccessorsTogetherAnalyzer>(
				source,
				TestRuleSets.GapRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheBlankLineBeforeAnInitAccessorIsTakenAway()
	{
		const string source = """
			class C
			{
				private int _value;

				public int Value
				{
					get { return _value; }

					init { _value = value; }
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _value;

				public int Value
				{
					get { return _value; }
					init { _value = value; }
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepSingleLineAccessorsTogetherAnalyzer>(
				source,
				TestRuleSets.GapRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ATrailingCommentStaysOnItsAccessorWhileTheGapGoes()
	{
		const string source = """
			class C
			{
				private int _value;

				public int Value
				{
					get { return _value; } // why

					set { _value = value; }
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _value;

				public int Value
				{
					get { return _value; } // why
					set { _value = value; }
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepSingleLineAccessorsTogetherAnalyzer>(
				source,
				TestRuleSets.GapRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
