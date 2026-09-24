using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Bodies;

using static Nickelony.Stanza.Analyzers.Tests.BodyFamilyFixtures;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class UseExpressionBodyForSingleLineMemberTests
{
	[TestMethod]
	public async Task ThePlacementDecidesTheShapeOfABrokenBody()
	{
		const string source = """
			class C
			{
				string Describe(int value)
				{
					return value.ToString();
				}
			}
			""";

		const string trailingSource = """
			class C
			{
				string Describe(int value) =>
					value.ToString();
			}
			""";

		const string leadingSource = """
			class C
			{
				string Describe(int value)
					=> value.ToString();
			}
			""";

		Dictionary<string, string> width = new() { ["stanza_max_line_length"] = "40" };

		AnalyzerRunResult trailing =
			await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineMemberAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,
				analyzerConfigOptions: width
			);

		AnalyzerRunResult leading =
			await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineMemberAnalyzer>(
				source,

				TestRuleSets.Only(
					DiagnosticDescriptors.UseExpressionBodyForSingleLineMember,
					DiagnosticDescriptors.UseExpressionBodyForSingleLineValueMember,
					DiagnosticDescriptors.PutArrowOnLineAfterSignature
				),

				analyzerConfigOptions: width
			);

		Assert.HasCount(1, trailing.Diagnostics);
		RoslynTestHarness.AssertFixedText(trailingSource, trailing);

		Assert.HasCount(1, leading.Diagnostics);
		RoslynTestHarness.AssertFixedText(leadingSource, leading);
	}

	[TestMethod]
	public async Task ASingleLineBodyBecomesAnExpression()
	{
		const string source = """
			class C
			{
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
				int GetValue() => _value;

				private int _value;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineMemberAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);

		// Anchors on the brace that opens the block it would replace.
		Assert.AreEqual(3, result.DiagnosticLines[0]);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ASingleLineBlockIsCollapsedAsWell()
	{
		const string source = """
			class C
			{
				int GetValue() { return _value; }

				private int _value;
			}
			""";

		const string fixedSource = """
			class C
			{
				int GetValue() => _value;

				private int _value;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineMemberAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheStatementShapedKindsAreAllCollapsed()
	{
		const string source = """
			class C
			{
				C(int value)
				{
					_value = value;
				}

				~C()
				{
					_value = 0;
				}

				public static C operator +(C left, C right)
				{
					return left;
				}

				public static explicit operator int(C value)
				{
					return value._value;
				}

				int Throw()
				{
					throw new InvalidOperationException();
				}

				void Reset()
				{
					_value = 0;
				}

				int Count()
				{
					return Local();

					static int Local()
					{
						return 1;
					}
				}

				private int _value;
			}
			""";

		const string fixedSource = """
			class C
			{
				C(int value) => _value = value;

				~C() => _value = 0;

				public static C operator +(C left, C right) => left;

				public static explicit operator int(C value) => value._value;

				int Throw() => throw new InvalidOperationException();

				void Reset() => _value = 0;

				int Count()
				{
					return Local();

					static int Local() => 1;
				}

				private int _value;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineMemberAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(7, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheRuleLeavesOtherBodiesAlone()
	{
		const string source = """
			class C
			{
				int TwoStatements()
				{
					int value = 1;
					return value;
				}

				void ReturnsNothing()
				{
					return;
				}

				IEnumerable<int> Yields()
				{
					yield return 1;
				}

				int WrappedStatement()
				{
					return Build(
						1);
				}

				int Commented()
				{
					// why
					return 1;
				}

				int Decided()
				{
					if (_value > 0)
						return 1;

					return 2;
				}

				void Runs()
				{
					Action action = () =>
					{
						DoThing();
					};

					_ = action;
				}

				int Build(int value) => value;

				void DoThing()
				{
				}

				private int _value;
			}
			""";

		// Shapes the arrow cannot carry or would swallow stay: two statements, a bare return, a yield,
		// a wrapped statement, an interior comment, a branch, a lambda body.
		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineMemberAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheWidthDecidesTheShapeOfACollapsedMember()
	{
		const string source = """
			class C
			{
				string Describe(int value)
				{
					return value.ToString();
				}
			}
			""";

		const string oneLine = """
			class C
			{
				string Describe(int value) => value.ToString();
			}
			""";

		const string broken = """
			class C
			{
				string Describe(int value) =>
					value.ToString();
			}
			""";

		// The signature line is 30 columns, its line with the arrow 33, the statement's 25,
		// and the content of the member 47.
		AnalyzerRunResult narrow =
			await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineMemberAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "40",
				}
			);

		Assert.HasCount(1, narrow.Diagnostics);
		RoslynTestHarness.AssertFixedText(broken, narrow);

		AnalyzerRunResult arrowDoesNotFit =
			await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineMemberAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "31",
				}
			);

		Assert.IsEmpty(
			arrowDoesNotFit.Diagnostics,
			"The arrow ends the signature's line, which needs 33 columns."
		);

		AnalyzerRunResult byDefault =
			await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineMemberAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, byDefault.Diagnostics);
		RoslynTestHarness.AssertFixedText(oneLine, byDefault);

		AnalyzerRunResult uncheckedRun =
			await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineMemberAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "0",
				}
			);

		// Zero is not a width the collapse rules measure a line against,
		// so they report nothing rather than collapse every member that could take one line.
		Assert.IsEmpty(uncheckedRun.Diagnostics);
	}

	[TestMethod]
	public async Task TheTriggerDecidesWhetherTheArrowPaysForItsLine()
	{
		const string source = """
			class C
			{
				string Describe(int value)
				{
					return value.ToString();
				}
			}
			""";

		const string broken = """
			class C
			{
				string Describe(int value) =>
					value.ToString();
			}
			""";

		// The signature line is 30 columns, its line with the arrow 33, and the statement's 25,
		// so 31 misses the arrow while either line still fits on its own.
		foreach (string charged in new[] { "either", "signature" })
		{
			AnalyzerRunResult result = await RunCollapseAsync(source, 31, charged);

			Assert.IsEmpty(
				result.Diagnostics,
				$"'{charged}' charges the arrow to the line it lands on."
			);
		}

		foreach (string free in new[] { "body", "both" })
		{
			AnalyzerRunResult result = await RunCollapseAsync(source, 31, free);

			Assert.HasCount(1, result.Diagnostics, $"'{free}' frees the arrow from the width.");
			RoslynTestHarness.AssertFixedText(broken, result);
		}

		// Below the signature's own 30 columns no line the fix could write holds it.
		foreach (string trigger in new[] { "either", "signature", "body", "both" })
		{
			AnalyzerRunResult result = await RunCollapseAsync(source, 28, trigger);

			Assert.IsEmpty(
				result.Diagnostics,
				$"'{trigger}' cannot write a line for that signature."
			);
		}
	}

	[TestMethod]
	public async Task AWithOrQueryBodyKeepsItsBlockWhereTheTriggerCountsTheBrokenBody()
	{
		const string with = """
			record R(int A);

			class C
			{
				R Describe(R value)
				{
					return value with { A = 1 };
				}
			}
			""";

		const string brokenWith = """
			record R(int A);

			class C
			{
				R Describe(R value) =>
					value with { A = 1 };
			}
			""";

		const string query = """
			using System.Linq;

			class C
			{
				object Describe(int[] items)
				{
					return from item in items select item;
				}
			}
			""";

		// Each signature line with its arrow and each statement's line fit 40 columns,
		// and neither member fits one line, so only the broken shape could take them.
		foreach (string source in new[] { with, query })
		{
			foreach (string counted in new[] { "either", "body" })
			{
				AnalyzerRunResult result = await ReportCollapseAsync(source, 40, counted);

				Assert.IsEmpty(
					result.Diagnostics,
					$"'{counted}' counts the body that CSharpier breaks after the arrow: {source}"
				);
			}

			foreach (string free in new[] { "signature", "both" })
			{
				AnalyzerRunResult result = await ReportCollapseAsync(source, 40, free);

				Assert.HasCount(
					1,
					result.Diagnostics,
					$"'{free}' leaves the body uncounted: {source}"
				);
			}

			// A member that fits one line gives CSharpier no arrow to break after.
			AnalyzerRunResult wide = await ReportCollapseAsync(source, 80, "either");
			Assert.HasCount(1, wide.Diagnostics, source);
		}

		RoslynTestHarness.AssertFixedText(
			brokenWith,
			await RunCollapseAsync(with, 40, "signature")
		);
	}

	// A member at column zero takes the file's indentation for its continuation, so a file that indents with tabs measures a tab.
	[TestMethod]
	public async Task AColumnZeroMemberMeasuresTheTabItsContinuationTakes()
	{
		const string source = """
			P(true);

			int P(bool f)
			{
				return f ? 1 : 2;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseExpressionBodyForSingleLineMember),

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "16",
					["tab_width"] = "8",
					["indent_size"] = "4",
				}
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheWidthOfATabIsTheOneTheFileNames()
	{
		const string source = """
			class C
			{
				int GetValue()
				{
					return _value;
				}

				private int _value;
			}
			""";

		const string oneLine = """
			class C
			{
				int GetValue() => _value;

				private int _value;
			}
			""";

		const string broken = """
			class C
			{
				int GetValue() =>
					_value;

				private int _value;
			}
			""";

		// The signature line is 14 columns plus a tab, the statement is 7 columns,
		// and the arrow adds 3 to the line it ends.
		// The widths below are that arrow line and the content line,
		// which is the signature line plus the arrow, a space either side, and the statement.
		(
			string Label,
			Dictionary<string, string> Settings,
			int ArrowLine,
			int ContentWidth
		)[] cases =
		[
			("no width keys, so a tab is 4 columns", [], 21, 29),
			("stanza_tab_width = 2", new() { ["stanza_tab_width"] = "2" }, 19, 27),
			("tab_width = 2", new() { ["tab_width"] = "2" }, 19, 27),
			("indent_size = 2", new() { ["indent_size"] = "2" }, 19, 27),

			(
				"tab_width = 8 beside indent_size = 2",
				new() { ["tab_width"] = "8", ["indent_size"] = "2" },
				25,
				33
			),

			(
				"stanza_tab_width = 2 beside tab_width = 8",
				new() { ["stanza_tab_width"] = "2", ["tab_width"] = "8" },
				19,
				27
			),

			("indent_size = tab, which is not a width", new() { ["indent_size"] = "tab" }, 21, 29),

			(
				"stanza_tab_width = 0, which is not a width either",
				new() { ["stanza_tab_width"] = "0" },
				21,
				29
			),
		];

		foreach (
			(
				string label,
				Dictionary<string, string> settings,
				int arrowLine,
				int contentWidth
			) in cases
		)
		{
			AnalyzerRunResult atTheArrowLine =
				await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineMemberAnalyzer>(
					source,
					TestRuleSets.StandaloneRulesEnabled,

					analyzerConfigOptions: new Dictionary<string, string>(settings)
					{
						["stanza_max_line_length"] = $"{arrowLine}",
					}
				);

			Assert.HasCount(
				1,
				atTheArrowLine.Diagnostics,
				$"{label}: both lines of the member fit a width of {arrowLine}."
			);

			RoslynTestHarness.AssertFixedText(broken, atTheArrowLine, label);

			AnalyzerRunResult oneColumnShort =
				await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineMemberAnalyzer>(
					source,
					TestRuleSets.StandaloneRulesEnabled,

					analyzerConfigOptions: new Dictionary<string, string>(settings)
					{
						["stanza_max_line_length"] = $"{arrowLine - 1}",
					}
				);

			Assert.IsEmpty(
				oneColumnShort.Diagnostics,
				$"{label}: the arrow needs {arrowLine} columns on the signature's line."
			);

			AnalyzerRunResult atTheContentWidth =
				await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineMemberAnalyzer>(
					source,
					TestRuleSets.StandaloneRulesEnabled,

					analyzerConfigOptions: new Dictionary<string, string>(settings)
					{
						["stanza_max_line_length"] = $"{contentWidth}",
					}
				);

			Assert.HasCount(1, atTheContentWidth.Diagnostics, label);

			RoslynTestHarness.AssertFixedText(
				oneLine,
				atTheContentWidth,
				$"{label}: the content of the member fits a width of {contentWidth}."
			);

			AnalyzerRunResult belowTheContentWidth =
				await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineMemberAnalyzer>(
					source,
					TestRuleSets.StandaloneRulesEnabled,

					analyzerConfigOptions: new Dictionary<string, string>(settings)
					{
						["stanza_max_line_length"] = $"{contentWidth - 1}",
					}
				);

			Assert.HasCount(1, belowTheContentWidth.Diagnostics, label);

			RoslynTestHarness.AssertFixedText(
				broken,
				belowTheContentWidth,
				$"{label}: the content of the member needs {contentWidth} columns on one line."
			);
		}
	}

	[TestMethod]
	public async Task AContinuationUsesIndentSizeWhenSpacesAreTheIndent()
	{
		const string source = """
			class C
			{
			  int A()
			  {
			    return 12345;
			  }
			}
			""";

		const string fixedSource = """
			class C
			{
			  int A() =>
			    12345;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineMemberAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["indent_style"] = "space",
					["indent_size"] = "2",
					["tab_width"] = "4",
					["stanza_multiline_body_trigger"] = "body",
					["stanza_max_line_length"] = "10",
				}
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheExpressionBodyFixFindsTheBlockWithoutAnAdditionalLocation()
	{
		const string source = """
			class C
			{
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
				int GetValue() => _value;

				private int _value;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<UseExpressionBodyForSingleLineMemberAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnExpressionBodyIsWrittenOnlyWhereTheLanguageVersionHasIt()
	{
		const string method = """
			class C
			{
				int GetValue()
				{
					return _value;
				}

				private int _value;
			}
			""";

		const string constructor = """
			class C
			{
				C()
				{
					_value = 1;
				}

				private int _value;
			}
			""";

		const string thrower = """
			class C
			{
				void Fail()
				{
					throw new System.InvalidOperationException();
				}
			}
			""";

		(string Source, LanguageVersion Version, int Expected)[] cases =
		[
			(method, LanguageVersion.CSharp5, 0),
			(method, LanguageVersion.CSharp6, 1),
			(constructor, LanguageVersion.CSharp6, 0),
			(constructor, LanguageVersion.CSharp7, 1),
			(thrower, LanguageVersion.CSharp6, 0),
			(thrower, LanguageVersion.CSharp7, 1),
		];

		foreach ((string source, LanguageVersion version, int expected) in cases)
		{
			using IDisposable scope = RoslynTestHarness.AtLanguageVersion(version);

			AnalyzerRunResult result =
				await RoslynTestHarness.ReportAsync<UseExpressionBodyForSingleLineMemberAnalyzer>(
					source,
					TestRuleSets.StandaloneRulesEnabled
				);

			Assert.HasCount(expected, result.Diagnostics, $"{version}: {source}");
		}
	}

	[TestMethod]
	[DataRow(LanguageVersion.CSharp6, false)]
	[DataRow(LanguageVersion.CSharp7, true)]
	public async Task ADestructorTakesAnExpressionBodyFromCSharp7(
		LanguageVersion version,
		bool isCollapsed
	)
	{
		const string source = """
			class C
			{
				~C()
				{
					Release();
				}

				void Release() { }
			}
			""";

		const string fixedSource = """
			class C
			{
				~C() => Release();

				void Release() { }
			}
			""";

		using IDisposable scope = RoslynTestHarness.AtLanguageVersion(version);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineMemberAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(isCollapsed ? 1 : 0, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(isCollapsed ? fixedSource : source, result);
	}

	// A fix writes the arrow over the gaps it closes, so a comment in one of them would be lost.
	[TestMethod]
	[DataRow("return 1 /*c*/;")]
	[DataRow("return /*c*/ 1;")]
	[DataRow("return 1; // c")]
	public async Task AMethodWhoseStatementHoldsACommentKeepsItsBlock(string statement)
	{
		string source = $$"""
			class C
			{
				int Value()
				{
					{{statement}}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineMemberAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AMethodBodyHoldingADirectiveIsLeftAlone()
	{
		const string source = """
			class C
			{
				int Value()
				{
			#if STANZA_UNDEFINED
					return 1;
			#else
					return 2;
			#endif
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineMemberAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	private static Task<AnalyzerRunResult> ReportCollapseAsync(
		string source,
		int width,
		string trigger
	)
	{
		Dictionary<string, string> options = Trigger(trigger);
		options["stanza_max_line_length"] = $"{width}";

		return RoslynTestHarness.ReportAsync<UseExpressionBodyForSingleLineMemberAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled,
			analyzerConfigOptions: options
		);
	}

	private static Task<AnalyzerRunResult> RunCollapseAsync(
		string source,
		int width,
		string trigger
	)
	{
		Dictionary<string, string> options = Trigger(trigger);
		options["stanza_max_line_length"] = $"{width}";

		return RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineMemberAnalyzer>(
			source,
			TestRuleSets.StandaloneRulesEnabled,
			analyzerConfigOptions: options
		);
	}
}
