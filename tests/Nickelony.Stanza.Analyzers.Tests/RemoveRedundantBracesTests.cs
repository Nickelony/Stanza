using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Braces;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class RemoveRedundantBracesTests
{
	[TestMethod]
	public async Task SingleLineLoopBodiesDropTheirBraces()
	{
		const string source = """
			class C
			{
				void M(int[] items)
				{
					for (int index = 0; index < items.Length; index += 1)
					{
						DoThing();
					}

					foreach (int item in items)
					{
						DoThing();
					}

					lock (items)
					{
						DoThing();
					}

					using (System.IO.MemoryStream stream = new())
					{
						DoThing();
					}

					do
					{
						DoThing();
					}
					while (false);

					while (items.Length > 0)
					{
						DoThing();
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int[] items)
				{
					for (int index = 0; index < items.Length; index += 1)
						DoThing();

					foreach (int item in items)
						DoThing();

					lock (items)
						DoThing();

					using (System.IO.MemoryStream stream = new())
						DoThing();

					do
						DoThing();
					while (false);

					while (items.Length > 0)
						DoThing();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(6, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task RedundantBracesAreRemoved()
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

		const string fixedSource = """
			class C
			{
				void M(int value)
				{
					if (value > 0)
						DoThing();
					else
						DoThing();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(2, result.Diagnostics);

		// Anchors on the opening brace with the closing brace as a second location.
		// The descriptor is tagged unnecessary, so the editor fades the anchor span,
		// and a span across both braces would fade the body too.
		TextSpan span = result.Diagnostics[0].Location.SourceSpan;

		Assert.AreEqual('{', source[span.Start]);
		Assert.AreEqual(1, span.Length);

		Assert.HasCount(1, result.Diagnostics[0].AdditionalLocations);

		TextSpan closingSpan = result.Diagnostics[0].AdditionalLocations[0].SourceSpan;

		Assert.AreEqual('}', source[closingSpan.Start]);
		Assert.AreEqual(1, closingSpan.Length);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnElseThatHoldsOnlyAnIfKeepsTheChainBraced()
	{
		const string source = """
			class C
			{
				void M(bool first, bool second)
				{
					if (first)
					{
						DoThing();
					}
					else
					{
						if (second) DoThing();
					}
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.RemoveRedundantBraces)
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task BracesAreKeptWhenAnInnerIfCouldSwallowTheElse()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					if (value > 0)
					{
						if (value > 1) DoThing();
					}
					else if (value < 0)
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
						if (value > 1) DoThing();
					}
					else if (value < 0)
						DoThing();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		// Removing the first branch's braces would hand its else to the inner if.
		// So only the second branch is unwrapped.
		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task BracesAreKeptWhenAnInnerIfCouldSwallowAnElseInAnInactiveDirective()
	{
		const string source = """
			class C
			{
				void M(bool a, bool b)
				{
					if (a)
					{
						if (b) DoThing();
					}
			#if DEBUG
					else
						DoThing();
			#endif
				}
			}
			""";

		const string control = """
			class C
			{
				void M(bool a, bool b)
				{
					if (a)
					{
						if (b) DoThing();
					}
				}
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rule = TestRuleSets.Only(
			DiagnosticDescriptors.RemoveRedundantBraces
		);

		AnalyzerRunResult reported =
			await RoslynTestHarness.ReportAsync<RemoveRedundantBracesAnalyzer>(control, rule);

		Assert.HasCount(
			1,
			reported.Diagnostics,
			"Without the directive the braces go, so the refusal below proves the directive."
		);

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<RemoveRedundantBracesAnalyzer>(source, rule);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task BracesAreKeptWhenALabeledInnerIfCouldSwallowTheElse()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					if (value > 0)
					{
						Inner: if (value > 1) DoThing();
					}
					else if (value < 0)
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
						Inner: if (value > 1) DoThing();
					}
					else if (value < 0)
						DoThing();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		// A label is no barrier: the first branch would still hand its else to the if behind it.
		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task BracesAreKeptOnMultiLineBodiesAndInlineBlocks()
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
				}

				void N(int value)
				{
					if (value > 0) { DoThing(); }
				}

				void P(int value)
				{
					if (value > 0)
					{
					}
				}
			}
			""";

		// A body with more than one statement is multi-line,
		// an inline block cannot be unwrapped without re-indenting it,
		// and an empty body would leave a bare if behind.
		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheRedundantBraceRuleIsOffWhenTheFilePrefersBraces()
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

		AnalyzerRunResult prefersBraces =
			await RoslynTestHarness.ReportAsync<RemoveRedundantBracesAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.RemoveRedundantBraces),

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["csharp_prefer_braces"] = "true:warning",
				}
			);

		Assert.IsEmpty(prefersBraces.Diagnostics);

		AnalyzerRunResult prefersMultiLine =
			await RoslynTestHarness.ReportAsync<RemoveRedundantBracesAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(2, prefersMultiLine.Diagnostics);
	}

	[TestMethod]
	public async Task BracesAreKeptWhenTheLastBranchCouldSwallowAnEnclosingElse()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					if (value > 0)
						if (value > 1)
							DoThing();
						else
						{
							if (value > 2) DoThing();
						}
					else
						DoThing();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		// The braces are the only thing that keeps the enclosing else with the outer if,
		// so the rule leaves the whole chain alone.
		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task BracesAreKeptOnALoopBodyThatCouldSwallowAnEnclosingElse()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					if (value > 0)
						while (value > 1)
						{
							if (value > 2) DoThing();
						}
					else
						DoThing();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task BracesAreKeptWhenAnElseIfChainWithoutAnElseCouldSwallowTheEnclosingElse()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					if (value > 0)
					{
						if (value > 1) DoThing(); else if (value > 2) DoThing();
					}
					else
						DoThing();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	// A `do` body ends before its `while`, so the else that follows the loop cannot reach the `if` inside it.
	[TestMethod]
	public async Task TheBracesAroundADoLoopComeOffBecauseTheWhileShieldsTheElse()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					if (value > 0)
					{
						do if (value > 1) DoThing(); while (value > 2);
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
						do if (value > 1) DoThing(); while (value > 2);
					else
						DoThing();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheBracesAroundALoopBlockComeOffBecauseTheBlockShieldsTheElse()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					if (value > 0)
					{
						while (value > 1) { if (value > 2) DoThing(); }
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
						while (value > 1) { if (value > 2) DoThing(); }
					else
						DoThing();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task BracesAreKeptOnADeconstructionLoopThatCouldSwallowAnEnclosingElse()
	{
		const string source = """
			class C
			{
				void M(int value, (int, int)[] pairs)
				{
					if (value > 0)
					{
						foreach (var (first, second) in pairs)
							if (value > 1) DoThing();
					}
					else
						DoThing();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheBracesOfALastBranchStillComeOffWhileNoElseFollows()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					if (value > 0)
					{
						if (value > 1) DoThing();
					}

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
						if (value > 1) DoThing();

					DoThing();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		// Only the two brace lines are deleted, so every statement keeps the indentation it had.
		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABodyHoldingADeclarationOrALabelKeepsItsBraces()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					if (value > 0)
					{
						int local = value;
					}

					if (value > 1)
					{
						Above: Foo();
					}

					if (value > 2)
					{
						void Inner() { }
					}
				}

				void Foo() { }
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ABodyHoldingOnlyAnEmptyStatementKeepsItsBraces()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					if (value > 0)
					{
						;
					}
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ABlockWithADirectiveAboveItsOpeningBraceKeepsItsBraces()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					if (value > 0)
			#region Body
					{
						DoThing();
					}
			#endregion
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AFixedBodyDropsItsBraces()
	{
		const string source = """
			class C
			{
				unsafe void M(int[] items)
				{
					fixed (int* first = items)
					{
						DoThing();
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				unsafe void M(int[] items)
				{
					fixed (int* first = items)
						DoThing();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABodyThatIsNotIndentedDeeperThanItsBracesKeepsThem()
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
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ABodyThatIsOnlyCharacterWiseDeeperKeepsItsBraces()
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
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task RedundantBracesOfATopLevelIfAtTheEndOfTheFileAreRemoved()
	{
		const string source = """
			var items = new[] { 1 };

			if (items.Length > 0)
			{
				Call(items.Length, 1);
			}
			""";

		const string fixedSource = """
			var items = new[] { 1 };

			if (items.Length > 0)
				Call(items.Length, 1);
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
