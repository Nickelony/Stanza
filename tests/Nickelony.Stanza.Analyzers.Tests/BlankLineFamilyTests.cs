using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class BlankLineFamilyTests
{
	[TestMethod]
	public async Task AdjacentUsingDirectivesRemainTogether()
	{
		const string source = """
			using System;
			using Items = System.Collections.Generic.Dictionary<
				int,
				string>;

			namespace N
			{
				using System.Linq;
				using Other = System.Collections.Generic.Dictionary<
					int,
					string>;

				class C
				{
					public int Value { get; set; }
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ConstAndPlainDeclarationRunsAreSeparatedOnce()
	{
		const string source = """
			class C
			{
				void M()
				{
					const int first = 1;
					const int second = 2;
					int third = 3;
					int fourth = 4;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					const int first = 1;
					const int second = 2;

					int third = 3;
					int fourth = 4;
				}
			}
			""";

		// The run boundary belongs to the declaration run rule, which owns every boundary it reports,
		// so the same-type rule stays silent here instead of reporting the same gap twice.
		AnalyzerRunResult declarationRuns =
			await RoslynTestHarness.RunAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(source);

		Assert.HasCount(1, declarationRuns.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, declarationRuns);

		AnalyzerRunResult sameTypeDeclarations =
			await RoslynTestHarness.RunAsync<SeparateSameTypeDeclarationGroupsAnalyzer>(source);

		Assert.IsEmpty(sameTypeDeclarations.Diagnostics);
	}

	[TestMethod]
	[DataRow("a property", "public int Count => _count;")]
	[DataRow("a method", "public void Reset() { }")]
	public async Task SameTypeRunHasNoBoundaryTowardAPropertyOrAMethod(string name, string member)
	{
		string source = $$"""
			class C
			{
				private int _count;
				private int _total;
				{{member}}
			}
			""";

		// Neither is a field, a local or an event field, so the run has no boundary toward it;
		// the declaration run rule reports that boundary instead.
		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateSameTypeDeclarationGroupsAnalyzer>(source);

		Assert.IsEmpty(result.Diagnostics, name);

		AnalyzerRunResult owner =
			await RoslynTestHarness.ReportAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(
				source
			);

		Assert.HasCount(1, owner.Diagnostics, name);
	}

	[TestMethod]
	public async Task NoGapIsReportedBySameTypeRuleAndAnOwnerRule()
	{
		string[] layouts =
		[
			"class C\n{\n\tint a;\n\tint b;\n\tstring c;\n}\n",
			"class C\n{\n\tint b = 2;\n\tint c = 3;\n\tconst string D = \"\";\n}\n",
			"class C\n{\n\tconst int A = 1;\n\tint b = 2;\n\tint c = 3;\n}\n",
			"class C\n{\n\tint b = 2;\n\tint c = 3;\n\tvoid M() { }\n}\n",
			"class C\n{\n\tint b = 2;\n\tint c = 3;\n\tint P => 1;\n}\n",
			"class C\n{\n\tconst int A = 1;\n\tconst int B = 2;\n\tint c = 3;\n}\n",
			"class C\n{\n\tvoid M()\n\t{\n\t\tProcess(0);\n\t\tint a = 1;\n\t\tint b = 2;\n\t}\n\n\tvoid Process(int value) { }\n}\n",
			"class C\n{\n\tvoid M()\n\t{\n\t\tint a = 1;\n\t\tint b = 2;\n\t\tProcess(a);\n\t}\n\n\tvoid Process(int value) { }\n}\n",
			"class C\n{\n\tvoid M()\n\t{\n\t\tint a = 1;\n\t\tint b = 2;\n\t\tstring c = \"\";\n\t}\n}\n",
		];

		int sameTypeReports = 0;

		foreach (string source in layouts)
		{
			AnalyzerRunResult sameType =
				await RoslynTestHarness.ReportAsync<SeparateSameTypeDeclarationGroupsAnalyzer>(
					source
				);

			AnalyzerRunResult declarationRuns =
				await RoslynTestHarness.ReportAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(
					source
				);

			AnalyzerRunResult memberGroups =
				await RoslynTestHarness.ReportAsync<SeparateMemberGroupsAnalyzer>(source);

			HashSet<int> owned =
			[
				.. declarationRuns.DiagnosticLines,
				.. memberGroups.DiagnosticLines,
			];

			List<int> shared = [.. sameType.DiagnosticLines.Where(owned.Contains)];

			Assert.IsEmpty(
				shared,
				$"The same-type rule and an owner both report a gap in:\n{source}"
			);

			sameTypeReports += sameType.Diagnostics.Length;
		}

		Assert.IsGreaterThan(0, sameTypeReports, "The layouts must exercise the same-type rule.");
	}

	[TestMethod]
	public async Task MemberRunsGetSeparated()
	{
		const string source = """
			class C
			{
				private int _count;
				private string _name;
				public int Count => _count;
				public string Name => _name;
				public void Reset() => _count = 0;
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _count;
				private string _name;

				public int Count => _count;
				public string Name => _name;

				public void Reset() => _count = 0;
			}
			""";

		// The field run belongs to the declaration run rule and the property run to the member group rule,
		// so a run of declarations is separated by exactly one of the two.
		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source);

		string reportedIds = string.Join(
			",",
			result.Diagnostics.Select(diagnostic => diagnostic.Id).OrderBy(id => id)
		);

		Assert.AreEqual(
			$"{DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns.Id},{DiagnosticDescriptors.SeparateMemberGroups.Id}",
			reportedIds
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AMultiLineCallEndsARunWhereItsBlankLinesGo()
	{
		const string source = """
			class C
			{
				void M()
				{
					var first = 1;
					var second = 2;
					DoWork();
					DoWork(
						first);
					DoWork(second);
					return;
				}

				void DoWork(int value = 0)
				{
					DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					var first = 1;
					var second = 2;

					DoWork();

					DoWork(
						first);

					DoWork(second);
					return;
				}

				void DoWork(int value = 0)
				{
					DoThing();
				}
			}
			""";

		// ZA0005 keeps the gaps around the multi-line call, so the calls make no run, and only the declarations end one.
		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task RegionDirectivesBlockTheirOwnBoundaryOnly()
	{
		const string source = """
			class C
			{
				void M()
				{
			#region helpers
					DoWork();
			#endregion
					DoWork();
					DoWork();
					return;
				}

				void DoWork()
				{
					DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
			#region helpers

					DoWork();

			#endregion

					DoWork();
					DoWork();

					return;
				}

				void DoWork()
				{
					DoThing();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source);

		// A directive line ends the run before it and blocks the boundary next to it,
		// while the boundary after the region is still separated.
		// The region rule pads a blank line between each marker and the code beside it,
		// unless that neighbor is a brace, so a marker under an opening brace stays put.
		Assert.HasCount(4, result.Diagnostics);

		Assert.AreSequenceEqual(
			[
				DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns.Id,
				DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors.Id,
				DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors.Id,
				DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors.Id,
			],

			[.. result.Diagnostics.Select(diagnostic => diagnostic.Id).OrderBy(id => id)]
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("ZA0007", 0, 1)]
	[DataRow("za0007", 1, 0)]
	public async Task OnlyAPragmaThatSpellsTheIdExactlyHandsTheImportBoundaryToTheNeighbor(
		string id,
		int ownerReports,
		int neighborReports
	)
	{
		string source = $$"""
			#pragma warning disable {{id}}
			using System;
			class C
			{
			}
			""";

		AnalyzerRunResult owned =
			await RoslynTestHarness.ReportAsync<SeparateImportSectionAnalyzer>(source);

		Assert.HasCount(ownerReports, owned.Diagnostics, $"The import section rule under {id}.");

		AnalyzerRunResult neighbor =
			await RoslynTestHarness.ReportAsync<SeparateMultiLineConstructsAnalyzer>(source);

		Assert.HasCount(neighborReports, neighbor.Diagnostics, $"The neighbor under {id}.");
	}

	[TestMethod]
	public async Task SingleLineAccessorsStayTight()
	{
		const string source = """
			class C
			{
				public int Value { get; set; }

				public int Other
				{
					get
					{
						return 1;
					}
				}
			}
			""";

		AnalyzerRunResult separated =
			await RoslynTestHarness.RunAsync<SeparateMultiLineAccessorsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(separated.Diagnostics);

		AnalyzerRunResult tight =
			await RoslynTestHarness.RunAsync<KeepSingleLineAccessorsTogetherAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(tight.Diagnostics);
	}

	[TestMethod]
	public async Task BothAccessorRulesPinOnePolicy()
	{
		const string source = """
			class C
			{
				private int _value;

				public int Value
				{
					get { return _value; }

					set { _value = value; }
				}

				public int Other
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
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _value;

				public int Value
				{
					get { return _value; }
					set { _value = value; }
				}

				public int Other
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
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> bothRules = TestRuleSets.Only(
			DiagnosticDescriptors.SeparateMultiLineAccessors,
			DiagnosticDescriptors.KeepSingleLineAccessorsTogether
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source, bothRules);

		// These rules judge disjoint gaps, so the file settles on one shape:
		// the single-line accessors lose their blank line, and the block-bodied ones gain it.
		Assert.HasCount(
			1,

			result.Diagnostics.Where(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.SeparateMultiLineAccessors.Id
			)
		);

		Assert.HasCount(
			1,

			result.Diagnostics.Where(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.KeepSingleLineAccessorsTogether.Id
			)
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// Each row holds the source, the rules it runs under, and the rule that reports the other gap.
	public static IEnumerable<object?[]> ImportSectionNeighbors { get; } =
	[
		[
			"a file-scoped namespace header below the section",

			"""
				using System;
				namespace N;
				class C
				{
					int Value => 1;
				}
				""",

			null,
			DiagnosticDescriptors.SeparateFileScopedNamespaceDeclarationFromFirstMember.Id,
		],

		[
			"global and plain usings in one section",

			"""
				global using System;
				using System.Text;
				class C
				{
					int Value => 1;
				}
				""",

			TestRuleSets.StandaloneRulesEnabled,
			DiagnosticDescriptors.SeparateImportDirectiveGroups.Id,
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(ImportSectionNeighbors),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task TheBoundaryAfterTheImportSectionBelongsToTheImportSectionRule(
		string name,
		string source,
		IReadOnlyDictionary<string, ReportDiagnostic>? rules,
		string neighborId
	)
	{
		AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(source, rules);

		string reported = string.Join(
			",",

			result
				.Diagnostics.Select(diagnostic => diagnostic.Id)
				.Distinct()
				.OrderBy(id => id, StringComparer.Ordinal)
		);

		Assert.AreEqual(
			$"{DiagnosticDescriptors.SeparateImportSection.Id},{neighborId}",
			reported,
			name
		);
	}

	[TestMethod]
	public async Task EachListRuleReportsItsOwnGapAndNoGapTwice()
	{
		const string source = """
			class C
			{
				Item M(
					IReadOnlyDictionary<
						string,
						int> lookup,
					int capacity)
				{
					int first = Build(
							1), second = 2;

					var item = new Item
					{
						Name = "a",
						Tags = new List<string>
						{
							"x",
							"y" +
								"z",
						},
					};

					Call(
						1, Build(
							2)
					);

					return item;
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(
			source,
			TestRuleSets.ListRulesEnabled
		);

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

		// Every list-family rule anchors on the first token of the second unit of its list; one rule per gap.
		Assert.AreEqual(TestRuleSets.JoinSortedIds(TestRuleSets.ListRules), reportedIds);
		Assert.HasCount(5, result.Diagnostics);
		Assert.HasCount(locations.Length, locations.Distinct());
	}

	// A file that enables only the separating half gives it the gap, wherever it sets the key,
	// so the tight half, which ships on, leaves alone a blank line it would otherwise remove.
	[TestMethod]
	[DataRow(false)]
	[DataRow(true)]
	public async Task TheSeparatingClauseRuleAloneOwnsTheGapAndTheTightRuleYieldsIt(bool inTheTree)
	{
		const string adjacent = """
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
			}
			""";

		const string separated = """
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
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = new()
		{
			[DiagnosticDescriptors.SeparateClauseKeywordFromBraceBeforeIt.Id] =
				ReportDiagnostic.Warn,
		};

		AnalyzerRunResult spacing = inTheTree
			? await RoslynTestHarness.ReportAsync<SeparateClauseKeywordFromBraceBeforeItAnalyzer>(
				adjacent,
				treeOptions: rules
			)
			: await RoslynTestHarness.ReportAsync<SeparateClauseKeywordFromBraceBeforeItAnalyzer>(
				adjacent,
				rules
			);

		AnalyzerRunResult tight = inTheTree
			? await RoslynTestHarness.ReportAsync<KeepClauseKeywordsAdjacentAnalyzer>(
				separated,
				treeOptions: rules
			)
			: await RoslynTestHarness.ReportAsync<KeepClauseKeywordsAdjacentAnalyzer>(
				separated,
				rules
			);

		AnalyzerRunResult control =
			await RoslynTestHarness.ReportAsync<KeepClauseKeywordsAdjacentAnalyzer>(separated);

		Assert.HasCount(1, spacing.Diagnostics);
		Assert.IsEmpty(tight.Diagnostics);

		Assert.HasCount(
			1,
			control.Diagnostics,
			"With neither half enabled, the tight half owns the gap and removes the blank line."
		);
	}

	[TestMethod]
	public async Task TheTightRuleOwnsTheGapWhenTheTreeSetsBothHalves()
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
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = new()
		{
			[DiagnosticDescriptors.SeparateClauseKeywordFromBraceBeforeIt.Id] =
				ReportDiagnostic.Warn,

			[DiagnosticDescriptors.KeepClauseKeywordsAdjacent.Id] = ReportDiagnostic.Warn,
		};

		AnalyzerRunResult spacing =
			await RoslynTestHarness.ReportAsync<SeparateClauseKeywordFromBraceBeforeItAnalyzer>(
				source,
				treeOptions: rules
			);

		AnalyzerRunResult tight =
			await RoslynTestHarness.ReportAsync<KeepClauseKeywordsAdjacentAnalyzer>(
				source,
				treeOptions: rules
			);

		Assert.IsEmpty(spacing.Diagnostics);
		Assert.HasCount(1, tight.Diagnostics);
	}

	[TestMethod]
	public async Task TheTwoSidesOfAClauseGapBelongToDifferentRules()
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

					finally

					{
						DoThing();
					}

					if (value > 0)
					{
						DoThing();
					}

					else

					{
						DoThing();
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

					if (value > 0)
					{
						DoThing();
					}
					else
					{
						DoThing();
						DoThing();
					}
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		// The header gaps are padding, and the gaps after a closing brace are clause gaps.
		Assert.HasCount(6, result.Diagnostics);

		Assert.HasCount(
			3,

			result.Diagnostics.Where(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.RemoveBracePadding.Id
			)
		);

		Assert.HasCount(
			3,

			result.Diagnostics.Where(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.KeepClauseKeywordsAdjacent.Id
			)
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
