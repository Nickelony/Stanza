using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Analyzers.Tests.Expressions;

[TestClass]
public sealed class ExpressionFamilyTests
{
	private const string WithOperators = """
		class C
		{
			bool M(object value) => value == null;

			bool N(object value) => value != null;
		}
		""";

	private const string WithPatterns = """
		class C
		{
			bool M(object value) => value is null;

			bool N(object value) => value is not null;
		}
		""";

	private static IReadOnlyDictionary<string, ReportDiagnostic> BothHalves { get; } =
		TestRuleSets.Only(
			DiagnosticDescriptors.UsePatternForNullCheck,
			DiagnosticDescriptors.UseEqualityOperatorForNullCheck
		);

	[TestMethod]
	public async Task BothHalvesEnabledLeaveTheChecksToThePatternHalf()
	{
		AnalyzerRunResult operators = await RoslynTestHarness.RunAllAsync(
			WithOperators,
			BothHalves
		);

		Assert.HasCount(2, operators.Diagnostics);

		Assert.IsTrue(
			operators.Diagnostics.All(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.UsePatternForNullCheck.Id
			)
		);

		RoslynTestHarness.AssertFixedText(WithPatterns, operators);

		AnalyzerRunResult patterns = await RoslynTestHarness.RunAllAsync(WithPatterns, BothHalves);

		Assert.IsEmpty(
			patterns.Diagnostics,
			"The operator half stays silent where the pattern half owns the checks."
		);
	}

	[TestMethod]
	public async Task OnlyTheOperatorHalfEnabledOwnsTheChecks()
	{
		AnalyzerRunResult patterns = await RoslynTestHarness.RunAllAsync(
			WithPatterns,
			TestRuleSets.Only(DiagnosticDescriptors.UseEqualityOperatorForNullCheck)
		);

		Assert.HasCount(2, patterns.Diagnostics);
		RoslynTestHarness.AssertFixedText(WithOperators, patterns);

		AnalyzerRunResult operators = await RoslynTestHarness.RunAllAsync(
			WithOperators,
			TestRuleSets.Only(DiagnosticDescriptors.UseEqualityOperatorForNullCheck)
		);

		Assert.IsEmpty(operators.Diagnostics);
	}

	[TestMethod]
	public async Task NeitherHalfEnabledLeavesTheChecksToThePatternHalfThatShipsOn()
	{
		AnalyzerRunResult operators = await RoslynTestHarness.RunAllAsync(WithOperators);

		Assert.HasCount(2, operators.Diagnostics.Where(IsNullCheckRule));
		RoslynTestHarness.AssertFixedText(WithPatterns, operators);

		Assert.IsTrue(
			operators
				.Diagnostics.Where(IsNullCheckRule)
				.All(diagnostic => diagnostic.Id == DiagnosticDescriptors.UsePatternForNullCheck.Id)
		);

		AnalyzerRunResult patterns = await RoslynTestHarness.RunAllAsync(WithPatterns);

		Assert.IsFalse(patterns.Diagnostics.Any(IsNullCheckRule));
	}

	[TestMethod]
	public async Task ThePatternHalfDisabledLeavesNeitherHalfReporting()
	{
		IReadOnlyDictionary<string, ReportDiagnostic> off = TestRuleSets.WithOneOff(
			on: [],
			off: DiagnosticDescriptors.UsePatternForNullCheck
		);

		foreach (string source in new[] { WithOperators, WithPatterns })
		{
			AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source, off);
			Assert.IsFalse(result.Diagnostics.Any(IsNullCheckRule));
		}
	}

	private static bool IsNullCheckRule(Diagnostic diagnostic)
	{
		return diagnostic.Id == DiagnosticDescriptors.UsePatternForNullCheck.Id
			|| diagnostic.Id == DiagnosticDescriptors.UseEqualityOperatorForNullCheck.Id;
	}

	[TestMethod]
	public async Task APragmaOverThePatternHalfHandsTheCheckToTheOperatorHalf()
	{
		const string source = """
			class C
			{
			#pragma warning disable ZA0651
				bool M(object value) => value is null;
			#pragma warning restore ZA0651
			}
			""";

		const string fixedSource = """
			class C
			{
			#pragma warning disable ZA0651
				bool M(object value) => value == null;
			#pragma warning restore ZA0651
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source, BothHalves);

		Assert.AreEqual(
			DiagnosticDescriptors.UseEqualityOperatorForNullCheck.Id,
			result.Diagnostics.Single().Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
