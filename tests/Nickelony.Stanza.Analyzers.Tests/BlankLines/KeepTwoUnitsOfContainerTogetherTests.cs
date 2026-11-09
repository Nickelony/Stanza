using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed class KeepTwoUnitsOfContainerTogetherTests
{
	// The views of a fixture: the file as it stands, and with B defined on its first line.
	private static readonly string[] s_views = ["", "#define B\n"];

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

	[TestMethod]
	public async Task ABlockWhoseSizeDependsOnTheViewIsNotJudged()
	{
		const string source = """
			class C
			{
				void M()
				{
					int a = 1;

					Use(a);
			#if B
					Use(2);
			#endif
				}

				void Use(int value) { }
			}
			""";

		await AssertQuietInEveryViewAsync(source);
	}

	[TestMethod]
	public async Task ABlockWithAConditionalStatementSettlesToTheSameTextInEveryView()
	{
		const string source = """
			class C
			{
				void Check()
				{
					Options options = Options.Default;
					Expect.IsTrue(options.Accept);
			#if !B
					Expect.AreEqual(120, options.Delay);
			#endif
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void Check()
				{
					Options options = Options.Default;

					Expect.IsTrue(options.Accept);
			#if !B
					Expect.AreEqual(120, options.Delay);
			#endif
				}
			}
			""";

		// The block calls members of two types that the snippet has to compile with.
		const string support = """


			class Options
			{
				public static Options Default = new();
				public bool Accept;
				public int Delay;
			}

			static class Expect
			{
				public static void IsTrue(bool condition) { }
				public static void AreEqual(int expected, int actual) { }
			}
			""";

		// The conditional block rule would write its own blank line above the #if, so it stays off.
		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.WithOneOff(
			on:
			[
				DiagnosticDescriptors.SeparateAssertionRuns,
				DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
			],

			off: DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors
		);

		// Without `B` the block holds three statements, so the grouping rule asks for the blank line.
		AnalyzerRunResult withoutB = await RoslynTestHarness.RunAllAsync(source + support, rules);

		Assert.HasCount(1, withoutB.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.SeparateAssertionRuns.Id, withoutB.Diagnostics[0].Id);

		RoslynTestHarness.AssertFixedText(fixedSource + support, withoutB);

		// With `B` it holds two, which no grouping rule separates, and the two-unit rule must not take the blank line back.
		AnalyzerRunResult withB = await RoslynTestHarness.RunAllAsync(
			"#define B\n" + source + support,
			rules
		);

		Assert.IsEmpty(withB.Diagnostics);

		foreach (string view in s_views)
		{
			AnalyzerRunResult settled = await RoslynTestHarness.RunAllAsync(
				view + fixedSource + support,
				rules
			);

			Assert.IsEmpty(settled.Diagnostics, $"view {Describe(view)}");
		}
	}

	[TestMethod]
	public async Task AFileWhoseSizeDependsOnTheViewIsNotJudged()
	{
		// The directive that ends the conditional sits on the end-of-file token.
		const string source = """
			int a = 1;

			a++;
			#if B
			a++;
			#endif
			""";

		await AssertQuietInEveryViewAsync(source);
	}

	[TestMethod]
	public async Task AnEnumWhoseSizeDependsOnTheViewIsNotJudged()
	{
		// Without `B`, the directives sit on the closing brace, behind the comma that follows the last member.
		const string source = """
			enum Color
			{
				Red,

				Green,
			#if B
				Blue,
			#endif
			}
			""";

		await AssertQuietInEveryViewAsync(source);
	}

	[TestMethod]
	public async Task ASwitchSectionWhoseSizeDependsOnTheViewIsNotJudged()
	{
		// The directive that ends the conditional sits on the next section, and in the last section on the closing brace of the switch.
		const string nextSection = """
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1:
							Use(1);

							break;
			#if B
							Use(3);
			#endif
						case 2:
							break;
					}
				}

				void Use(int value) { }
			}
			""";

		const string lastSection = """
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1:
							Use(1);

							break;
			#if B
							Use(3);
			#endif
					}
				}

				void Use(int value) { }
			}
			""";

		await AssertQuietInEveryViewAsync(nextSection);
		await AssertQuietInEveryViewAsync(lastSection);
	}

	[TestMethod]
	public async Task ATypeAndANamespaceWhoseSizeDependsOnTheViewAreNotJudged()
	{
		const string type = """
			class C
			{
				int a;

				int b;
			#if B
				int c;
			#endif
			}
			""";

		const string @namespace = """
			namespace N
			{
				class A { }

				class D { }
			#if B
				class E { }
			#endif
			}
			""";

		await AssertQuietInEveryViewAsync(type);
		await AssertQuietInEveryViewAsync(@namespace);
	}

	[TestMethod]
	public async Task ADirectiveOutsideTheBracesOrOfNoCountLeavesTheContainerJudged()
	{
		// A conditional around the head of the method sits on the open brace, outside the container.
		const string aroundTheContainer = """
			class C
			{
			#if B
				void M()
			#else
				void M(int value)
			#endif
				{
					Use(1);

					Use(2);
				}

				void Use(int value) { }
			}
			""";

		const string aroundTheContainerFixed = """
			class C
			{
			#if B
				void M()
			#else
				void M(int value)
			#endif
				{
					Use(1);
					Use(2);
				}

				void Use(int value) { }
			}
			""";

		const string region = """
			class C
			{
				void M()
				{
			#region Calls
					Use(1);

					Use(2);
			#endregion
				}

				void Use(int value) { }
			}
			""";

		const string regionFixed = """
			class C
			{
				void M()
				{
			#region Calls
					Use(1);
					Use(2);
			#endregion
				}

				void Use(int value) { }
			}
			""";

		// A directive of another kind on the first or the last token of the file changes no count either.
		const string otherDirectives = """
			#nullable enable
			#pragma warning disable CS0219

			int a = 1;

			a++;
			#pragma warning restore CS0219
			""";

		const string otherDirectivesFixed = """
			#nullable enable
			#pragma warning disable CS0219

			int a = 1;
			a++;
			#pragma warning restore CS0219
			""";

		await AssertJoinedInEveryViewAsync(aroundTheContainer, aroundTheContainerFixed);
		await AssertJoinedInEveryViewAsync(region, regionFixed);
		await AssertJoinedInEveryViewAsync(otherDirectives, otherDirectivesFixed);
	}

	[TestMethod]
	public void OnlyADirectiveBetweenTheUnitsCanChangeTheirCount()
	{
		const string source = """
			class C
			{
				void M()
				{
					Use(
			#if B
						1
			#endif
					);
					{
			#if B
						Use(2);
			#endif
					}
				}

				void N()
				{
					Use(1);
			#if B
					Use(2);
			#endif
				}

				void Use(int value) { }
			}
			""";

		// With `B` defined, the conditional statements join the blocks, so each holds two units.
		CompilationUnitSyntax root = CSharpSyntaxTree
			.ParseText(source, new CSharpParseOptions(preprocessorSymbols: ["B"]))
			.GetCompilationUnitRoot();

		var type = (ClassDeclarationSyntax)root.Members[0];

		BlockSyntax inside = ((MethodDeclarationSyntax)type.Members[0]).Body!;
		BlockSyntax between = ((MethodDeclarationSyntax)type.Members[1]).Body!;

		Assert.IsTrue(BlankLineOwnership.HoldsTwoKeptUnits(StanzaSyntax.ReadSiblings(inside)));
		Assert.IsFalse(BlankLineOwnership.HoldsTwoKeptUnits(StanzaSyntax.ReadSiblings(between)));
	}

	// The units of a container that stands wholly inside a conditional exist in one view and in no other, so no two views disagree.
	[TestMethod]
	public async Task AContainerWhollyInsideAConditionalIsJoinedInTheViewThatHoldsIt()
	{
		const string block = """
			class C
			{
				void M()
				{
			#if B
					Use(1);

					Use(2);
			#endif
				}

				void Use(int value) { }
			}
			""";

		const string blockFixed = """
			class C
			{
				void M()
				{
			#if B
					Use(1);
					Use(2);
			#endif
				}

				void Use(int value) { }
			}
			""";

		const string stacked = """
			class C
			{
				void M()
				{
			#if B
			#if B
					Use(1);

					Use(2);
			#endif
			#endif
				}

				void Use(int value) { }
			}
			""";

		const string stackedFixed = """
			class C
			{
				void M()
				{
			#if B
			#if B
					Use(1);
					Use(2);
			#endif
			#endif
				}

				void Use(int value) { }
			}
			""";

		const string type = """
			class C
			{
			#if B
				int a;

				int b;
			#endif
			}
			""";

		const string typeFixed = """
			class C
			{
			#if B
				int a;
				int b;
			#endif
			}
			""";

		// The conditional of a file stands on the first type and on the end-of-file token.
		const string file = """
			#if B
			class A { }

			class D { }
			#endif
			""";

		const string fileFixed = """
			#if B
			class A { }
			class D { }
			#endif
			""";

		// A comma may follow the last member, and then the closing directive stands on the brace behind it.
		const string enumWithComma = """
			enum Color
			{
			#if B
				Red,

				Green,
			#endif
			}
			""";

		const string enumWithCommaFixed = """
			enum Color
			{
			#if B
				Red,
				Green,
			#endif
			}
			""";

		const string enumWithoutComma = """
			enum Color
			{
			#if B
				Red,

				Green
			#endif
			}
			""";

		const string enumWithoutCommaFixed = """
			enum Color
			{
			#if B
				Red,
				Green
			#endif
			}
			""";

		const string section = """
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1:
			#if B
							Use(1);

							break;
			#endif
						case 2:
							break;
					}
				}

				void Use(int value) { }
			}
			""";

		const string sectionFixed = """
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1:
			#if B
							Use(1);
							break;
			#endif
						case 2:
							break;
					}
				}

				void Use(int value) { }
			}
			""";

		await AssertJoinedOnlyWhereTheConditionalHoldsAsync(block, blockFixed);
		await AssertJoinedOnlyWhereTheConditionalHoldsAsync(stacked, stackedFixed);
		await AssertJoinedOnlyWhereTheConditionalHoldsAsync(type, typeFixed);
		await AssertJoinedOnlyWhereTheConditionalHoldsAsync(file, fileFixed);
		await AssertJoinedOnlyWhereTheConditionalHoldsAsync(enumWithComma, enumWithCommaFixed);

		await AssertJoinedOnlyWhereTheConditionalHoldsAsync(
			enumWithoutComma,
			enumWithoutCommaFixed
		);

		await AssertJoinedOnlyWhereTheConditionalHoldsAsync(section, sectionFixed);
	}

	[TestMethod]
	public async Task AConditionalThatSplitsTheUnitsOrHoldsABranchStillKeepsTheContainerQuiet()
	{
		// A branch can hold units of its own, so a view that takes it holds another count.
		const string branch = """
			class C
			{
				void M()
				{
			#if B
					Use(1);

					Use(2);
			#else
					Use(3);
			#endif
				}

				void Use(int value) { }
			}
			""";

		// The conditional ends inside the units, so the view without it holds a unit that the other does not.
		const string splitting = """
			class C
			{
				void M()
				{
			#if B
					Use(0);
			#endif
					Use(1);

					Use(2);
				}

				void Use(int value) { }
			}
			""";

		// The conditional starts above the container and ends in it, with a branch on the way.
		const string opened = """
			class C
			{
			#if B
				void M()
				{
					Use(0);
			#else
				void M()
				{
			#endif
					Use(1);

					Use(2);
				}

				void Use(int value) { }
			}
			""";

		await AssertQuietInEveryViewAsync(branch);
		await AssertQuietInEveryViewAsync(splitting);
		await AssertQuietInEveryViewAsync(opened);
	}

	// A conditional above the next section stands outside this section, whatever the view takes, when it holds whole sections.
	[TestMethod]
	public async Task AConditionalAboveTheNextSwitchSectionLeavesTheSectionJudgedInEveryView()
	{
		const string nextSection = """
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1:
							Use(1);

							break;
			#if B
						case 2:
							Use(2);
							break;
			#endif
						case 3:
							break;
					}
				}

				void Use(int value) { }
			}
			""";

		const string nextSectionFixed = """
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1:
							Use(1);
							break;
			#if B
						case 2:
							Use(2);
							break;
			#endif
						case 3:
							break;
					}
				}

				void Use(int value) { }
			}
			""";

		const string lastSection = """
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1:
							Use(1);

							break;
			#if B
						default:
							break;
			#endif
					}
				}

				void Use(int value) { }
			}
			""";

		const string lastSectionFixed = """
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1:
							Use(1);
							break;
			#if B
						default:
							break;
			#endif
					}
				}

				void Use(int value) { }
			}
			""";

		// The conditional holds only a label, so a view without it joins the statements below to this section.
		const string hidesTheBoundary = """
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1:
							Use(1);

							break;
			#if B
						case 2:
			#endif
							Use(3);
							break;
					}
				}

				void Use(int value) { }
			}
			""";

		// The conditional ends inside a section, so a view without it joins that section's tail to this one.
		const string endsInASection = """
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1:
							Use(1);

							break;
			#if B
						case 2:
							Use(2);
			#endif
							Use(3);
							break;
					}
				}

				void Use(int value) { }
			}
			""";

		// A branch can hold statements that the other view puts into this section.
		const string branch = """
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1:
							Use(1);

							break;
			#if B
						case 2:
							break;
			#else
						case 3:
							break;
			#endif
					}
				}

				void Use(int value) { }
			}
			""";

		await AssertJoinedInEveryViewAsync(nextSection, nextSectionFixed);
		await AssertJoinedInEveryViewAsync(lastSection, lastSectionFixed);

		await AssertQuietInEveryViewAsync(hidesTheBoundary);
		await AssertQuietInEveryViewAsync(endsInASection);
		await AssertQuietInEveryViewAsync(branch);
	}

	// A conditional among the labels leaves the statements as they are, unless it holds the first label or a statement.
	[TestMethod]
	public async Task AConditionalAmongTheCaseLabelsLeavesTheSectionJudgedInEveryView()
	{
		const string amongLabels = """
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1:
			#if B
						case 2:
			#endif
							Use(1);

							break;
					}
				}

				void Use(int value) { }
			}
			""";

		const string amongLabelsFixed = """
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1:
			#if B
						case 2:
			#endif
							Use(1);
							break;
					}
				}

				void Use(int value) { }
			}
			""";

		// The conditional holds a label that a pattern guards, which is still a label.
		const string patternLabel = """
			class C
			{
				void M(object value)
				{
					switch (value)
					{
						case null:
			#if B
						case int number when number > 1:
			#endif
							Use(1);

							break;
					}
				}

				void Use(int value) { }
			}
			""";

		const string patternLabelFixed = """
			class C
			{
				void M(object value)
				{
					switch (value)
					{
						case null:
			#if B
						case int number when number > 1:
			#endif
							Use(1);
							break;
					}
				}

				void Use(int value) { }
			}
			""";

		// The conditional holds the first label, so a view without it joins these statements to the section above.
		const string firstLabel = """
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 0:
							Use(0);
							break;
			#if B
						case 1:
			#endif
							Use(1);

							break;
					}
				}

				void Use(int value) { }
			}
			""";

		// The conditional holds a statement, so a view without it has fewer statements in this section.
		const string statementAmongLabels = """
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1:
			#if B
							Use(0);
							break;
						case 2:
			#endif
							Use(1);

							break;
					}
				}

				void Use(int value) { }
			}
			""";

		// A branch can hold a statement in place of a label.
		const string branch = """
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1:
			#if B
						case 2:
			#else
							Use(0);
			#endif
							Use(1);

							break;
					}
				}

				void Use(int value) { }
			}
			""";

		await AssertJoinedInEveryViewAsync(amongLabels, amongLabelsFixed);
		await AssertJoinedInEveryViewAsync(patternLabel, patternLabelFixed);

		await AssertQuietInEveryViewAsync(firstLabel);
		await AssertQuietInEveryViewAsync(statementAmongLabels);
		await AssertQuietInEveryViewAsync(branch);
	}

	// A directive inside a unit can change how many units a view has, as it can give an if its body, so these shapes lie outside the claim below.
	// Each such unit spans lines in the view that has fewer units, which the rule leaves alone, so the views still do not conflict.
	[TestMethod]
	public async Task AContainerWhoseUnitsAreSplitByADirectiveIsLeftAloneInEveryView()
	{
		const string ifBody = """
			class C
			{
				void M(bool a)
				{
					if (a)
			#if B
						Log();
			#endif
						Use();

					Next();
				}

				void Log() { }
				void Use() { }
				void Next() { }
			}
			""";

		const string splitBraces = """
			class C
			{
				void M(bool a, bool b)
				{
					if (a)
			#if B
					{
			#endif
						Use();
			#if B
					}
			#endif

					Next();
				}

				void Use() { }
				void Next() { }
			}
			""";

		await AssertQuietInEveryViewAsync(ifBody);
		await AssertQuietInEveryViewAsync(splitBraces);
	}

	// The proof that no two views conflict for single-line units: a container that is judged holds the same units wherever else it appears.
	[TestMethod]
	public void AJudgedContainerOfSingleLineUnitsHoldsTheSameUnitsInEveryView()
	{
		string[][] symbolSets =
		[
			[],
			["B"],
			["C"],
			["B", "C"],
		];

		int judged = 0;
		int shapes = 0;

		foreach ((string template, string[] lines) in s_unitTemplates)
		{
			for (int seed = 0; seed < 600; seed++)
			{
				string source = template.Replace(
					"$BODY",
					GenerateBody(new Random(seed), lines),
					StringComparison.Ordinal
				);

				List<SyntaxTree> views =
				[
					.. symbolSets.Select(symbols =>
						CSharpSyntaxTree.ParseText(
							source,
							new CSharpParseOptions(preprocessorSymbols: symbols)
						)
					),
				];

				// A body that does not parse in every view is broken code, which no rule judges.
				if (
					views.Any(view =>
						view.GetDiagnostics()
							.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
					)
				)
				{
					continue;
				}

				shapes++;
				judged += AssertSameUnitsInEveryView(source, views);
			}
		}

		Assert.IsGreaterThan(100, shapes, "The generator wrote too few shapes to prove anything.");

		Assert.IsGreaterThan(
			100,
			judged,
			"The generator wrote too few judged containers to prove anything."
		);
	}

	private static string Describe(string view) => view.Length == 0 ? "without B" : "with B";

	// Each template holds a container of $BODY, and the lines are what its body is made of.
	private static readonly (string Template, string[] Lines)[] s_unitTemplates =
	[
		(
			"class C\n{\n\tvoid M(int value)\n\t{\n\t\tswitch (value)\n\t\t{\n$BODY\t\t}\n\t}\n}\n",
			["case 1:", "case 2:", "default:", "Use(1);", "Use(2);", "break;"]
		),

		("class C\n{\n\tvoid M()\n\t{\n$BODY\t}\n}\n", ["Use(1);", "Use(2);", "Use(3);", "{ }"]),
		("class C\n{\n$BODY}\n", ["int a;", "int b;", "int c;"]),
		("enum E\n{\n$BODY}\n", ["A,", "B,", "C"]),
	];

	// A body of lines, some of them wrapped in a conditional with a branch or two, as deep as two levels.
	private static string GenerateBody(Random random, string[] lines)
	{
		StringBuilder body = new();

		AppendLines(random, lines, body, depth: 0);

		return body.ToString();
	}

	private static void AppendLines(Random random, string[] lines, StringBuilder body, int depth)
	{
		int count = random.Next(1, depth == 0 ? 6 : 4);

		for (int index = 0; index < count; index++)
		{
			if (depth < 2 && random.Next(4) == 0)
			{
				body.Append("#if ").Append(random.Next(2) == 0 ? "B" : "C").Append('\n');

				AppendLines(random, lines, body, depth: depth + 1);

				if (random.Next(3) == 0)
				{
					body.Append("#else\n");
					AppendLines(random, lines, body, depth: depth + 1);
				}

				body.Append("#endif\n");
			}
			else
			{
				body.Append("\t\t\t").Append(lines[random.Next(lines.Length)]).Append('\n');
			}
		}
	}

	// Every container that holds units, with the spans of those units, which the text fixes whatever the view.
	private static List<(ImmutableArray<SyntaxNode> Siblings, TextSpan[] Spans)> ReadContainers(
		SyntaxTree syntaxTree
	)
	{
		return
		[
			.. syntaxTree
				.GetRoot()
				.DescendantNodes()
				.Where(node =>
					node
						is BlockSyntax
							or SwitchSectionSyntax
							or TypeDeclarationSyntax
							or EnumDeclarationSyntax
				)
				.Select(StanzaSyntax.ReadSiblings)
				.Where(siblings => siblings.Length > 0)
				.Select(siblings => (siblings, siblings.Select(sibling => sibling.Span).ToArray())),
		];
	}

	// A container that a view judges must not appear with other units in another view, or two views would disagree about its gaps.
	private static int AssertSameUnitsInEveryView(string source, List<SyntaxTree> views)
	{
		List<List<(ImmutableArray<SyntaxNode> Siblings, TextSpan[] Spans)>> containers =
		[
			.. views.Select(ReadContainers),
		];

		int judged = 0;

		for (int view = 0; view < views.Count; view++)
		{
			foreach ((ImmutableArray<SyntaxNode> siblings, TextSpan[] spans) in containers[view])
			{
				if (BlankLineDirectives.HoldsConditionalDirective(siblings))
					continue;

				judged++;
				AssertSameUnitsInOtherViews(source, spans, view, containers);
			}
		}

		return judged;
	}

	private static void AssertSameUnitsInOtherViews(
		string source,
		TextSpan[] spans,
		int view,
		List<List<(ImmutableArray<SyntaxNode> Siblings, TextSpan[] Spans)>> containers
	)
	{
		for (int other = 0; other < containers.Count; other++)
		{
			foreach ((_, TextSpan[] otherSpans) in containers[other])
			{
				if (otherSpans.Intersect(spans).Any())
				{
					Assert.AreSequenceEqual(
						spans,
						otherSpans,
						$"View {view} and view {other} hold other units in:\n{source}"
					);
				}
			}
		}
	}

	// The container exists in the view that defines B and has no units in the other, and the fix leaves both views settled.
	private static async Task AssertJoinedOnlyWhereTheConditionalHoldsAsync(
		string source,
		string fixedSource
	)
	{
		AnalyzerRunResult held =
			await RoslynTestHarness.RunAsync<KeepTwoUnitsOfContainerTogetherAnalyzer>(
				"#define B\n" + source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, held.Diagnostics, "view with B");
		RoslynTestHarness.AssertFixedText("#define B\n" + fixedSource, held, "view with B");

		AnalyzerRunResult hidden =
			await RoslynTestHarness.RunAsync<KeepTwoUnitsOfContainerTogetherAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(hidden.Diagnostics, "view without B");
		RoslynTestHarness.AssertFixedText(source, hidden, "view without B");

		await AssertQuietInEveryViewAsync(fixedSource);
	}

	private static async Task AssertQuietInEveryViewAsync(string source)
	{
		foreach (string view in s_views)
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<KeepTwoUnitsOfContainerTogetherAnalyzer>(
					view + source,
					TestRuleSets.StandaloneRulesEnabled
				);

			Assert.IsEmpty(result.Diagnostics, $"view {Describe(view)}");
		}
	}

	private static async Task AssertJoinedInEveryViewAsync(string source, string fixedSource)
	{
		foreach (string view in s_views)
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<KeepTwoUnitsOfContainerTogetherAnalyzer>(
					view + source,
					TestRuleSets.StandaloneRulesEnabled
				);

			Assert.HasCount(1, result.Diagnostics, $"view {Describe(view)}");
			RoslynTestHarness.AssertFixedText(view + fixedSource, result, $"view {Describe(view)}");
		}
	}
}
