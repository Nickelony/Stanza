using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class SeparateMultiLineConstructsTests
{
	[TestMethod]
	public async Task AStatementBlockWithoutABlankLineGetsOne()
	{
		const string source = """
			class C
			{
				void M()
				{
					DoThing();
					{
						DoThing();
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					DoThing();

					{
						DoThing();
					}
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source);

		Assert.HasCount(1, result.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.SeparateMultiLineConstructs.Id,
			result.Diagnostics[0].Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// A record and a record struct hold members as a class does, so their multi-line members are separated too.
	[TestMethod]
	[DataRow("record")]
	[DataRow("record struct")]
	public async Task TheMembersOfARecordAreSeparated(string keyword)
	{
		string source = $$"""
			{{keyword}} R(int Value)
			{
				public int First()
				{
					return Value;
				}
				public int Second()
				{
					return Value;
				}
			}
			""";

		string fixedSource = $$"""
			{{keyword}} R(int Value)
			{
				public int First()
				{
					return Value;
				}

				public int Second()
				{
					return Value;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineConstructsAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.SeparateMultiLineConstructs)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task MultiLineSiblingGetsSeparated()
	{
		const string source = """
			class C
			{
				void M()
				{
					if (true)
					{
						DoThing();
					}
					DoThing();
				}

				void DoThing()
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					if (true)
					{
						DoThing();
					}

					DoThing();
				}

				void DoThing()
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineConstructsAnalyzer>(source);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentThatRunsOverTheGapGetsTheBlankLineAfterIt()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					DoThing(
						value); /* note
					continued */
					Other();
				}

				void DoThing(int value)
				{
				}

				void Other()
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int value)
				{
					DoThing(
						value); /* note
					continued */

					Other();
				}

				void DoThing(int value)
				{
				}

				void Other()
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineConstructsAnalyzer>(source);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// A build that writes no documentation file parses the comment as a plain one, and it travels all the same.
	[TestMethod]
	[DataRow(DocumentationMode.Parse)]
	[DataRow(DocumentationMode.None)]
	public async Task ADocumentationCommentTravelsWithItsMemberAcrossBlankLines(
		DocumentationMode mode
	)
	{
		using IDisposable parse = RoslynTestHarness.AtDocumentationMode(mode);

		const string source = """
			class C
			{
				int a;
				/// <summary>Runs.</summary>


				void M()
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				int a;

				/// <summary>Runs.</summary>


				void M()
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineConstructsAnalyzer>(source);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnAttributeListOnItsOwnLineMakesAMemberMultiLine()
	{
		const string source = """
			class C
			{
				[System.Obsolete]
				int a;
				int b;
			}
			""";

		const string fixedSource = """
			class C
			{
				[System.Obsolete]
				int a;

				int b;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineConstructsAnalyzer>(source);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ADocumentationCommentDoesNotMakeAMemberMultiLine()
	{
		const string source = """
			class C
			{
				/// <summary>Holds a.</summary>
				int a;
				int b;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineConstructsAnalyzer>(source);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ALineCommentDoesNotTravelWithItsMemberAcrossABlankLine()
	{
		const string source = """
			class C
			{
				int a;
				// Runs.

				void M()
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineConstructsAnalyzer>(source);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AWhitespaceOnlyLineCountsAsABlankLine()
	{
		string[] lines =
		[
			"class C",
			"{",
			"\tvoid M()",
			"\t{",
			"\t\tif (true)",
			"\t\t{",
			"\t\t\tDoThing();",
			"\t\t}",
			"\t   ",
			"\t\twhile (false)",
			"\t\t{",
			"\t\t\tDoThing();",
			"\t\t}",
			"\t}",
			"}",
			string.Empty,
		];

		AnalyzerRunResult separated =
			await RoslynTestHarness.ReportAsync<SeparateMultiLineConstructsAnalyzer>(
				string.Join("\n", lines)
			);

		Assert.IsEmpty(separated.Diagnostics);

		string[] withoutGap = [.. lines[..8], .. lines[9..]];

		AnalyzerRunResult reported =
			await RoslynTestHarness.ReportAsync<SeparateMultiLineConstructsAnalyzer>(
				string.Join("\n", withoutGap)
			);

		Assert.HasCount(1, reported.Diagnostics);
	}

	[TestMethod]
	public async Task ANullableDirectiveGuardsItsBoundary()
	{
		const string source = """
			class C
			{
				void M()
				{
					if (true)
					{
						DoThing();
					}
			#nullable enable
					while (false)
					{
						DoThing();
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineConstructsAnalyzer>(source);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ADisabledImportSectionRuleHandsItsBoundaryToTheNeighbor()
	{
		const string source = """
			using System;
			class C
			{
			}
			""";

		// While the owner is on, the pair rule defers at the boundary that owner reports.
		AnalyzerRunResult owned =
			await RoslynTestHarness.ReportAsync<SeparateMultiLineConstructsAnalyzer>(source);

		Assert.IsEmpty(owned.Diagnostics);

		AnalyzerRunResult handedOver =
			await RoslynTestHarness.ReportAsync<SeparateMultiLineConstructsAnalyzer>(
				source,

				treeOptions: new Dictionary<string, ReportDiagnostic>
				{
					[DiagnosticDescriptors.SeparateImportSection.Id] = ReportDiagnostic.Suppress,
				}
			);

		Assert.HasCount(1, handedOver.Diagnostics);
	}

	[TestMethod]
	public async Task TheGroupingRulesLeaveARegionBoundaryAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				private void M()
				{
					int value = 1;
				}
				#region Methods
				private void N()
				{
					int other = 2;
				}
				#endregion
				private void P()
				{
					int third = 3;
				}
				private void Q()
				{
					int fourth = 4;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateMultiLineConstructsAnalyzer>(source);

		Assert.HasCount(
			1,
			result.DiagnosticLines,
			"Only the boundary away from the region is reported."
		);

		Assert.AreEqual(16, result.DiagnosticLines[0]);
	}

	[TestMethod]
	public async Task ABlankLineGoesAfterATrailingCommentThatEndsWhereTheNextUnitStarts()
	{
		const string source = """
			class C
			{
				void M(int x)
				{
					Helpers.DoThing(); /* note
					more */ if (x > 0)
					{
						Helpers.DoThing();
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int x)
				{
					Helpers.DoThing(); /* note
					more */

					if (x > 0)
					{
						Helpers.DoThing();
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineConstructsAnalyzer>(source);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The grouping rules cannot see the attribute, so they defer the boundary to a rule it silenced, and nothing reports it.
	[TestMethod]
	public async Task ASuppressMessageAttributeLeavesTheBoundaryTheGroupingRulesDeferUnreported()
	{
		const string attribute =
			"[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage(\"Formatting\", \"ZA0005\")]\n\n";

		const string source = """
			class C
			{
				void M(bool value)
				{
					DoThing();

					if (value)
					{
						DoThing();
					}
					int first = 1;
					int second = 2;
				}

				void DoThing() { }
			}
			""";

		AnalyzerRunResult control = await RoslynTestHarness.ReportAllAsync(source);

		Assert.HasCount(1, control.Diagnostics);

		AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(attribute + source);

		Assert.IsEmpty(result.Diagnostics);
	}
}
