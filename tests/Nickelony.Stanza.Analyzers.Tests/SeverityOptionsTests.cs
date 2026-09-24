using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Analyzers.Documentation;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class SeverityOptionsTests
{
	private static readonly string[] s_boundaryOwners =
	[
		.. StanzaRules
			.All.Where(static rule => !rule.DefersTo.IsDefault)
			.SelectMany(static rule => rule.DefersTo)
			.Select(static owner => owner.Id)
			.Distinct(),
	];

	private static string CategoryKey =>
		$"dotnet_analyzer_diagnostic.category-{DiagnosticDescriptors.SeparateMultiLineConstructs.Category}.severity";

	[TestMethod]
	[DataRow("none", true)]
	[DataRow("silent", false)]
	[DataRow("suggestion", false)]
	[DataRow("warning", false)]
	[DataRow("error", false)]
	[DataRow("default", false)]
	[DataRow("false", false)]
	[DataRow("unset", false)]
	[DataRow("", false)]
	[DataRow("warn", false)]
	public void OnlyTheNoneSeverityDisablesAnOwner(string severity, bool disabled)
	{
		bool isDisabled = ReadRulesOffUnlessEnabled((CategoryKey, severity))
			.Contains(DiagnosticDescriptors.SeparateMultiLineConstructs.Id);

		Assert.AreEqual(disabled, isDisabled, $"The severity '{severity}' was read as disabling.");
	}

	[TestMethod]
	[DataRow("dotnet_analyzer_diagnostic.category-Formatting.severity")]
	[DataRow("dotnet_analyzer_diagnostic.severity")]
	public void ABulkSeverityKeyDisablesEveryOwner(string bulkKey)
	{
		ImmutableHashSet<string> disabled = ReadRulesOffUnlessEnabled((bulkKey, "none"));

		foreach (string ownerId in s_boundaryOwners)
			Assert.Contains(ownerId, disabled, $"{bulkKey} does not disable {ownerId}.");
	}

	// A category value that names no severity leaves `dotnet_analyzer_diagnostic.severity` to decide, as Roslyn reads it.
	[TestMethod]
	[DataRow("warning", "none", false)]
	[DataRow("none", "warning", true)]
	[DataRow("default", "none", false)]
	[DataRow("refactoring", "none", false)]
	[DataRow("bogus", "none", true)]
	public void TheMostSpecificSeverityKeyDecides(
		string categorySeverity,
		string bulkSeverity,
		bool expected
	)
	{
		bool disabled = ReadRulesOffUnlessEnabled(
				(CategoryKey, categorySeverity),
				("dotnet_analyzer_diagnostic.severity", bulkSeverity)
			)
			.Contains(DiagnosticDescriptors.SeparateMultiLineConstructs.Id);

		Assert.AreEqual(expected, disabled);
	}

	[TestMethod]
	[DataRow("dotnet_diagnostic.ZA9999.severity", "none")]
	[DataRow("dotnet_analyzer_diagnostic.category-Formatting.severity", "default")]
	public void AnOwnerNoKeyNamesKeepsTheDefaultItShipsWith(string key, string value)
	{
		ImmutableHashSet<string> disabled = ReadRulesOffUnlessEnabled((key, value));

		foreach (string ownerId in s_boundaryOwners)
		{
			bool shipsOff = !StanzaRules.Find(ownerId)!.Descriptor.IsEnabledByDefault;

			Assert.AreEqual(
				shipsOff,
				disabled.Contains(ownerId),
				$"'{key} = {value}' decided {ownerId} on its own."
			);
		}
	}

	[TestMethod]
	[CrossCutting]
	public async Task AnInfoSeverityRuleStillReportsAndIsStillFixed()
	{
		const string source = """
			/// <summary>One two three four five six seven eight nine.</summary>
			class C
			{
			}
			""";

		Dictionary<string, string> options = new() { ["stanza_max_line_length"] = "40" };

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.SuggestionTierEnabled,
				analyzerConfigOptions: options
			);

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual(DiagnosticSeverity.Info, result.Diagnostics[0].Severity);

		const string fixedSource = """
			/// <summary>
			/// One two three four five six seven
			/// eight nine.
			/// </summary>
			class C
			{
			}
			""";

		RoslynTestHarness.AssertFixedText(fixedSource, result);

		// The control: the width is what the rule reports for, not the snippet on its own.
		AnalyzerRunResult wide =
			await RoslynTestHarness.RunAsync<LayOutDocumentationCommentAnalyzer>(
				source,
				TestRuleSets.SuggestionTierEnabled
			);

		Assert.IsEmpty(wide.Diagnostics);
	}

	[TestMethod]
	[DataRow("99999999999999999999")]
	[DataRow("-4")]
	[DataRow("not a number")]
	public void ACountTheReaderCannotTakeLeavesTheDefault(string value)
	{
		int fallback = StanzaOptions.From(new ProbeOptions([])).MaxLineLength;

		Dictionary<string, string> values = new() { ["stanza_max_line_length"] = value };

		Assert.AreEqual(
			fallback,
			StanzaOptions.From(new ProbeOptions(values)).MaxLineLength,
			$"The value '{value}' reached the width."
		);
	}

	private const string TightAccessors = """
		class C
		{
			private int _value;

			public int Value
			{
				get
				{
					return _value;
				}
				set
				{
					_value = value;
				}
			}
		}
		""";

	private const string SpacedAccessors = """
		class C
		{
			private int _value;

			public int Value
			{
				get
				{
					return _value;
				}

				set
				{
					_value = value;
				}
			}
		}
		""";

	[TestMethod]
	[CrossCutting]
	public async Task AGlobalConfigSeverityGivesTheSpacedHalfItsGap()
	{
		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineAccessorsAnalyzer>(
				TightAccessors,
				globalOptions: TestRuleSets.Only(DiagnosticDescriptors.SeparateMultiLineAccessors)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(SpacedAccessors, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task AGlobalConfigSeverityGivesTheTightHalfItsGap()
	{
		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepMultiLineAccessorsTogetherAnalyzer>(
				SpacedAccessors,

				globalOptions: TestRuleSets.Only(
					DiagnosticDescriptors.KeepMultiLineAccessorsTogether
				)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(TightAccessors, result);
	}

	[TestMethod]
	[DataRow("stanza_max_line_length", 1000)]
	[DataRow("stanza_doc_max_line_length", 1000)]
	[DataRow("stanza_tab_width", 64)]
	[DataRow("stanza_doc_indent_size", 64)]
	[DataRow("stanza_doc_min_tail_words", 1000)]
	[DataRow("stanza_doc_min_line_words", 1000)]
	[DataRow("stanza_doc_max_overflow_columns", 1000)]
	[DataRow("stanza_named_argument_count", 1000)]
	[DataRow("stanza_markup_max_blank_lines", 1000)]
	public void ACountAboveItsCapLeavesTheDefault(string key, int cap)
	{
		int fallback = ReadNumber(key, new ProbeOptions([]));

		Assert.AreEqual(
			cap,
			ReadNumber(key, new ProbeOptions(new() { [key] = $"{cap}" })),
			"The cap itself was refused."
		);

		Assert.AreEqual(
			fallback,
			ReadNumber(key, new ProbeOptions(new() { [key] = $"{cap + 1}" })),
			"A value above the cap was read."
		);
	}

	private static int ReadNumber(string key, ProbeOptions options)
	{
		var read = StanzaOptions.From(options);

		return key switch
		{
			"stanza_max_line_length" => read.MaxLineLength,
			"stanza_tab_width" => read.TabColumns,
			"stanza_named_argument_count" => read.NamedArgumentCount,
			"stanza_markup_max_blank_lines" => read.Markup.MaxBlankLines,
			"stanza_doc_max_line_length" => read.DocLayout!.Value.ContentWidth,
			"stanza_doc_indent_size" => read.DocLayout!.Value.IndentSize,
			"stanza_doc_min_tail_words" => read.DocLayout!.Value.MinTailWords,
			"stanza_doc_min_line_words" => read.DocLayout!.Value.MinLineWords,
			"stanza_doc_max_overflow_columns" => read.DocLayout!.Value.MaxOverflowColumns,
			_ => throw new AssertFailedException($"No reader for {key}."),
		};
	}

	// <NoWarn> disables a rule for the compilation, and Roslyn lets that beat a severity line in .editorconfig,
	// so the gap ZA0001 defers to ZA0004 goes back to ZA0001.
	[TestMethod]
	[CrossCutting]
	public async Task ARuleThatNoWarnDisablesIsNoOwnerToDeferTo()
	{
		const string source = """
			class C
			{
				void M()
				{
					var count = 1;
					var total = 2;
					if (count > total)
					{
						DoThing();
					}
				}
			}
			""";

		string deferredTo = DiagnosticDescriptors.SeparateMultiLineConstructs.Id;

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(
				source,

				diagnosticOptions: new Dictionary<string, ReportDiagnostic>
				{
					[deferredTo] = ReportDiagnostic.Suppress,
				},

				treeOptions: new Dictionary<string, ReportDiagnostic>
				{
					[deferredTo] = ReportDiagnostic.Warn,
				}
			);

		Assert.HasCount(1, result.Diagnostics);
	}

	// Roslyn lets a rule's own key block a bulk key, and `default` keeps the state the rule ships with,
	// so ZA0004 still owns the gap that ZA0001 defers to it.
	[TestMethod]
	[CrossCutting]
	public async Task ARuleSetToDefaultKeepsItsShippedStateUnderABulkKey()
	{
		const string source = """
			class C
			{
				void M()
				{
					var count = 1;
					var total = 2;
					if (count > total)
					{
						DoThing();
					}
				}
			}
			""";

		string deferredTo = DiagnosticDescriptors.SeparateMultiLineConstructs.Id;
		string deferring = DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns.Id;

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDeclarationAndInvocationRunsAnalyzer>(
				source,

				treeOptions: new Dictionary<string, ReportDiagnostic>
				{
					[CategoryKey] = ReportDiagnostic.Suppress,
					[deferredTo] = ReportDiagnostic.Default,
					[deferring] = ReportDiagnostic.Warn,
				}
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	[CrossCutting]
	public async Task AHalfThatNoWarnDisablesLeavesTheGapToTheOtherHalf()
	{
		const string source = """
			class C
			{
				int M(int value)
				{
					switch (value)
					{
						case 1:
							return 1;
						case 2:
							return 2;
					}

					return 0;
				}
			}
			""";

		string tight = DiagnosticDescriptors.KeepMultiLineSwitchSectionsTogether.Id;

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateMultiLineSwitchSectionsAnalyzer>(
				source,

				diagnosticOptions: new Dictionary<string, ReportDiagnostic>
				{
					[tight] = ReportDiagnostic.Suppress,
				},

				treeOptions: new Dictionary<string, ReportDiagnostic>
				{
					[tight] = ReportDiagnostic.Warn,
				}
			);

		Assert.HasCount(1, result.Diagnostics);
	}

	// Roslyn applies a bulk key only to a rule that ships on, so a rule that ships off stays off whatever it says.
	[TestMethod]
	[DataRow("dotnet_analyzer_diagnostic.severity", "suggestion")]
	[DataRow("dotnet_analyzer_diagnostic.severity", "warning")]
	[DataRow("dotnet_analyzer_diagnostic.severity", "error")]
	[DataRow("dotnet_analyzer_diagnostic.category-Formatting.severity", "suggestion")]
	[DataRow("dotnet_analyzer_diagnostic.category-Formatting.severity", "warning")]
	[DataRow("dotnet_analyzer_diagnostic.category-Formatting.severity", "error")]
	public void ABulkSeverityLeavesAnOwnerThatShipsOffOff(string key, string value)
	{
		string ownerId = DiagnosticDescriptors.LayOutDocumentationComment.Id;

		Assert.Contains(
			ownerId,
			ReadRulesOffUnlessEnabled((key, value)),
			$"The bulk key {key} = {value} switched {ownerId} on."
		);
	}

	// An analyzer gets the rule it defers to through its entry, which throws for a deferral the entry does not record.
	[TestMethod]
	public void EachDeferralNamesARecordedRuleWithAnEntry()
	{
		Assert.IsTrue(
			s_boundaryOwners.All(static id => StanzaRules.Find(id) is not null),
			"a rule defers to one with no entry."
		);

		Assert.ThrowsExactly<InvalidOperationException>(() =>
			StanzaRules.SeparateMultiLineConstructs.GetDeferral(
				DiagnosticDescriptors.SeparateMemberGroups
			)
		);
	}

	private static ImmutableHashSet<string> ReadRulesOffUnlessEnabled(
		params (string Key, string Value)[] settings
	)
	{
		var values = new Dictionary<string, string>();

		foreach ((string key, string value) in settings)
			values[key] = value;

		return StanzaOptions.From(new ProbeOptions(values)).RulesOffUnlessEnabled;
	}

	private sealed class ProbeOptions(Dictionary<string, string> values) : AnalyzerConfigOptions
	{
		public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
			values.TryGetValue(key, out value);
	}
}
