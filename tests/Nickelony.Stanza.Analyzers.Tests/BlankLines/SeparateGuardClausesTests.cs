using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Analyzers.Regions;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed class SeparateGuardClausesTests
{
	private const string RootsKey = "stanza_guard_roots";

	// Each row is a body that opens with a guard of one kind, and the body with the blank line below the guard.
	public static IEnumerable<object[]> Guards { get; } =
	[
		[
			"an if with a return",
			"void M(int x, string text)",

			"""
				if (x == 0) return;
				a = 1;
				b = 2;
				""",

			"""
				if (x == 0) return;

				a = 1;
				b = 2;
				""",
		],

		[
			"an if with a return of a value",
			"int M(int x, string text)",

			"""
				if (x == 0) return 1;
				a = 1;
				return a;
				""",

			"""
				if (x == 0) return 1;

				a = 1;
				return a;
				""",
		],

		[
			"an if with a throw",
			"void M(int x, string text)",

			"""
				if (x == 0) throw new System.ArgumentException("x");
				a = 1;
				b = 2;
				""",

			"""
				if (x == 0) throw new System.ArgumentException("x");

				a = 1;
				b = 2;
				""",
		],

		[
			"an if with a continue",
			"void M(int x, string text)",

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
			"an if with a break",
			"void M(int x, string text)",

			"""
				while (x > 0)
				{
					if (a == 1) break;
					a = 1;
					b = 2;
				}
				""",

			"""
				while (x > 0)
				{
					if (a == 1) break;

					a = 1;
					b = 2;
				}
				""",
		],

		[
			"an if with a yield break",
			"System.Collections.Generic.IEnumerable<int> M(int x, string text)",

			"""
				if (x == 0) yield break;
				a = 1;
				yield return a;
				""",

			"""
				if (x == 0) yield break;

				a = 1;
				yield return a;
				""",
		],

		[
			"an if with a goto",
			"void M(int x, string text)",

			"""
				if (x == 0) goto done;
				a = 1;
				done: b = 2;
				""",

			"""
				if (x == 0) goto done;

				a = 1;
				done: b = 2;
				""",
		],

		[
			"a braced if on one line",
			"void M(int x, string text)",

			"""
				if (x == 0) { return; }
				a = 1;
				b = 2;
				""",

			"""
				if (x == 0) { return; }

				a = 1;
				b = 2;
				""",
		],

		[
			"a braced if on several lines, where the multi-line rule is off",
			"void M(int x, string text)",

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
			"a framework throw helper",
			"void M(int x, string text)",

			"""
				ArgumentNullException.ThrowIfNull(text);
				a = 1;
				b = 2;
				""",

			"""
				ArgumentNullException.ThrowIfNull(text);

				a = 1;
				b = 2;
				""",
		],

		[
			"a framework throw helper of a qualified type",
			"void M(int x, string text)",

			"""
				System.ArgumentException.ThrowIfNullOrEmpty(text);
				a = 1;
				b = 2;
				""",

			"""
				System.ArgumentException.ThrowIfNullOrEmpty(text);

				a = 1;
				b = 2;
				""",
		],

		[
			"an if whose body is a throw helper",
			"void M(int x, string text)",

			"""
				if (x == 0) ArgumentException.ThrowIfNullOrEmpty(text);
				a = 1;
				b = 2;
				""",

			"""
				if (x == 0) ArgumentException.ThrowIfNullOrEmpty(text);

				a = 1;
				b = 2;
				""",
		],

		[
			"a chain of guards of every kind",
			"void M(int x, string text)",

			"""
				ArgumentNullException.ThrowIfNull(text);
				if (x == 0) return;
				if (x == 1) throw new System.ArgumentException("x");
				a = 1;
				b = 2;
				""",

			"""
				ArgumentNullException.ThrowIfNull(text);
				if (x == 0) return;
				if (x == 1) throw new System.ArgumentException("x");

				a = 1;
				b = 2;
				""",
		],

		[
			"a gap written between two guards",
			"void M(int x, string text)",

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
			"a guard of a lambda block",
			"void M(int x, string text)",

			"""
				Action<int> action = value =>
				{
					if (value == 0) return;
					a = 1;
					b = 2;
				};
				""",

			"""
				Action<int> action = value =>
				{
					if (value == 0) return;

					a = 1;
					b = 2;
				};
				""",
		],

		[
			"a guard of a local function",
			"void M(int x, string text)",

			"""
				void Local(int value)
				{
					if (value == 0) return;
					a = 1;
					b = 2;
				}

				Local(x);
				""",

			"""
				void Local(int value)
				{
					if (value == 0) return;

					a = 1;
					b = 2;
				}

				Local(x);
				""",
		],
	];

	// Each row is a body the rule leaves tight.
	public static IEnumerable<object[]> QuietBodies { get; } =
	[
		["a body of guards alone", "if (x == 0) return;\nif (x == 1) return;"],
		["a guard below another statement", "a = 1;\nif (x == 0) return;\nb = 2;\nc = 3;"],
		["a guard below a declaration", "var local = x;\nif (local == 0) return;\nb = 2;\nc = 3;"],
		["a guard already set apart", "if (x == 0) return;\n\na = 1;\nb = 2;"],
		["a guard with an else", "if (x == 0) return; else a = 1;\nb = 2;\nc = 3;"],
		["an if of two statements", "if (x == 0)\n{\n\ta = 1;\n\treturn;\n}\nb = 2;\nc = 3;"],
		["an if with a lone call", "if (x == 0) Do();\nb = 2;\nc = 3;"],
		["an if with a lone assignment", "if (x == 0) a = 1;\nb = 2;\nc = 3;"],
		["an if with a nested if", "if (x == 0) { if (x == 1) return; }\nb = 2;\nc = 3;"],
		["a call of a name that is no throw helper", "Do();\nb = 2;\nc = 3;"],

		[
			"a method of an exception type that does not start with Throw",
			"ArgumentNullException.Equals(text, text);\nb = 2;\nc = 3;",
		],

		[
			"a Throw method of a type that does not end with Exception",
			"System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(new Exception());\nb = 2;\nc = 3;",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(Guards),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task TheStatementBelowTheLeadingGuardsIsSetApart(
		string name,
		string signature,
		string body,
		string fixedBody
	)
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateGuardClausesAnalyzer>(
			Method(body, signature),
			Alone()
		);

		Assert.HasCount(1, result.Diagnostics, name);
		RoslynTestHarness.AssertFixedText(Method(fixedBody, signature), result, name);
	}

	[TestMethod]
	[DynamicData(
		nameof(QuietBodies),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ABodyThatAsksForNoBlankLineBelowItsGuardsIsLeftAlone(string name, string body)
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateGuardClausesAnalyzer>(
			Method(body),
			Alone()
		);

		Assert.IsEmpty(result.Diagnostics, name);
	}

	[TestMethod]
	public async Task TheRuleShipsOffAndReportsNothingUntilASeverityEnablesIt()
	{
		string source = Method("if (x == 0) return;\na = 1;\nb = 2;");

		AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(source);

		AnalyzerRunResult enabled =
			await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(source, Alone());

		Assert.DoesNotContain(
			DiagnosticDescriptors.SeparateGuardClauses.Id,
			result.Diagnostics.Select(diagnostic => diagnostic.Id)
		);

		Assert.HasCount(1, enabled.Diagnostics);
	}

	[TestMethod]
	public async Task OnlyABlockIsJudged()
	{
		string source = Method(
			"""
			switch (x)
			{
				case 1:
					if (a == 1) break;
					a = 1;
					b = 2;
					break;
			}
			"""
		);

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(source, Alone());

		Assert.IsEmpty(result.Diagnostics, "A switch section is no body.");
	}

	[TestMethod]
	public async Task ACommentAboveTheStatementBelowTheGuardsMovesWithItSoTheBlankLineGoesAboveTheComment()
	{
		string source = Method(
			"""
			if (x == 0) return;
			// Next.
			a = 1;
			b = 2;
			"""
		);

		string fixedSource = Method(
			"""
			if (x == 0) return;

			// Next.
			a = 1;
			b = 2;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateGuardClausesAnalyzer>(
			source,
			Alone()
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ATrailingCommentOnTheLastGuardStaysOnItsLine()
	{
		string source = Method(
			"""
			if (x == 0) return; // Nothing to do.
			a = 1;
			b = 2;
			"""
		);

		string fixedSource = Method(
			"""
			if (x == 0) return; // Nothing to do.

			a = 1;
			b = 2;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateGuardClausesAnalyzer>(
			source,
			Alone()
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentInsideABracedGuardLeavesItAGuard()
	{
		string source = Method(
			"""
			if (x == 0)
			{
				// Nothing to do.
				return;
			}
			a = 1;
			b = 2;
			"""
		);

		string fixedSource = Method(
			"""
			if (x == 0)
			{
				// Nothing to do.
				return;
			}

			a = 1;
			b = 2;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateGuardClausesAnalyzer>(
			source,
			Alone()
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ADirectiveInsideABracedGuardLeavesItAGuardInEveryView()
	{
		const string body = """
			if (x == 0)
			{
			#pragma warning disable CS0162
				return;
			#pragma warning restore CS0162
			}
			a = 1;
			b = 2;
			""";

		const string fixedSource = """
			if (x == 0)
			{
			#pragma warning disable CS0162
				return;
			#pragma warning restore CS0162
			}

			a = 1;
			b = 2;
			""";

		foreach (string view in new[] { string.Empty, "#define B\n" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<SeparateGuardClausesAnalyzer>(
					view + Method(body),
					Alone()
				);

			Assert.HasCount(1, result.Diagnostics, $"[{view.Trim()}]");
			RoslynTestHarness.AssertFixedText(view + Method(fixedSource), result, view.Trim());
		}
	}

	[TestMethod]
	public async Task AConditionalDirectiveInsideAGuardMakesItAGuardOnlyInTheViewThatHoldsTheExit()
	{
		const string body = """
			if (x == 0)
			{
			#if B
				return;
			#endif
			}
			a = 1;
			b = 2;
			""";

		const string fixedSource = """
			if (x == 0)
			{
			#if B
				return;
			#endif
			}

			a = 1;
			b = 2;
			""";

		AnalyzerRunResult without = await RoslynTestHarness.RunAsync<SeparateGuardClausesAnalyzer>(
			Method(body),
			Alone()
		);

		Assert.IsEmpty(without.Diagnostics, "The block is empty without B.");

		AnalyzerRunResult with = await RoslynTestHarness.RunAsync<SeparateGuardClausesAnalyzer>(
			"#define B\n" + Method(body),
			Alone()
		);

		Assert.HasCount(1, with.Diagnostics);
		RoslynTestHarness.AssertFixedText("#define B\n" + Method(fixedSource), with);
	}

	[TestMethod]
	public async Task AConditionalBlockAroundTheGuardKeepsTheGapAsWrittenInEveryView()
	{
		const string body = """
			#if B
			if (x == 0) return;
			#endif
			a = 1;
			b = 2;
			""";

		foreach (string view in new[] { string.Empty, "#define B\n" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<SeparateGuardClausesAnalyzer>(
					view + Method(body),
					Alone()
				);

			Assert.IsEmpty(result.Diagnostics, $"[{view.Trim()}] The directive keeps the gap.");
		}
	}

	[TestMethod]
	public async Task ADirectiveBetweenTheGuardsAndTheStatementBelowKeepsTheGapAsWrittenInEveryView()
	{
		const string body = """
			if (x == 0) return;
			#if B
			a = 1;
			#endif
			b = 2;
			""";

		foreach (string view in new[] { string.Empty, "#define B\n" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<SeparateGuardClausesAnalyzer>(
					view + Method(body),
					Alone()
				);

			Assert.IsEmpty(result.Diagnostics, $"[{view.Trim()}] The directive keeps the gap.");
		}
	}

	[TestMethod]
	public async Task AGuardInAConditionalBlockBetweenTwoGuardsLeavesTheGapBelowTheChainAsWrittenInEveryView()
	{
		const string body = """
			if (x == 0) return;
			#if B
			if (x == 1) return;
			#endif
			a = 1;
			b = 2;
			""";

		foreach (string view in new[] { string.Empty, "#define B\n" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<SeparateGuardClausesAnalyzer>(
					view + Method(body),
					Alone()
				);

			Assert.IsEmpty(
				result.Diagnostics,
				$"[{view.Trim()}] The directive above the statement keeps the gap."
			);
		}
	}

	[TestMethod]
	public async Task AGuardBeforeAConditionalBlockOfTwoStatementsIsSetApartInEveryView()
	{
		const string body = """
			if (x == 0) return;
			a = 1;
			#if B
			b = 2;
			#endif
			c = 3;
			""";

		const string fixedSource = """
			if (x == 0) return;

			a = 1;
			#if B
			b = 2;
			#endif
			c = 3;
			""";

		foreach (string view in new[] { string.Empty, "#define B\n" })
		{
			AnalyzerRunResult result =
				await RoslynTestHarness.RunAsync<SeparateGuardClausesAnalyzer>(
					view + Method(body),
					Alone()
				);

			Assert.HasCount(1, result.Diagnostics, $"[{view.Trim()}]");
			RoslynTestHarness.AssertFixedText(view + Method(fixedSource), result, view.Trim());
		}
	}

	[TestMethod]
	public async Task APragmaOverTheStatementBelowTheGuardsSilencesTheRule()
	{
		string source = Method(
			"""
			if (x == 0) return;
			#pragma warning disable ZA0011
			a = 1;
			b = 2;
			#pragma warning restore ZA0011
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateGuardClausesAnalyzer>(
			source,
			Alone()
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ARootTheKeyListsMakesACallAGuard()
	{
		string source = Method("Guard.NotNull(text);\nGuard.Positive(x);\na = 1;\nb = 2;");
		string fixedSource = Method("Guard.NotNull(text);\nGuard.Positive(x);\n\na = 1;\nb = 2;");

		AnalyzerRunResult none = await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
			source,
			Alone()
		);

		Assert.IsEmpty(none.Diagnostics, "No name is a root by default.");

		AnalyzerRunResult listed = await RoslynTestHarness.RunAsync<SeparateGuardClausesAnalyzer>(
			source,
			Alone(),
			analyzerConfigOptions: new Dictionary<string, string> { [RootsKey] = "Guard" }
		);

		Assert.HasCount(1, listed.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, listed);
	}

	[TestMethod]
	public async Task ARootMatchesAsOneWholeSegmentOfTheCalleeAsAssertionRootsDo()
	{
		const string source = """
			class C
			{
				void Direct(int x)
				{
					Guard.NotNull(x);
					int a = x;
					int b = x;
				}

				void Qualified(int x)
				{
					Nickelony.Guard.NotNull(x);
					int a = x;
					int b = x;
				}
			}

			namespace Nickelony
			{
				static class Guard
				{
					public static void NotNull(object value) { }
				}
			}
			""";

		static Dictionary<string, string> Roots(string value) => new() { [RootsKey] = value };

		AnalyzerRunResult segment =
			await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
				source,
				Alone(),
				Roots("Guard")
			);

		Assert.HasCount(2, segment.Diagnostics, "Guard names a segment of both callees.");

		AnalyzerRunResult dotted =
			await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
				source,
				Alone(),
				Roots("Nickelony.Guard")
			);

		Assert.IsEmpty(dotted.Diagnostics, "An entry with a dot names no segment.");

		AnalyzerRunResult partial =
			await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
				source,
				Alone(),
				Roots("Guar")
			);

		Assert.IsEmpty(partial.Diagnostics, "A name matches whole.");

		AnalyzerRunResult otherCase =
			await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
				source,
				Alone(),
				Roots("guard")
			);

		Assert.IsEmpty(otherCase.Diagnostics, "A name matches in its case.");
	}

	[TestMethod]
	[DataRow("", DisplayName = "an empty value")]
	[DataRow(" , ,", DisplayName = "a value of separators only")]
	public async Task ARootsKeyThatListsNoRootNamesNoGuard(string value)
	{
		string source = Method("Guard.NotNull(text);\na = 1;\nb = 2;");

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
				source,
				Alone(),
				analyzerConfigOptions: new Dictionary<string, string> { [RootsKey] = value }
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheWordNoneNamesNoRootInsteadOfARootCalledNone()
	{
		string source = Method(
			"none(text);\na = 1;\nb = 2;",
			signature: "void M(int x, string text, System.Action<string> none)"
		);

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
				source,
				Alone(),
				analyzerConfigOptions: new Dictionary<string, string> { [RootsKey] = "none" }
			);

		Assert.IsEmpty(result.Diagnostics, "None alone lists no root.");

		AnalyzerRunResult beside =
			await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
				source,
				Alone(),
				analyzerConfigOptions: new Dictionary<string, string> { [RootsKey] = "none, Guard" }
			);

		Assert.HasCount(1, beside.Diagnostics, "None beside another name is a root called none.");
	}

	// A run of two or more calls of one helper is the invocation rule's, and a lone helper is this rule's.
	[TestMethod]
	public async Task ARunOfOneHelperBelowTheGuardsIsLeftToTheInvocationRuleWhereItIsOn()
	{
		string source = Method(
			"""
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(text);
			a = 1;
			b = 2;
			"""
		);

		AnalyzerRunResult guard = await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
			source,
			Alone(DiagnosticDescriptors.SeparateInvocationRuns)
		);

		AnalyzerRunResult runs =
			await RoslynTestHarness.ReportAsync<SeparateInvocationRunsAnalyzer>(
				source,
				Alone(DiagnosticDescriptors.SeparateInvocationRuns)
			);

		Assert.IsEmpty(
			guard.Diagnostics,
			"ZA0004 reports the gap below the run, so this rule stays quiet."
		);

		Assert.HasCount(1, runs.Diagnostics);
	}

	[TestMethod]
	public async Task ARunOfOneHelperBelowTheGuardsIsSetApartByThisRuleWhereTheInvocationRuleIsOff()
	{
		string source = Method(
			"""
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(text);
			a = 1;
			b = 2;
			"""
		);

		string fixedSource = Method(
			"""
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(text);

			a = 1;
			b = 2;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateGuardClausesAnalyzer>(
			source,
			Alone()
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ALoneHelperBelowTheGuardsIsSetApartByThisRuleWhereTheInvocationRuleIsOn()
	{
		string source = Method(
			"""
			ArgumentNullException.ThrowIfNull(text);
			a = 1;
			b = 2;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(
			source,
			Alone(DiagnosticDescriptors.SeparateInvocationRuns)
		);

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.SeparateGuardClauses.Id, result.Diagnostics[0].Id);
	}

	[TestMethod]
	public async Task ADeclarationRunBelowTheGuardsIsLeftToTheDeclarationRuleWhereItIsOn()
	{
		string source = Method(
			"""
			if (x == 0) return;
			var first = 1;
			var second = 2;
			a = first;
			"""
		);

		AnalyzerRunResult guard = await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
			source,
			Alone(DiagnosticDescriptors.SeparateDeclarationRuns)
		);

		AnalyzerRunResult runs =
			await RoslynTestHarness.ReportAsync<SeparateDeclarationRunsAnalyzer>(
				source,
				Alone(DiagnosticDescriptors.SeparateDeclarationRuns)
			);

		Assert.IsEmpty(
			guard.Diagnostics,
			"ZA0002 reports the gap above its run, so this rule stays quiet."
		);

		Assert.HasCount(2, runs.Diagnostics);
	}

	[TestMethod]
	public async Task AMultiLineStatementBelowTheGuardsIsLeftToTheMultiLineRuleWhereItIsOn()
	{
		string source = Method(
			"""
			if (x == 0) return;
			if (a == 1)
			{
				Do();
			}
			"""
		);

		AnalyzerRunResult guard = await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
			source,
			Alone(DiagnosticDescriptors.SeparateMultiLineConstructs)
		);

		AnalyzerRunResult multiLine =
			await RoslynTestHarness.ReportAsync<SeparateMultiLineConstructsAnalyzer>(
				source,
				Alone(DiagnosticDescriptors.SeparateMultiLineConstructs)
			);

		Assert.IsEmpty(guard.Diagnostics, "ZA0001 owns the gap beside a multi-line statement.");
		Assert.HasCount(1, multiLine.Diagnostics);
	}

	[TestMethod]
	public async Task AMultiLineGuardIsLeftToTheMultiLineRuleWhereItIsOnAndSetApartByThisRuleWhereItIsOff()
	{
		string source = Method(
			"""
			if (x == 0)
			{
				return;
			}
			a = 1;
			b = 2;
			"""
		);

		AnalyzerRunResult on = await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
			source,
			Alone(DiagnosticDescriptors.SeparateMultiLineConstructs)
		);

		AnalyzerRunResult off = await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
			source,
			Alone()
		);

		Assert.IsEmpty(on.Diagnostics);
		Assert.HasCount(1, off.Diagnostics);
	}

	[TestMethod]
	public async Task AContainerOfTwoUnitsIsLeftToTheTwoUnitRuleWhereItIsOn()
	{
		string source = Method("if (x == 0) return;\na = 1;");

		AnalyzerRunResult on = await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
			source,
			Alone(DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether)
		);

		AnalyzerRunResult off = await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
			source,
			Alone()
		);

		Assert.IsEmpty(on.Diagnostics, "ZA0051 removes the blank line of such a container.");
		Assert.HasCount(1, off.Diagnostics);
	}

	[TestMethod]
	public async Task AContainerOfThreeUnitsIsJudgedWhereTheTwoUnitRuleIsOn()
	{
		string source = Method("if (x == 0) return;\na = 1;\nb = 2;");

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
				source,
				Alone(DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether)
			);

		Assert.HasCount(1, result.Diagnostics);
	}

	[TestMethod]
	public async Task AnAssertionRunBelowTheGuardsIsLeftToTheAssertionRuleWhereItIsOn()
	{
		string source = Method("if (x == 0) return;\nAssert.That(a);\nAssert.That(b);\nc = 3;");

		AnalyzerRunResult guard = await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
			source,
			Alone(DiagnosticDescriptors.SeparateAssertionRuns)
		);

		AnalyzerRunResult runs = await RoslynTestHarness.ReportAsync<SeparateAssertionRunsAnalyzer>(
			source,
			Alone(DiagnosticDescriptors.SeparateAssertionRuns)
		);

		AnalyzerRunResult off = await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
			source,
			Alone()
		);

		Assert.IsEmpty(
			guard.Diagnostics,
			"ZA0005 reports the gap above its run, so this rule stays quiet."
		);

		Assert.HasCount(2, runs.Diagnostics);

		Assert.HasCount(
			1,
			off.Diagnostics,
			"Where ZA0005 is off, this rule writes the blank line."
		);
	}

	[TestMethod]
	public async Task AnAssignmentRunBelowTheGuardsIsLeftToTheAssignmentRuleWhereItIsOn()
	{
		string source = Method("if (x == 0) return;\na = 1;\nb = 2;\nDo();");

		AnalyzerRunResult guard = await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
			source,
			Alone(DiagnosticDescriptors.SeparateAssignmentRuns)
		);

		AnalyzerRunResult runs =
			await RoslynTestHarness.ReportAsync<SeparateAssignmentRunsAnalyzer>(
				source,
				Alone(DiagnosticDescriptors.SeparateAssignmentRuns)
			);

		AnalyzerRunResult off = await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
			source,
			Alone()
		);

		Assert.IsEmpty(
			guard.Diagnostics,
			"ZA0008 reports the gap above its run, so this rule stays quiet."
		);

		Assert.HasCount(2, runs.Diagnostics);

		Assert.HasCount(
			1,
			off.Diagnostics,
			"Where ZA0008 is off, this rule writes the blank line."
		);
	}

	[TestMethod]
	public async Task ASubscriptionRunBelowTheGuardsIsLeftToTheSubscriptionRuleWhereItIsOn()
	{
		string source = SubscriptionMethod(
			"if (x == 0) return;\nChanged += Handler;\nClosed += Handler;\nx = 1;"
		);

		AnalyzerRunResult guard = await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
			source,
			Alone(DiagnosticDescriptors.SeparateEventSubscriptionRuns)
		);

		AnalyzerRunResult runs =
			await RoslynTestHarness.ReportAsync<SeparateEventSubscriptionRunsAnalyzer>(
				source,
				Alone(DiagnosticDescriptors.SeparateEventSubscriptionRuns)
			);

		AnalyzerRunResult off = await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
			source,
			Alone()
		);

		Assert.IsEmpty(
			guard.Diagnostics,
			"ZA0007 reports the gap above its run, so this rule stays quiet."
		);

		Assert.HasCount(2, runs.Diagnostics);

		Assert.HasCount(
			1,
			off.Diagnostics,
			"Where ZA0007 is off, this rule writes the blank line."
		);
	}

	[TestMethod]
	public async Task ARunOfOneTypeBelowTheGuardsIsLeftToTheSameTypeRuleWhereItIsOn()
	{
		string source = Method(
			"if (x == 0) return;\nint first = 1;\nint second = 2;\nstring text2 = \"\";"
		);

		AnalyzerRunResult guard = await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
			source,
			Alone(DiagnosticDescriptors.SeparateSameTypeDeclarationRuns)
		);

		AnalyzerRunResult groups =
			await RoslynTestHarness.ReportAsync<SeparateSameTypeDeclarationRunsAnalyzer>(
				source,
				Alone(DiagnosticDescriptors.SeparateSameTypeDeclarationRuns)
			);

		AnalyzerRunResult off = await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
			source,
			Alone()
		);

		Assert.IsEmpty(
			guard.Diagnostics,
			"ZA0003 reports the gap above its run, so this rule stays quiet."
		);

		Assert.IsNotEmpty(groups.Diagnostics);

		Assert.HasCount(
			1,
			off.Diagnostics,
			"Where ZA0003 is off, this rule writes the blank line."
		);
	}

	[TestMethod]
	public async Task ARegionMarkerBelowTheGuardsKeepsTheGapAsWrittenWhetherOrNotZA0401IsOn()
	{
		string source = Method("if (x == 0) return;\n#region Work\na = 1;\nb = 2;\n#endregion");

		AnalyzerRunResult on = await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
			source,
			Alone(DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors)
		);

		AnalyzerRunResult marker =
			await RoslynTestHarness.ReportAsync<SeparateRegionMarkerFromNeighborsAnalyzer>(
				source,
				Alone(DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors)
			);

		Dictionary<string, ReportDiagnostic> rules = Alone();

		rules[DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors.Id] =
			ReportDiagnostic.Suppress;

		AnalyzerRunResult off = await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
			source,
			rules
		);

		Assert.IsEmpty(on.Diagnostics, "ZA0401 asks for the blank line above the marker.");
		Assert.IsNotEmpty(marker.Diagnostics);
		Assert.IsEmpty(off.Diagnostics, "The directive keeps the gap as written.");
	}

	[TestMethod]
	public async Task AConditionalBlockBelowTheGuardsKeepsTheGapAsWrittenWhetherOrNotZA0402IsOn()
	{
		string source = Method("if (x == 0) return;\n#if B\na = 1;\n#endif\nb = 2;");

		AnalyzerRunResult on = await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
			source,
			Alone(DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors)
		);

		AnalyzerRunResult block =
			await RoslynTestHarness.ReportAsync<SeparateConditionalBlockFromNeighborsAnalyzer>(
				source,
				Alone(DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors)
			);

		AnalyzerRunResult off = await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
			source,
			Alone()
		);

		Assert.IsEmpty(on.Diagnostics, "ZA0402 asks for the blank line above the block.");
		Assert.IsNotEmpty(block.Diagnostics);
		Assert.IsEmpty(off.Diagnostics, "The directive keeps the gap as written.");
	}

	[TestMethod]
	public async Task ACommentedStatementBelowTheGuardsIsLeftToTheCommentedSiblingRuleWhereItIsOn()
	{
		string source = Method("if (x == 0) return;\n// Next.\na = 1;\nb = 2;");

		AnalyzerRunResult guard = await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
			source,
			Alone(DiagnosticDescriptors.SeparateCommentedSibling)
		);

		AnalyzerRunResult sibling =
			await RoslynTestHarness.ReportAsync<SeparateCommentedSiblingAnalyzer>(
				source,
				Alone(DiagnosticDescriptors.SeparateCommentedSibling)
			);

		AnalyzerRunResult off = await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
			source,
			Alone()
		);

		Assert.IsEmpty(guard.Diagnostics, "ZA0012 asks for the blank line above the comment.");
		Assert.HasCount(1, sibling.Diagnostics);

		Assert.HasCount(
			1,
			off.Diagnostics,
			"Where ZA0012 is off, this rule writes the blank line above the comment."
		);
	}

	// The jump rule reads a guard as a statement of the stanza above a jump, and a gap it asks for is its own.
	[TestMethod]
	public async Task ARowOfGuardsAboveAJumpGetsItsBlankLineFromTheJumpRuleWhereItIsOn()
	{
		string source = Method(
			"""
			if (x == 0) return;
			if (x == 1) return;
			return;
			"""
		);

		string fixedSource = Method(
			"""
			if (x == 0) return;
			if (x == 1) return;

			return;
			"""
		);

		Dictionary<string, ReportDiagnostic> rules = Alone(
			DiagnosticDescriptors.SeparateJumpStatement
		);

		AnalyzerRunResult guard = await RoslynTestHarness.ReportAsync<SeparateGuardClausesAnalyzer>(
			source,
			rules
		);

		AnalyzerRunResult jump = await RoslynTestHarness.ReportAsync<SeparateJumpStatementAnalyzer>(
			source,
			rules
		);

		AnalyzerRunResult both = await RoslynTestHarness.ReportAllAsync(source, rules);

		Assert.IsEmpty(guard.Diagnostics, "ZA0009 asks for the gap, so this rule stays quiet.");
		Assert.HasCount(1, jump.Diagnostics);
		Assert.HasCount(1, both.Diagnostics);

		RoslynTestHarness.AssertEachGapIsClaimedOnce(both);

		AnalyzerRunResult jumpAlone =
			await RoslynTestHarness.RunAsync<SeparateJumpStatementAnalyzer>(source, rules);

		AnalyzerRunResult guardAlone =
			await RoslynTestHarness.RunAsync<SeparateGuardClausesAnalyzer>(source, Alone());

		RoslynTestHarness.AssertFixedText(fixedSource, jumpAlone);

		RoslynTestHarness.AssertFixedText(
			fixedSource,
			guardAlone,
			"The two rules write the same text."
		);
	}

	[TestMethod]
	public async Task OneGuardAboveAJumpIsSetApartByThisRuleSinceTheStanzaIsSingleLine()
	{
		string source = Method(
			"""
			if (x == 0) return;
			return;
			"""
		);

		string fixedSource = Method(
			"""
			if (x == 0) return;

			return;
			"""
		);

		Dictionary<string, ReportDiagnostic> rules = Alone(
			DiagnosticDescriptors.SeparateJumpStatement
		);

		AnalyzerRunResult jump = await RoslynTestHarness.ReportAsync<SeparateJumpStatementAnalyzer>(
			source,
			rules
		);

		AnalyzerRunResult guard = await RoslynTestHarness.RunAsync<SeparateGuardClausesAnalyzer>(
			source,
			rules
		);

		Assert.IsEmpty(jump.Diagnostics, "One line above keeps the jump tight.");
		Assert.HasCount(1, guard.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, guard);
	}

	[TestMethod]
	public async Task ARowOfGuardsAboveAJumpIsSetApartByThisRuleWhereTheJumpRuleIsOff()
	{
		string source = Method(
			"""
			if (x == 0) return;
			if (x == 1) return;
			return;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(source, Alone());

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.SeparateGuardClauses.Id, result.Diagnostics[0].Id);
	}

	[TestMethod]
	public async Task APragmaInTheGapAboveTheJumpKeepsTheGapAsWritten()
	{
		string source = Method(
			"""
			if (x == 0) return;
			if (x == 1) return;
			#pragma warning disable ZA0009
			return;
			#pragma warning restore ZA0009
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(
			source,
			Alone(DiagnosticDescriptors.SeparateJumpStatement)
		);

		Assert.IsEmpty(result.Diagnostics, "A directive in the gap keeps it as written.");
	}

	// ZA0011 alone: the rules that share its gaps are off unless a test names them.
	private static Dictionary<string, ReportDiagnostic> Alone(params DiagnosticDescriptor[] alsoOn)
	{
		return TestRuleSets.OnSuppressingOthersOf(
			[DiagnosticDescriptors.SeparateGuardClauses, .. alsoOn],
			TestRuleSets.SharedGapRules
		);
	}

	private static string SubscriptionMethod(string body)
	{
		string indented = string.Join("\n", body.Split('\n').Select(static line => "\t\t" + line));

		return "class C\n{\n\tevent Action Changed;\n\tevent Action Closed;\n\n\tvoid Handler() { }\n\n\tvoid M(int x)\n\t{\n"
			+ indented
			+ "\n\t}\n}\n";
	}

	private static string Method(string body, string signature = "void M(int x, string text)")
	{
		return SourceFixtures.WrapBody(
			body,
			signature,
			members: "\tint a, b, c, d;\n\n",
			after: "\n\tvoid Do() { }\n"
		);
	}
}
