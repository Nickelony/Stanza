using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.CodeFixes;
using Nickelony.Stanza.Markup.Core;
using Nickelony.Stanza.Markup.Core.DocComments;
using Nickelony.Stanza.Markup.Core.Options;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>Checks that the registries, descriptors and providers agree with each other.</summary>
[TestClass]
public sealed partial class RuleRegistryTests
{
	// The codes only the tool reports, so no descriptor of the analyzer backs them.
	private static readonly string[] s_toolOnlyIds = ["ZA2901", "ZA2902"];

	[TestMethod]
	public async Task TheSharedSupportDocumentHasNoAnalyzerDiagnostics()
	{
		// All the rules are enabled, so the document is clean under the ones that ship off as well.
		ImmutableArray<Diagnostic> diagnostics =
			await RoslynTestHarness.RunAllOnSupportDocumentAsync(TestRuleSets.AllRulesEnabled);

		GuardAssert.None(
			[.. diagnostics.Select(diagnostic => diagnostic.ToString())],
			"The support document is not clean under every rule"
		);
	}

	[TestMethod]
	public void EveryRuleButTheReportOnlyOnesIsFixedByExactlyOneProvider()
	{
		ImmutableArray<ImmutableArray<string>> providers =
		[
			.. RoslynTestHarness.AllCodeFixProviders.Select(provider =>
				provider.FixableDiagnosticIds
			),
		];

		ImmutableArray<string> ruleIds =
		[
			.. RoslynTestHarness.AllAnalyzers.Select(analyzer =>
				analyzer.SupportedDiagnostics.Single().Id
			),
		];

		foreach (ImmutableArray<string> provider in providers)
		{
			Assert.HasCount(
				provider.Length,
				provider.Distinct(StringComparer.Ordinal),
				"a provider names an id twice."
			);

			foreach (string id in provider)
			{
				Assert.Contains(
					id,
					ruleIds,
					$"{id} is fixed by a provider but is the id of no rule."
				);
			}
		}

		foreach (DiagnosticAnalyzer analyzer in RoslynTestHarness.AllAnalyzers)
		{
			string id = analyzer.SupportedDiagnostics.Single().Id;

			int owners = providers.Count(provider => provider.Contains(id));

			// A report-only rule is listed by name, so a rule that loses its fix by mistake still fails here.
			Assert.AreEqual(
				TestRuleSets.ReportOnly.Contains(id) ? 0 : 1,
				owners,
				$"{id} is not fixed by exactly one provider, and is not a report-only rule."
			);
		}

		foreach (string id in TestRuleSets.ReportOnly)
		{
			Assert.Contains(
				id,
				ruleIds,
				$"{id} is listed as report-only but is the id of no rule."
			);

			Assert.IsTrue(
				StanzaRules.Find(id) is null or { Fix: GapFix.None },
				$"{id} is listed as report-only but the registry gives it a gap fix."
			);
		}
	}

	[TestMethod]
	public void EveryFixTitleNamesItsObjectWithTheDefiniteArticle()
	{
		PropertyInfo fixes = typeof(StanzaCodeFixProvider).GetProperty(
			"Fixes",
			BindingFlags.Instance | BindingFlags.NonPublic
		)!;

		foreach (CodeFixProvider provider in RoslynTestHarness.AllCodeFixProviders)
		{
			var table = (FixTable)fixes.GetValue(provider)!;

			foreach (string id in provider.FixableDiagnosticIds)
			{
				Assert.IsTrue(
					table.TryGet(id, out FixRow row),
					$"{id} has no row in its provider."
				);

				// A title may say "as a self-closing tag", but it names what it changes with "the".
				Assert.IsTrue(
					DefiniteArticle().IsMatch(row.Title) && !OtherDeterminer().IsMatch(row.Title),
					$"The {id} fix title \"{row.Title}\" does not name its object with \"the\"."
				);
			}
		}
	}

	[TestMethod]
	public void NoTwoAnalyzersOwnOneDiagnostic()
	{
		Assert.IsNotEmpty(
			RoslynTestHarness.AllAnalyzers,
			"The analyzer assembly carries no analyzer at all."
		);

		Dictionary<string, Type> owners = new(StringComparer.Ordinal);

		foreach (DiagnosticAnalyzer analyzer in RoslynTestHarness.AllAnalyzers)
		{
			Type type = analyzer.GetType();

			string id = analyzer.SupportedDiagnostics[0].Id;

			Assert.IsTrue(
				owners.TryAdd(id, type),
				$"{id} is exposed by both {owners[id].Name} and {type.Name}."
			);
		}
	}

	[TestMethod]
	public void EachAnalyzerDerivesFromANamedBase()
	{
		Type[] bases =
		[
			typeof(StanzaNodeAnalyzer),
			typeof(StanzaTreeAnalyzer),
			typeof(StanzaCompilationTreeAnalyzer),
		];

		HashSet<string> hasEntry = [.. StanzaRules.All.Select(static rule => rule.Descriptor.Id)];

		HashSet<string> namesKinds =
		[
			.. StanzaRules
				.All.Where(static rule => !rule.Kinds.IsDefault)
				.Select(static rule => rule.Descriptor.Id),
		];

		List<string> offenders = [];

		foreach (DiagnosticAnalyzer analyzer in RoslynTestHarness.AllAnalyzers)
		{
			Type type = analyzer.GetType();

			if (!bases.Any(baseType => baseType.IsAssignableFrom(type)))
				offenders.Add($"{type.Name} derives from none of the analyzer bases");

			string id = analyzer.SupportedDiagnostics[0].Id;

			bool needsRuleBase =
				namesKinds.Contains(id)
				|| (hasEntry.Contains(id) && typeof(StanzaNodeAnalyzer).IsAssignableFrom(type));

			if (needsRuleBase && !typeof(StanzaRuleAnalyzer).IsAssignableFrom(type))
			{
				offenders.Add(
					$"{type.Name} judges node kinds of a StanzaRules entry, so it is a rule analyzer"
				);
			}
		}

		GuardAssert.None(
			offenders,
			"A4: an analyzer derives from the wrong base, as CONVENTIONS.md says"
		);
	}

	[TestMethod]
	public void DescriptorMetadataFollowsTheConventions()
	{
		foreach (DiagnosticAnalyzer analyzer in RoslynTestHarness.AllAnalyzers)
		{
			DiagnosticDescriptor descriptor = analyzer.SupportedDiagnostics.Single();

			string message = descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture);

			Assert.AreEqual(
				"Formatting",
				descriptor.Category,
				$"{descriptor.Id} is not a Formatting rule."
			);

			Assert.IsTrue(
				descriptor.DefaultSeverity is DiagnosticSeverity.Warning or DiagnosticSeverity.Info,
				$"{descriptor.Id} has a default severity no tier of the readme names."
			);

			Assert.IsFalse(
				message.EndsWith('.'),
				$"{descriptor.Id} ends its message with a period."
			);

			Assert.IsTrue(
				char.IsUpper(message[0]),
				$"{descriptor.Id} does not open its message with a capital."
			);

			// A gap rule says which way its fix goes, so the message cannot promise the other.
			if (StanzaRules.Find(descriptor.Id) is { Fix: not GapFix.None and var fix })
			{
				bool removes =
					fix
					is GapFix.AdjacentRemoval
						or GapFix.RunRemoval
						or GapFix.DeclarationGapRemoval
						or GapFix.MarkupCapRemoval
						or GapFix.MarkupSurplusRemoval
						or GapFix.TriviaRunRemoval;

				Assert.StartsWith(
					removes ? "Remove" : "Add",
					message,
					$"{descriptor.Id} words its message against the direction of its fix."
				);
			}

			Assert.IsFalse(
				descriptor.Title.ToString(CultureInfo.InvariantCulture).EndsWith('.'),
				$"{descriptor.Id} ends its title with a period."
			);

			string description = descriptor.Description.ToString(CultureInfo.InvariantCulture);

			Assert.StartsWith(
				"Requires ",
				description,
				$"{descriptor.Id} does not describe itself as a requirement."
			);

			Assert.AreEqual(
				description.Length - 1,
				description.IndexOf('.'),
				$"{descriptor.Id} carries a second sentence in its description."
			);
		}
	}

	[TestMethod]
	public void TheDocumentationKeysTheToolReadsAreTheOnesTheAnalyzerReads()
	{
		// A file that names every key, one that names none, each width key set to a number, to zero and to a non-number,
		// and one that sets a tab indent: the two readers have to agree on each.
		Dictionary<string, string>[] cases =
		[
			new(StringComparer.Ordinal)
			{
				["stanza_max_line_length"] = "88",
				["stanza_doc_max_line_length"] = "120",
				["tab_width"] = "2",
				["stanza_doc_block_elements"] = "sections",
				["stanza_doc_min_tail_words"] = "3",
				["stanza_doc_min_line_words"] = "1",
				["stanza_doc_weak_line_end_words"] = "the, and",
				["stanza_doc_max_overflow_columns"] = "6",
				["stanza_doc_indent_size"] = "4",
				["stanza_doc_text_indent"] = "aligned",
			},

			new(StringComparer.Ordinal),
			new(StringComparer.Ordinal) { ["stanza_max_line_length"] = "0" },
			new(StringComparer.Ordinal) { ["stanza_max_line_length"] = "not a number" },
			new(StringComparer.Ordinal) { ["stanza_doc_max_line_length"] = "120" },
			new(StringComparer.Ordinal) { ["stanza_doc_max_line_length"] = "0" },
			new(StringComparer.Ordinal) { ["stanza_doc_max_line_length"] = "not a number" },

			new(StringComparer.Ordinal)
			{
				["stanza_max_line_length"] = "150",
				["stanza_doc_max_line_length"] = "120",
				["stanza_doc_indent_size"] = "tab",
			},
		];

		foreach (Dictionary<string, string> values in cases)
		{
			DocLayoutOptions? tool = MarkupOptionReader.ResolveDocLayout(key =>
				values.TryGetValue(key, out string? value) ? value : null
			);

			DocLayoutOptions? analyzer = StanzaOptions
				.From(new FixedConfigOptions(values))
				.DocLayout;

			Assert.AreEqual(
				tool?.ToString() ?? "none",
				analyzer?.ToString() ?? "none",
				"the tool and the rules resolve different documentation layouts."
			);
		}
	}

	[TestMethod]
	public void BothHalvesOfAPairReadOneValueWhoseOwnersAreItsHalves()
	{
		ImmutableArray<StanzaRule> paired =
		[
			.. StanzaRules.All.Where(static rule => rule.Pair is not null),
		];

		Assert.IsNotEmpty(paired, "The registry pairs no rule at all.");

		foreach (StanzaRule rule in paired)
		{
			RulePair pair = rule.Pair!;
			StanzaRule? partner = StanzaRules.Find(pair.GetOtherHalf(rule.Descriptor.Id).Id);

			Assert.IsNotNull(
				partner,
				$"{rule.Descriptor.Id} is paired with a rule the registry does not carry."
			);

			Assert.AreSame(
				pair,
				partner.Pair,
				$"{rule.Descriptor.Id} and {partner.Descriptor.Id} read different pair values."
			);

			string[] halves = [pair.First.Id, pair.Second.Id];

			Assert.Contains(
				pair.DefaultHalf.Id,
				halves,
				$"{rule.Descriptor.Id} names a default half outside its pair."
			);

			Assert.Contains(
				pair.OwnerWhenBothEnabled.Id,
				halves,
				$"{rule.Descriptor.Id} names a tie half outside its pair."
			);
		}
	}

	// A trio is a separating rule that inserts a sibling gap, the multi-line tight rule it is paired with,
	// and the single-line tight rule the registry holds for the same kinds, so none is named by a pair.
	[TestMethod]
	public void EverySiblingTrioOfTheRegistryHasARowAndEveryRowNamesATrio()
	{
		string[] trios =
		[
			.. from separate in StanzaRules.All
			where
				separate.Pair is { } pair
				&& pair.First == separate.Descriptor
				&& separate.Fix == GapFix.SiblingInsertion
			from single in StanzaRules.All
			where
				single.Pair is null
				&& single.Fix == GapFix.AdjacentRemoval
				&& !single.Kinds.IsDefault
				&& single.Kinds.SequenceEqual(separate.Kinds)
			select Describe(separate.Descriptor, separate.Pair!.Second, single.Descriptor),
		];

		string[] rows =
		[
			.. SiblingRuleSets.All.Select(siblings =>
				Describe(siblings.Separate, siblings.KeepMultiLine, siblings.KeepSingleLine)
			),
		];

		Assert.IsNotEmpty(trios, "The registry holds no sibling trio, so the check reads nothing.");

		GuardAssert.None(
			[
				.. trios.Except(rows).Select(trio => $"{trio} has no row in SiblingRuleSets"),
				.. rows.Except(trios).Select(row => $"{row} is no trio of the registry"),
			],

			"The sibling rows and the registry's sibling trios differ, so a trio goes untested"
		);
	}

	[TestMethod]
	public void TheRegistryListsEveryRuleItDeclares()
	{
		string[] declared =
		[
			.. typeof(StanzaRules)
				.GetFields(BindingFlags.Public | BindingFlags.Static)
				.Where(static field => field.FieldType == typeof(StanzaRule))
				.Select(static field => ((StanzaRule)field.GetValue(null)!).Descriptor.Id),
		];

		Assert.IsNotEmpty(declared);

		Assert.AreSequenceEqual(
			declared,
			[.. StanzaRules.All.Select(static rule => rule.Descriptor.Id)],
			Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder
		);
	}

	[TestMethod]
	public void EveryDeclaredDescriptorIsReportedByAnAnalyzer()
	{
		HashSet<string> reported = new(
			RoslynTestHarness.AllAnalyzers.SelectMany(analyzer =>
				analyzer.SupportedDiagnostics.Select(descriptor => descriptor.Id)
			),

			StringComparer.Ordinal
		);

		Assert.IsNotEmpty(
			reported,
			"No analyzer reports anything, so the checks below would pass vacuously."
		);

		Assert.IsNotEmpty(
			TestRuleSets.Declared,
			"DiagnosticDescriptors declares no descriptor field."
		);

		foreach ((string field, DiagnosticDescriptor descriptor) in TestRuleSets.Declared)
		{
			Assert.Contains(
				descriptor.Id,
				reported,
				$"{descriptor.Id} is declared as {field} but no analyzer reports it."
			);

			Assert.IsFalse(
				string.IsNullOrWhiteSpace(descriptor.HelpLinkUri),
				$"{descriptor.Id} has no help link."
			);
		}
	}

	[TestMethod]
	public void TheSharedMarkupDeclaresNoIdOfItsOwn()
	{
		HashSet<string> descriptors = [.. TestRuleSets.Declared.Select(rule => rule.Descriptor.Id)];

		List<string> orphans =
		[
			.. ReadMarkupIds()
				.Where(id => !descriptors.Contains(id) && !s_toolOnlyIds.Contains(id)),
		];

		Assert.IsEmpty(
			orphans,

			"The IDs below are declared by the shared markup diagnostics but name no rule of the analyzer and no code "
				+ "of the tool, so the products would report under different texts:"
				+ Environment.NewLine
				+ string.Join(Environment.NewLine, orphans)
		);
	}

	private static string Describe(
		DiagnosticDescriptor separate,
		DiagnosticDescriptor keepMultiLine,
		DiagnosticDescriptor keepSingleLine
	) => $"{separate.Id} {keepMultiLine.Id} {keepSingleLine.Id}";

	private static IEnumerable<string> ReadMarkupIds()
	{
		return typeof(MarkupDiagnostics)
			.GetFields(BindingFlags.Public | BindingFlags.Static)
			.Where(field =>
				field.FieldType == typeof(string)
				&& field.Name.EndsWith("Id", StringComparison.Ordinal)
			)
			.Select(field => (string)field.GetValue(null)!);
	}

	[GeneratedRegex(@"\bthe\b")]
	private static partial Regex DefiniteArticle();

	// "As a" is allowed, as in "as a self-closing tag".
	[GeneratedRegex(@"(?<!\bas )\b(?:a|an|this|these)\b")]
	private static partial Regex OtherDeterminer();
}
