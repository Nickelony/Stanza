using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed class KeepAccumulatorWithLoopTests
{
	private static readonly IReadOnlyDictionary<string, ReportDiagnostic> s_on = TestRuleSets.Only(
		DiagnosticDescriptors.KeepAccumulatorWithLoop
	);

	[TestMethod]
	public async Task ASeedJoinsTheLoopThatAssignsIt()
	{
		const string source = """
			class C
			{
				decimal M(decimal[] prices)
				{
					Check(prices);
					decimal total = 0m;

					foreach (decimal price in prices)
						total += price;
					return total;
				}

				void Check(decimal[] prices)
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				decimal M(decimal[] prices)
				{
					Check(prices);
					decimal total = 0m;
					foreach (decimal price in prices)
						total += price;
					return total;
				}

				void Check(decimal[] prices)
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepAccumulatorWithLoopAnalyzer>(source, s_on);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task EachLoopAndEachBuilderCallBuildsTheSeed()
	{
		const string source = """
			using System.Collections.Generic;
			using System.Text;

			class C
			{
				string _text;

				void M(int[] values)
				{
					var builder = new StringBuilder();

					for (int i = 0; i < values.Length; i++)
					{
						builder.Append(values[i]);
					}

					List<int> list = [];

					while (list.Count < 3)
					{
						list.Add(1);
					}

					var queue = new Queue<int>();

					do
					{
						queue.Enqueue(1);
					}
					while (queue.Count < 3);

					int count = 0;

					foreach (int value in values)
					{
						if (value > 0)
							count++;
					}

					_text = string.Empty;

					foreach (int value in values)
						_text = _text + value;

					Dictionary<int, int> seen = new();

					foreach (int value in values)
						seen.TryAdd(value, count);

					var (low, high) = (0, 0);

					foreach (int value in values)
						(low, high) = (value, value);
				}
			}
			""";

		const string fixedSource = """
			using System.Collections.Generic;
			using System.Text;

			class C
			{
				string _text;

				void M(int[] values)
				{
					var builder = new StringBuilder();
					for (int i = 0; i < values.Length; i++)
					{
						builder.Append(values[i]);
					}

					List<int> list = [];
					while (list.Count < 3)
					{
						list.Add(1);
					}

					var queue = new Queue<int>();
					do
					{
						queue.Enqueue(1);
					}
					while (queue.Count < 3);

					int count = 0;
					foreach (int value in values)
					{
						if (value > 0)
							count++;
					}

					_text = string.Empty;
					foreach (int value in values)
						_text = _text + value;

					Dictionary<int, int> seen = new();
					foreach (int value in values)
						seen.TryAdd(value, count);

					var (low, high) = (0, 0);

					foreach (int value in values)
						(low, high) = (value, value);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepAccumulatorWithLoopAnalyzer>(source, s_on);

		Assert.HasCount(6, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ALoopThatOnlyCallsAnotherMethodOnTheObjectKeepsTheBlankLine()
	{
		const string source = """
			class Client
			{
				public void Send(string message)
				{
				}
			}

			class C
			{
				void M(string[] messages)
				{
					var client = new Client();

					foreach (string message in messages)
					{
						client.Send(message);
					}

					var names = new System.Collections.Generic.List<string>();

					foreach (string message in messages)
					{
						names.Contains(message);
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepAccumulatorWithLoopAnalyzer>(source, s_on);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ASeedThatReadsAVariableOrSpansLinesOrBindsTwoNamesIsNoSeed()
	{
		const string source = """
			class C
			{
				int _start;

				void M(int[] values, int start, int capacity)
				{
					int total = start;

					foreach (int value in values)
						total += value;

					var list = new System.Collections.Generic.List<int>(capacity);

					foreach (int value in values)
						list.Add(value);

					int sum = _start;

					foreach (int value in values)
						sum += value;

					int wrapped =
						0;

					foreach (int value in values)
						wrapped += value;

					int low = 0, high = 0;

					foreach (int value in values)
						low += value;

					int other = 0;

					foreach (int value in values)
						total += value;

					System.GC.KeepAlive(high + other + sum + wrapped);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepAccumulatorWithLoopAnalyzer>(source, s_on);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACommentOrADirectiveInTheGapKeepsTheBlankLine()
	{
		const string source = """
			class C
			{
				void M(int[] values)
				{
					int total = 0;

					// Sum the positive values only.
					foreach (int value in values)
						total += value;

					int count = 0; // every value counts

					foreach (int value in values)
						count++;

					int last = 0;

			#if DEBUG
					foreach (int value in values)
						last = value;
			#endif
					System.GC.KeepAlive(total + count + last);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepAccumulatorWithLoopAnalyzer>(source, s_on);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheRuleWinsTheGapOverTheMultiLineAndDeclarationRulesAndSettles()
	{
		const string source = """
			using System.Collections.Generic;

			class C
			{
				List<string> M(string[] items)
				{
					int skipped = 0;
					var names = new List<string>();

					foreach (string item in items)
					{
						names.Add(item);
					}
					return names;
				}
			}
			""";

		const string fixedSource = """
			using System.Collections.Generic;

			class C
			{
				List<string> M(string[] items)
				{
					int skipped = 0;

					var names = new List<string>();
					foreach (string item in items)
					{
						names.Add(item);
					}

					return names;
				}
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.Only(
			DiagnosticDescriptors.KeepAccumulatorWithLoop,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateDeclarationRuns
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source, rules);

		RoslynTestHarness.AssertFixedText(fixedSource, result);

		AnalyzerRunResult settled = await RoslynTestHarness.ReportAllAsync(fixedSource, rules);

		Assert.IsEmpty(settled.Diagnostics);

		// With the rule off, the separating rules own the gap again and write its blank line back.
		AnalyzerRunResult off = await RoslynTestHarness.RunIterativelyAsync(
			fixedSource,

			TestRuleSets.Only(
				DiagnosticDescriptors.SeparateMultiLineConstructs,
				DiagnosticDescriptors.SeparateDeclarationRuns
			)
		);

		const string separatedSource = """
			using System.Collections.Generic;

			class C
			{
				List<string> M(string[] items)
				{
					int skipped = 0;

					var names = new List<string>();

					foreach (string item in items)
					{
						names.Add(item);
					}

					return names;
				}
			}
			""";

		RoslynTestHarness.AssertFixedText(separatedSource, off);
	}

	[TestMethod]
	public async Task ATopLevelSeedJoinsItsLoop()
	{
		const string source = """
			int total = 0;

			foreach (int value in new[] { 1, 2 })
				total += value;
			System.GC.KeepAlive(total);
			""";

		const string fixedSource = """
			int total = 0;
			foreach (int value in new[] { 1, 2 })
				total += value;
			System.GC.KeepAlive(total);
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepAccumulatorWithLoopAnalyzer>(source, s_on);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ASeparatingGapMovesAboveTheAccumulatorPair()
	{
		const string source = """
			class C
			{
				void M(int[] values)
				{
					System.GC.KeepAlive(values);
					int total = 0;

					foreach (int value in values)
						total += value;
					System.GC.KeepAlive(total);
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int[] values)
				{
					System.GC.KeepAlive(values);

					int total = 0;
					foreach (int value in values)
						total += value;

					System.GC.KeepAlive(total);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.KeepAccumulatorWithLoop,
				DiagnosticDescriptors.SeparateMultiLineConstructs
			)
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);

		AnalyzerRunResult settled = await RoslynTestHarness.ReportAllAsync(
			fixedSource,

			TestRuleSets.Only(
				DiagnosticDescriptors.KeepAccumulatorWithLoop,
				DiagnosticDescriptors.SeparateMultiLineConstructs
			)
		);

		Assert.IsEmpty(settled.Diagnostics);
	}

	[TestMethod]
	public async Task AnOpeningAccumulatorPairHasNoUpperGap()
	{
		const string source = """
			class C
			{
				void M(int[] values)
				{
					int total = 0;

					foreach (int value in values)
						total += value;
					System.GC.KeepAlive(total);
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int[] values)
				{
					int total = 0;
					foreach (int value in values)
						total += value;

					System.GC.KeepAlive(total);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.KeepAccumulatorWithLoop,
				DiagnosticDescriptors.SeparateMultiLineConstructs
			)
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow(true, "ZA0001")]
	[DataRow(false, "ZA0002")]
	public async Task AnAccumulatorTransferHasOneSeparatingOwner(bool multiLine, string owner)
	{
		const string source = """
			class C
			{
				void M(int[] values)
				{
					int unread = 0;
					int total = 0;
					foreach (int value in values)
						total += value;
					System.GC.KeepAlive(total + unread);
				}
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepAccumulatorWithLoop,
			DiagnosticDescriptors.SeparateDeclarationRuns,
		]);

		if (multiLine)
			rules[DiagnosticDescriptors.SeparateMultiLineConstructs.Id] = ReportDiagnostic.Warn;

		AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(source, rules);

		Diagnostic[] upper =
		[
			.. result.Diagnostics.Where(diagnostic =>
				diagnostic.Location.SourceSpan.Start
				== source.IndexOf("int total", System.StringComparison.Ordinal)
			),
		];

		Assert.HasCount(1, upper);
		Assert.AreEqual(owner, upper[0].Id);
	}

	[TestMethod]
	[DataRow("System.Func<int> deferred = () => total++;", false)]
	[DataRow("System.Func<int> deferred = delegate { return ++total; };", false)]
	[DataRow("int Deferred() { return total++; }", false)]
	[DataRow("values[total] = item;", false)]
	[DataRow("if (item > 0) { total += item; }", true)]
	[DataRow("foreach (int nested in values) { total += nested; }", true)]
	[DataRow("System.Func<int> deferred = () => total++; total += item;", true)]
	public async Task OnlyExecutingUpdatesToTheSeededBindingJoin(string body, bool joins)
	{
		string source =
			"class C { void M(int[] values) {\nint total = 0;\n\nforeach (int item in values) { "
			+ body
			+ " }\n} }";

		string fixedSource = joins ? source.Replace("0;\n\n", "0;\n") : source;

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepAccumulatorWithLoopAnalyzer>(source, s_on);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("int total = 10; total++;")]
	[DataRow("int total = 10; total += item;")]
	public async Task AFieldSeedIsNotBuiltByAShadowingLoopLocal(string body)
	{
		string source =
			"class C { int total; void M(int[] values) {\ntotal = 0;\n\nforeach (int item in values) { "
			+ body
			+ " }\n} }";

		string fixedSource = source;

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepAccumulatorWithLoopAnalyzer>(source, s_on);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("(total)++;")]
	[DataRow("++((total));")]
	[DataRow("((total)) += item;")]
	[DataRow("((total)) = item;")]
	[DataRow("((total!), _) = (item, item);")]
	public async Task TransparentWriteTargetsStillBuildTheSeededBinding(string body)
	{
		string source =
			"class C { void M(int[] values) {\nint total = 0;\n\nforeach (int item in values) { "
			+ body
			+ " }\n} }";

		string fixedSource = source.Replace("0;\n\n", "0;\n");

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepAccumulatorWithLoopAnalyzer>(source, s_on);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("((items!)).Add(item);", true)]
	[DataRow("((items!))[0] = item;", false)]
	[DataRow("Use((items!).Count);", false)]
	public async Task TransparentBuilderReceiversDoNotTurnMemberUsesIntoBindingWrites(
		string body,
		bool joins
	)
	{
		string source =
			"class C { void M(int[] values) {\nvar items = new System.Collections.Generic.List<int>();\n\nforeach (int item in values) { "
			+ body
			+ " }\n} void Use(int value) {} }";

		string fixedSource = joins ? source.Replace("List<int>();\n\n", "List<int>();\n") : source;

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepAccumulatorWithLoopAnalyzer>(source, s_on);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task AWrappedAccumulatorUpdateKeepsItsGapAheadOfMultiLineSeparation()
	{
		const string source =
			"class C { void M(int[] values) {\nint total = 0;\nforeach (int item in values)\n(total)++;\n} }";

		const string fixedSource = source;

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.OnSuppressingAllOthers([
				DiagnosticDescriptors.KeepAccumulatorWithLoop,
				DiagnosticDescriptors.SeparateMultiLineConstructs,
			])
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AParenthesizedAssignmentCanSeedTheActualAccumulator()
	{
		const string source =
			"class C { void M(int[] values) {\nint total;\n((total)) = 0;\n\nforeach (int item in values) total += item;\n} }";

		const string fixedSource =
			"class C { void M(int[] values) {\nint total;\n((total)) = 0;\nforeach (int item in values) total += item;\n} }";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepAccumulatorWithLoopAnalyzer>(source, s_on);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("var sum = 0;\nvar count = 0;\n\n", "sum += x;\ncount++;")]
	[DataRow("var sum = 0;\n\nvar count = 0;\n\n", "sum += x;\ncount++;")]
	[DataRow("var count = 0;\nvar seen = new HashSet<int>();\n\n", "count++;\nseen.Add(x);")]
	public async Task EverySeedTheLoopBuildsJoinsItsGroup(string seeds, string body)
	{
		string source =
			"using System.Collections.Generic;\nclass C { void M(int[] xs) {\n"
			+ seeds
			+ "foreach (var x in xs)\n{\n"
			+ body
			+ "\n}\n} }";

		string fixedSource = source.Replace("\n\n", "\n");

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepAccumulatorWithLoopAnalyzer>(source, s_on);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ClaimingEveryGapOfManyAccumulatorsCostsInStepWithTheirCount()
	{
		(int small, int large) = await LinearCost.AssertQueryLinearAsync(
			rule: StanzaRules.KeepAccumulatorWithLoop,

			buildSource: groups =>
				"class C { void M(int[] xs) {\n"
				+ string.Join(
					"\n",

					Enumerable
						.Range(0, groups)
						.Select(index => $"int total{index} = 0;\nforeach (int x in xs) total{index} += x;")
				)
				+ "\n} }",

			units: 50,
			rules: s_on,
			query: container => LinearCost.CountClaims(container, StanzaRules.KeepAccumulatorWithLoop)
		);

		// Each accumulator claims the gap above its loop.
		Assert.AreEqual(50, small);
		Assert.AreEqual(200, large);
	}

	[TestMethod]
	[CrossCutting]
	public async Task TheMultiLineRuleSeparatesTheWholeSeedGroupInEveryFixOrder()
	{
		const string source = """
			class C
			{
				void M(int[] xs)
				{
					System.GC.KeepAlive(xs);
					var sum = 0;
					var count = 0;

					foreach (var x in xs)
					{
						sum += x;
						count++;
					}
					System.GC.KeepAlive(sum + count);
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int[] xs)
				{
					System.GC.KeepAlive(xs);

					var sum = 0;
					var count = 0;
					foreach (var x in xs)
					{
						sum += x;
						count++;
					}

					System.GC.KeepAlive(sum + count);
				}
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepAccumulatorWithLoop,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
		]);

		AnalyzerRunResult reported = await RoslynTestHarness.ReportAllAsync(source, rules);

		Assert.IsFalse(
			reported.Diagnostics.Any(diagnostic =>
				diagnostic.Location.SourceSpan.Start
				== source.IndexOf("var count", System.StringComparison.Ordinal)
			),

			"No separating rule asks for a blank line between two seeds of one loop."
		);

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			fixedSource,
			source,
			new RunOptions(RoslynTestHarness.AllAnalyzers) { DiagnosticOptions = rules }
		);
	}

	[TestMethod]
	[CrossCutting]
	public async Task APragmaOverTheLoopReleasesTheGapToTheMultiLineRule()
	{
		const string source = """
			class C
			{
				void M(int[] xs)
				{
			#pragma warning disable ZA0063
					System.GC.KeepAlive(xs);
					var sum = 0;
					foreach (var x in xs)
					{
						sum += x;
					}
			#pragma warning restore ZA0063
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int[] xs)
				{
			#pragma warning disable ZA0063
					System.GC.KeepAlive(xs);
					var sum = 0;

					foreach (var x in xs)
					{
						sum += x;
					}
			#pragma warning restore ZA0063
				}
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepAccumulatorWithLoop,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
		]);

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			fixedSource,
			source,
			new RunOptions(RoslynTestHarness.AllAnalyzers) { DiagnosticOptions = rules }
		);
	}
}
