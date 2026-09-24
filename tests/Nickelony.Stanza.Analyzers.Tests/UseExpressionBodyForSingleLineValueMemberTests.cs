using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Bodies;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class UseExpressionBodyForSingleLineValueMemberTests
{
	[TestMethod]
	public async Task AnInlineAccessorKeepsItsLineWhenTheWholeLineExceedsTheWidth()
	{
		const string source = """
			class C
			{
				int Value { get { return 1; } set { } }
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineValueMemberAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "20",
				}
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task APropertyWithALoneGetterBecomesAnArrow()
	{
		const string source = """
			class C
			{
				int Value
				{
					get { return 1; }
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				int Value => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineValueMemberAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);

		Assert.AreEqual(4, result.DiagnosticLines[0]);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnIndexerWithALoneGetterBecomesAnArrow()
	{
		const string source = """
			class C
			{
				int this[int index]
				{
					get { return index; }
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				int this[int index] => index;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineValueMemberAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnEventAccessorListIsCollapsedOneAccessorAtATime()
	{
		const string source = """
			class C
			{
				public event Handler Notify
				{
					add { _handlers += value; }
					remove { _handlers -= value; }
				}

				public delegate void Handler(int value);

				private Handler? _handlers;
			}
			""";

		const string fixedSource = """
			class C
			{
				public event Handler Notify
				{
					add => _handlers += value;
					remove => _handlers -= value;
				}

				public delegate void Handler(int value);

				private Handler? _handlers;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineValueMemberAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("set")]
	[DataRow("init")]
	public async Task AnAccessorInsideAListBecomesAnArrow(string setter)
	{
		string source = $$"""
			class C
			{
				int Value
				{
					get { return _field; }

					{{setter}} { _field = value; }
				}

				int _field;
			}
			""";

		string fixedSource = $$"""
			class C
			{
				int Value
				{
					get => _field;

					{{setter}} => _field = value;
				}

				int _field;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineValueMemberAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ASetOnlyMemberCollapsesItsAccessorAlone()
	{
		const string source = """
			class C
			{
				int Value
				{
					set { _field = value; }
				}

				int _field;
			}
			""";

		const string fixedSource = """
			class C
			{
				int Value
				{
					set => _field = value;
				}

				int _field;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineValueMemberAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheValueRuleLeavesOtherBodiesAlone()
	{
		const string source = """
			class C
			{
				int Value
				{
					get
					{
						int local = 1;
						return local;
					}

					set
					{
						// why
						_field = value;
					}
				}

				int this[
					int index]
				{
					get { return 1; }
				}

				int _field;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineValueMemberAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AValueMemberThatCannotShareOneLineBreaksAfterItsArrow()
	{
		const string source = """
			class C
			{
				string Description
				{
					get { return "a value that is long enough"; }
				}
			}
			""";

		const string oneLine = """
			class C
			{
				string Description => "a value that is long enough";
			}
			""";

		const string broken = """
			class C
			{
				string Description =>
					"a value that is long enough";
			}
			""";

		// The member's line is 22 columns, its line with the arrow 25 and the statement's 38,
		// while the whole member on one line is 56.
		AnalyzerRunResult narrow =
			await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineValueMemberAnalyzer>(
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
			await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineValueMemberAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "24",
				}
			);

		Assert.IsEmpty(
			arrowDoesNotFit.Diagnostics,
			"The arrow needs 25 columns to close the member's line."
		);

		AnalyzerRunResult byDefault =
			await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineValueMemberAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, byDefault.Diagnostics);
		RoslynTestHarness.AssertFixedText(oneLine, byDefault);

		AnalyzerRunResult uncheckedRun =
			await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineValueMemberAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled,

				analyzerConfigOptions: new Dictionary<string, string>
				{
					["stanza_max_line_length"] = "0",
				}
			);

		// The value half reports nothing on a width of zero exactly as the statement half does.
		Assert.IsEmpty(uncheckedRun.Diagnostics);
	}

	[TestMethod]
	public async Task AWithValueKeepsItsAccessorWhereTheTriggerCountsTheBrokenBody()
	{
		const string source = """
			record R(int A);

			class C
			{
				R _value;

				R Value
				{
					get { return _value with { A = 1 }; }
				}
			}
			""";

		// The member's line with its arrow is 14 columns and the statement's 30,
		// while the whole member on one line is 37.
		(string Trigger, int Expected)[] cases =
		[
			("either", 0),
			("body", 0),
			("signature", 1),
			("both", 1),
		];

		foreach ((string trigger, int expected) in cases)
		{
			Dictionary<string, string> options = BodyFamilyFixtures.Trigger(trigger);
			options["stanza_max_line_length"] = "32";

			AnalyzerRunResult result =
				await RoslynTestHarness.ReportAsync<UseExpressionBodyForSingleLineValueMemberAnalyzer>(
					source,
					TestRuleSets.StandaloneRulesEnabled,
					analyzerConfigOptions: options
				);

			Assert.HasCount(expected, result.Diagnostics, trigger);
		}
	}

	[TestMethod]
	public async Task TheValueExpressionBodyFixFindsTheBlockWithoutAnAdditionalLocation()
	{
		const string source = """
			class C
			{
				int Value
				{
					get { return 1; }
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				int Value => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<UseExpressionBodyForSingleLineValueMemberAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABlockBodyHoldingADirectiveIsLeftAlone()
	{
		const string source = """
			class C
			{
				int Value
				{
					get
					{
			#if STANZA_UNDEFINED
						return 1;
			#else
						return 2;
			#endif
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineValueMemberAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnAccessorThatUsesTheFieldKeywordTakesAnExpressionBody()
	{
		RoslynTestHarness.RequireRoslynWithCSharp14();

		const string source = """
			class C
			{
				int Value
				{
					get
					{
						return field;
					}
					set
					{
						field = value;
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				int Value
				{
					get => field;
					set => field = value;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineValueMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseExpressionBodyForSingleLineValueMember)
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// A property or an indexer takes an arrow from C# 6, and an accessor of a list takes one from C# 7.
	[TestMethod]
	[DataRow(LanguageVersion.CSharp5, "int Value { get { return 1; } }", null)]
	[DataRow(LanguageVersion.CSharp6, "int Value { get { return 1; } }", "int Value => 1;")]
	[DataRow(LanguageVersion.CSharp5, "int this[int i] { get { return i; } }", null)]
	[DataRow(
		LanguageVersion.CSharp6,
		"int this[int i] { get { return i; } }",
		"int this[int i] => i;"
	)]
	[DataRow(LanguageVersion.CSharp6, "int Value { get { return 1; } set { } }", null)]
	[DataRow(
		LanguageVersion.CSharp7,
		"int Value { get { return 1; } set { } }",
		"int Value { get => 1; set { } }"
	)]
	public async Task AnArrowIsWrittenOnlyWhereTheLanguageVersionHasItForThatOwner(
		LanguageVersion version,
		string member,
		string? fixedMember
	)
	{
		string source = $$"""
			class C
			{
				{{member}}
			}
			""";

		string fixedSource = $$"""
			class C
			{
				{{fixedMember ?? member}}
			}
			""";

		using IDisposable scope = RoslynTestHarness.AtLanguageVersion(version);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineValueMemberAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(fixedMember is null ? 0 : 1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AGetterOnlyPropertyWithAnInitializerCollapsesOnlyItsAccessor()
	{
		RoslynTestHarness.RequireRoslynWithCSharp14();

		const string source = """
			class C(string name)
			{
				public string Name { get { return field.ToUpperInvariant(); } } = name;

				public int Count
				{
					get { return field; }
				} = 3;
			}
			""";

		const string fixedSource = """
			class C(string name)
			{
				public string Name { get => field.ToUpperInvariant(); } = name;

				public int Count
				{
					get => field;
				} = 3;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseExpressionBodyForSingleLineValueMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseExpressionBodyForSingleLineValueMember)
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
