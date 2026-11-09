using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Braces;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Braces;

[TestClass]
public sealed class UseBracesConsistentlyTests
{
	[TestMethod]
	public async Task ABodyWithADirectiveIsNotWrapped()
	{
		const string source = """
			class C
			{
				void M(bool first, bool second)
				{
					if (first &&
						second)
						DoThing(
			#if DEBUG
							1
			#else
							2
			#endif
						);
				}

				void DoThing(int value)
				{
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<UseBracesConsistentlyAnalyzer>(
			source
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	// The else of another view belongs to the inner if, and braces around the body would close that if first.
	[TestMethod]
	[DataRow("")]
	[DataRow("#define X\n")]
	public async Task ABodyWhoseIfTakesAnElseOfAnotherViewIsNotWrapped(string view)
	{
		string source =
			view
			+ """
				class C
				{
					void M(bool first, bool second, bool third)
					{
						while (first &&
							second)
							if (third) DoThing();
				#if X
							else
								DoThing();
				#endif
					}
				}
				""";

		IReadOnlyDictionary<string, ReportDiagnostic> rule = TestRuleSets.Only(
			DiagnosticDescriptors.UseBracesConsistently
		);

		AnalyzerRunResult fixAll = await RoslynTestHarness.RunFixAllAsync(
			source,
			diagnosticOptions: rule,
			assertFixedPoint: false
		);

		AnalyzerRunResult oneByOne = await RoslynTestHarness.RunIterativelyAsync(source, rule);

		Assert.IsEmpty(fixAll.Diagnostics);

		RoslynTestHarness.AssertFixedText(source, fixAll);
		RoslynTestHarness.AssertFixedText(source, oneByOne);
	}

	[TestMethod]
	public async Task ABodyWhoseIfTakesNoElseOfAnotherViewIsWrapped()
	{
		const string source = """
			class C
			{
				void M(bool first, bool second, bool third)
				{
					while (first &&
						second)
						if (third) DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool first, bool second, bool third)
				{
					while (first &&
						second)
					{
						if (third) DoThing();
					}
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<UseBracesConsistentlyAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task MultiLineChainGetsBraces()
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
						return;
					}
					else
						DoThing();
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
						return;
					}
					else if (value < 0)
					{
						DoThing();
						return;
					}
					else
					{
						DoThing();
					}
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<UseBracesConsistentlyAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		// The braced branch holds two statements,
		// so the chain is multi-line and both branches without braces are reported.
		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task MultiLineLoopHeaderGetsBraces()
	{
		const string source = """
			class C
			{
				void M(bool flag)
				{
					while (flag
						&& true)
						DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool flag)
				{
					while (flag
						&& true)
					{
						DoThing();
					}
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<UseBracesConsistentlyAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(1, result.Diagnostics);

		int bodyStart = source.IndexOf("DoThing();", StringComparison.Ordinal);

		Assert.AreEqual(
			new TextSpan(bodyStart, "DoThing".Length),
			result.Diagnostics[0].Location.SourceSpan
		);

		Assert.AreEqual(
			new TextSpan(bodyStart, "DoThing();".Length),
			result.Diagnostics[0].AdditionalLocations[0].SourceSpan
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AForLoopWithAMultiLineHeaderGetsBraces()
	{
		const string source = """
			class C
			{
				void M(int count)
				{
					for (int index = 0;
						index < count;
						index++)
						DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int count)
				{
					for (int index = 0;
						index < count;
						index++)
					{
						DoThing();
					}
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<UseBracesConsistentlyAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The body of a `do` loop comes before its condition, so its closing brace stands before `while`.
	[TestMethod]
	public async Task ADoLoopWithAMultiLineBodyGetsBraces()
	{
		const string source = """
			class C
			{
				void M(bool flag)
				{
					do
						flag = Next(
							flag);
					while (flag);
				}

				static bool Next(bool value) => !value;
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool flag)
				{
					do
					{
						flag = Next(
							flag);
					}
					while (flag);
				}

				static bool Next(bool value) => !value;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<UseBracesConsistentlyAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABodyThatSharesItsLineWithAFollowingStatementIsNotWrapped()
	{
		const string source = """
			class C
			{
				void M(bool flag)
				{
					while (flag
						&& true)
						DoThing(); DoThing();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<UseBracesConsistentlyAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ABodyThatEndsTheFileIsStillWrapped()
	{
		const string source = "bool flag = true;\nwhile (flag\n\t&& true)\n\tDoThing();";
		const string fixedSource = "bool flag = true;\nwhile (flag\n\t&& true)\n{\n\tDoThing();\n}";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<UseBracesConsistentlyAnalyzer>(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.UseBracesConsistently)
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task SingleLineChainsKeepTheirLayout()
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

		// Neither chain is multi-line, so neither of them is the consistency rule's business.
		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<UseBracesConsistentlyAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheBraceConsistencyRuleIsOffWhenTheFilePrefersNoBraces()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					if (value > 0)
					{
						DoThing();
						DoThing();
					}
					else
						DoThing();
				}
			}
			""";

		AnalyzerRunResult prefersNoBraces =
			await RoslynTestHarness.ReportAsync<UseBracesConsistentlyAnalyzer>(
				source,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["csharp_prefer_braces"] = "false:warning",
				}
			);

		Assert.IsEmpty(prefersNoBraces.Diagnostics);

		AnalyzerRunResult prefersMultiLine =
			await RoslynTestHarness.ReportAsync<UseBracesConsistentlyAnalyzer>(
				source,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["csharp_prefer_braces"] = "when_multiline:warning",
				}
			);

		Assert.HasCount(1, prefersMultiLine.Diagnostics);
	}

	[TestMethod]
	public async Task AMultiLineDeconstructionLoopGetsBraces()
	{
		const string source = """
			class C
			{
				void M(bool flag, (int, int)[] pairs)
				{
					foreach (var (first, second) in flag
						? pairs
						: pairs)
						DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool flag, (int, int)[] pairs)
				{
					foreach (var (first, second) in flag
						? pairs
						: pairs)
					{
						DoThing();
					}
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<UseBracesConsistentlyAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AWrappedHeaderWhoseBodySharesItsLineIsLeftAlone()
	{
		const string source = """
			class C
			{
				void M(bool first, bool second)
				{
					if (first
						|| second) DoThing();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<UseBracesConsistentlyAnalyzer>(
			source
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AWrappedHeaderWhoseBodyStartsItsOwnLineIsWrapped()
	{
		const string source = """
			class C
			{
				void M(bool first, bool second)
				{
					if (first
						|| second)
						DoThing(); // the work
					else
						DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool first, bool second)
				{
					if (first
						|| second)
					{
						DoThing(); // the work
					}
					else
					{
						DoThing();
					}
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<UseBracesConsistentlyAnalyzer>(
			source
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentHeldApartFromTheBodyIsNotSwallowedByTheBrace()
	{
		const string source = """
			class C
			{
				void M(bool first, bool second)
				{
					foreach (int item in new[] { 1, 2 })

						// explain the call

						DoThing(
							item,
							second);
				}

				void DoThing(int value, bool flag)
				{
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<UseBracesConsistentlyAnalyzer>(
			source
		);

		Assert.IsEmpty(result.Diagnostics, "The brace deletes the comment above the body.");
	}

	[TestMethod]
	[DataRow(
		"if (!flag)\nforeach (int item in items)\nHelpers.Call(\n\titem, 2);",
		"if (!flag)\n{\nforeach (int item in items)\n{\nHelpers.Call(\n\titem, 2);\n}\n}",
		2
	)]
	[DataRow(
		"using (a)\nlock (b)\nHelpers.Call(\n\t1, 2);",
		"using (a)\n{\nlock (b)\n{\nHelpers.Call(\n\t1, 2);\n}\n}",
		2
	)]
	[DataRow(
		"using (a)\nusing (b)\nHelpers.Call(\n\t1, 2);",
		"using (a)\nusing (b)\n{\nHelpers.Call(\n\t1, 2);\n}",
		1
	)]
	[DataRow(
		"using (a)\nusing (b)\nusing (a)\nHelpers.Call(\n\t1, 2);",
		"using (a)\nusing (b)\nusing (a)\n{\nHelpers.Call(\n\t1, 2);\n}",
		1
	)]
	public async Task FixAllBracesFlatNestedStatementsWithoutLosingABrace(
		string body,
		string fixedBody,
		int expectedDiagnostics
	)
	{
		string source = Wrap(body);
		string fixedSource = Wrap(fixedBody);

		AnalyzerRunResult result = await RoslynTestHarness.RunFixAllAsync(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.UseBracesConsistently),
			assertFixedPoint: false
		);

		Assert.HasCount(expectedDiagnostics, result.Diagnostics);

		// The nested statements stand flat, so the fix wraps each without a step of indentation.
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AUsingOrFixedBodyOfTheSameKindStaysUnbracedWhileTheInnermostBodyIsBraced()
	{
		const string source = """
			class C
			{
				unsafe void M(int[] first, int[] second)
				{
					fixed (int* a = first)
					fixed (int* b = second)
						Helpers.Call(
							*a, *b);
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				unsafe void M(int[] first, int[] second)
				{
					fixed (int* a = first)
					fixed (int* b = second)
					{
						Helpers.Call(
							*a, *b);
					}
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<UseBracesConsistentlyAnalyzer>(
			source
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AStackOfLocksBracesOnlyTheInnermostBody()
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<UseBracesConsistentlyAnalyzer>(
			Wrap("lock (a)\nlock (b)\n\tHelpers.Call(\n\t\t1, 2);")
		);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(
			Wrap("lock (a)\nlock (b)\n{\n\tHelpers.Call(\n\t\t1, 2);\n}"),
			result
		);
	}

	[TestMethod]
	public async Task AStackOfUsingsWrittenAsTheRuleLeavesItStaysAsWritten()
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<UseBracesConsistentlyAnalyzer>(
			Wrap("using (a)\nusing (b)\n{\n\tHelpers.Call(\n\t\t1, 2);\n}")
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	private static string Wrap(string body)
	{
		string indented = string.Join("\n", body.Split('\n').Select(line => "\t\t" + line));

		return $$"""
			class C
			{
				void M(IDisposable a, IDisposable b, bool flag, int[] items)
				{
			{{indented}}
				}
			}
			""";
	}

	[TestMethod]
	public async Task NestedBracelessStatementsGetBothBracesInOneFixAllPass()
	{
		const string source = """
			class C
			{
				void M(int[] items)
				{
					foreach (int item in items)
						if (item > 0 &&
							item < 5)
							DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int[] items)
				{
					foreach (int item in items)
					{
						if (item > 0 &&
							item < 5)
						{
							DoThing();
						}
					}
				}
			}
			""";

		AnalyzerRunResult fixAll = await RoslynTestHarness.RunFixAllAsync(source);
		AnalyzerRunResult iterative = await RoslynTestHarness.RunIterativelyAsync(source);

		RoslynTestHarness.AssertFixedText(fixedSource, iterative);
		RoslynTestHarness.AssertFixedText(fixedSource, fixAll);
	}

	[TestMethod]
	public async Task ACommentedBlockBracesEveryBranchOfItsChain()
	{
		const string source = """
			class C
			{
				void M(bool isIssue, bool isEdit)
				{
					if (isIssue)
						DoThing();
					else if (isEdit)
					{
						// An empty edit changes nothing,
						// so it is skipped.
						Call(1, 2);
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool isIssue, bool isEdit)
				{
					if (isIssue)
					{
						DoThing();
					}
					else if (isEdit)
					{
						// An empty edit changes nothing,
						// so it is skipped.
						Call(1, 2);
					}
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<UseBracesConsistentlyAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);

		AnalyzerRunResult fixAll = await RoslynTestHarness.RunFixAllAsync(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		RoslynTestHarness.AssertFixedText(fixedSource, fixAll);
	}

	[TestMethod]
	[DynamicData(
		nameof(BraceFamilyFixtures.HeldBlocks),
		typeof(BraceFamilyFixtures),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ABlockHoldingALineBesidesItsStatementBracesTheOtherBranch(
		string shape,
		string block
	)
	{
		string source = BraceFamilyFixtures.Chain(block, bracedFirst: false);
		string fixedSource = BraceFamilyFixtures.Chain(block, bracedFirst: true);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<UseBracesConsistentlyAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(1, result.Diagnostics, shape);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DynamicData(
		nameof(BraceFamilyFixtures.PlainBlocks),
		typeof(BraceFamilyFixtures),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ACommentOnTheStatementsLineLeavesTheChainAlone(string shape, string block)
	{
		string source = BraceFamilyFixtures.Chain(block, bracedFirst: false);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<UseBracesConsistentlyAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.IsEmpty(result.Diagnostics, shape);
	}

	[TestMethod]
	public async Task ACommentAboveAnUnbracedStatementDoesNotMakeItMultiLine()
	{
		const string source = """
			class C
			{
				void M(bool first, bool second)
				{
					if (first)
						// Skip it.
						DoThing();
					else if (second)
						DoThing();
				}
			}
			""";

		// IDE0011 judges the statement alone, so a comment above it adds no braces there either.
		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<UseBracesConsistentlyAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.IsEmpty(result.Diagnostics);
	}
}
