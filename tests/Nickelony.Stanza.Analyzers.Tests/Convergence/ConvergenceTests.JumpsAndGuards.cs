using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Convergence;

public sealed partial class ConvergenceTests
{
	// Gathering a loose run can make the stanza above a jump two lines long, which the jump rule then separates.
	// The gap above the jump has to stay blank, and the run ends there, whichever rule fixes first.
	public static IEnumerable<object[]> JumpFixOrderRows { get; } =
	[
		[
			"a gathered run above a jump keeps the blank line above the jump",
			3,
			JumpRuleSet(),

			"""
				a = 1;

				b = 2;

				c = 3;

				d = 4;

				return;
				""",

			"""
				a = 1;
				b = 2;
				c = 3;
				d = 4;

				return;
				""",
		],

		[
			"a run that drops to the limit once the gap above the jump stays is left as written",
			3,
			JumpRuleSet(),

			"""
				a = 1;

				b = 2;

				c = 3;

				return;
				""",

			"""
				a = 1;

				b = 2;

				c = 3;

				return;
				""",
		],

		[
			"a lone statement above a jump is gathered with it, since one line asks for nothing",
			1,
			JumpRuleSet(),

			"""
				a = 1;

				return;
				""",

			"""
				a = 1;
				return;
				""",
		],

		[
			"two statements above a jump are gathered and keep the blank line above the jump",
			1,
			JumpRuleSet(),

			"""
				a = 1;

				b = 2;

				return;
				""",

			"""
				a = 1;
				b = 2;

				return;
				""",
		],

		[
			"a run in a switch section keeps the blank line above its return",
			3,
			JumpRuleSet(),

			"""
				switch (x)
				{
					case 1:
						a = 1;

						b = 2;

						c = 3;

						d = 4;

						return;
				}
				""",

			"""
				switch (x)
				{
					case 1:
						a = 1;
						b = 2;
						c = 3;
						d = 4;

						return;
				}
				""",
		],

		[
			"a run in a loop body keeps the blank line above its break",
			2,
			JumpRuleSet(),

			"""
				while (x > 0)
				{
					a = 1;

					b = 2;

					c = 3;

					break;
				}
				""",

			"""
				while (x > 0)
				{
					a = 1;
					b = 2;
					c = 3;

					break;
				}
				""",
		],

		[
			"a run above a throw keeps the blank line above it, and a return below it needs none",
			2,
			JumpRuleSet(),

			"""
				a = 1;

				b = 2;

				c = 3;

				throw new System.InvalidOperationException();
				""",

			"""
				a = 1;
				b = 2;
				c = 3;

				throw new System.InvalidOperationException();
				""",
		],

		[
			"a gathered run beside two jumps in a row asks for one blank line",
			2,
			JumpRuleSet(),

			"""
				a = 1;

				b = 2;

				c = 3;

				return;
				throw new System.InvalidOperationException();
				""",

			"""
				a = 1;
				b = 2;
				c = 3;

				return;
				throw new System.InvalidOperationException();
				""",
		],

		[
			"a comment above the jump moves with it, and the gathered run ends above the comment",
			2,
			JumpRuleSet(),

			"""
				a = 1;

				b = 2;

				c = 3;

				// Done.
				return;
				""",

			"""
				a = 1;
				b = 2;
				c = 3;

				// Done.
				return;
				""",
		],

		[
			"a directive above the jump keeps its gap, and the statement tight against it stays out of the run",
			1,
			JumpRuleSet(),

			"""
				a = 1;

				b = 2;

				c = 3;
				#pragma warning disable CS0162
				return;
				""",

			"""
				a = 1;
				b = 2;

				c = 3;
				#pragma warning disable CS0162
				return;
				""",
		],

		[
			"a multi-line statement above the jump asks for the blank line where the multi-line rule is off",
			2,
			JumpRuleSet(),

			"""
				a = 1;

				b = 2;

				c = 3;

				if (x > 0)
				{
					Do();
				}
				return;
				""",

			"""
				a = 1;
				b = 2;
				c = 3;

				if (x > 0)
				{
					Do();
				}

				return;
				""",
		],

		[
			"a multi-line statement above the jump leaves the blank line to the multi-line rule where it is on",
			2,
			JumpRuleSet(DiagnosticDescriptors.SeparateMultiLineConstructs),

			"""
				a = 1;

				b = 2;

				c = 3;

				if (x > 0)
				{
					Do();
				}
				return;
				""",

			"""
				a = 1;
				b = 2;
				c = 3;

				if (x > 0)
				{
					Do();
				}

				return;
				""",
		],

		[
			"a commented sibling above the jump ends the stanza where the commented sibling rule is on",
			2,
			JumpRuleSet(DiagnosticDescriptors.SeparateCommentedSibling),

			"""
				a = 1;
				// The second.
				b = 2;
				return;
				""",

			"""
				a = 1;

				// The second.
				b = 2;
				return;
				""",
		],

		[
			"a commented sibling above the jump stays inside the stanza where the commented sibling rule is off",
			2,
			JumpRuleSet(),

			"""
				a = 1;
				// The second.
				b = 2;
				return;
				""",

			"""
				a = 1;
				// The second.
				b = 2;

				return;
				""",
		],

		[
			"a call run above the jump is set apart by the run rule and ends the stanza there",
			3,
			JumpRuleSet(TestRuleSets.RunRules),

			"""
				var first = 1;
				var second = 2;
				Do();
				return;
				""",

			"""
				var first = 1;
				var second = 2;

				Do();
				return;
				""",
		],

		[
			"a call run above the jump is set apart by the jump rule where the run rule is off",
			3,
			JumpRuleSet(),

			"""
				var first = 1;
				var second = 2;
				Do();
				return;
				""",

			"""
				var first = 1;
				var second = 2;
				Do();

				return;
				""",
		],

		[
			"a pragma over the jump rule hands the gap above the jump back to the run",
			2,
			JumpRuleSet(),

			"""
				#pragma warning disable ZA0009
				a = 1;

				b = 2;

				c = 3;

				return;
				""",

			"""
				#pragma warning disable ZA0009
				a = 1;
				b = 2;
				c = 3;
				return;
				""",
		],

		[
			"a multi-line statement and a comment above the jump, with the multi-line and commented sibling rules on",
			3,

			JumpRuleSet(
				DiagnosticDescriptors.SeparateMultiLineConstructs,
				DiagnosticDescriptors.SeparateCommentedSibling
			),

			"""
				if (x > 0)
				{
					Do();
				}
				// Next.
				a = 1;
				return;
				""",

			"""
				if (x > 0)
				{
					Do();
				}

				// Next.
				a = 1;
				return;
				""",
		],

		[
			"a multi-line statement and a comment above the jump, with the multi-line rule on",
			3,
			JumpRuleSet(DiagnosticDescriptors.SeparateMultiLineConstructs),

			"""
				if (x > 0)
				{
					Do();
				}
				// Next.
				a = 1;
				return;
				""",

			"""
				if (x > 0)
				{
					Do();
				}

				// Next.
				a = 1;
				return;
				""",
		],

		[
			"a multi-line statement and a comment above the jump, with the commented sibling rule on",
			3,
			JumpRuleSet(DiagnosticDescriptors.SeparateCommentedSibling),

			"""
				if (x > 0)
				{
					Do();
				}
				// Next.
				a = 1;
				return;
				""",

			"""
				if (x > 0)
				{
					Do();
				}
				// Next.
				a = 1;

				return;
				""",
		],

		[
			"a multi-line statement and a comment above the jump, with both rules off",
			3,
			JumpRuleSet(),

			"""
				if (x > 0)
				{
					Do();
				}
				// Next.
				a = 1;
				return;
				""",

			"""
				if (x > 0)
				{
					Do();
				}
				// Next.
				a = 1;

				return;
				""",
		],

		[
			"a gap held back for a type group is gathered again, and the one above the jump stays blank",
			1,

			JumpRuleSet(
				DiagnosticDescriptors.SeparateDeclarationRuns,
				DiagnosticDescriptors.SeparateInvocationRuns,
				DiagnosticDescriptors.SeparateAssertionRuns,
				DiagnosticDescriptors.SeparateSameTypeDeclarationRuns
			),

			"""
				int number1 = 1;

				int count2 = 2;

				string text3 = "3";

				a = 4;

				return;
				""",

			"""
				int number1 = 1;
				int count2 = 2;

				string text3 = "3";
				a = 4;

				return;
				""",
		],

		[
			"a gap held back for a type group is gathered again, and a jump after a lone statement joins it",
			1,

			JumpRuleSet(
				DiagnosticDescriptors.SeparateDeclarationRuns,
				DiagnosticDescriptors.SeparateInvocationRuns,
				DiagnosticDescriptors.SeparateAssertionRuns,
				DiagnosticDescriptors.SeparateSameTypeDeclarationRuns
			),

			"""
				int number1 = 1;

				int count2 = 2;

				string text3 = "3";

				return;
				""",

			"""
				int number1 = 1;
				int count2 = 2;

				string text3 = "3";
				return;
				""",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(JumpFixOrderRows),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AJumpBesideALooseRunSettlesTheSameWayWhicheverFixComesFirst(
		string name,
		int limit,
		IReadOnlyDictionary<string, ReportDiagnostic> rules,
		string body,
		string fixedBody
	)
	{
		string source = JumpMethod(body);
		string fixedSource = JumpMethod(fixedBody);

		Dictionary<string, string> options = new() { ["stanza_max_loose_run_size"] = $"{limit}" };

		// The Fix All of ZA0052 runs before the one of ZA0009 in the first order and after it in the second.
		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			expected: fixedSource,
			source,

			options: new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = rules,
				AnalyzerConfigOptions = options,
			},

			name
		);

		// Gathering a run never writes back a blank line the jump rule asks for.
		await AssertGatheringLeavesNoWorkAsync(source, options);
	}

	// A file of top-level statements is a container of the jump rule too, so a gathered run keeps the blank line above its return.
	[TestMethod]
	public async Task ATopLevelJumpBesideALooseRunSettlesTheSameWayWhicheverFixComesFirst()
	{
		const string source = """
			int a = 0;

			a = 1;

			a = 2;

			a = 3;

			return;
			""";

		const string fixedSource = """
			int a = 0;
			a = 1;
			a = 2;
			a = 3;

			return;
			""";

		Dictionary<string, ReportDiagnostic> rules = JumpRuleSet();
		Dictionary<string, string> options = new() { ["stanza_max_loose_run_size"] = "1" };

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			expected: fixedSource,
			source,

			options: new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = rules,
				AnalyzerConfigOptions = options,
			}
		);

		await AssertGatheringLeavesNoWorkAsync(source, options, s_fileCorpus);
	}

	// The guard rule asks for the blank line below the guards that open a block, and the loose run rule gathers around it.
	// The gap stays blank, and the run below it gathers, whichever rule fixes first.
	public static IEnumerable<object[]> GuardFixOrderRows { get; } =
	[
		[
			"a run below a guard is gathered and the guard keeps its blank line",
			3,
			GuardRuleSet(),

			"""
				if (x == 0) return;

				a = 1;

				b = 2;

				c = 3;

				d = 4;
				""",

			"""
				if (x == 0) return;

				a = 1;
				b = 2;
				c = 3;
				d = 4;
				""",
		],

		[
			"a chain of guards is gathered and keeps the blank line below its last guard",
			1,
			GuardRuleSet(),

			"""
				if (x == 0) return;

				if (x == 1) return;

				a = 1;

				b = 2;
				""",

			"""
				if (x == 0) return;
				if (x == 1) return;

				a = 1;
				b = 2;
				""",
		],

		[
			"a framework helper keeps the blank line below it",
			1,
			GuardRuleSet(),

			"""
				ArgumentOutOfRangeException.ThrowIfNegative(x);

				a = 1;

				b = 2;
				""",

			"""
				ArgumentOutOfRangeException.ThrowIfNegative(x);

				a = 1;
				b = 2;
				""",
		],

		[
			"a lone helper keeps the blank line below it where the invocation rule is on",
			1,
			GuardRuleSet(DiagnosticDescriptors.SeparateInvocationRuns),

			"""
				ArgumentOutOfRangeException.ThrowIfNegative(x);

				a = 1;

				b = 2;
				""",

			"""
				ArgumentOutOfRangeException.ThrowIfNegative(x);

				a = 1;
				b = 2;
				""",
		],

		[
			"a run of one helper is left to the invocation rule, which keeps the blank line below it",
			1,
			GuardRuleSet(DiagnosticDescriptors.SeparateInvocationRuns),

			"""
				ArgumentOutOfRangeException.ThrowIfNegative(x);

				ArgumentOutOfRangeException.ThrowIfNegative(x);

				a = 1;

				b = 2;
				""",

			"""
				ArgumentOutOfRangeException.ThrowIfNegative(x);
				ArgumentOutOfRangeException.ThrowIfNegative(x);

				a = 1;
				b = 2;
				""",
		],

		[
			"a statement between a guard and a jump ends the stanza above the jump, so the jump is gathered",
			1,
			GuardRuleSet(DiagnosticDescriptors.SeparateJumpStatement),

			"""
				if (x == 0) return;

				a = 1;

				return;
				""",

			"""
				if (x == 0) return;

				a = 1;
				return;
				""",
		],

		[
			"a statement between a guard and a jump is set apart from the jump where the guard rule is off",
			1,
			JumpRuleSet(),

			"""
				if (x == 0) return;

				a = 1;

				return;
				""",

			"""
				if (x == 0) return;
				a = 1;

				return;
				""",
		],

		[
			"a chain of guards above a jump leaves the blank line above the jump to the jump rule",
			1,
			GuardRuleSet(DiagnosticDescriptors.SeparateJumpStatement),

			"""
				if (x == 0) return;

				if (x == 1) return;

				return;
				""",

			"""
				if (x == 0) return;
				if (x == 1) return;

				return;
				""",
		],

		[
			"one guard above a jump keeps the blank line below it, since the jump rule asks for none",
			1,
			GuardRuleSet(DiagnosticDescriptors.SeparateJumpStatement),

			"""
				if (x == 0) return;

				return;
				""",

			"""
				if (x == 0) return;

				return;
				""",
		],

		[
			"one guard above a jump is gathered with it where the guard rule is off",
			1,
			JumpRuleSet(),

			"""
				if (x == 0) return;

				return;
				""",

			"""
				if (x == 0) return;
				return;
				""",
		],

		[
			"a multi-line guard is left to the multi-line rule, which keeps the blank line below it",
			1,
			GuardRuleSet(DiagnosticDescriptors.SeparateMultiLineConstructs),

			"""
				if (x == 0)
				{
					return;
				}

				a = 1;

				b = 2;
				""",

			"""
				if (x == 0)
				{
					return;
				}

				a = 1;
				b = 2;
				""",
		],

		[
			"a guard of a loop body keeps the blank line below it",
			1,
			GuardRuleSet(),

			"""
				while (x > 0)
				{
					if (a == 1) continue;

					a = 1;

					b = 2;
				}
				""",

			"""
				while (x > 0)
				{
					if (a == 1) continue;

					a = 1;
					b = 2;
				}
				""",
		],

		[
			"a declaration run below a guard is left to the declaration rule",
			1,
			GuardRuleSet(DiagnosticDescriptors.SeparateDeclarationRuns),

			"""
				if (x == 0) return;

				var first = 1;

				var second = 2;

				a = first;
				""",

			"""
				if (x == 0) return;

				var first = 1;
				var second = 2;

				a = first;
				""",
		],

		[
			"a gap another rule asks for ends the run, with a guard above it",
			2,
			GuardRuleSet(DiagnosticDescriptors.SeparateCommentedSibling),

			"""
				if (x == 0) return;

				a = 1;
				// Next.
				b = 2;

				c = 3;
				""",

			"""
				if (x == 0) return;

				a = 1;

				// Next.
				b = 2;

				c = 3;
				""",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(GuardFixOrderRows),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AGuardBesideALooseRunSettlesTheSameWayWhicheverFixComesFirst(
		string name,
		int limit,
		IReadOnlyDictionary<string, ReportDiagnostic> rules,
		string body,
		string fixedBody
	)
	{
		string source = JumpMethod(body);
		string fixedSource = JumpMethod(fixedBody);

		Dictionary<string, string> options = new() { ["stanza_max_loose_run_size"] = $"{limit}" };

		// The Fix All of ZA0052 runs before the one of ZA0011 in the first order and after it in the second.
		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			expected: fixedSource,
			source,

			options: new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = rules,
				AnalyzerConfigOptions = options,
			},

			name
		);

		// Gathering a run never writes back a blank line the guard rule asks for.
		await AssertGatheringLeavesNoWorkAsync(source, options);
	}

	// A method of statements, written without its indent, in a class that gives them names to assign.
	private static string JumpMethod(string body)
	{
		return SourceFixtures.WrapBody(
			body,
			signature: "void M(int x)",
			members: "\tint a, b, c, d;\n\n",
			after: "\n\tvoid Do() { }\n"
		);
	}

	// The loose run rule and the jump rule on beside the rules that ship on, less the ones not named.
	private static Dictionary<string, ReportDiagnostic> JumpRuleSet(
		params DiagnosticDescriptor[] alsoOn
	)
	{
		return OnSuppressingShippedStatementRules([
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateJumpStatement,
			.. alsoOn,
		]);
	}

	// The loose run rule and the guard rule on beside the rules that ship on, less the ones not named.
	private static Dictionary<string, ReportDiagnostic> GuardRuleSet(
		params DiagnosticDescriptor[] alsoOn
	)
	{
		return OnSuppressingShippedStatementRules([
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateGuardClauses,
			.. alsoOn,
		]);
	}

	// A statement stands apart from the imports above it, so an import is no line of the stanza above a jump.
	[TestMethod]
	public async Task AnImportIsNoPartOfTheStanzaAboveAJump()
	{
		const string source = """
			using System.Text;
			int value = 0;
			return;
			""";

		const string fixedSource = """
			using System.Text;

			int value = 0;
			return;
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.Only(
			DiagnosticDescriptors.SeparateJumpStatement,
			DiagnosticDescriptors.SeparateImportSection
		);

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			expected: fixedSource,
			source,
			options: new RunOptions(RoslynTestHarness.AllAnalyzers) { DiagnosticOptions = rules }
		);
	}

	// The blank line under the guard stays asked for: the gather only hands it from the guard rule to the assignment rule.
	[TestMethod]
	public async Task AGapAnotherRuleTakesOverIsNoNewGapOfGathering()
	{
		const string source = """
			class C
			{
				void M(object items, int loose)
				{
					Guard.NotNull(items);
					loose += 0;

					(loose, loose) = (1, 2);
				}
			}
			""";

		await AssertGatheringLeavesNoWorkAsync(
			source,

			new Dictionary<string, string>
			{
				["stanza_max_loose_run_size"] = "1",
				["stanza_guard_roots"] = "Guard",
			}
		);
	}
}
