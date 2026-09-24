using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Bodies;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class BodyFamilyTests
{
	[TestMethod]
	public async Task FixAllAppliesOnlyTheActionItWasAskedFor()
	{
		const string source = """
			class C
			{
				int Pick(bool first) =>
					first ? 1
					: 2;

				int GetValue()
				{
					return _value;
				}

				private int _value;
			}
			""";

		const string fixedSource = """
			class C
			{
				int Pick(bool first)
				{
					return first ? 1
					: 2;
				}

				int GetValue()
				{
					return _value;
				}

				private int _value;
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.Only(
			DiagnosticDescriptors.UseBlockBodyForMultiLineMember,
			DiagnosticDescriptors.UseExpressionBodyForSingleLineMember
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunFixAllAsync(
			source,
			rules,
			assertFixedPoint: false
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task BothStatementRulesPinOnePolicy()
	{
		const string source = """
			class C
			{
				int Short()
				{
					return _value;
				}

				int Long(int value) =>
					_value
					+ value;

				private int _value;
			}
			""";

		const string fixedSource = """
			class C
			{
				int Short() => _value;

				int Long(int value)
				{
					return _value
					+ value;
				}

				private int _value;
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> bothRules = TestRuleSets.Only(
			DiagnosticDescriptors.UseBlockBodyForMultiLineMember,
			DiagnosticDescriptors.UseExpressionBodyForSingleLineMember
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source, bothRules);

		Assert.HasCount(
			1,

			result.Diagnostics.Where(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.UseExpressionBodyForSingleLineMember.Id
			)
		);

		Assert.HasCount(
			1,

			result.Diagnostics.Where(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.UseBlockBodyForMultiLineMember.Id
			)
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheTwoRulesCoverDifferentHalvesOfTheMembers()
	{
		const string source = """
			class C
			{
				int Build(int value) =>
					value
					+ 1;

				int Value =>
					Build(1)
					+ 2;
			}
			""";

		const string fixedSource = """
			class C
			{
				int Build(int value)
				{
					return value
					+ 1;
				}

				int Value
				{
					get
					{
						return Build(1)
					+ 2;
					}
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.Only(
				DiagnosticDescriptors.UseBlockBodyForMultiLineMember,
				DiagnosticDescriptors.UseBlockBodyForMultiLineValueMember
			)
		);

		// Each member is reported exactly once, by the rule that owns its shape.
		Assert.HasCount(2, result.Diagnostics);

		Assert.HasCount(
			1,

			result.Diagnostics.Where(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.UseBlockBodyForMultiLineMember.Id
			)
		);

		Assert.HasCount(
			1,

			result.Diagnostics.Where(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.UseBlockBodyForMultiLineValueMember.Id
			)
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task BothValueRulesPinOnePolicy()
	{
		const string source = """
			class C
			{
				int Short
				{
					get { return 1; }
				}

				int Long =>
					1
					+ 2;
			}
			""";

		const string fixedSource = """
			class C
			{
				int Short => 1;

				int Long
				{
					get
					{
						return 1
					+ 2;
					}
				}
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> bothRules = TestRuleSets.Only(
			DiagnosticDescriptors.UseBlockBodyForMultiLineValueMember,
			DiagnosticDescriptors.UseExpressionBodyForSingleLineValueMember
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source, bothRules);

		Assert.HasCount(
			1,

			result.Diagnostics.Where(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.UseExpressionBodyForSingleLineValueMember.Id
			)
		);

		Assert.HasCount(
			1,

			result.Diagnostics.Where(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.UseBlockBodyForMultiLineValueMember.Id
			)
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheTwoCollapseRulesCoverDifferentHalves()
	{
		const string source = """
			class C
			{
				int Build()
				{
					return 1;
				}

				int Value
				{
					get { return 1; }
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				int Build() => 1;

				int Value => 1;
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> bothHalves = TestRuleSets.Only(
			DiagnosticDescriptors.UseExpressionBodyForSingleLineMember,
			DiagnosticDescriptors.UseExpressionBodyForSingleLineValueMember
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source, bothHalves);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ALambdaKeepsItsArrow()
	{
		// Each lambda has the shape the rule reports on a member.
		const string leadingArrow = """
			class C
			{
				Func<int, int> Add = value
					=> value + 1;
			}
			""";

		const string trailingArrow = """
			class C
			{
				Func<int, int> Add = value =>
					value + 1;
			}
			""";

		AnalyzerRunResult trailing =
			await RoslynTestHarness.RunAsync<KeepArrowOnSignatureLineAnalyzer>(
				leadingArrow,
				TestRuleSets.Only(DiagnosticDescriptors.KeepArrowOnSignatureLine)
			);

		AnalyzerRunResult leading =
			await RoslynTestHarness.RunAsync<PutArrowOnLineAfterSignatureAnalyzer>(
				trailingArrow,
				TestRuleSets.GapRulesEnabled
			);

		Assert.IsEmpty(trailing.Diagnostics);
		Assert.IsEmpty(leading.Diagnostics);
	}
}
