using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Limits;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Limits;

[TestClass]
public sealed class ShortenLongParameterListTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> Rule { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.ShortenLongParameterList);

	// Each declaration takes four parameters, one more than a limit of three allows.
	public static IEnumerable<object[]> Declarations { get; } =
	[
		["a method", "public void Run(int a, int b, int c, int d) { }"],
		["a static method", "public static void Run(int a, int b, int c, int d) { }"],
		["a generic method", "public void Run<T>(T a, int b, int c, int d) { }"],
		["an abstract method", "public abstract void Run(int a, int b, int c, int d);"],
		["an expression-bodied method", "public int Run(int a, int b, int c, int d) => a;"],

		[
			"a method with optional parameters",
			"public void Run(int a, int b = 1, int c = 2, int d = 3) { }",
		],

		[
			"a method with a params array",
			"public void Run(int a, int b, int c, params int[] d) { }",
		],

		[
			"a method with out parameters",
			"public void Run(int a, out int b, out int c, ref int d) { b = 0; c = 0; }",
		],

		[
			"a local function",
			"public void Outer() { void Run(int a, int b, int c, int d) { } Run(1, 2, 3, 4); }",
		],

		["a delegate", "public delegate void Run(int a, int b, int c, int d);"],
	];

	[TestMethod]
	[DynamicData(
		nameof(Declarations),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ADeclarationPastTheLimitIsReportedOnceAtItsParameterList(
		string name,
		string declaration
	)
	{
		string source = $"internal abstract class Sample\n{{\n\t{declaration}\n}}\n";

		AnalyzerRunResult result = await ReportAsync(source, "3");

		Assert.HasCount(1, result.Diagnostics, name);
		Assert.StartsWith("(", LimitFixtures.Text(result.Diagnostics[0]), name);
		Assert.EndsWith(")", LimitFixtures.Text(result.Diagnostics[0]), name);

		Assert.AreEqual(
			2,
			result.Diagnostics[0].Location.GetLineSpan().StartLinePosition.Line,
			name
		);
	}

	[TestMethod]
	[DynamicData(
		nameof(Declarations),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ADeclarationAtTheLimitPasses(string name, string declaration)
	{
		string source = $"internal abstract class Sample\n{{\n\t{declaration}\n}}\n";

		AnalyzerRunResult result = await ReportAsync(source, "4");

		Assert.IsEmpty(result.Diagnostics, name);
	}

	[TestMethod]
	public async Task SevenParametersPassAndAnEighthIsReported()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Seven(int a, int b, int c, int d, int e, int f, int g) { }

				internal static void Eight(int a, int b, int c, int d, int e, int f, int g, int h) { }
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source);

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual(4, result.Diagnostics[0].Location.GetLineSpan().StartLinePosition.Line);
	}

	[TestMethod]
	public async Task TheThisParameterOfAnExtensionMethodCounts()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Run(this string a, int b, int c, int d) { }
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source, "3");

		Assert.HasCount(1, result.Diagnostics);
	}

	[TestMethod]
	public async Task AListOverSeveralLinesIsReportedOnceAsAWhole()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Run(
					int a,
					int b,
					int c,
					int d
				) { }
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source, "3");

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual(2, result.Diagnostics[0].Location.GetLineSpan().StartLinePosition.Line);
		Assert.AreEqual(7, result.Diagnostics[0].Location.GetLineSpan().EndLinePosition.Line);
	}

	// A constructor is left out, since dependency injection makes it long.
	public static IEnumerable<object[]> LeftOut { get; } =
	[
		[
			"a constructor",
			"internal sealed class Sample { public Sample(int a, int b, int c, int d) { } }",
		],

		["a record", "internal sealed record Sample(int A, int B, int C, int D);"],
		["a primary constructor", "internal sealed class Sample(int a, int b, int c, int d) { }"],

		[
			"a lambda",
			"internal sealed class Sample { System.Func<int, int, int, int, int> F = (a, b, c, d) => a; }",
		],

		[
			"an anonymous method",
			"internal sealed class Sample { System.Func<int, int, int, int, int> F = delegate (int a, int b, int c, int d) { return a; }; }",
		],

		[
			"an indexer",
			"internal sealed class Sample { public int this[int a, int b, int c, int d] => a; }",
		],

		[
			"an extern method",
			"internal sealed class Sample { [System.Runtime.InteropServices.DllImport(\"kernel32.dll\")] private static extern bool Call(int a, int b, int c, int d); }",
		],

		[
			"a method with fewer",
			"internal sealed class Sample { public void Run(int a, int b, int c) { } }",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(LeftOut),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ADeclarationThatIsNotAMethodIsLeftOut(string name, string source)
	{
		AnalyzerRunResult result = await ReportAsync(source + "\n", "3");
		Assert.IsEmpty(result.Diagnostics, name);
	}

	[TestMethod]
	public async Task AnOverrideTakesTheSignatureOfTheDeclarationItOverrides()
	{
		const string source = """
			internal class Base
			{
				public virtual void Run(int a, int b, int c, int d) { }
			}

			internal sealed class Derived : Base
			{
				public override void Run(int a, int b, int c, int d) { }
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source, "3");

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual(2, result.Diagnostics[0].Location.GetLineSpan().StartLinePosition.Line);
	}

	[TestMethod]
	public async Task AnExplicitInterfaceImplementationTakesTheSignatureOfItsInterface()
	{
		const string source = """
			internal interface IRunner
			{
				void Run(int a, int b, int c, int d);
			}

			internal sealed class Runner : IRunner
			{
				void IRunner.Run(int a, int b, int c, int d) { }
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source, "3");

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual(2, result.Diagnostics[0].Location.GetLineSpan().StartLinePosition.Line);
	}

	[TestMethod]
	public async Task AnImplicitInterfaceImplementationIsReportedBesideItsInterface()
	{
		const string source = """
			internal interface IRunner
			{
				void Run(int a, int b, int c, int d);
			}

			internal sealed class Runner : IRunner
			{
				public void Run(int a, int b, int c, int d) { }
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source, "3");

		Assert.HasCount(2, result.Diagnostics);
	}

	[TestMethod]
	public async Task EachDeclarationIsJudgedOnItsOwn()
	{
		const string source = """
			internal static class Sample
			{
				internal static void First(int a, int b, int c, int d) { }

				internal static void Second(int a, int b, int c, int d) { }

				internal static void Third(int a) { }
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source, "3");

		Assert.HasCount(2, result.Diagnostics);
	}

	[TestMethod]
	[DataRow("1", 3)]
	[DataRow("2", 2)]
	[DataRow("3", 1)]
	[DataRow("9", 0)]
	[DataRow("0", 0)]
	[DataRow("-1", 1)]
	[DataRow("1001", 1)]
	[DataRow("many", 1)]
	[DataRow("", 1)]
	public async Task TheLimitComesFromTheOptionAndZeroTurnsTheRuleOff(string limit, int reports)
	{
		const string source = """
			internal static class Sample
			{
				internal static void Nine(int a, int b, int c, int d, int e, int f, int g, int h, int i) { }

				internal static void Three(int a, int b, int c) { }

				internal static void Two(int a, int b) { }
			}
			""";

		// A missing or invalid limit reads as the default of seven, which reports the one list of nine parameters.
		AnalyzerRunResult result = await ReportAsync(source, limit);

		Assert.HasCount(reports, result.Diagnostics, $"A limit of '{limit}'.");
	}

	[TestMethod]
	public async Task AParameterUnderAConditionalDirectiveCountsInTheViewThatKeepsIt()
	{
		const string source = """
			internal static class Sample
			{
				internal static void Run(
					int a,
					int b,
					int c
			#if EXTRA
					, int d
			#endif
				) { }
			}
			""";

		Assert.HasCount(1, (await ReportAsync("#define EXTRA\n" + source, "3")).Diagnostics);
		Assert.IsEmpty((await ReportAsync(source, "3")).Diagnostics);
	}

	[TestMethod]
	public async Task APragmaSilencesTheListItCovers()
	{
		const string source = """
			internal static class Sample
			{
			#pragma warning disable ZA1003
				internal static void Run(int a, int b, int c, int d) { }
			#pragma warning restore ZA1003
			}
			""";

		AnalyzerRunResult result = await ReportAsync(source, "3");

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public void TheCostGrowsLinearlyWithTheNumberOfDeclarations()
	{
		static string Many(int count)
		{
			string methods = string.Concat(
				Enumerable
					.Range(0, count)
					.Select(i => $"\tinternal static void M{i}(int a, int b, int c, int d) {{ }}\n")
			);

			return $"internal static class Sample\n{{\n{methods}}}\n";
		}

		AssertLinear(Many(5_000), Many(20_000));
	}

	[TestMethod]
	public void TheCostGrowsLinearlyWithTheLengthOfAList()
	{
		static string Long(int count)
		{
			string parameters = string.Join(
				", ",
				Enumerable.Range(0, count).Select(i => $"int p{i}")
			);

			return $"internal static class Sample\n{{\n\tinternal static void M({parameters}) {{ }}\n}}\n";
		}

		AssertLinear(Long(5_000), Long(20_000));
	}

	[TestMethod]
	public async Task AVeryLongListIsOneDiagnostic()
	{
		string parameters = string.Join(", ", Enumerable.Range(0, 3_000).Select(i => $"int p{i}"));

		AnalyzerRunResult result = await ReportAsync(
			$"internal static class Sample\n{{\n\tinternal static void M({parameters}) {{ }}\n}}\n"
		);

		Assert.HasCount(1, result.Diagnostics);
	}

	private static void AssertLinear(string small, string large)
	{
		LinearCost.AssertLinear(
			small,
			large,
			root => ParameterLists.CollectLongLists(root, 3, default)
		);
	}

	private static Task<AnalyzerRunResult> ReportAsync(string source, string? limit = null)
	{
		return RoslynTestHarness.ReportWithLimitAsync<ShortenLongParameterListAnalyzer>(
			source,
			diagnosticOptions: Rule,
			key: "stanza_max_parameters",
			limit
		);
	}
}
