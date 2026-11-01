using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Analyzers.Documentation;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class SeverityKeyTests
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
		bool isDisabled = ReadRulesDisabledUnlessEnabled((CategoryKey, severity))
			.Contains(DiagnosticDescriptors.SeparateMultiLineConstructs.Id);

		Assert.AreEqual(disabled, isDisabled, $"The severity '{severity}' was read as disabling.");
	}

	[TestMethod]
	[DataRow("dotnet_analyzer_diagnostic.category-Formatting.severity")]
	[DataRow("dotnet_analyzer_diagnostic.severity")]
	public void ABulkSeverityKeyDisablesEveryOwner(string bulkKey)
	{
		ImmutableHashSet<string> disabled = ReadRulesDisabledUnlessEnabled((bulkKey, "none"));

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
		bool disabled = ReadRulesDisabledUnlessEnabled(
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
		ImmutableHashSet<string> disabled = ReadRulesDisabledUnlessEnabled((key, value));

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

	// <NoWarn> disables a rule for the compilation, and Roslyn lets that beat a severity line in .editorconfig,
	// so the gap ZA0002 defers to ZA0001 goes back to ZA0002.
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
			await RoslynTestHarness.ReportAsync<SeparateDeclarationRunsAnalyzer>(
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
	// so ZA0001 still owns the gap that ZA0002 defers to it.
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
		string deferring = DiagnosticDescriptors.SeparateDeclarationRuns.Id;

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateDeclarationRunsAnalyzer>(
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
			ReadRulesDisabledUnlessEnabled((key, value)),
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

	private static ImmutableHashSet<string> ReadRulesDisabledUnlessEnabled(
		params (string Key, string Value)[] settings
	)
	{
		var values = new Dictionary<string, string>();

		foreach ((string key, string value) in settings)
			values[key] = value;

		return StanzaOptions.From(new FixedConfigOptions(values)).RulesDisabledUnlessEnabled;
	}
}
