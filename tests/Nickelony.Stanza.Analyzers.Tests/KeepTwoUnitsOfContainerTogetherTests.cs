using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class KeepTwoUnitsOfContainerTogetherTests
{
	[TestMethod]
	public async Task TwoUnitContainerLosesItsBlankLine()
	{
		const string source = """
			class C
			{
				int M(int value)
				{
					int result = value + 1;

					return result;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				int M(int value)
				{
					int result = value + 1;
					return result;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepTwoUnitsOfContainerTogetherAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ContainersOfThreeUnitsOrWithAMultiLineNeighborKeepTheirBlankLine()
	{
		const string source = """
			class C
			{
				void M()
				{
					DoThing();
					DoOther();

					return;
				}

				void N(int value)
				{
					Build(
						value);

					return;
				}

				void DoThing()
				{
				}

				void DoOther()
				{
				}

				int Build(int value)
				{
					return value;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepTwoUnitsOfContainerTogetherAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACommentOrADirectiveInTheGapKeepsTheTwoUnitsApart()
	{
		const string source = """
			class C
			{
				int M(int value)
				{
					int result = value + 1;

					// the caller needs it back
					return result;
				}

				void N()
				{
					DoThing();

			#if DEBUG
					DoOther();
			#endif
				}

				void DoThing()
				{
				}

				void DoOther()
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepTwoUnitsOfContainerTogetherAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ASwitchSectionWithTwoStatementsIsJoined()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1:
							DoThing();

							break;
					}
				}

				void DoThing()
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1:
							DoThing();
							break;
					}
				}

				void DoThing()
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepTwoUnitsOfContainerTogetherAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ALocalFunctionAndAnImportDirectiveAreNotUnits()
	{
		const string source = """
			using System;

			class C
			{
				void M()
				{
					int Helper() => 1;

					DoThing(Helper());
				}

				void DoThing(int value)
				{
				}

				void DoOther()
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepTwoUnitsOfContainerTogetherAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task MemberContainersAndTopLevelStatementsAreJoinedToo()
	{
		const string source = """
			interface IContract
			{
				void Start();

				void Stop();
			}
			""";

		const string fixedSource = """
			interface IContract
			{
				void Start();
				void Stop();
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepTwoUnitsOfContainerTogetherAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);

		const string topLevel = """
			int value = 1;

			value = value + 1;
			""";

		const string fixedTopLevel = """
			int value = 1;
			value = value + 1;
			""";

		AnalyzerRunResult topLevelResult =
			await RoslynTestHarness.RunAsync<KeepTwoUnitsOfContainerTogetherAnalyzer>(
				topLevel,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, topLevelResult.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedTopLevel, topLevelResult);
	}

	[TestMethod]
	public async Task AnEnumOfTwoMembersIsJoinedAcrossItsComma()
	{
		const string source = """
			enum Color
			{
				Red,

				Green
			}

			enum Shade
			{
				Light,

				Dark,
			}

			enum Tone
			{
				Warm, // the default

				Cool
			}
			""";

		const string fixedSource = """
			enum Color
			{
				Red,
				Green
			}

			enum Shade
			{
				Light,
				Dark,
			}

			enum Tone
			{
				Warm, // the default

				Cool
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepTwoUnitsOfContainerTogetherAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(2, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnEnumGapThatHoldsADocumentationCommentOrADirectiveKeepsItsBlankLine()
	{
		const string source = """
			enum Color
			{
				Red,

				/// <summary>The green.</summary>
				Green
			}

			enum Shade
			{
				Light,

			#if DEBUG
				Dark
			#endif
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepTwoUnitsOfContainerTogetherAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ATrailingCommentKeepsTheTwoUnitsApart()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					int result = value + 1; // the caller needs it back

					return;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepTwoUnitsOfContainerTogetherAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}
}
