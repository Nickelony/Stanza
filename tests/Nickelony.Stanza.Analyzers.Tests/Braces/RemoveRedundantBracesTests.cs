using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Braces;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Braces;

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
			TestRuleSets.StandaloneRulesEnabled,
			new Dictionary<string, string> { ["csharp_prefer_braces"] = "false" }
		);

		// Removing the first branch's braces would hand its else to the inner if.
		// So where the option asks for no braces, only the second branch is unwrapped.
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
	[DataRow("#region Next\n\tvoid N()\n\t{\n\t}\n#endregion")]
	[DataRow("#if DEBUG\n\tvoid N()\n\t{\n\t}\n#endif")]
	[DataRow("#pragma warning disable CS0618\n\tvoid N()\n\t{\n\t}")]
	public async Task ABlockInAMethodBodyIsJudgedByWhatItsOwnBraceLeadsTo(string next)
	{
		// The method body can never lose its braces, so the directive after it cannot follow the loop's block.
		string source = $$"""
			class C
			{
				void M(bool a, bool[] xs)
				{
					foreach (bool x in xs)
					{
						if (a) DoThing();
					}
				}
			{{next}}
			}
			""";

		string control = $$"""
			class C
			{
				void M(bool a, bool[] xs)
				{
					foreach (bool x in xs)
					{
						if (a) DoThing();
					}
				}

				void N()
				{
				}
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rule = TestRuleSets.Only(
			DiagnosticDescriptors.RemoveRedundantBraces
		);

		AnalyzerRunResult reported =
			await RoslynTestHarness.ReportAsync<RemoveRedundantBracesAnalyzer>(control, rule);

		Assert.HasCount(1, reported.Diagnostics);

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<RemoveRedundantBracesAnalyzer>(source, rule);

		Assert.HasCount(1, result.Diagnostics);
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
			TestRuleSets.StandaloneRulesEnabled,
			new Dictionary<string, string> { ["csharp_prefer_braces"] = "false" }
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

	// A stack of usings is the shape the braces rule leaves unbraced, so only the body of the innermost one loses its braces.
	[TestMethod]
	public async Task AStackOfUsingsDropsOnlyTheBracesOfItsInnermostBody()
	{
		const string source = """
			class C
			{
				void M(System.IDisposable a, System.IDisposable b)
				{
					using (a)
					using (b)
					{
						DoThing();
					}

					using (a)
					{
						using (b)
						{
							DoThing();
						}
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(System.IDisposable a, System.IDisposable b)
				{
					using (a)
					using (b)
						DoThing();

					using (a)
					{
						using (b)
							DoThing();
					}
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(2, result.Diagnostics);
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
	public async Task HandAddedBracesOnTheBranchesOfACommentedChainSurvive()
	{
		const string source = """
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

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	[DynamicData(
		nameof(BraceFamilyFixtures.HeldBlocks),
		typeof(BraceFamilyFixtures),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AChainKeepsAllItsBracesWhenABlockHoldsALineBesidesItsStatement(
		string shape,
		string block
	)
	{
		string source = BraceFamilyFixtures.Chain(block, bracedFirst: true);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.IsEmpty(result.Diagnostics, shape);
	}

	// IDE0011 braces a chain with one braced branch, so unbracing the rest would never settle.
	[TestMethod]
	[DataRow("a local declaration", "{|\tint value = 1;|}")]
	[DataRow("a blank line above the statement", "{||\tDoThing();|}")]
	[DataRow("no statement", "{ }")]
	public async Task AChainKeepsAllItsBracesWhenABlockCannotLoseItsOwn(string shape, string block)
	{
		string source = BraceFamilyFixtures.Chain(block, bracedFirst: true);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled,
			new Dictionary<string, string> { ["csharp_prefer_braces"] = "when_multiline" }
		);

		Assert.IsEmpty(result.Diagnostics, shape);
	}

	[TestMethod]
	public async Task AChainKeepsAllItsBracesWhenAnElseCouldAttachToAnInnerIf()
	{
		const string source = """
			class C
			{
				void M(bool first, bool second)
				{
					if (first)
					{
						if (second) DoThing();
					}
					else
					{
						DoThing();
					}
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled,
			new Dictionary<string, string> { ["csharp_prefer_braces"] = "when_multiline" }
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	// ZA0205 braces the view with the multi-line branch, so taking the braces off in the other view would never settle.
	[TestMethod]
	[DataRow("")]
	[DataRow("#define B\n")]
	public async Task AChainKeepsItsBracesWhenAViewCanAddABranch(string view)
	{
		string source =
			view
			+ """
				class C
				{
					void M(bool first)
					{
						if (first)
						{
							DoThing();
						}
				#if B
						else
						{
							DoThing();
							DoThing();
						}
				#endif
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
	[DataRow("")]
	[DataRow("#define B\n")]
	public async Task AChainKeepsItsBracesWhenAViewPicksTheBlockOfABranch(string view)
	{
		string source =
			view
			+ """
				class C
				{
					void M(bool first)
					{
						if (first)
						{
							DoThing();
						}
						else
				#if B
						{
							DoThing();
							DoThing();
						}
				#else
						{
							DoThing();
						}
				#endif
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
	[DataRow("")]
	[DataRow("#define B\n")]
	public async Task AChainAtTheEndOfTheFileKeepsItsBracesWhenAViewCanAddABranch(string view)
	{
		string source =
			view
			+ """
				var items = new[] { 1 };

				if (items.Length > 0)
				{
					Call(items.Length, 1);
				}
				#if B
				else
				{
					Call(items.Length, 1);
					Call(items.Length, 1);
				}
				#endif
				""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	// With B the else is active and each block could lose its braces, so only the directive among the branches keeps them.
	[TestMethod]
	[DataRow("")]
	[DataRow("#define B\n")]
	public async Task AChainKeepsItsBracesWhenAViewCanDropABranch(string view)
	{
		string source =
			view
			+ """
				class C
				{
					void M(bool first)
					{
						if (first)
						{
							DoThing();
						}
				#if B
						else
						{
							DoThing();
						}
				#endif
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
	[DataRow("")]
	[DataRow("#define B\n")]
	public async Task AStatementAfterADisabledElseKeepsItsBracesInEachView(string view)
	{
		string source =
			view
			+ """
				class C
				{
					void M(bool first, bool second)
					{
				#if B
						if (first)
						{
							DoThing();
							DoThing();
						}
						else
				#endif
						if (second)
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

	// Without B the else sits after the statement in the other view, and with B it sits in the branch's own place.
	[TestMethod]
	[DataRow("")]
	[DataRow("#define B\n")]
	public async Task AnElseInTheOtherArmOfAStatementKeepsTheBracesInEachView(string view)
	{
		string source =
			view
			+ """
				class C
				{
					void M(bool first)
					{
						if (first)
						{
							DoThing();
						}
				#if B
						DoThing();
				#else
						else
						{
							DoThing();
							DoThing();
						}
				#endif
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
	[DataRow("", "")]
	[DataRow("B", "")]
	[DataRow("", "C")]
	[DataRow("B", "C")]
	public async Task AnElseInASecondRegionKeepsTheBracesOfTheChainBeforeItInEachView(
		string first,
		string second
	)
	{
		string source =
			(first.Length > 0 ? "#define " + first + "\n" : string.Empty)
			+ (second.Length > 0 ? "#define " + second + "\n" : string.Empty)
			+ """
				class C
				{
					void M(bool one, bool two)
					{
						if (one)
						{
							DoThing();
						}
				#if B
						if (two)
							DoThing();
				#endif
				#if C
						else
						{
							DoThing();
						}
				#endif
					}
				}
				""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	// No view enables an else under a condition that is always false, so it joins no chain.
	[TestMethod]
	[DataRow("false", "")]
	[DataRow("false", "#define B\n")]
	[DataRow("!true", "")]
	[DataRow("(false)", "")]
	[DataRow("B && false", "#define B\n")]
	[DataRow("B && false", "")]
	public async Task AnElseUnderAConditionNoViewMeetsLeavesTheBracesToTheRule(
		string condition,
		string view
	)
	{
		string source =
			view
			+ $$"""
				class C
				{
					void M(bool first)
					{
						if (first)
						{
							DoThing();
						}
				#if {{condition}}
						else
						{
							DoThing();
						}
				#endif
					}
				}
				""";

		string fixedSource =
			view
			+ $$"""
				class C
				{
					void M(bool first)
					{
						if (first)
							DoThing();
				#if {{condition}}
						else
						{
							DoThing();
						}
				#endif
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
	[DataRow("")]
	[DataRow("#define B\n")]
	public async Task AnElseUnderAnArmAfterAnAlwaysTrueConditionLeavesTheBracesToTheRule(
		string view
	)
	{
		string source =
			view
			+ """
				class C
				{
					void M(bool first)
					{
						if (first)
						{
							DoThing();
						}
				#if true
						DoThing();
				#else
						else
						{
							DoThing();
						}
				#endif
					}
				}
				""";

		string fixedSource =
			view
			+ """
				class C
				{
					void M(bool first)
					{
						if (first)
							DoThing();
				#if true
						DoThing();
				#else
						else
						{
							DoThing();
						}
				#endif
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

	// The while of a do loop ends the statement, so a directive there changes no branch.
	[TestMethod]
	[DataRow("")]
	[DataRow("#define B\n")]
	public async Task AConditionalWhileTailOfADoLoopLeavesTheBracesToTheRule(string view)
	{
		string source =
			view
			+ """
				class C
				{
					void M(bool first, bool second)
					{
						do
						{
							DoThing();
						}
				#if B
						while (first);
				#else
						while (second);
				#endif
					}
				}
				""";

		string fixedSource =
			view
			+ """
				class C
				{
					void M(bool first, bool second)
					{
						do
							DoThing();
				#if B
						while (first);
				#else
						while (second);
				#endif
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
	public async Task ADisabledLineThatOnlyStartsLikeAnElseLeavesTheBracesToTheRule()
	{
		const string source = """
			class C
			{
				void M(bool first)
				{
					if (first)
					{
						DoThing();
					}
			#if B
					else_count();
			#endif
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool first)
				{
					if (first)
						DoThing();
			#if B
					else_count();
			#endif
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
	public async Task AnElseOfAnotherStatementInDisabledTextLeavesTheBracesToTheRule()
	{
		const string source = """
			class C
			{
				void M(bool first)
				{
					if (first)
					{
						DoThing();
					}
			#if B
					if (first)
						DoThing();
					else
						DoThing();
			#endif
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool first)
				{
					if (first)
						DoThing();
			#if B
					if (first)
						DoThing();
					else
						DoThing();
			#endif
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
	public async Task ANonConditionalDirectiveAmongTheBranchesLeavesTheBracesToTheRule()
	{
		const string source = """
			class C
			{
				void M(bool first)
				{
					if (first)
					{
						DoThing();
					}
			#pragma warning disable CS0618
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
				void M(bool first)
				{
					if (first)
						DoThing();
			#pragma warning disable CS0618
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
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ALoopCannotTakeAnElseFromDisabledText()
	{
		const string source = """
			class C
			{
				void M(bool first)
				{
					while (first)
					{
						DoThing();
					}
			#if B
					else
					{
						DoThing();
					}
			#endif
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool first)
				{
					while (first)
						DoThing();
			#if B
					else
					{
						DoThing();
					}
			#endif
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
	public async Task AChainThatEndsInAnElseCannotTakeAnotherFromDisabledText()
	{
		const string source = """
			class C
			{
				void M(bool first)
				{
					if (first)
					{
						DoThing();
					}
					else
					{
						DoThing();
					}
			#if B
					else
					{
						DoThing();
					}
			#endif
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool first)
				{
					if (first)
						DoThing();
					else
						DoThing();
			#if B
					else
					{
						DoThing();
					}
			#endif
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("")]
	[DataRow("#define B\n")]
	public async Task AConditionalStatementBesideAChainLeavesItsBracesToTheRule(string view)
	{
		string source =
			view
			+ """
				class C
				{
					void M(bool first)
					{
				#if B
						DoThing();
				#endif
						if (first)
						{
							DoThing();
						}
				#if B
						DoThing();
				#endif
					}
				}
				""";

		string fixedSource =
			view
			+ """
				class C
				{
					void M(bool first)
					{
				#if B
						DoThing();
				#endif
						if (first)
							DoThing();
				#if B
						DoThing();
				#endif
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
	[DynamicData(
		nameof(BraceFamilyFixtures.HeldBlocks),
		typeof(BraceFamilyFixtures),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ABlockHoldingALineBesidesItsStatementDoesNotPinTheChainWhereTheOptionAsksForNone(
		string shape,
		string block
	)
	{
		foreach (string view in new[] { string.Empty, "#define FLAG\n" })
		{
			string source = view + BraceFamilyFixtures.Chain(block, bracedFirst: true);
			string fixedSource = view + BraceFamilyFixtures.Chain(block, bracedFirst: false);

			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
					source,
					TestRuleSets.StandaloneRulesEnabled,
					new Dictionary<string, string> { ["csharp_prefer_braces"] = "false" }
				);

			Assert.HasCount(1, result.Diagnostics, shape + view);
			RoslynTestHarness.AssertFixedText(fixedSource, result);
		}
	}

	[TestMethod]
	public async Task AMultiLineBranchDoesNotPinTheChainWhereTheOptionAsksForNone()
	{
		string source = BraceFamilyFixtures.Chain(
			"{|\tDoThing();|\tDoThing();|}",
			bracedFirst: true
		);

		string fixedSource = BraceFamilyFixtures.Chain(
			"{|\tDoThing();|\tDoThing();|}",
			bracedFirst: false
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled,
			new Dictionary<string, string> { ["csharp_prefer_braces"] = "false" }
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task EachBlockThatCanLoseItsBracesDoesWhereTheOptionAsksForNone()
	{
		string source = BraceFamilyFixtures.Chain("{|\tint value = 1;|}", bracedFirst: true);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled,
			new Dictionary<string, string> { ["csharp_prefer_braces"] = "false" }
		);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(
			BraceFamilyFixtures.Chain("{|\tint value = 1;|}", bracedFirst: false),
			result
		);
	}

	[TestMethod]
	public async Task ACommentOnTheStatementsLineDoesNotKeepTheBraces()
	{
		string source = BraceFamilyFixtures.Chain("{|\tDoThing(); // Done.|}", bracedFirst: true);
		string fixedSource = BraceFamilyFixtures.Chain("\tDoThing(); // Done.", bracedFirst: false);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABlockCommentBeforeTheStatementDoesNotKeepTheBraces()
	{
		string source = BraceFamilyFixtures.Chain(
			"{|\t/* Skip it. */ DoThing();|}",
			bracedFirst: true
		);

		string fixedSource = BraceFamilyFixtures.Chain(
			"\t/* Skip it. */ DoThing();",
			bracedFirst: false
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveRedundantBracesAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// An else belongs to the nearest open if before it, so removing the braces of several blocks on one path
	// can hand the else to an if inside them, though no single removal does.
	[TestMethod]
	[DynamicData(
		nameof(BraceFamilyFixtures.BoundShapes),
		typeof(BraceFamilyFixtures),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task NoFixHandsAnElseToAnotherIf(string shape, string source, string? preferBraces)
	{
		IReadOnlyDictionary<string, ReportDiagnostic> rule = TestRuleSets.Only(
			DiagnosticDescriptors.RemoveRedundantBraces
		);

		Dictionary<string, string>? config = preferBraces is null
			? null
			: new() { ["csharp_prefer_braces"] = preferBraces };

		string owners = ReadElseOwners(source);

		AnalyzerRunResult fixAll = await RoslynTestHarness.RunFixAllAsync(
			source,
			diagnosticOptions: rule,
			analyzerConfigOptions: config,
			assertFixedPoint: false
		);

		AnalyzerRunResult oneByOne = await RoslynTestHarness.RunIterativelyAsync(
			source,
			diagnosticOptions: rule,
			analyzerConfigOptions: config
		);

		string[] alone = await RoslynTestHarness.ApplyEachFixAloneAsync(
			source,
			rule: DiagnosticDescriptors.RemoveRedundantBraces,
			diagnosticOptions: rule,
			analyzerConfigOptions: config
		);

		Assert.AreEqual(owners, ReadElseOwners(fixAll.FixedText), shape + ", Fix All");
		Assert.AreEqual(owners, ReadElseOwners(oneByOne.FixedText), shape + ", one at a time");

		foreach (string text in alone)
			Assert.AreEqual(owners, ReadElseOwners(text), shape + ", a fix alone");
	}

	// The position of each if that owns an else, among all the ifs in order.
	private static string ReadElseOwners(string text)
	{
		IfStatementSyntax[] statements =
		[
			.. CSharpSyntaxTree
				.ParseText(text)
				.GetRoot()
				.DescendantNodes()
				.OfType<IfStatementSyntax>(),
		];

		int[] owners =
		[
			.. Enumerable
				.Range(0, statements.Length)
				.Where(index => statements[index].Else is not null),
		];

		return statements.Length + ": " + string.Join(", ", owners);
	}

	[TestMethod]
	public async Task BracesAroundALoopHoldingAnOpenIfStayWhereTheEnclosingBlockLosesItsOwn()
	{
		const string source = """
			class C
			{
				void M(bool a, bool b, bool[] xs)
				{
					if (a)
					{
						foreach (bool x in xs)
						{
							if (b) DoThing();
						}
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
				void M(bool a, bool b, bool[] xs)
				{
					if (a)
						foreach (bool x in xs)
						{
							if (b) DoThing();
						}
					else
						DoThing();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunFixAllAsync(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.RemoveRedundantBraces),
			new Dictionary<string, string> { ["csharp_prefer_braces"] = "false" }
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheBlankLinesBeforeAClauseKeywordGoWithTheClosingBrace()
	{
		const string source = """
			class C
			{
				void M(bool flag)
				{
					if (flag)
					{
						Call();
					}

					// Otherwise.
					else
					{
						Other();
					}

					do
					{
						Call();
					}

					while (flag);
				}

				void Call() { }

				void Other() { }
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool flag)
				{
					if (flag)
						Call();
					// Otherwise.
					else
						Other();

					do
						Call();
					while (flag);
				}

				void Call() { }

				void Other() { }
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.RemoveRedundantBraces)
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ADirectiveBeforeAClauseKeywordKeepsTheBlankLineAboveIt()
	{
		const string source = """
			class C
			{
				void M(bool flag)
				{
					if (flag)
					{
						Call();
					}

			#if DEBUG
					else
					{
						Other();
					}
			#endif
				}

				void Call() { }

				void Other() { }
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool flag)
				{
					if (flag)
						Call();

			#if DEBUG
					else
					{
						Other();
					}
			#endif
				}

				void Call() { }

				void Other() { }
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunFixAllAsync(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.RemoveRedundantBraces),
			new Dictionary<string, string> { ["csharp_prefer_braces"] = "false" }
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public void TheCostGrowsLinearlyWithTheLengthOfAnElseIfChain()
	{
		static string Chain(int count)
		{
			string branches = string.Concat(
				Enumerable.Repeat("\t\telse if (c0)\n\t\t{\n\t\t\tDoThing();\n\t\t}\n", count)
			);

			return "class C\n{\n\tbool c0;\n\n\tvoid M()\n\t{\n\t\tif (c0)\n\t\t{\n\t\t\tDoThing();\n\t\t}\n"
				+ branches
				+ "\t}\n}\n";
		}

		LinearCost.AssertLinear(
			Chain(150),
			Chain(600),

			static root =>
			{
				SourceText sourceText = root.SyntaxTree.GetText();

				foreach (
					IfStatementSyntax head in root.DescendantNodes()
						.OfType<IfStatementSyntax>()
						.Where(static ifStatement => ifStatement.Parent is not ElseClauseSyntax)
				)
				{
					foreach (BraceTarget target in BraceTargets.GetTargets(head))
						BraceEdits.CreateSafeBraceRemoval(sourceText, target, tabColumns: 4);
				}
			}
		);
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
