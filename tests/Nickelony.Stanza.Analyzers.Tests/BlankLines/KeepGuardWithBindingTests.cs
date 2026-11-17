using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed class KeepGuardWithBindingTests
{
	private static readonly IReadOnlyDictionary<string, ReportDiagnostic> s_on = TestRuleSets.Only(
		DiagnosticDescriptors.KeepGuardWithBinding
	);

	[TestMethod]
	public async Task AOneLineGuardJoinsTheDeclarationOfTheNameItTests()
	{
		const string source = """
			class C
			{
				string M(int id)
				{
					Audit(id);
					string name = Find(id);

					if (name == null) return "";
					return name;
				}

				void Audit(int id)
				{
				}

				string Find(int id) => id.ToString();
			}
			""";

		const string fixedSource = """
			class C
			{
				string M(int id)
				{
					Audit(id);
					string name = Find(id);
					if (name == null) return "";
					return name;
				}

				void Audit(int id)
				{
				}

				string Find(int id) => id.ToString();
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<KeepGuardWithBindingAnalyzer>(
			source,
			s_on
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABracedGuardJoinsAMultiLineBindingADeconstructionAndAnAssignment()
	{
		const string source = """
			class C
			{
				string _name;

				void M(int id)
				{
					string name = Find(
						id);

					if (name.Length == 0)
					{
						return;
					}

					var (first, second) = Read();

					if (second < first) throw new System.InvalidOperationException();

					_name = Find(first);
					name = Find(second);

					if (name is null) return;
					_name = name;
				}

				string Find(int id) => id.ToString();

				(int, int) Read() => (1, 2);
			}
			""";

		const string fixedSource = """
			class C
			{
				string _name;

				void M(int id)
				{
					string name = Find(
						id);
					if (name.Length == 0)
					{
						return;
					}

					var (first, second) = Read();
					if (second < first) throw new System.InvalidOperationException();

					_name = Find(first);
					name = Find(second);
					if (name is null) return;
					_name = name;
				}

				string Find(int id) => id.ToString();

				(int, int) Read() => (1, 2);
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<KeepGuardWithBindingAnalyzer>(
			source,
			s_on
		);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AGuardThatTestsNoBoundNameKeepsTheBlankLine()
	{
		const string source = """
			class C
			{
				int _count;

				void M(int id, C other)
				{
					string name = Find(id);

					if (id < 0) return;

					int _count = 1;

					if (other._count == 0) return;

					Use(name, _count);

					if (name == null) return;
				}

				string Find(int id) => id.ToString();

				void Use(string name, int count)
				{
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<KeepGuardWithBindingAnalyzer>(
			source,
			s_on
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACommentOrADirectiveInTheGapKeepsTheBlankLine()
	{
		const string source = """
			class C
			{
				void M(int id)
				{
					string name = Find(id);

					// A missing name ends the lookup.
					if (name == null) return;

					string title = Find(id); // the title may be missing

					if (title == null) return;

					string label = Find(id);

			#if DEBUG
					if (label == null) return;
			#endif
				}

				string Find(int id) => id.ToString();
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<KeepGuardWithBindingAnalyzer>(
			source,
			s_on
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnIfWithAnElseOrMoreThanAnExitIsNoGuard()
	{
		const string source = """
			class C
			{
				void M(int id)
				{
					string name = Find(id);

					if (name == null) return; else Use(name);

					string title = Find(id);

					if (title == null)
					{
						Use(title);
						return;
					}

					string label = Find(id);

					System.ArgumentNullException.ThrowIfNull(label);
				}

				string Find(int id) => id.ToString();

				void Use(string name)
				{
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<KeepGuardWithBindingAnalyzer>(
			source,
			s_on
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheRuleWinsTheGapOverTheMultiLineAndDeclarationRulesAndSettles()
	{
		const string source = """
			class C
			{
				void M(int id)
				{
					int first = id + 1;
					string name = Find(first);

					if (name is null)
					{
						return;
					}
					Use(name);
				}

				string Find(int id) => id.ToString();

				void Use(string name) { }
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int id)
				{
					int first = id + 1;

					string name = Find(first);
					if (name is null)
					{
						return;
					}

					Use(name);
				}

				string Find(int id) => id.ToString();

				void Use(string name) { }
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.Only(
			DiagnosticDescriptors.KeepGuardWithBinding,
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
			class C
			{
				void M(int id)
				{
					int first = id + 1;

					string name = Find(first);

					if (name is null)
					{
						return;
					}

					Use(name);
				}

				string Find(int id) => id.ToString();

				void Use(string name) { }
			}
			""";

		RoslynTestHarness.AssertFixedText(separatedSource, off);
	}

	[TestMethod]
	public async Task TheRuleWinsTheGapBelowARunOfDeclarationsAndSettles()
	{
		const string source = """
			class C
			{
				int M(int id)
				{
					int first = id + 1;
					int second = id + 2;
					if (second == 0) return first;
					return second;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				int M(int id)
				{
					int first = id + 1;

					int second = id + 2;
					if (second == 0) return first;
					return second;
				}
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.Only(
			DiagnosticDescriptors.KeepGuardWithBinding,
			DiagnosticDescriptors.SeparateDeclarationRuns
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source, rules);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task CheckedBindingsShareGeometryWithoutTheSeparator()
	{
		const string source = """
			class C
			{
				void M()
				{
					object? a = Load();

					object? b = Load();
					if (a is null || b is null) return;
					Use(a, b);
				}
				object? Load() => null;
				void Use(object a, object b) { }
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					object? a = Load();
					object? b = Load();
					if (a is null || b is null) return;
					Use(a, b);
				}
				object? Load() => null;
				void Use(object a, object b) { }
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<KeepGuardWithBindingAnalyzer>(
			source,
			s_on
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("nameof(value).Length == 0", false)]
	[DataRow("((nameof(value))).Length == 0", false)]
	[DataRow("nameof(value).Contains(input)", false)]
	[DataRow("@nameof(value)", true)]
	[DataRow("Check(nameof(value), value)", true)]
	[DataRow("nameof(value) == value.ToString()", true)]
	public async Task NameofDoesNotSupplyTheValueAGuardChecks(string condition, bool joins)
	{
		string source =
			"class C { void M(string input) {\nvar value = new object();\n\nif ("
			+ condition
			+ ") return;\nUse(value);\n} "
			+ (
				condition.Contains("@nameof")
					? "bool @nameof(object value) => value is null; "
					: string.Empty
			)
			+ "bool Check(string name, object value) => value is null; void Use(object value) {} }";

		string fixedSource = joins ? source.Replace("object();\n\n", "object();\n") : source;

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<KeepGuardWithBindingAnalyzer>(
			source,
			s_on
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow(false)]
	[DataRow(true)]
	public async Task AMethodNamedNameofStillReadsItsArgument(bool escaped)
	{
		string call = escaped ? "@nameof(value)" : "nameof(value)";

		string source =
			"class C { void M() {\nvar value = new object();\n\nif ("
			+ call
			+ ") return;\nUse(value);\n} bool @nameof(object value) => value is null; void Use(object value) {} }";

		string fixedSource = source.Replace("object();\n\n", "object();\n");

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<KeepGuardWithBindingAnalyzer>(
			source,
			s_on
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("(value = 1) > 0", false)]
	[DataRow("(((value)) = 1) > 0", false)]
	[DataRow("Overwrite(out value) > 0", false)]
	[DataRow("Overwrite(out (value!)) > 0", false)]
	[DataRow("Overwrite(out value) + value > 0", true)]
	[DataRow("(value += 1) > 0", true)]
	[DataRow("value++ > 0", true)]
	[DataRow("(value = value + 1) > 0", true)]
	[DataRow("Read(in value) > 0", true)]
	[DataRow("Touch(ref value) > 0", true)]
	public async Task WriteOnlyTargetsDoNotSupplyTheValueAGuardChecks(string condition, bool joins)
	{
		string source =
			"class C { void M() {\nint value = 0;\n\nif ("
			+ condition
			+ ") return;\nUse(value);\n} static int Overwrite(out int value) { value = 1; return value; } static int Read(in int value) => value; static int Touch(ref int value) => value++; void Use(int value) {} }";

		string fixedSource = joins ? source.Replace("0;\n\n", "0;\n") : source;

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<KeepGuardWithBindingAnalyzer>(
			source,
			s_on
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("out value.Field")]
	[DataRow("out (value!).Field")]
	[DataRow("ref (value!).Field")]
	public async Task AWritableMemberArgumentStillReadsTheGuardBindingOwner(string argument)
	{
		string source =
			"class ReviewBox { public int Field; } class C { void M() {\nvar value = new ReviewBox();\n\nif (Touch("
			+ argument
			+ ")) return;\n} bool Touch(out int value) { value = 1; return true; } bool Touch(ref int value, int unused = 0) => value > 0; }";

		string fixedSource = source.Replace("ReviewBox();\n\n", "ReviewBox();\n");

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<KeepGuardWithBindingAnalyzer>(
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
	public async Task ExpressionLocalNamesCannotConsumeTheFieldAGuardFollows(
		string condition,
		bool joins
	)
	{
		string source =
			"using System.Linq;\nclass C { int value; void M(object input, int[] xs) {\nvalue = 7;\n\nif ("
			+ condition
			+ ") return;\nUse(value);\n} void Use(int value) {} }";

		string fixedSource = joins ? source.Replace("7;\n\n", "7;\n") : source;

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<KeepGuardWithBindingAnalyzer>(
			source,
			s_on
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ATopLevelGuardJoinsItsBinding()
	{
		const string source = """
			var a = System.Environment.GetCommandLineArgs();

			if (a.Length == 0) return 1;
			return 0;
			""";

		const string fixedSource = """
			var a = System.Environment.GetCommandLineArgs();
			if (a.Length == 0) return 1;
			return 0;
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<KeepGuardWithBindingAnalyzer>(
			source,
			s_on
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task APragmaOverTheGuardReleasesItsGapToTheMultiLineRule()
	{
		const string source = """
			class C
			{
				void M(int x)
				{
			#pragma warning disable ZA0058
					System.GC.KeepAlive(x);
					int y = x + 1;
					if (y == 0)
					{
						return;
					}
			#pragma warning restore ZA0058
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int x)
				{
			#pragma warning disable ZA0058
					System.GC.KeepAlive(x);
					int y = x + 1;

					if (y == 0)
					{
						return;
					}
			#pragma warning restore ZA0058
				}
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepGuardWithBinding,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
		]);

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			fixedSource,
			source,
			new RunOptions(RoslynTestHarness.AllAnalyzers) { DiagnosticOptions = rules }
		);
	}

	[TestMethod]
	[CrossCutting]
	[DataRow(true, "ZA0054")]
	[DataRow(false, "ZA0058")]
	public async Task ATwoStatementBlockIsTheTwoUnitRulesOnlyWhileItIsOn(bool twoUnits, string owner)
	{
		const string source =
			"class C { void M(object input) {\nvar value = input;\n\nif (value is null) return;\n} }";

		const string fixedSource =
			"class C { void M(object input) {\nvar value = input;\nif (value is null) return;\n} }";

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepGuardWithBinding,
			.. twoUnits ? new[] { DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether } : [],
		]);

		AnalyzerRunResult reported = await RoslynTestHarness.ReportAllAsync(source, rules);

		Assert.HasCount(1, reported.Diagnostics);
		Assert.AreEqual(owner, reported.Diagnostics[0].Id);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source, rules);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
