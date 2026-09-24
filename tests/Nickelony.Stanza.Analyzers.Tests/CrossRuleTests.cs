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

			class F
			{
				int Pick()
					=> 1;

				string Describe(int value) => value.ToString() + value.ToString() + value.ToString() + value.ToString();
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(source);

		string reportedIds = string.Join(
			",",
			result.Diagnostics.Select(diagnostic => diagnostic.Id).Distinct().OrderBy(id => id)
		);

		ImmutableArray<TextSpan> locations =
		[
			.. result
				.Diagnostics.Where(diagnostic => diagnostic.Location.SourceTree is not null)
				.Select(diagnostic => diagnostic.Location.SourceSpan),
		];

		Assert.AreEqual(TestRuleSets.JoinSortedIds(TestRuleSets.DefaultRules), reportedIds);

		Dictionary<string, int> expectedCounts = new(StringComparer.Ordinal)
		{
			[DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns.Id] = 2,
			[DiagnosticDescriptors.SeparateSameTypeDeclarationGroups.Id] = 2,
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
			DiagnosticDescriptors.MarkNullKeyword.Id,
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
	// because no rule removes a blank line between single-line members,
	// or the one before an else that follows no brace.
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

	[TestMethod]
	public async Task ABlankLineBeforeAnElseWhoseBracesWereDroppedStaysUnowned()
	{
		const string source = """
			class C
			{
				void M(bool flag)
				{
					if (flag)
						Call();

					else
						Other();
				}

				void Call() { }

				void Other() { }
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.RemoveRedundantBraces,
				DiagnosticDescriptors.KeepClauseKeywordsAdjacent
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
	public async Task TheBraceFamilyOnlyClaimsEachGapOnce()
	{
		const string source = """
			class C
			{
				void M(int value)
				{

					if (value > 0)
						return;
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
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		string reportedIds = string.Join(
			",",
			result.Diagnostics.Select(diagnostic => diagnostic.Id).Distinct().OrderBy(id => id)
		);

		// Padding, the multi-line chain and the clause keyword all fire;
		// the redundant brace rule stays out of a chain a multi-line branch pins.
		// Every gap is claimed once: padding takes the gaps next to a brace,
		// the clause keyword takes the gap before the else, and brace consistency takes the braceless branch of the chain.
		Assert.AreEqual(
			$"{DiagnosticDescriptors.KeepClauseKeywordsAdjacent.Id},{DiagnosticDescriptors.RemoveBracePadding.Id},{DiagnosticDescriptors.UseBracesConsistently.Id}",
			reportedIds
		);

		Assert.HasCount(5, result.Diagnostics);
		RoslynTestHarness.AssertEachGapIsClaimedOnce(result);
	}

	[TestMethod]
	public async Task TheEmptyBlockRuleOwnsABodyWithNothingButBlankLines()
	{
		const string source = """
			class C
			{
				void M()
				{

				}

				void N()
				{

					DoThing();
					DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{ }

				void N()
				{
					DoThing();
					DoThing();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		string reportedIds = string.Join(
			",",
			result.Diagnostics.Select(diagnostic => diagnostic.Id).Distinct().OrderBy(id => id)
		);

		Assert.AreEqual(
			$"{DiagnosticDescriptors.RemoveBracePadding.Id},{DiagnosticDescriptors.CollapseEmptyBlocks.Id}",
			reportedIds
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
