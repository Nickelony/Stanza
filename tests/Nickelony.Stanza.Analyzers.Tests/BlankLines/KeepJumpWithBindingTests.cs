using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed class KeepJumpWithBindingTests
{
	private static readonly IReadOnlyDictionary<string, ReportDiagnostic> s_on = TestRuleSets.Only(
		DiagnosticDescriptors.KeepJumpWithBinding
	);

	[TestMethod]
	public async Task ALoneBindingJoinsTheReturnOrTheThrowThatUsesIt()
	{
		const string source = """
			class C
			{
				int M(int[] values)
				{
					Check(values);

					int total = Sum(values);

					return total * 2;
				}

				void N(string message)
				{
					var error = new System.InvalidOperationException(message);

					throw error;
				}

				void Check(int[] values) { }

				int Sum(int[] values) => values.Length;
			}
			""";

		const string fixedSource = """
			class C
			{
				int M(int[] values)
				{
					Check(values);

					int total = Sum(values);
					return total * 2;
				}

				void N(string message)
				{
					var error = new System.InvalidOperationException(message);
					throw error;
				}

				void Check(int[] values) { }

				int Sum(int[] values) => values.Length;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<KeepJumpWithBindingAnalyzer>(
			source,
			s_on
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABindingInsideAStanzaOrAJumpThatUsesNoBoundNameKeepsTheBlankLine()
	{
		const string source = """
			class C
			{
				int M(int[] values)
				{
					Check(values);
					int total = Sum(values);

					return total;
				}

				int N(int[] values, int other)
				{
					Check(values);

					int total = Sum(values);

					return other;
				}

				void O(int[] values)
				{
					Check(values);

					int total = Sum(values);

					return;
				}

				int P(int[] values)
				{
					Check(values);

					int total = Sum(
						values);

					return total;
				}

				void Check(int[] values) { }

				int Sum(int[] values) => values.Length;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<KeepJumpWithBindingAnalyzer>(
			source,
			s_on
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACommentInTheGapKeepsTheBlankLine()
	{
		const string source = """
			class C
			{
				int M(int[] values)
				{
					Check(values);

					int total = Sum(values);

					// The caller doubles it.
					return total;
				}

				int N(int[] values)
				{
					Check(values);

					int total = Sum(values); // the sum of all values

					return total;
				}

				void Check(int[] values) { }

				int Sum(int[] values) => values.Length;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<KeepJumpWithBindingAnalyzer>(
			source,
			s_on
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheJumpRuleKeepsTheJumpApartFromAStanzaAndTheRuleSettlesBesideIt()
	{
		const string source = """
			class C
			{
				int M(int[] values)
				{
					Check(values);
					int total = Sum(values);
					return total;
				}

				int N(int[] values)
				{
					Check(values);

					int total = Sum(values);

					return total;
				}

				void Check(int[] values) { }

				int Sum(int[] values) => values.Length;
			}
			""";

		const string fixedSource = """
			class C
			{
				int M(int[] values)
				{
					Check(values);
					int total = Sum(values);

					return total;
				}

				int N(int[] values)
				{
					Check(values);

					int total = Sum(values);
					return total;
				}

				void Check(int[] values) { }

				int Sum(int[] values) => values.Length;
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.Only(
			DiagnosticDescriptors.KeepJumpWithBinding,
			DiagnosticDescriptors.SeparateJumpStatement,
			DiagnosticDescriptors.SeparateDeclarationRuns
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source, rules);

		RoslynTestHarness.AssertFixedText(fixedSource, result);

		AnalyzerRunResult settled = await RoslynTestHarness.ReportAllAsync(fixedSource, rules);

		Assert.IsEmpty(settled.Diagnostics);
	}

	[TestMethod]
	public async Task ABlankLineAboveThatAKeptRunRemovesLeavesTheBindingInItsStanza()
	{
		const string source = """
			class C
			{
				int M(int start)
				{
					int first = start + 1;

					int total = first + 1;

					return total;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				int M(int start)
				{
					int first = start + 1;
					int total = first + 1;

					return total;
				}
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.Only(
			DiagnosticDescriptors.KeepJumpWithBinding,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			DiagnosticDescriptors.SeparateDeclarationRuns
		);

		AnalyzerRunResult reported = await RoslynTestHarness.ReportAllAsync(source, rules);

		Assert.IsFalse(
			reported.Diagnostics.Any(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.KeepJumpWithBinding.Id
			),

			"The kept run takes the binding into its stanza."
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source, rules);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ClaimingEveryGapOfManyLoneBindingsCostsInStepWithTheirCount()
	{
		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepJumpWithBinding,
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			DiagnosticDescriptors.SeparateDeclarationRuns,
		]);

		(int small, int large) = await LinearCost.AssertQueryLinearAsync(
			rule: StanzaRules.KeepJumpWithBinding,
			buildSource: pairs => BuildPairs(pairs, "int value{0} = x;\nreturn value{0};", "\n\n"),
			units: 50,
			rules,
			query: container => LinearCost.CountClaims(container, StanzaRules.KeepJumpWithBinding)
		);

		// Each binding claims the gap above the jump that returns it.
		Assert.AreEqual(50, small);
		Assert.AreEqual(200, large);
	}

	[TestMethod]
	[CrossCutting]
	public async Task ReadingTheOuterBoundaryOfEveryGuardedBindingCostsInStepWithTheirCount()
	{
		(int small, int large) = await LinearCost.AssertQueryLinearAsync(
			rule: StanzaRules.KeepJumpWithBinding,

			buildSource: pairs =>
				BuildPairs(pairs, "var value{0} = Get();\nif (value{0} is null) return 0;", "\n"),

			units: 25,

			rules: TestRuleSets.OnSuppressingAllOthers([
				DiagnosticDescriptors.KeepGuardWithBinding,
				DiagnosticDescriptors.KeepJumpWithBinding,
			]),

			query: container =>
			{
				int moved = 0;

				for (int boundary = 1; boundary < container.Siblings.Length; boundary += 2)
				{
					moved += BlankLineKeptPairs.ReadOuterBoundaries(container, [boundary]).Count(outer => outer != boundary);

					moved += BlankLineKeptPairs
						.ReadOuterBoundaries(container with { ReadsRuns = true }, [boundary])
						.Count(outer => outer != boundary);
				}

				return moved;
			}
		);

		// In both reading modes each gap inside a guarded binding moves above it, but the first, which opens the block.
		Assert.AreEqual(48, small);
		Assert.AreEqual(198, large);
	}

	[TestMethod]
	[CrossCutting]
	public async Task ClaimingEveryGapBesideManyReceiverGroupsCostsInStepWithTheirCount()
	{
		(int small, int large) = await LinearCost.AssertQueryLinearAsync(
			rule: StanzaRules.KeepJumpWithBinding,

			buildSource: pairs =>
				BuildPairs(
					pairs,
					"db.First();\ndb.Second();\nint result{0} = 1;\n\nreturn result{0};",
					"\n"
				),

			units: 25,

			rules: TestRuleSets.OnSuppressingAllOthers([
				DiagnosticDescriptors.SeparateReceiverGroups,
				DiagnosticDescriptors.KeepJumpWithBinding,
			]),

			query: container => LinearCost.CountClaims(container, StanzaRules.KeepJumpWithBinding),
			options: new Dictionary<string, string> { ["stanza_receiver_roots"] = "db" }
		);

		// Each result claims the gap above the jump that returns it.
		Assert.AreEqual(25, small);
		Assert.AreEqual(100, large);
	}

	[TestMethod]
	[DataRow("nameof(value)", false)]
	[DataRow("((nameof(value)))", false)]
	[DataRow("nameof(value).ToString()", false)]
	[DataRow("@nameof(value)", true)]
	[DataRow("nameof(value) + value", true)]
	[DataRow("value.ToString() ?? nameof(value)", true)]
	public async Task NameofDoesNotSupplyTheValueAJumpConsumes(string value, bool joins)
	{
		string source =
			"class C { string M() {\nvar value = new object();\n\nreturn "
			+ value
			+ ";\n} "
			+ (
				value.Contains("@nameof")
					? "string @nameof(object value) => value.ToString()!; "
					: string.Empty
			)
			+ "}";

		string fixedSource = joins ? source.Replace("object();\n\n", "object();\n") : source;

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<KeepJumpWithBindingAnalyzer>(
			source,
			s_on
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task ReadingTheInitializationGapsOncePerStatementCostsInStepWithTheirCount()
	{
		(int small, int large) = await LinearCost.AssertQueryLinearAsync(
			rule: StanzaRules.KeepJumpWithBinding,

			buildSource: pairs =>
				BuildPairs(pairs, "var item{0} = new C();\n\nitem{0}.Call();", "\n\n"),

			units: 25,

			rules: TestRuleSets.OnSuppressingAllOthers([
				DiagnosticDescriptors.KeepJumpWithBinding,
				DiagnosticDescriptors.KeepInitializationWithCalls,
			]),

			query: container =>
			{
				for (int index = 1; index < container.Siblings.Length; index++)
					_ = BlankLineKeptPairs.GetInitializationGaps(container);

				return BlankLineKeptPairs.GetInitializationGaps(container).Count;
			}
		);

		// Each initialization keeps the gap above its call.
		Assert.AreEqual(25, small);
		Assert.AreEqual(100, large);
	}

	// The method holds the pairs, each the pair's text with {0} as its index.
	private static string BuildPairs(int pairs, string pair, string separator)
	{
		string statements = string.Join(
			separator,

			Enumerable
				.Range(0, pairs)
				.Select(index => string.Format(CultureInfo.InvariantCulture, pair, index))
		);

		return "class C { int M(int x, C db) {\n"
			+ statements
			+ "\nreturn 0;\n} object? Get() => null; void First() {} void Second() {} void Call() {} }";
	}

	[TestMethod]
	[DataRow("(value = 1)", false)]
	[DataRow("(((value)) = 1)", false)]
	[DataRow("((value, _) = (1, 2)).Item1", false)]
	[DataRow("Overwrite(out value)", false)]
	[DataRow("Overwrite(out value!)", false)]
	[DataRow("Overwrite(out value) + value", true)]
	[DataRow("(value += 1)", true)]
	[DataRow("value++", true)]
	[DataRow("(value = value + 1)", true)]
	[DataRow("Read(in value)", true)]
	[DataRow("Touch(ref value)", true)]
	public async Task WriteOnlyTargetsDoNotSupplyTheValueAJumpConsumes(string value, bool joins)
	{
		string source =
			"class C { int M() {\nint value = 0;\n\nreturn "
			+ value
			+ ";\n} static int Overwrite(out int value) { value = 1; return value; } static int Read(in int value) => value; static int Touch(ref int value) => value++; }";

		string fixedSource = joins ? source.Replace("0;\n\n", "0;\n") : source;

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<KeepJumpWithBindingAnalyzer>(
			source,
			s_on
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("System.Array.Exists(xs, value => value > 0)", false)]
	[DataRow("System.Array.Exists(xs, other => other > value)", true)]
	[DataRow("(from value in xs select value).Any()", false)]
	[DataRow("(from other in xs select other + value).Any()", true)]
	[DataRow("input switch { int value => value > 0, _ => false }", false)]
	[DataRow(
		"((System.Func<object, bool>)(candidate => candidate is int value && value > 0))(input)",
		false
	)]
	[DataRow("System.Array.Exists(xs, value => value > 0) || value > 0", true)]
	public async Task ExpressionLocalNamesCannotConsumeTheFieldAJumpFollows(
		string value,
		bool joins
	)
	{
		string source =
			"using System.Linq;\nclass C { int value; bool M(object input, int[] xs) {\nvalue = 7;\n\nreturn "
			+ value
			+ ";\n} }";

		string fixedSource = joins ? source.Replace("7;\n\n", "7;\n") : source;

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<KeepJumpWithBindingAnalyzer>(
			source,
			s_on
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task APragmaOverTheJumpLeavesItsBlankLineWritten()
	{
		const string source = """
			class C
			{
				int M(int x)
				{
			#pragma warning disable ZA0060
					System.GC.KeepAlive(x);

					int y = x + 1;

					return y;
			#pragma warning restore ZA0060
				}
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepJumpWithBinding,
			DiagnosticDescriptors.SeparateJumpStatement,
		]);

		AnalyzerRunResult reported = await RoslynTestHarness.ReportAllAsync(source, rules);

		Assert.IsEmpty(reported.Diagnostics);

		AnalyzerRunResult unsilenced = await RoslynTestHarness.ReportAllAsync(
			source.Replace("#pragma warning disable ZA0060", string.Empty),
			rules
		);

		Assert.HasCount(1, unsilenced.Diagnostics);
	}
}
