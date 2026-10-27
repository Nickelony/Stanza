using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class SeparateModifierGroupsTests
{
	[TestMethod]
	public async Task FieldsRunApartByStaticAndReadonly()
	{
		const string source = """
			class C
			{
				private static int s_count;
				private static int s_limit;
				private readonly int _id;
				private readonly int _seed;
				private static readonly int s_first;
				private static readonly int s_second;
				private int _attempts;
				private int _failures;
			}
			""";

		const string fixedSource = """
			class C
			{
				private static int s_count;
				private static int s_limit;

				private readonly int _id;
				private readonly int _seed;

				private static readonly int s_first;
				private static readonly int s_second;

				private int _attempts;
				private int _failures;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateModifierGroupsAnalyzer>(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.SeparateModifierGroups)
		);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task StaticMethodsRunApartFromInstanceMethods()
	{
		const string source = """
			class C
			{
				static int First() => 1;
				static int Second() => 2;
				int Third() => 3;
			}
			""";

		const string fixedSource = """
			class C
			{
				static int First() => 1;
				static int Second() => 2;

				int Third() => 3;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateModifierGroupsAnalyzer>(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.SeparateModifierGroups)
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task OtherModifiersKeepTheRun()
	{
		const string source = """
			class C
			{
				public int First() => 1;
				private int Second() => 2;
				protected virtual int Third() => 3;
				public override string ToString() => "C";
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateModifierGroupsAnalyzer>(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.SeparateModifierGroups)
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AGapTheMemberGroupRuleOwnsIsLeftToIt()
	{
		const string source = """
			class C
			{
				static int First => 1;
				static int Second => 2;
				int Third() => 3;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateModifierGroupsAnalyzer>(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.SeparateModifierGroups)
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AGapTheMemberGroupRuleLeavesOffIsReportedHere()
	{
		const string source = """
			class C
			{
				static int First => 1;
				static int Second => 2;
				int Third() => 3;
			}
			""";

		const string fixedSource = """
			class C
			{
				static int First => 1;
				static int Second => 2;

				int Third() => 3;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateModifierGroupsAnalyzer>(
			source,

			new Dictionary<string, ReportDiagnostic>
			{
				[DiagnosticDescriptors.SeparateModifierGroups.Id] = ReportDiagnostic.Warn,
				[DiagnosticDescriptors.SeparateMemberGroups.Id] = ReportDiagnostic.Suppress,
			}
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
