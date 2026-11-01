using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>Checks that the rules share each gap and settle together.</summary>
[TestClass]
public sealed class CrossRuleTests
{
	[TestMethod]
	public async Task RulesNeverReportTheSameBoundaryTwice()
	{
		const string source = """
			using System;
			namespace N;
			class C
			{
				long first = 1;
				int second = 2;
				int third = 3;
				bool fourth = false;
				private int _value;

				public int Value
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

				string M(int value)
				{
					return value switch
					{
						1 => Build(
							value),
						2 => "two",
					};
				}

				string Build(int value)
				{
					return value.ToString();
				}

				void N()
				{
					var value = 1;
					DoWork();
					DoWork();
					return;
				}

				void Q(int value)
				{
					if (value > 0
						&& value < 10)
					{

						DoThing();
					}

					else
						DoThing();
				}

				void P()
				{
					var item = new Item
					{
						Name = "a",
						Tags = new List<string>
						{
							"x",
						},
						Count = 3,
					};
					DoWork();

					Call(
						1, Build(
							2
							)
					);

					switch (1)
					{
						case 1:
							break;
						case 2:
							break;
					}
				}

				void DoWork()
				{
				}

				void Call(int first, string second)
				{
				}

				public int Count => _value;
				public int Total => _value;
				void Reset() => DoThing();
			}

			class D
			{
				int first = Build(
					1), second = 2;
			}

			class E
			{
				#region Fields
				int value;
				#endregion
			}

			class G
			{
				void M(int value)
				{
					var first = 1;
					var second = 2;
					const int third = 3;
					Assert.That(value);
					Assert.That(first);
					return;
				}
			}

			class F
			{
				int Pick()
					=> 1;

				string Describe(int value) => value.ToString() + value.ToString() + value.ToString() + value.ToString();
			}
			""";

		// The rules that ship on, and the same-type rule, which shares the run gaps.
		AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.SeparateSameTypeDeclarationRuns)
		);

		ImmutableArray<TextSpan> locations =
		[
			.. result
				.Diagnostics.Where(diagnostic => diagnostic.Location.SourceTree is not null)
				.Select(diagnostic => diagnostic.Location.SourceSpan),
		];

		Dictionary<string, int> expectedCounts = new(StringComparer.Ordinal)
		{
			[DiagnosticDescriptors.SeparateDeclarationRuns.Id] = 1,
			[DiagnosticDescriptors.SeparateInvocationRuns.Id] = 2,
			[DiagnosticDescriptors.SeparateAssertionRuns.Id] = 2,
			[DiagnosticDescriptors.SeparateSameTypeDeclarationRuns.Id] = 2,
			[DiagnosticDescriptors.SeparateMemberGroups.Id] = 1,
			[DiagnosticDescriptors.SeparateImportSection.Id] = 1,
			[DiagnosticDescriptors.SeparateFileScopedNamespaceDeclarationFromFirstMember.Id] = 1,
			[DiagnosticDescriptors.SeparateMultiLineConstructs.Id] = 1,
			[DiagnosticDescriptors.SeparateMultiLineSwitchSections.Id] = 1,
			[DiagnosticDescriptors.SeparateMultiLineSwitchExpressionArms.Id] = 1,
			[DiagnosticDescriptors.RemoveBracePadding.Id] = 1,
			[DiagnosticDescriptors.UseBracesConsistently.Id] = 1,
			[DiagnosticDescriptors.KeepClauseKeywordsAdjacent.Id] = 1,
			[DiagnosticDescriptors.CollapseEmptyBlocks.Id] = 2,
			[DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors.Id] = 2,
			[DiagnosticDescriptors.BreakExpressionOffSignatureLine.Id] = 1,
		};

		Dictionary<string, int> counts = new(StringComparer.Ordinal);

		foreach (
			IGrouping<string, Diagnostic> group in result.Diagnostics.GroupBy(
				diagnostic => diagnostic.Id,
				StringComparer.Ordinal
			)
		)
		{
			counts[group.Key] = group.Count();
		}

		Assert.AreEqual(
			RenderCounts(expectedCounts),
			RenderCounts(counts),
			"The fixture does not report what it was written for."
		);

		Assert.HasCount(locations.Length, locations.Distinct());

		foreach (
			Diagnostic diagnostic in result.Diagnostics.Where(diagnostic =>
				diagnostic.Location.SourceTree is not null
			)
		)
		{
			Assert.HasCount(1, diagnostic.AdditionalLocations);

			if (TestRuleSets.RewritingRules.Contains(diagnostic.Id))
				continue;

			int previousEnd = diagnostic.AdditionalLocations[0].SourceSpan.End;
			int nextStart = diagnostic.Location.SourceSpan.Start;

			Assert.IsLessThanOrEqualTo(
				nextStart,
				previousEnd,
				$"{diagnostic.Id} does not carry the previous unit as its additional location."
			);
		}
	}

	[TestMethod]
	public async Task AStandaloneBlockAfterARunSettlesWhenTheConstructRuleIsOff()
	{
		const string source = """
			class A
			{
				void M()
				{
					Helpers.DoThing();
					Helpers.DoThing();
					{
						Helpers.DoThing();
					}
				}
			}
			""";

		const string fixedSource = """
			class A
			{
				void M()
				{
					Helpers.DoThing();
					Helpers.DoThing();

					{
						Helpers.DoThing();
					}
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,

			diagnosticOptions: new Dictionary<string, ReportDiagnostic>
			{
				[DiagnosticDescriptors.SeparateMultiLineConstructs.Id] = ReportDiagnostic.Suppress,
			}
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// Gathering the run rewrites the text ahead of the argument names the later fixes wait on.
	[TestMethod]
	public async Task AWholeRunFixLeavesTheFixesBeforeItTheirPlaceInOnePass()
	{
		const string source = """
			class A
			{
				static void F(int value) { }

				static void M(int value)
				{
					F(value: value);

					F(value: value);

					F(value: value);

					F(value: value);
				}
			}
			""";

		const string fixedSource = """
			class A
			{
				static void F(int value) { }

				static void M(int value)
				{
					F(value);
					F(value);
					F(value);
					F(value);
				}
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = new(
			TestRuleSets.Only(
				DiagnosticDescriptors.GatherRunOfLooseStatements,
				DiagnosticDescriptors.RemoveRedundantArgumentName
			)
		);

		AnalyzerRunResult onePass = await RoslynTestHarness.RunAllAsync(source, rules);
		AnalyzerRunResult oneAtATime = await RoslynTestHarness.RunIterativelyAsync(source, rules);

		RoslynTestHarness.AssertFixedText(fixedSource, onePass);
		RoslynTestHarness.AssertFixedText(fixedSource, oneAtATime);
	}

	[TestMethod]
	public async Task TheOptionalFamiliesSettleOneFixAtATime()
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
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.Contains(
			DiagnosticDescriptors.MarkLanguageKeywords.Id,
			result.Diagnostics.Select(diagnostic => diagnostic.Id),
			"the optional family did not report, so the run proves nothing."
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task SeveralOptionalFamiliesSettleOneFixAtATime()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _value;

				internal int Value
				{
					get { return _value; }
					set { _value = value; }
				}

				internal static int Mix(int first, int second, int third, int fourth) =>
					first + second + third + fourth;

				internal static int Run() => Mix(1, 2, 3, 4);
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private int _value;

				internal int Value
				{
					get => _value;
					set => _value = value;
				}

				internal static int Mix(int first, int second, int third, int fourth) =>
					first + second + third + fourth;

				internal static int Run() => Mix(first: 1, second: 2, third: 3, fourth: 4);
			}
			""";

		AnalyzerRunResult oneByOne = await RoslynTestHarness.RunIterativelyAsync(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.IsNotEmpty(oneByOne.Diagnostics, "the optional families reported nothing.");

		Assert.IsGreaterThan(
			1,
			oneByOne.Diagnostics.Select(diagnostic => diagnostic.Id).Distinct().Count(),
			"the run has to walk more than one family to prove the order does not matter."
		);

		Assert.AreNotEqual(source, oneByOne.FixedText, "the walk changed nothing.");
		RoslynTestHarness.AssertFixedText(fixedSource, oneByOne);
	}

	[TestMethod]
	public async Task TheOptionalFamilyOnlyClaimsEachGapOnce()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					try
					{

						DoThing();
					}

					catch (Exception)

					{
						DoThing();
					}

					if (value > 0)
						DoThing();
					else if (value < 0)
					{
						DoThing();
						DoThing();
					}

					else

					{
						DoThing();
					}
				}

				void Empty()
				{
				}

				int Wrapped =>
					Build(
						1);

				void Declared()
				{
					int first = Build(
							1), second = 2;
				}

				void Noted()
				{
					DoThing();//the end of the sample
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		RoslynTestHarness.AssertEachGapIsClaimedOnce(result);
	}

	// A blank line that one fix writes beside a unit stays when another fix changes the unit,
	// because no rule removes a blank line between single-line members.
	[TestMethod]
	public async Task ABlankLineBesideAMemberThatTheBodyRuleCollapsedStaysUnowned()
	{
		const string source = """
			class C
			{
				int X => 1;

				int Y() => 2;

				int Z => 3;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.SeparateMultiLineConstructs,
				DiagnosticDescriptors.UseExpressionBodyForSingleLineMember
			)
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	private static string RenderCounts(Dictionary<string, int> counts)
	{
		return string.Join(
			",",

			counts
				.OrderBy(pair => pair.Key, StringComparer.Ordinal)
				.Select(pair => $"{pair.Key}={pair.Value}")
		);
	}

	[TestMethod]
	public async Task ALabelWithAGotoIsLeftAlone()
	{
		const string source = """
			class C
			{
				void M()
				{
					goto end;

					end:
					DoThing();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,
			TestRuleSets.AllRulesEnabled
		);

		Assert.IsEmpty(result.Diagnostics);
	}
}
