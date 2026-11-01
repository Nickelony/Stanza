using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Nickelony.Stanza.Analyzers.Tests.Harness;

/// <summary>
/// Named rule sets that tests enable through diagnostic options or compare against.
/// </summary>
internal static class TestRuleSets
{
	/// <summary>The second half of each pair that ships off, which the registry names.</summary>
	private static readonly ImmutableArray<StanzaRule> s_secondHalves =
	[
		.. StanzaRules.All.Where(rule =>
			rule.Pair is { } pair
			&& pair.Second == rule.Descriptor
			&& !rule.Descriptor.IsEnabledByDefault
		),
	];

	// No registry says that a fix edits a token of code, the marker of a markup comment or the slash or the tags of a markup element.
	private static readonly DiagnosticDescriptor[] s_tokenRulesOutsideRegistry =
	[
		DiagnosticDescriptors.UseBracesConsistently,
		DiagnosticDescriptors.RemoveRedundantBraces,
		DiagnosticDescriptors.RemoveElseAfterJump,
		DiagnosticDescriptors.MergeNestedIfStatements,
		DiagnosticDescriptors.UsePatternForNullCheck,
		DiagnosticDescriptors.UseEqualityOperatorForNullCheck,
		DiagnosticDescriptors.UseBlockBodyForMultiLineMember,
		DiagnosticDescriptors.UseExpressionBodyForSingleLineMember,
		DiagnosticDescriptors.UseBlockBodyForMultiLineValueMember,
		DiagnosticDescriptors.UseExpressionBodyForSingleLineValueMember,
		DiagnosticDescriptors.NameArgumentsOfCall,
		DiagnosticDescriptors.NameLiteralArgumentsOfCall,
		DiagnosticDescriptors.RemoveRedundantArgumentName,
		DiagnosticDescriptors.SeparateMarkupCommentMarkerFromText,
		DiagnosticDescriptors.AddTrailingComma,
		DiagnosticDescriptors.RemoveTrailingComma,
		DiagnosticDescriptors.SeparateSlashOfSelfClosingMarkupElement,
		DiagnosticDescriptors.KeepSlashOfSelfClosingMarkupElementTight,
		DiagnosticDescriptors.WriteEmptyMarkupElementAsSelfClosingTag,
	];

	// A first half whose partner ships on or is left off too, and two rules that read an off rule.
	private static readonly DiagnosticDescriptor[] s_optionalRulesOutsidePairs =
	[
		DiagnosticDescriptors.SeparateClauseKeywordFromBraceBeforeIt,
		DiagnosticDescriptors.KeepArrowOnSignatureLine,
		DiagnosticDescriptors.KeepSingleLineMarkupElementsTogether,
		DiagnosticDescriptors.LayOutItemsOfDocumentationList,
	];

	// No registry says that a rule rewrites braces and keeps no anchor on the previous unit.
	private static readonly DiagnosticDescriptor[] s_rewritingRulesOutsideRegistry =
	[
		DiagnosticDescriptors.UseBracesConsistently,
		DiagnosticDescriptors.SeparateClauseKeywordFromBraceBeforeIt,
		DiagnosticDescriptors.CollapseEmptyBlocks,
	];

	/// <summary>Each descriptor the analyzer declares, with the name of its field.</summary>
	internal static ImmutableArray<(
		string Field,
		DiagnosticDescriptor Descriptor
	)> Declared { get; } =
	[
		.. typeof(DiagnosticDescriptors)
			.GetFields(BindingFlags.Public | BindingFlags.Static)
			.Where(field => field.FieldType == typeof(DiagnosticDescriptor))
			.Select(field => (field.Name, (DiagnosticDescriptor)field.GetValue(null)!)),
	];

	/// <summary>The rules whose fix writes or removes a token.</summary>
	internal static IReadOnlySet<string> TokenRules { get; } =
		new HashSet<string>(
			[
				.. StanzaRules
					.All.Where(rule => IsMarkupFix(rule.Fix))
					.Select(rule => rule.Descriptor.Id),

				.. s_tokenRulesOutsideRegistry.Select(descriptor => descriptor.Id),
			],

			StringComparer.Ordinal
		);

	/// <summary>The rules that only report, which no provider fixes.</summary>
	internal static IReadOnlySet<string> ReportOnly { get; } =
		new HashSet<string>(
			[
				DiagnosticDescriptors.BreakUpLongTernaryChain.Id,
				DiagnosticDescriptors.ShortenLongCondition.Id,
				DiagnosticDescriptors.FlattenDeeplyNestedCode.Id,
				DiagnosticDescriptors.ShortenLongMember.Id,
				DiagnosticDescriptors.ShortenLongParameterList.Id,
				DiagnosticDescriptors.ShortenLongLine.Id,
				DiagnosticDescriptors.BreakUpLongElseIfChain.Id,
			],

			StringComparer.Ordinal
		);

	/// <summary>The rules the jump, guard and base call rules share a gap with.</summary>
	internal static DiagnosticDescriptor[] SharedGapRules { get; } =
	[
		DiagnosticDescriptors.SeparateDeclarationRuns,
		DiagnosticDescriptors.SeparateInvocationRuns,
		DiagnosticDescriptors.SeparateAssertionRuns,
		DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
		DiagnosticDescriptors.SeparateMultiLineConstructs,
	];

	/// <summary>The rules that separate a run of statements, one for each kind of run.</summary>
	internal static DiagnosticDescriptor[] RunRules { get; } =
	[
		DiagnosticDescriptors.SeparateDeclarationRuns,
		DiagnosticDescriptors.SeparateInvocationRuns,
		DiagnosticDescriptors.SeparateAssertionRuns,
		DiagnosticDescriptors.SeparateAwaitRuns,
		DiagnosticDescriptors.SeparateEventSubscriptionRuns,
		DiagnosticDescriptors.SeparateAssignmentRuns,
	];

	/// <summary>The rules at warn, and every other rule at its default.</summary>
	internal static IReadOnlyDictionary<string, ReportDiagnostic> Only(
		params DiagnosticDescriptor[] descriptors
	)
	{
		return descriptors.ToDictionary(
			descriptor => descriptor.Id,
			_ => ReportDiagnostic.Warn,
			StringComparer.Ordinal
		);
	}

	/// <summary>The rules at warn, and each of the candidates they leave out suppressed.</summary>
	internal static Dictionary<string, ReportDiagnostic> OnSuppressingOthersOf(
		DiagnosticDescriptor[] rules,
		DiagnosticDescriptor[] candidates
	)
	{
		Dictionary<string, ReportDiagnostic> enabled = new(Only(rules), StringComparer.Ordinal);

		foreach (DiagnosticDescriptor candidate in candidates)
		{
			if (!rules.Contains(candidate))
				enabled[candidate.Id] = ReportDiagnostic.Suppress;
		}

		return enabled;
	}

	/// <summary>The rules at warn, and the one rule that stays off.</summary>
	internal static Dictionary<string, ReportDiagnostic> WithOneOff(
		DiagnosticDescriptor[] on,
		DiagnosticDescriptor? off = null
	)
	{
		Dictionary<string, ReportDiagnostic> enabled = new(Only(on), StringComparer.Ordinal);

		if (off is not null)
			enabled[off.Id] = ReportDiagnostic.Suppress;

		return enabled;
	}

	/// <summary>The rules at warn, and every other rule the analyzer declares suppressed.</summary>
	internal static Dictionary<string, ReportDiagnostic> OnSuppressingAllOthers(
		DiagnosticDescriptor[] rules
	)
	{
		return Declared.ToDictionary(
			rule => rule.Descriptor.Id,

			rule =>
				rules.Contains(rule.Descriptor) ? ReportDiagnostic.Warn : ReportDiagnostic.Suppress,

			StringComparer.Ordinal
		);
	}

	/// <summary>The rules that ship off and stay off in the optional set below.</summary>
	private static readonly DiagnosticDescriptor[] s_optionalExceptions =
	[
		.. s_secondHalves.Select(rule => rule.Descriptor),
		.. s_optionalRulesOutsidePairs,
	];

	/// <summary>The optional rules with no counterpart enabled; a new one joins.</summary>
	internal static IReadOnlyDictionary<string, ReportDiagnostic> StandaloneRulesEnabled { get; } =
		Only([
			.. Declared
				.Select(rule => rule.Descriptor)
				.Where(descriptor =>
					!descriptor.IsEnabledByDefault && !s_optionalExceptions.Contains(descriptor)
				),
		]);

	internal static IReadOnlyDictionary<string, ReportDiagnostic> SuggestionTierEnabled { get; } =
		new Dictionary<string, ReportDiagnostic>
		{
			[DiagnosticDescriptors.LayOutDocumentationComment.Id] = ReportDiagnostic.Info,
		};

	/// <summary>
	/// The off-by-default gap, clause, tag and arrow rules, plus the list layout rule.
	/// </summary>
	internal static IReadOnlyDictionary<string, ReportDiagnostic> GapRulesEnabled { get; } =
		Only([
			.. s_secondHalves.Where(rule => !IsMarkupFix(rule.Fix)).Select(rule => rule.Descriptor),
			.. SiblingRuleSets.All.Select(siblings => siblings.KeepSingleLine),
			DiagnosticDescriptors.SeparateClauseKeywordFromBraceBeforeIt,
			DiagnosticDescriptors.LayOutItemsOfDocumentationList,
		]);

	internal static IReadOnlyDictionary<string, ReportDiagnostic> ArrowRulesEnabled { get; } =
		Only(
			DiagnosticDescriptors.PutArrowOnLineAfterSignature,
			DiagnosticDescriptors.KeepArrowOnSignatureLine
		);

	internal static IReadOnlyDictionary<string, ReportDiagnostic> ClauseRulesEnabled { get; } =
		Only(
			DiagnosticDescriptors.KeepClauseKeywordsAdjacent,
			DiagnosticDescriptors.SeparateClauseKeywordFromBraceBeforeIt
		);

	internal static IReadOnlyDictionary<string, ReportDiagnostic> TagRulesEnabled { get; } =
		Only(
			DiagnosticDescriptors.SeparateSlashOfSelfClosingTag,
			DiagnosticDescriptors.KeepSlashOfSelfClosingTagTight
		);

	internal static IReadOnlyDictionary<string, ReportDiagnostic> MarkupSlashRulesEnabled { get; } =
		Only(
			DiagnosticDescriptors.SeparateSlashOfSelfClosingMarkupElement,
			DiagnosticDescriptors.KeepSlashOfSelfClosingMarkupElementTight
		);

	internal static IReadOnlySet<string> ShipsOff { get; } =
		new HashSet<string>(
			Declared
				.Where(rule => !rule.Descriptor.IsEnabledByDefault)
				.Select(rule => rule.Descriptor.Id)
		);

	internal static IReadOnlyDictionary<string, ReportDiagnostic> AllRulesEnabled { get; } =
		RoslynTestHarness
			.AllAnalyzers.Select(analyzer => analyzer.SupportedDiagnostics.Single().Id)
			.ToDictionary(id => id, _ => ReportDiagnostic.Warn, StringComparer.Ordinal);

	/// <summary>The rules that are enabled by default.</summary>
	internal static ImmutableArray<DiagnosticDescriptor> DefaultRules { get; } =
	[.. Declared.Where(rule => rule.Descriptor.IsEnabledByDefault).Select(rule => rule.Descriptor)];

	/// <summary>
	/// Rules that rewrite braces and clause keywords, exempt from the previous-unit location check.
	/// </summary>
	internal static IReadOnlySet<string> RewritingRules { get; } =
		new HashSet<string>(
			[
				.. StanzaRules
					.All.Where(rule => rule.AnchoredOnPrevious)
					.Select(rule => rule.Descriptor.Id),

				.. s_rewritingRulesOutsideRegistry.Select(descriptor => descriptor.Id),
			],

			StringComparer.Ordinal
		);

	/// <summary>The separating rules of the comma-separated lists that ship off.</summary>
	internal static ImmutableArray<DiagnosticDescriptor> ListRules { get; } =
	[
		.. SiblingRuleSets
			.All.Where(siblings =>
				siblings.Separator == "," && !siblings.Separate.IsEnabledByDefault
			)
			.Select(siblings => siblings.Separate),
	];

	internal static IReadOnlyDictionary<string, ReportDiagnostic> ListRulesEnabled { get; } =
		Only([.. ListRules]);

	internal static string JoinSortedIds(IEnumerable<DiagnosticDescriptor> descriptors) =>
		string.Join(",", descriptors.Select(descriptor => descriptor.Id).OrderBy(id => id));

	private static bool IsMarkupFix(GapFix fix) =>
		fix is GapFix.MarkupInsertion or GapFix.MarkupCapRemoval or GapFix.MarkupSurplusRemoval;
}
