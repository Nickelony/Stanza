using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

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

		// A statement ahead of the pair counts the same, since the directive sits on the first unit.
		const string ahead = """
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

		await AssertQuietInEveryViewAsync(source);
		await AssertQuietInEveryViewAsync(ahead);
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

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.Only(
			DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns,
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether
		);

		// Without `B` the block holds three statements, so the grouping rule asks for the blank line.
		AnalyzerRunResult withoutB = await RoslynTestHarness.RunAllAsync(source + support, rules);

		Assert.HasCount(1, withoutB.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns.Id,
			withoutB.Diagnostics[0].Id
		);

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

		// The `#define` of a view stays on the first token of a file too.
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
	public void ADirectiveInsideAUnitMovesNoUnitInOrOut()
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

	private static string Describe(string view) => view.Length == 0 ? "without B" : "with B";

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
