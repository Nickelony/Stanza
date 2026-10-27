using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Analyzers.Regions;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class SeparateDeclarationAndInvocationRunsTests
{
	[TestMethod]
	public async Task AVarRunAndAnExplicitlyTypedRunAreSeparated()
	{
		const string source = """
			class C
			{
				void M()
				{
					var first = 1;
					var second = 2;
					int third = 3;
					int fourth = 4;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					var first = 1;
					var second = 2;

					int third = 3;
					int fourth = 4;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(source);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task PreprocessorBoundariesAreLeftUntouched()
	{
		const string source = """
			class C
			{
				void M()
				{
					var value = 1;
			#if DEBUG
					DoThing();
					DoThing();
			#endif
					return;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(source);

		Assert.IsEmpty(result.Diagnostics);
	}

	// The comment starts on the line below the label, so it must not set the indent step.
	private const string BlockCommentBelowTheLabel = """

					/*
					 * The end of the switch.
					 */
		""";

	public static IEnumerable<object[]> TextsBelowTheCaseLabel { get; } =
	[
		["nothing below the label", ""],
		["a block comment below the label", BlockCommentBelowTheLabel],
	];

	[TestMethod]
	[DynamicData(
		nameof(TextsBelowTheCaseLabel),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task UnitsThatFollowACaseLabelOnItsLineAreIndentedUnderIt(
		string name,
		string below
	)
	{
		string source = $$"""
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1: var a = 1; var b = 2; Call(a, b); break;{{below}}
					}
				}

				void Call(int first, int second)
				{
				}
			}
			""";

		string fixedSource = $$"""
			class C
			{
				void M(int value)
				{
					switch (value)
					{
						case 1: var a = 1; var b = 2;

							Call(a, b); break;{{below}}
					}
				}

				void Call(int first, int second)
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(source);

		Assert.HasCount(1, result.Diagnostics, name);
		RoslynTestHarness.AssertFixedText(fixedSource, result, name);
	}

	[TestMethod]
	public async Task UnitsThatFollowABraceOnItsLineAreIndentedUnderIt()
	{
		const string source = """
			class C
			{
				void M()
				{
					if (true) { var a = 1; var b = 2; Call(a, b); }
				}

				void Call(int first, int second)
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					if (true) { var a = 1; var b = 2;

						Call(a, b); }
				}

				void Call(int first, int second)
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(source);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task RepeatedInvocationGroupGetsSeparated()
	{
		const string source = """
			class C
			{
				void M()
				{
					var value = 1;
					DoThing();
					DoThing();
					return;
				}

				void DoThing()
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					var value = 1;

					DoThing();
					DoThing();

					return;
				}

				void DoThing()
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(source);

		Assert.HasCount(2, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AttachedCommentMovesWithTheNextRun()
	{
		const string source = """
			class C
			{
				void M()
				{
					var value = 1;
					// first call
					DoThing();
					DoThing();
					return;
				}

				void DoThing()
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					var value = 1;

					// first call
					DoThing();
					DoThing();

					return;
				}

				void DoThing()
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(source);

		Assert.HasCount(2, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task RepeatedConstDeclarationsGetSeparated()
	{
		const string source = """
			class C
			{
				void M()
				{
					Prepare();
					const int first = 1;
					const int second = 2;
					return;
				}

				void Prepare()
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					Prepare();

					const int first = 1;
					const int second = 2;

					return;
				}

				void Prepare()
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(source);

		Assert.HasCount(2, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AssertionRootGroupsDifferentAssertionMethods()
	{
		const string source = """
			class C
			{
				void M()
				{
					var value = 1;
					Assert.That(value);
					Assert.AreEqual(1, value);
					return;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					var value = 1;

					Assert.That(value);
					Assert.AreEqual(1, value);

					return;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(source);

		Assert.HasCount(2, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARootWithADotMatchesNothingAndLeavesTheOtherRootsAlone()
	{
		const string source = """
			internal static class Checks
			{
				internal static class Verify
				{
					internal static void That(int value)
					{
					}

					internal static void AreEqual(int left, int right)
					{
					}
				}
			}

			internal sealed class Verifier
			{
				internal void That(int value)
				{
				}

				internal void AreEqual(int left, int right)
				{
				}
			}

			internal sealed class Holder
			{
				internal Verifier Verify { get; } = new();
			}

			internal static class Factory
			{
				internal static Holder Get() => new();
			}

			class C
			{
				void Qualified(int value)
				{
					var actual = 1;
					Checks.Verify.That(value);
					Checks.Verify.AreEqual(1, value);
					return;
				}

				void Chained(int value)
				{
					var actual = 1;
					Factory.Get().Verify.That(value);
					Factory.Get().Verify.AreEqual(1, value);
					return;
				}
			}
			""";

		// Each method has an assertion run, so a root that names Verify finds two boundaries in each.
		AnalyzerRunResult plain =
			await RoslynTestHarness.ReportAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(
				source,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_assertion_roots"] = "Verify",
				}
			);

		Assert.HasCount(4, plain.Diagnostics);

		// A root with a dot names no segment, so it matches nothing and the other roots still match.
		AnalyzerRunResult dotted =
			await RoslynTestHarness.ReportAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(
				source,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_assertion_roots"] = "Verify, Checks.Verify",
				}
			);

		Assert.HasCount(4, dotted.Diagnostics);

		// Alone, the dotted root replaces the defaults and matches no call, so no run forms.
		AnalyzerRunResult dottedAlone =
			await RoslynTestHarness.ReportAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(
				source,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_assertion_roots"] = "Checks.Verify",
				}
			);

		Assert.IsEmpty(dottedAlone.Diagnostics);
	}

	[TestMethod]
	public async Task TopLevelStatementsAreGrouped()
	{
		const string source = """
			var value = 1;
			Run();
			Run();
			return;

			static void Run()
			{
			}
			""";

		const string fixedSource = """
			var value = 1;

			Run();
			Run();

			return;

			static void Run()
			{
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(source);

		Assert.HasCount(2, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task LoneDeclarationBeforeRepeatedGroupIsSeparated()
	{
		const string source = """
			class C
			{
				void M()
				{
					DoThing();
					const int first = 1;
					int second = 2;
					int third = 3;
				}

				void DoThing()
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					DoThing();

					const int first = 1;

					int second = 2;
					int third = 3;
				}

				void DoThing()
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(source);

		Assert.HasCount(2, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ALoneDeclarationAlreadySeparatedFromTheRunStaysWithItsNeighbor()
	{
		const string source = """
			class C
			{
				void M()
				{
					DoThing();
					const int first = 1;

					int second = 2;
					int third = 3;
				}

				void DoThing()
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(source);

		Assert.IsEmpty(result.Diagnostics);
	}

	// A blank line inside an inactive block is no blank line of the compiled code, so the run reaches across the block.
	[TestMethod]
	public async Task ABlankLineInsideAnInactiveBlockDoesNotEndARun()
	{
		const string source = """
			class C
			{
				void M()
				{
					DoThing();
					var a = 1;
			#if NEVER

			#endif
					var b = 2;
					DoThing();
				}

				void DoThing()
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					DoThing();

					var a = 1;
			#if NEVER

			#endif
					var b = 2;

					DoThing();
				}

				void DoThing()
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(source);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("", DisplayName = "an empty value")]
	[DataRow(" , ,", DisplayName = "a value of separators only")]
	public async Task ARootsKeyThatListsNoRootKeepsTheDefaultRoots(string value)
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					var actual = 1;
					Assert.That(value);
					Assert.AreEqual(1, value);
					return;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(
				source,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_assertion_roots"] = value,
				}
			);

		Assert.HasCount(2, result.Diagnostics);
	}

	[TestMethod]
	public async Task ChangingAnalyzerOptionsOnTheSameTreeIsPickedUp()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					var actual = 1;
					Verify.That(value);
					Verify.AreEqual(1, value);
					return;
				}
			}
			""";

		var configuredRoots = new Dictionary<string, string>
		{
			["stanza_assertion_roots"] = "Verify",
		};

		(ImmutableArray<Diagnostic> first, ImmutableArray<Diagnostic> second) =
			await RoslynTestHarness.RunTwiceOnSameTreeAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(
				source,
				configuredRoots,
				new Dictionary<string, string>()
			);

		// The first run groups the Verify chain into one repeated group,
		// the second run falls back to the default roots even though it analyzes the same syntax tree.
		Assert.HasCount(2, first);
		Assert.IsEmpty(second);
	}

	[TestMethod]
	public async Task TheGroupingRulesTakeTheMultiLineBoundariesOverWhenThatRuleIsOff()
	{
		const string source = """
			class C
			{
				void M(bool value)
				{
					DoThing();

					if (value)
					{
						DoThing();
					}
					int first = 1;
					int second = 2;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(bool value)
				{
					DoThing();

					if (value)
					{
						DoThing();
					}

					int first = 1;
					int second = 2;
				}
			}
			""";

		// While the owner is on, it is the only rule that reports the boundary.
		AnalyzerRunResult owned = await RoslynTestHarness.ReportAllAsync(source);

		Assert.HasCount(1, owned.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.SeparateMultiLineConstructs.Id,
			owned.Diagnostics[0].Id
		);

		var multiLineRuleOff = new Dictionary<string, ReportDiagnostic>
		{
			[DiagnosticDescriptors.SeparateMultiLineConstructs.Id] = ReportDiagnostic.Suppress,
		};

		AnalyzerRunResult handedOver =
			await RoslynTestHarness.RunAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(
				source,
				multiLineRuleOff
			);

		Assert.HasCount(1, handedOver.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, handedOver);
	}

	[TestMethod]
	public async Task OnlyTheNoneSeveritySwitchesABoundaryOwnerOff()
	{
		const string source = """
			class C
			{
				void M(bool value)
				{
					DoThing();

					if (value)
					{
						DoThing();
					}
					int first = 1;
					int second = 2;
				}
			}
			""";

		(ReportDiagnostic Severity, int Expected)[] cases =
		[
			(ReportDiagnostic.Suppress, 1),
			(ReportDiagnostic.Hidden, 0),
			(ReportDiagnostic.Info, 0),
			(ReportDiagnostic.Warn, 0),
		];

		foreach ((ReportDiagnostic severity, int expected) in cases)
		{
			var ownerSeverity = new Dictionary<string, ReportDiagnostic>
			{
				[DiagnosticDescriptors.SeparateMultiLineConstructs.Id] = severity,
			};

			AnalyzerRunResult result =
				await RoslynTestHarness.ReportAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(
					source,
					treeOptions: ownerSeverity
				);

			Assert.HasCount(
				expected,
				result.Diagnostics,
				$"The owner severity '{severity}' changed how the boundary after it was reported."
			);
		}
	}

	[TestMethod]
	public async Task APragmaRegionHandsAMultiLineBoundaryToTheGroupingRule()
	{
		string source = $$"""
			class C
			{
				void M(bool value)
				{
			#pragma warning disable {{DiagnosticDescriptors.SeparateMultiLineConstructs.Id}}
					DoThing();

					if (value)
					{
						DoThing();
					}
					int first = 1;
					int second = 2;
			#pragma warning restore {{DiagnosticDescriptors.SeparateMultiLineConstructs.Id}}
				}
			}
			""";

		string fixedSource = $$"""
			class C
			{
				void M(bool value)
				{
			#pragma warning disable {{DiagnosticDescriptors.SeparateMultiLineConstructs.Id}}
					DoThing();

					if (value)
					{
						DoThing();
					}

					int first = 1;
					int second = 2;
			#pragma warning restore {{DiagnosticDescriptors.SeparateMultiLineConstructs.Id}}
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source);

		Assert.HasCount(1, result.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns.Id,
			result.Diagnostics[0].Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task RepeatedNullConditionalInvocationsGetSeparated()
	{
		const string source = """
			class C
			{
				void M(C? other)
				{
					var value = 1;
					other?.DoThing();
					other?.DoThing();
					return;
				}

				void DoThing()
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(C? other)
				{
					var value = 1;

					other?.DoThing();
					other?.DoThing();

					return;
				}

				void DoThing()
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(source);

		Assert.HasCount(2, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ANullConditionalInvocationAndAPlainOneOfTheSameNameFormOneRun()
	{
		const string source = """
			class C
			{
				void M(C? other)
				{
					var value = 1;
					other?.DoThing();
					other!.DoThing();
					return;
				}

				void DoThing()
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(C? other)
				{
					var value = 1;

					other?.DoThing();
					other!.DoThing();

					return;
				}

				void DoThing()
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(source);

		Assert.HasCount(2, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ANullConditionalAssignmentEndsARunAsAnAssignmentDoes()
	{
		RoslynTestHarness.RequireRoslynWithCSharp14();

		const string source = """
			class C
			{
				int Value;

				void M(C? other)
				{
					var first = 1;
					var second = 2;
					other?.Value = first;
					DoThing();
					DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				int Value;

				void M(C? other)
				{
					var first = 1;
					var second = 2;

					other?.Value = first;

					DoThing();
					DoThing();
				}
			}
			""";

		AnalyzerRunResult conditional =
			await RoslynTestHarness.RunAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(source);

		AnalyzerRunResult plain =
			await RoslynTestHarness.RunAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(
				source.Replace("other?.Value", "other!.Value", StringComparison.Ordinal)
			);

		Assert.IsNotEmpty(conditional.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, conditional);

		RoslynTestHarness.AssertFixedText(
			fixedSource.Replace("other?.Value", "other!.Value", StringComparison.Ordinal),
			plain
		);
	}

	// A pragma over ZA0005 leaves the gaps a multi-line unit borders as `none` does, so ZA0001 asks for none of them.
	[TestMethod]
	public async Task APragmaOverTheMultiLineRuleHandsItsGapsToNoOtherRule()
	{
		const string source = """
			class C
			{
				int M()
				{
					int x = 0;

			#pragma warning disable ZA0005
					var a = 1;
					var a2 = 1;
					var b = Build(
						1);
					var c = 2;
					var c2 = 2;
			#pragma warning restore ZA0005

					return x + a + a2 + b + c + c2;
				}

				static int Build(int v) => v;
			}
			""";

		string multiLine = DiagnosticDescriptors.SeparateMultiLineConstructs.Id;

		AnalyzerRunResult silenced =
			await RoslynTestHarness.RunAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(source);

		AnalyzerRunResult disabled =
			await RoslynTestHarness.RunAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(
				source,

				treeOptions: new Dictionary<string, ReportDiagnostic>
				{
					[multiLine] = ReportDiagnostic.Suppress,
				}
			);

		Assert.IsEmpty(disabled.Diagnostics);
		Assert.IsEmpty(silenced.Diagnostics, "The pragma hands the gaps ZA0005 keeps to ZA0001.");
	}

	// The compiler ignores a pragma in a branch the build leaves out, so ZA0001 still leaves ZA0005 its gaps.
	[TestMethod]
	public async Task APragmaInABranchTheBuildLeavesOutSilencesNoRule()
	{
		const string source = """
			#if NEVER
			#pragma warning disable ZA0005
			#endif
			class C
			{
				int M()
				{
					int x = 0;
					var a = 1;
					var b = Build(
						1);
					var c = 2;
					return x + a + b + c;
				}

				static int Build(int v) => v;
			}
			""";

		// In force, the pragma hands the gaps ZA0005 keeps to this rule, which then writes them.
		const string fixedSource = """
			#if !NEVER
			#pragma warning disable ZA0005
			#endif
			class C
			{
				int M()
				{
					int x = 0;

					var a = 1;
					var b = Build(
						1);
					var c = 2;

					return x + a + b + c;
				}

				static int Build(int v) => v;
			}
			""";

		AnalyzerRunResult inactive =
			await RoslynTestHarness.RunAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(source);

		AnalyzerRunResult inForce =
			await RoslynTestHarness.RunAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(
				source.Replace("#if NEVER", "#if !NEVER", StringComparison.Ordinal)
			);

		RoslynTestHarness.AssertFixedText(fixedSource, inForce);
		Assert.IsEmpty(inactive.Diagnostics, "A pragma left out changes nothing.");
	}

	// ZA0047 writes a blank line beside the marker, so the fields make no run in either fix order.
	[TestMethod]
	public async Task ARegionMarkerSplitsAFieldRunSoTheMemberAboveStaysTight()
	{
		const string source = """
			class C
			{
				public int Count { get; set; }
				private int _first;
				#region Fields
				private int _second;
				#endregion Fields
			}
			""";

		const string fixedSource = """
			class C
			{
				public int Count { get; set; }
				private int _first;

				#region Fields

				private int _second;

				#endregion Fields
			}
			""";

		AnalyzerRunResult alone =
			await RoslynTestHarness.RunAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(source);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source);

		Assert.IsEmpty(alone.Diagnostics, "The marker ends the run, so the rule reports nothing.");
		RoslynTestHarness.AssertFixedText(fixedSource, result);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(expected: fixedSource, source);
	}

	[TestMethod]
	public async Task ARegionMarkerSplitsAFieldRunSoTheMemberBelowStaysTight()
	{
		const string source = """
			class C
			{
				#region Fields
				private int _first;
				#endregion Fields
				private int _second;
				public int Count { get; set; }
			}
			""";

		const string fixedSource = """
			class C
			{
				#region Fields

				private int _first;

				#endregion Fields

				private int _second;
				public int Count { get; set; }
			}
			""";

		AnalyzerRunResult alone =
			await RoslynTestHarness.RunAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(source);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source);

		Assert.IsEmpty(alone.Diagnostics, "The marker ends the run, so the rule reports nothing.");
		RoslynTestHarness.AssertFixedText(fixedSource, result);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(expected: fixedSource, source);
	}

	// The first and the last directive of a gap decide, so a marker ends a run from either end of the gap.
	[TestMethod]
	public async Task ARegionMarkerFirstInTheGapEndsTheRunBeforeAnotherDirective()
	{
		const string source = """
			class C
			{
				public int Count { get; set; }
				private int _first;
				#region Fields
				#nullable enable
				private int _second;
				#endregion Fields
			}
			""";

		const string fixedSource = """
			class C
			{
				public int Count { get; set; }
				private int _first;

				#region Fields
				#nullable enable
				private int _second;

				#endregion Fields
			}
			""";

		AnalyzerRunResult alone =
			await RoslynTestHarness.RunAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(source);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source);

		Assert.IsEmpty(alone.Diagnostics, "The marker ends the run, so the rule reports nothing.");
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARegionMarkerLastInTheGapEndsTheRunAfterAnotherDirective()
	{
		const string source = """
			class C
			{
				public int Count { get; set; }
				private int _first;
				#nullable enable
				#region Fields
				private int _second;
				#endregion Fields
			}
			""";

		const string fixedSource = """
			class C
			{
				public int Count { get; set; }
				private int _first;
				#nullable enable
				#region Fields

				private int _second;

				#endregion Fields
			}
			""";

		AnalyzerRunResult alone =
			await RoslynTestHarness.RunAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(source);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source);

		Assert.IsEmpty(alone.Diagnostics, "The marker ends the run, so the rule reports nothing.");
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARegionMarkerSplitsNoRunWhereTheRegionRuleIsOff()
	{
		const string source = """
			class C
			{
				public int Count { get; set; }
				private int _first;
				#region Fields
				private int _second;
				#endregion Fields
			}
			""";

		const string fixedSource = """
			class C
			{
				public int Count { get; set; }

				private int _first;
				#region Fields
				private int _second;
				#endregion Fields
			}
			""";

		string regionMarker = DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors.Id;

		AnalyzerRunResult disabled =
			await RoslynTestHarness.RunAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(
				source,

				new Dictionary<string, ReportDiagnostic>
				{
					[regionMarker] = ReportDiagnostic.Suppress,
				}
			);

		AnalyzerRunResult silenced =
			await RoslynTestHarness.ReportAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(
				$"#pragma warning disable {regionMarker}\n{source}"
			);

		RoslynTestHarness.AssertFixedText(fixedSource, disabled);
		Assert.HasCount(1, silenced.Diagnostics, "The pragma leaves the marker inside the run.");
	}

	// ZA0047 reports above the marker and not below it, where the pragma is the next directive,
	// so the pragma after the marker leaves the gap above it to ZA0047 and the run ends there.
	[TestMethod]
	public async Task APragmaAfterTheRegionMarkerLeavesTheMarkerEndingTheRun()
	{
		const string source = """
			class C
			{
				public int Count { get; set; }
				private int _first;
				#region Fields
			#pragma warning disable ZA0047
				private int _second;
				#endregion Fields
			}
			""";

		AnalyzerRunResult runs =
			await RoslynTestHarness.ReportAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(
				source
			);

		AnalyzerRunResult markers =
			await RoslynTestHarness.ReportAsync<SeparateRegionMarkerFromNeighborsAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors)
			);

		Assert.IsEmpty(runs.Diagnostics, "The marker still ends the run.");
		Assert.IsNotEmpty(markers.Diagnostics, "ZA0047 reports above the marker.");
	}
}
