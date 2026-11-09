using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

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

	// A member that writes no access runs with the access its container gives it.
	[TestMethod]
	public async Task AMemberWithoutAccessRunsWithTheAccessItsContainerGivesIt()
	{
		const string source = """
			class C
			{
				private int _first;
				int _second;
				public int Third;
				public int Fourth;
			}

			interface I
			{
				int First { get; }
				public int Second { get; }
				private int Third => 3;
				private int Fourth => 4;
			}

			namespace N
			{
				internal delegate void First();
				delegate void Second();
				public delegate void Third();
				public delegate void Fourth();
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _first;
				int _second;

				public int Third;
				public int Fourth;
			}

			interface I
			{
				int First { get; }
				public int Second { get; }

				private int Third => 3;
				private int Fourth => 4;
			}

			namespace N
			{
				internal delegate void First();
				delegate void Second();

				public delegate void Third();
				public delegate void Fourth();
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
				public virtual int Second() => 2;
				public extern int Third();
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
	public async Task FieldsRunApartByAccess()
	{
		const string source = """
			class C
			{
				public int A;
				public int B;
				private int _c;
				private int _d;
				protected int E;
				protected int F;
				internal int G;
				internal int H;
				protected internal int I;
				protected internal int J;
				private protected int K;
				private protected int L;
			}
			""";

		const string fixedSource = """
			class C
			{
				public int A;
				public int B;

				private int _c;
				private int _d;

				protected int E;
				protected int F;

				internal int G;
				internal int H;

				protected internal int I;
				protected internal int J;

				private protected int K;
				private protected int L;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateModifierGroupsAnalyzer>(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.SeparateModifierGroups)
		);

		Assert.HasCount(5, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AccessJoinsStaticAndReadonlyInTheKey()
	{
		const string source = """
			class C
			{
				public static int A;
				public static int B;
				private static int _c;
				private static int _d;
				private static readonly int s_e;
				private static readonly int s_f;
			}
			""";

		const string fixedSource = """
			class C
			{
				public static int A;
				public static int B;

				private static int _c;
				private static int _d;

				private static readonly int s_e;
				private static readonly int s_f;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateModifierGroupsAnalyzer>(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.SeparateModifierGroups)
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AMemberWithoutAccessKeepsTheRunOfItsDefaultAccess()
	{
		const string source = """
			class C
			{
				private int _a;
				int _b;
				private int _c;
			}

			interface I
			{
				int A { get; }
				public int B { get; }
			}

			internal class D;

			class E;

			class F
			{
				class Nested;
				private class Other;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateModifierGroupsAnalyzer>(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.SeparateModifierGroups)
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	// All parts of a partial type share one access, so a part that writes none has the access another part writes.
	[TestMethod]
	[DataRow(
		"public partial class P;\npartial class P;\ninternal class A;\ninternal class B;",
		"public partial class P;\npartial class P;\n\ninternal class A;\ninternal class B;"
	)]
	[DataRow(
		"partial class P;\npublic partial class P;\ninternal class A;\ninternal class B;",
		"partial class P;\npublic partial class P;\n\ninternal class A;\ninternal class B;"
	)]
	[DataRow(
		"class Outer\n{\n\tpublic partial class P;\n\tpartial class P;\n\tprivate class A;\n\tprivate class B;\n}",
		"class Outer\n{\n\tpublic partial class P;\n\tpartial class P;\n\n\tprivate class A;\n\tprivate class B;\n}"
	)]
	[DataRow(
		"public partial struct P;\npartial struct P;\ninternal class A;\ninternal class B;",
		"public partial struct P;\npartial struct P;\n\ninternal class A;\ninternal class B;"
	)]
	[DataRow(
		"public partial record P;\npartial record P;\ninternal class A;\ninternal class B;",
		"public partial record P;\npartial record P;\n\ninternal class A;\ninternal class B;"
	)]
	[DataRow(
		"public partial class P<T>;\npartial class P<T>;\ninternal class A;\ninternal class B;",
		"public partial class P<T>;\npartial class P<T>;\n\ninternal class A;\ninternal class B;"
	)]
	public async Task APartThatWritesNoAccessTakesTheAccessAnotherPartWrites(
		string source,
		string fixedSource
	)
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateModifierGroupsAnalyzer>(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.SeparateModifierGroups)
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// A part reads the access of the parts beside it from one map of its container, so many parts cost a pass for each.
	[TestMethod]
	[DoNotParallelize]
	public void AContainerOfManyPartsWithNoWrittenAccessIsReadInLinearTime()
	{
		const int parts = 6000;

		StringBuilder source = new();

		for (int index = 0; index < parts; index++)
			source.Append("partial class P").Append(index).Append(";\n");

		LinearCost.AssertWalkEndsWithin(
			milliseconds: 1_000,
			source.ToString(),

			static root =>
			{
				ImmutableArray<SyntaxNode> siblings = StanzaSyntax.ReadSiblings(root);

				ImmutableArray<int> boundaries = BlankLineGrouping.ReadBorderBoundaries(
					borders: BlankLineGrouping.ReadModifierGroupBorders(
						siblings,
						gapOwners: default,
						gathered: null,
						cancellationToken: CancellationToken.None
					)
				);

				Assert.IsEmpty(boundaries);
			}
		);
	}

	// A type of another name or arity is not a part of it, so it keeps the access its own modifiers give.
	[TestMethod]
	[DataRow(
		"public partial class P;\npartial class Q;\ninternal class A;\ninternal class B;",
		"public partial class P;\n\npartial class Q;\ninternal class A;\ninternal class B;"
	)]
	[DataRow(
		"public partial class P<T>;\npartial class P;\ninternal class A;\ninternal class B;",
		"public partial class P<T>;\n\npartial class P;\ninternal class A;\ninternal class B;"
	)]
	public async Task APartialTypeOfAnotherTypeKeepsItsOwnDefaultAccess(
		string source,
		string fixedSource
	)
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateModifierGroupsAnalyzer>(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.SeparateModifierGroups)
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AMemberWithoutAccessRunsApartFromAnotherAccess()
	{
		const string source = """
			class C
			{
				int _a;
				int _b;
				public int P;
				public int Q;
			}
			""";

		const string fixedSource = """
			class C
			{
				int _a;
				int _b;

				public int P;
				public int Q;
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
	public async Task TopLevelStatementsAreNotMembers()
	{
		const string source = """
			var first = 1;
			var second = 2;
			return;
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateModifierGroupsAnalyzer>(
			source,

			new Dictionary<string, ReportDiagnostic>
			{
				[DiagnosticDescriptors.SeparateModifierGroups.Id] = ReportDiagnostic.Warn,
				[DiagnosticDescriptors.SeparateDeclarationRuns.Id] = ReportDiagnostic.Suppress,
				[DiagnosticDescriptors.SeparateMemberGroups.Id] = ReportDiagnostic.Suppress,
			}
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
