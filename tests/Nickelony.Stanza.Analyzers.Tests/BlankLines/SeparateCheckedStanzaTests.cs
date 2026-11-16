using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed class SeparateCheckedStanzaTests
{
	private static readonly IReadOnlyDictionary<string, ReportDiagnostic> s_on =
		TestRuleSets.OnSuppressingAllOthers([DiagnosticDescriptors.SeparateCheckedStanza]);

	[TestMethod]
	public async Task AMidBlockChainGetsBothBoundaries()
	{
		const string source = """
			class C
			{
				void M(int x)
				{
					System.GC.KeepAlive(x);
					if (x == 0) return;
					if (x == 1) return;
					System.GC.KeepAlive(x);
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int x)
				{
					System.GC.KeepAlive(x);

					if (x == 0) return;
					if (x == 1) return;

					System.GC.KeepAlive(x);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateCheckedStanzaAnalyzer>(
			source,
			s_on
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AReadBindingTravelsWithItsGuard()
	{
		const string source = """
			class C
			{
				void M(int x)
				{
					System.GC.KeepAlive(x);
					int checkedValue = x;
					if (checkedValue == 0) return;
					System.GC.KeepAlive(checkedValue);
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int x)
				{
					System.GC.KeepAlive(x);

					int checkedValue = x;
					if (checkedValue == 0) return;

					System.GC.KeepAlive(checkedValue);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateCheckedStanzaAnalyzer>(
			source,
			s_on
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task SeveralReadBindingsStayInTheCheckedStanza()
	{
		const string source = """
			class C
			{
				void M(int x)
				{
					System.GC.KeepAlive(x);
					int a = x;
					int b = x + 1;
					if (a == 0 || b == 0) return;
					System.GC.KeepAlive(a + b);
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int x)
				{
					System.GC.KeepAlive(x);

					int a = x;
					int b = x + 1;
					if (a == 0 || b == 0) return;

					System.GC.KeepAlive(a + b);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateCheckedStanzaAnalyzer>(
			source,
			s_on
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnUnreadBindingStaysAboveTheBoundary()
	{
		const string source = """
			class C
			{
				void M(int x)
				{
					int unread = x;
					int checkedValue = x + 1;
					if (checkedValue == 0) return;
					System.GC.KeepAlive(unread + checkedValue);
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int x)
				{
					int unread = x;

					int checkedValue = x + 1;
					if (checkedValue == 0) return;

					System.GC.KeepAlive(unread + checkedValue);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateCheckedStanzaAnalyzer>(
			source,
			s_on
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnOpeningChainNeedsOnlyItsLowerBoundary()
	{
		const string source = """
			class C
			{
				void M(int x)
				{
					if (x == 0) return;
					if (x == 1) return;
					System.GC.KeepAlive(x);
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int x)
				{
					if (x == 0) return;
					if (x == 1) return;

					System.GC.KeepAlive(x);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateCheckedStanzaAnalyzer>(
			source,
			s_on
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ANarrowingAssertionNeedsOnlyItsUpperBoundary()
	{
		const string source = """
			class C
			{
				void M(int x)
				{
					System.GC.KeepAlive(x);
					string text = x.ToString();
					System.Diagnostics.Debug.Assert(text != null);
					System.GC.KeepAlive(text);
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int x)
				{
					System.GC.KeepAlive(x);

					string text = x.ToString();
					System.Diagnostics.Debug.Assert(text != null);
					System.GC.KeepAlive(text);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateCheckedStanzaAnalyzer>(
			source,
			s_on
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task APlainAssertionIsNoCheckedStanza()
	{
		const string source = """
			class C
			{
				void M(int x)
				{
					System.GC.KeepAlive(x);
					System.Diagnostics.Debug.Assert(x == 12);
					System.GC.KeepAlive(x);
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int x)
				{
					System.GC.KeepAlive(x);
					System.Diagnostics.Debug.Assert(x == 12);
					System.GC.KeepAlive(x);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateCheckedStanzaAnalyzer>(
			source,
			s_on
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentAboveTheChainTravelsWithIt()
	{
		const string source = """
			class C
			{
				void M(int x)
				{
					System.GC.KeepAlive(x);
					// A zero value ends this step.
					if (x == 0) return;
					System.GC.KeepAlive(x);
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int x)
				{
					System.GC.KeepAlive(x);

					// A zero value ends this step.
					if (x == 0) return;

					System.GC.KeepAlive(x);
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateCheckedStanzaAnalyzer>(
			source,
			s_on
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnUnreadBindingEndsTheKeptDeclarationRun()
	{
		const string source = """
			class C
			{
				void M(int x)
				{
					int service = x;
					int cart = x + 1;
					if (cart == 0) return;
					System.GC.KeepAlive(service + cart);
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int x)
				{
					int service = x;

					int cart = x + 1;
					if (cart == 0) return;

					System.GC.KeepAlive(service + cart);
				}
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.KeepGuardWithBinding,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			DiagnosticDescriptors.SeparateDeclarationRuns,
		]);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source, rules);

		RoslynTestHarness.AssertFixedText(fixedSource, result);

		AnalyzerRunResult settled = await RoslynTestHarness.ReportAllAsync(fixedSource, rules);

		Assert.IsEmpty(settled.Diagnostics);
	}

	[TestMethod]
	public async Task AnEnabledOpeningGuardRuleKeepsItsLowerBoundary()
	{
		const string source = """
			class C
			{
				void M(int x)
				{
					if (x == 0) return;
					if (x == 1) return;
					System.GC.KeepAlive(x);
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int x)
				{
					if (x == 0) return;
					if (x == 1) return;

					System.GC.KeepAlive(x);
				}
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.SeparateGuardClauses,
			DiagnosticDescriptors.KeepChainOfGuardClausesTogether,
		]);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source, rules);

		RoslynTestHarness.AssertFixedText(fixedSource, result);

		AnalyzerRunResult settled = await RoslynTestHarness.ReportAllAsync(fixedSource, rules);

		Assert.IsEmpty(settled.Diagnostics);
	}

	[TestMethod]
	public async Task AJumpBelowTheCheckedStanzaGetsItsBoundaryFromTheJumpRule()
	{
		const string source = """
			class C
			{
				void M(int x)
				{
					System.GC.KeepAlive(x);
					if (x == 0) return;
					if (x == 1) return;
					return;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int x)
				{
					System.GC.KeepAlive(x);

					if (x == 0) return;
					if (x == 1) return;

					return;
				}
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.SeparateJumpStatement,
			DiagnosticDescriptors.KeepChainOfGuardClausesTogether,
		]);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source, rules);

		RoslynTestHarness.AssertFixedText(fixedSource, result);

		AnalyzerRunResult settled = await RoslynTestHarness.ReportAllAsync(fixedSource, rules);

		Assert.IsEmpty(settled.Diagnostics);
	}

	[TestMethod]
	public async Task SeveralCheckedBindingsStayWholeAboveAMultiLineGuard()
	{
		const string source = """
			class C
			{
				void M(int x)
				{
					System.GC.KeepAlive(x);
					int a = x;
					int b = x + 1;
					if (a == 0 || b == 0)
					{
						return;
					}
					System.GC.KeepAlive(a + b);
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int x)
				{
					System.GC.KeepAlive(x);

					int a = x;
					int b = x + 1;
					if (a == 0 || b == 0)
					{
						return;
					}

					System.GC.KeepAlive(a + b);
				}
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.KeepGuardWithBinding,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
		]);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source, rules);

		RoslynTestHarness.AssertFixedText(fixedSource, result);

		AnalyzerRunResult settled = await RoslynTestHarness.ReportAllAsync(fixedSource, rules);

		Assert.IsEmpty(settled.Diagnostics);
	}

	[TestMethod]
	public async Task AMixedAssertionPhaseIsNotAValidationStanza()
	{
		const string source = """
			class C
			{
				void M(int x)
				{
					System.GC.KeepAlive(x);
					string text = x.ToString();
					System.Diagnostics.Debug.Assert(text != null);
					System.Diagnostics.Debug.Assert(text.Length == 12);
					System.GC.KeepAlive(text);
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int x)
				{
					System.GC.KeepAlive(x);
					string text = x.ToString();
					System.Diagnostics.Debug.Assert(text != null);
					System.Diagnostics.Debug.Assert(text.Length == 12);
					System.GC.KeepAlive(text);
				}
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.SeparateCheckedStanza,
		]);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source, rules);

		RoslynTestHarness.AssertFixedText(fixedSource, result);

		AnalyzerRunResult settled = await RoslynTestHarness.ReportAllAsync(fixedSource, rules);

		Assert.IsEmpty(settled.Diagnostics);
	}

	[TestMethod]
	public async Task AThrowHelperRemainsAGuardWhenItsParameterNarrows()
	{
		const string source = """
			class C
			{
				void M(int x)
				{
					string text = x.ToString();
					System.ArgumentNullException.ThrowIfNull(text);
					System.GC.KeepAlive(text);
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int x)
				{
					string text = x.ToString();
					System.ArgumentNullException.ThrowIfNull(text);

					System.GC.KeepAlive(text);
				}
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.SeparateCheckedStanza,
		]);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source, rules);

		RoslynTestHarness.AssertFixedText(fixedSource, result);

		AnalyzerRunResult settled = await RoslynTestHarness.ReportAllAsync(fixedSource, rules);

		Assert.IsEmpty(settled.Diagnostics);
	}

	[TestMethod]
	public async Task ReadBindingsJoinTheWholeGuardChainAcrossExistingBlankLines()
	{
		const string source = """
			class C
			{
				void M(int x)
				{
					System.GC.KeepAlive(x);
					int a = x;

					int b = x + 1;

					if (a == 0)
					{
						return;
					}

					if (b == 0)
					{
						return;
					}
					System.GC.KeepAlive(a + b);
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int x)
				{
					System.GC.KeepAlive(x);

					int a = x;
					int b = x + 1;
					if (a == 0)
					{
						return;
					}
					if (b == 0)
					{
						return;
					}

					System.GC.KeepAlive(a + b);
				}
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.KeepGuardWithBinding,
			DiagnosticDescriptors.KeepChainOfGuardClausesTogether,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
		]);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source, rules);

		RoslynTestHarness.AssertFixedText(fixedSource, result);

		AnalyzerRunResult settled = await RoslynTestHarness.ReportAllAsync(fixedSource, rules);

		Assert.IsEmpty(settled.Diagnostics);
	}

	[TestMethod]
	[DataRow(true, "ZA0001")]
	[DataRow(false, "ZA0018")]
	public async Task ATransferredMultiLineBoundaryHasOneOwner(bool multiLine, string owner)
	{
		const string source = """
			class C
			{
				void M(int x)
				{
					System.GC.KeepAlive(x);
					int a = x;
					int b = x + 1;
					if (a == 0 || b == 0)
						return;
					System.GC.KeepAlive(a + b);
				}
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.KeepGuardWithBinding,
		]);

		if (multiLine)
			rules[DiagnosticDescriptors.SeparateMultiLineConstructs.Id] = ReportDiagnostic.Warn;

		AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(source, rules);

		Diagnostic[] upper =
		[
			.. result.Diagnostics.Where(diagnostic =>
				diagnostic.Location.SourceSpan.Start
				== source.IndexOf("int a", System.StringComparison.Ordinal)
			),
		];

		Assert.HasCount(1, upper);
		Assert.AreEqual(owner, upper[0].Id);
	}

	[TestMethod]
	[DataRow(true, "ZA0001")]
	[DataRow(false, "ZA0018")]
	public async Task ACheckedBorderEndsAnOtherwiseRepeatedDeclarationRun(
		bool multiLine,
		string owner
	)
	{
		const string source = """
			class C
			{
				void M(int x)
				{
					int unread = x;
					int checkedValue = x + 1;
					if (checkedValue == 0)
						return;
					System.GC.KeepAlive(unread + checkedValue);
				}
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.KeepGuardWithBinding,
			DiagnosticDescriptors.SeparateDeclarationRuns,
		]);

		if (multiLine)
			rules[DiagnosticDescriptors.SeparateMultiLineConstructs.Id] = ReportDiagnostic.Warn;

		AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(source, rules);

		Diagnostic[] upper =
		[
			.. result.Diagnostics.Where(diagnostic =>
				diagnostic.Location.SourceSpan.Start
				== source.IndexOf("int checkedValue", System.StringComparison.Ordinal)
			),
		];

		Assert.HasCount(1, upper);
		Assert.AreEqual(owner, upper[0].Id);
	}

	[TestMethod]
	public async Task AnUncheckedDeclarationRunOwnsTheSharedCheckedBorder()
	{
		const string source = """
			class C
			{
				void M(int x)
				{
					int first = x;
					int second = x + 1;
					int cart = x + 2;
					if (cart == 0) return;
					System.GC.KeepAlive(first + second + cart);
				}
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.KeepGuardWithBinding,
			DiagnosticDescriptors.SeparateDeclarationRuns,
		]);

		AnalyzerRunResult result = await RoslynTestHarness.ReportAllAsync(source, rules);

		Diagnostic[] upper =
		[
			.. result.Diagnostics.Where(diagnostic =>
				diagnostic.Location.SourceSpan.Start
				== source.IndexOf("int cart", System.StringComparison.Ordinal)
			),
		];

		Assert.HasCount(1, upper);
		Assert.AreEqual(DiagnosticDescriptors.SeparateDeclarationRuns.Id, upper[0].Id);
	}

	[TestMethod]
	[CrossCutting]
	[DataRow(false, false)]
	[DataRow(false, true)]
	[DataRow(true, false)]
	[DataRow(true, true)]
	public async Task NameofDoesNotAttachAnUnreadBindingToACheck(bool keepsGuard, bool readsValue)
	{
		string condition = readsValue
			? "nameof(value) == value.ToString()"
			: "((nameof(value))).Contains(tag)";

		string source =
			"class C { void M(string tag) {\nBefore();\nvar value = new object();\nif ("
			+ condition
			+ ") return;\nAfter();\n} void Before() {} void After() {} }";

		string fixedSource = readsValue
			? source.Replace("Before();\n", "Before();\n\n").Replace("After();\n", "\nAfter();\n")
			: source.Replace("if (", "\nif (").Replace("After();\n", "\nAfter();\n");

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.SeparateCheckedStanza,
			.. keepsGuard ? new[] { DiagnosticDescriptors.KeepGuardWithBinding } : [],
		]);

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source, rules);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("(value = 1) > 0", false)]
	[DataRow("Overwrite(out value) > 0", false)]
	[DataRow("Overwrite(out value!) > 0", false)]
	[DataRow("(value += 1) > 0", true)]
	[DataRow("Read(in value) > 0", true)]
	[DataRow("Touch(ref value) > 0", true)]
	public async Task WriteOnlyTargetsDoNotAttachAnUnreadBindingToACheck(
		string condition,
		bool readsValue
	)
	{
		string source =
			"class C { void M() {\nBefore();\nint value = 0;\nif ("
			+ condition
			+ ") return;\nAfter();\n} static int Overwrite(out int value) { value = 1; return value; } static int Read(in int value) => value; static int Touch(ref int value) => value++; void Before() {} void After() {} }";

		string fixedSource = readsValue
			? source.Replace("Before();\n", "Before();\n\n").Replace("After();\n", "\nAfter();\n")
			: source.Replace("if (", "\nif (").Replace("After();\n", "\nAfter();\n");

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<Nickelony.Stanza.Analyzers.BlankLines.SeparateCheckedStanzaAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.SeparateCheckedStanza)
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
	public async Task ExpressionLocalNamesCannotAttachTheFieldACheckFollows(
		string condition,
		bool readsValue
	)
	{
		string source =
			"using System.Linq;\nclass C { int value; void M(object input, int[] xs) {\nBefore();\nvalue = 7;\nif ("
			+ condition
			+ ") return;\nAfter();\n} void Before() {} void After() {} }";

		string fixedSource = readsValue
			? source.Replace("Before();\n", "Before();\n\n").Replace("After();\n", "\nAfter();\n")
			: source.Replace("if (", "\nif (").Replace("After();\n", "\nAfter();\n");

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<Nickelony.Stanza.Analyzers.BlankLines.SeparateCheckedStanzaAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.SeparateCheckedStanza)
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task TheImportSectionRuleAloneOwnsTheGapAboveATopLevelStanza()
	{
		const string source = """
			using System;
			var a = Environment.GetCommandLineArgs();
			if (a.Length == 0) return 1;
			return 0;
			""";

		const string fixedSource = """
			using System;

			var a = Environment.GetCommandLineArgs();
			if (a.Length == 0) return 1;

			return 0;
			""";

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.SeparateImportSection,
		]);

		AnalyzerRunResult reported = await RoslynTestHarness.ReportAllAsync(source, rules);

		RoslynTestHarness.AssertEachGapIsClaimedOnce(reported);

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			fixedSource,
			source,
			new RunOptions(RoslynTestHarness.AllAnalyzers) { DiagnosticOptions = rules }
		);
	}

	[TestMethod]
	[CrossCutting]
	public async Task APragmaOverTheStanzaReleasesItsGapsToTheDeclarationRule()
	{
		const string source = """
			class C
			{
				void M(int x)
				{
			#pragma warning disable ZA0018
					System.GC.KeepAlive(x);
					int w = x;
					int y = x + 1;
					if (y == 0) return;
					System.GC.KeepAlive(y + w);
			#pragma warning restore ZA0018
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int x)
				{
			#pragma warning disable ZA0018
					System.GC.KeepAlive(x);

					int w = x;
					int y = x + 1;

					if (y == 0) return;
					System.GC.KeepAlive(y + w);
			#pragma warning restore ZA0018
				}
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.SeparateDeclarationRuns,
		]);

		AnalyzerRunResult reported = await RoslynTestHarness.ReportAllAsync(source, rules);

		Assert.IsTrue(
			reported.Diagnostics.All(diagnostic =>
				diagnostic.Id == DiagnosticDescriptors.SeparateDeclarationRuns.Id
			)
		);

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			fixedSource,
			source,
			new RunOptions(RoslynTestHarness.AllAnalyzers) { DiagnosticOptions = rules }
		);
	}
}
