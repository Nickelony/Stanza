using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class SeparateMemberGroupsTests
{
	[TestMethod]
	public async Task ALoneFieldBeforeAMemberRunIsSeparatedOnBothSides()
	{
		const string source = """
			class C
			{
				public void Setup() => DoThing();
				private int _count;
				public int First => _count;
				public int Second => _count;
				void DoThing() => _count = 0;
			}
			""";

		const string fixedSource = """
			class C
			{
				public void Setup() => DoThing();

				private int _count;

				public int First => _count;
				public int Second => _count;

				void DoThing() => _count = 0;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateMemberGroupsAnalyzer>(
			source
		);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ALoneFieldAlreadySeparatedFromTheRunStaysWithItsNeighbor()
	{
		const string source = """
			class C
			{
				public void Setup() => DoThing();
				private int _count;

				public int First => _count;
				public int Second => _count;
				void DoThing() => _count = 0;
			}
			""";

		const string fixedSource = """
			class C
			{
				public void Setup() => DoThing();
				private int _count;

				public int First => _count;
				public int Second => _count;

				void DoThing() => _count = 0;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateMemberGroupsAnalyzer>(
			source
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task MemberRunsAreOnlySeparatedFromANeighboringMember()
	{
		const string source = """
			class C
			{
				public int Value => 1;
				void M() => DoThing();
			}

			class D
			{
				int M() => 1;
				int N() => 2;
			}
			""";

		// A property and a method are different kinds,
		// and the two methods of the second type are one run that fills its whole container,
		// so neither boundary has a neighboring unit to separate.
		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateMemberGroupsAnalyzer>(
			source
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task RunsOfIndexersOperatorsAndMethodsAreSeparated()
	{
		const string source = """
			class First
			{
				public int this[int index] => index;
				public int this[string key] => 0;
				public void Method() { }
			}

			class Second
			{
				public static Second operator +(Second left, Second right) => left;
				public static Second operator -(Second left, Second right) => left;
				public void Method() { }
			}
			""";

		const string fixedSource = """
			class First
			{
				public int this[int index] => index;
				public int this[string key] => 0;

				public void Method() { }
			}

			class Second
			{
				public static Second operator +(Second left, Second right) => left;
				public static Second operator -(Second left, Second right) => left;

				public void Method() { }
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateMemberGroupsAnalyzer>(
			source
		);

		Assert.HasCount(2, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnIndexerIsSeparatedFromAPropertyRunOnEitherSide()
	{
		const string source = """
			class First
			{
				public int Count { get; set; }
				public int Total { get; set; }
				public int this[int index] => index;
			}

			class Second
			{
				public int this[int index] => index;
				public int Count { get; set; }
				public int Total { get; set; }
			}
			""";

		const string fixedSource = """
			class First
			{
				public int Count { get; set; }
				public int Total { get; set; }

				public int this[int index] => index;
			}

			class Second
			{
				public int this[int index] => index;

				public int Count { get; set; }
				public int Total { get; set; }
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateMemberGroupsAnalyzer>(
			source
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task OnlyADisabledDeclarationRunRuleHandsItsBoundaryToTheMemberGroupRule()
	{
		const string source = """
			class C
			{
				void M() { }
				int first = 1;
				long second = 2;
			}
			""";

		// The control: while the declaration run rule is on, it keeps the boundary.
		AnalyzerRunResult kept = await RoslynTestHarness.ReportAsync<SeparateMemberGroupsAnalyzer>(
			source
		);

		Assert.IsEmpty(kept.Diagnostics);

		var declarationRunsOff = new Dictionary<string, ReportDiagnostic>
		{
			[DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns.Id] =
				ReportDiagnostic.Suppress,
		};

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateMemberGroupsAnalyzer>(
				source,
				declarationRunsOff
			);

		Assert.HasCount(1, result.Diagnostics);

		// The run of fields starts after the method,
		// so the boundary it needs is the one before the first field,
		// which is the fixture's line 4 counted from one.
		Assert.HasCount(1, result.DiagnosticLines);
		Assert.AreEqual(3, result.DiagnosticLines[0]);
	}

	[TestMethod]
	public async Task BothHalvesOfAPartialPropertyJoinThePropertyGroup()
	{
		RoslynTestHarness.RequireRoslynWithCSharp14();

		const string source = """
			partial class C
			{
				public partial int Count { get; }
				public partial int Count => 1;
				public void Reset() { }
			}
			""";

		const string fixedSource = """
			partial class C
			{
				public partial int Count { get; }
				public partial int Count => 1;

				public void Reset() { }
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateMemberGroupsAnalyzer>(
			source
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARegionMarkerSplitsAPropertyRunInEitherFixOrder()
	{
		const string source = """
			class C
			{
				public int Total() => 0;
				public int First { get; set; }
				#region Second
				public int Second { get; set; }
				#endregion Second
			}
			""";

		const string fixedSource = """
			class C
			{
				public int Total() => 0;
				public int First { get; set; }

				#region Second

				public int Second { get; set; }

				#endregion Second
			}
			""";

		AnalyzerRunResult alone = await RoslynTestHarness.RunAsync<SeparateMemberGroupsAnalyzer>(
			source
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source);

		Assert.IsEmpty(alone.Diagnostics, "The marker ends the run, so the rule reports nothing.");
		RoslynTestHarness.AssertFixedText(fixedSource, result);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(expected: fixedSource, source);
	}

	[TestMethod]
	public async Task AnEnumMemberRunIsLeftAlone()
	{
		const string source = """
			enum Color
			{
				Red,
				Green,
				Blue,
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateMemberGroupsAnalyzer>(
			source
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	[DataRow("public C(int a) { }", "public C(string a) { }")]
	[DataRow("public delegate void First();", "public delegate void Second();")]
	[DataRow(
		"public event System.EventHandler First { add { } remove { } }",
		"public event System.EventHandler Second { add { } remove { } }"
	)]
	[DataRow(
		"public static implicit operator int(C value) => 0;",
		"public static implicit operator string(C value) => string.Empty;"
	)]
	public async Task ARunOfMembersOfOneKindIsSeparatedFromTheMethodAfterIt(
		string first,
		string second
	)
	{
		string source = $$"""
			class C
			{
				{{first}}
				{{second}}
				public void Method() { }
			}
			""";

		string fixedSource = $$"""
			class C
			{
				{{first}}
				{{second}}

				public void Method() { }
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateMemberGroupsAnalyzer>(
			source
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AConversionOperatorAndAnOperatorFormOneRun()
	{
		const string source = """
			class C
			{
				public static implicit operator int(C value) => 0;
				public static C operator +(C left, C right) => left;
				public void Method() { }
			}
			""";

		const string fixedSource = """
			class C
			{
				public static implicit operator int(C value) => 0;
				public static C operator +(C left, C right) => left;

				public void Method() { }
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateMemberGroupsAnalyzer>(
			source
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ADestructorEndsARunOfMethodsAndIsNotOneItself()
	{
		const string source = """
			class C
			{
				public void First() { }
				public void Second() { }
				~C() { }
			}
			""";

		const string fixedSource = """
			class C
			{
				public void First() { }
				public void Second() { }

				~C() { }
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateMemberGroupsAnalyzer>(
			source
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ADestructorAboveARunOfMethodsIsSeparatedFromIt()
	{
		const string source = """
			class C
			{
				~C() { }
				public void First() { }
				public void Second() { }
			}
			""";

		const string fixedSource = """
			class C
			{
				~C() { }

				public void First() { }
				public void Second() { }
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateMemberGroupsAnalyzer>(
			source
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task APrimaryConstructorClassSeparatesItsMemberRuns()
	{
		const string source = """
			class C(int count)
			{
				public int First => count;
				public int Second => count;
				public void Reset() { }
			}
			""";

		const string fixedSource = """
			class C(int count)
			{
				public int First => count;
				public int Second => count;

				public void Reset() { }
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateMemberGroupsAnalyzer>(
			source
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
