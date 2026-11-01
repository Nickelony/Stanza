using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests.Convergence;

/// <summary>Fixes seeded layouts and damaged comments until nothing changes.</summary>
[TestClass]
public sealed partial class ConvergenceTests
{
	private const int DefaultSeedCount = 6;
	private const int SoakFirstSeed = 1000;

	// A run is judged against the limit, so the limits take the rule through its quiet and its busy ends.
	private static readonly int[] s_looseLimits = [3, 2, 1, 4];

	// The rules that share a gap with the loose run rule, alone and all together.
	private static readonly DiagnosticDescriptor[][] s_looseRuleSets =
	[
		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.SeparateJumpStatement,
			DiagnosticDescriptors.SeparateBaseCall,
			DiagnosticDescriptors.SeparateGuardClauses,
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateBaseCall,
			DiagnosticDescriptors.SeparateGuardClauses,
			DiagnosticDescriptors.SeparateJumpStatement,
			DiagnosticDescriptors.SeparateInvocationRuns,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateGuardClauses,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateGuardClauses,
			DiagnosticDescriptors.SeparateJumpStatement,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateGuardClauses,
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateGuardClauses,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateCommentedSibling,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateGuardClauses,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.SeparateJumpStatement,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateGuardClauses,
			DiagnosticDescriptors.SeparateInvocationRuns,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateDeclarationRuns,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateInvocationRuns,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateAssertionRuns,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateAssignmentRuns,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
		],

		// Each run rule off in turn, with the rules that share its gaps on.
		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.SeparateJumpStatement,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.SeparateJumpStatement,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.SeparateJumpStatement,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.SeparateJumpStatement,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.SeparateJumpStatement,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.SeparateJumpStatement,
		],

		[DiagnosticDescriptors.GatherRunOfLooseStatements, DiagnosticDescriptors.SeparateAwaitRuns],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateJumpStatement,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateJumpStatement,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.SeparateCommentedSibling,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
		],

		// The keep rule joins the runs the others read, and writes the blank lines they then ask for.
		[
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.SeparateJumpStatement,
			DiagnosticDescriptors.SeparateBaseCall,
			DiagnosticDescriptors.SeparateGuardClauses,
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
		],

		[
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
		],

		[
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
		],
	];

	// The rule sets a source settles under, and the rules that ship on, ask for a blank line or gather around it.
	private sealed record LooseCorpus(
		DiagnosticDescriptor[][] RuleSets,
		DiagnosticDescriptor[] ShippedOn,
		DiagnosticDescriptor[] Asking,
		DiagnosticDescriptor[] Gathering,
		ImmutableArray<DiagnosticAnalyzer> GatheringAnalyzers
	);

	private static readonly DiagnosticDescriptor[] s_statementGapRules =
	[
		DiagnosticDescriptors.SeparateDeclarationRuns,
		DiagnosticDescriptors.SeparateInvocationRuns,
		DiagnosticDescriptors.SeparateAssertionRuns,
		DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
		DiagnosticDescriptors.SeparateMultiLineConstructs,
		DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors,
	];

	private static readonly LooseCorpus s_statementCorpus = new(
		RuleSets: s_looseRuleSets,
		ShippedOn: [.. TestRuleSets.DefaultRules],

		Asking:
		[
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.SeparateJumpStatement,
			DiagnosticDescriptors.SeparateBaseCall,
			DiagnosticDescriptors.SeparateGuardClauses,
		],

		Gathering: [DiagnosticDescriptors.GatherRunOfLooseStatements],
		GatheringAnalyzers: [new GatherRunOfLooseStatementsAnalyzer()]
	);

	// The limits a loose member run is judged against, which take the rule through its quiet and its busy ends.
	private static readonly int[] s_looseMemberLimits = [3, 2, 1, 4];

	// The rules that share a gap of the members with the loose member rule, alone and all together.
	private static readonly DiagnosticDescriptor[][] s_looseMemberRuleSets =
	[
		[
			DiagnosticDescriptors.GatherRunOfLooseMembers,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
			DiagnosticDescriptors.SeparateMemberGroups,
			DiagnosticDescriptors.SeparateModifierGroups,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
			DiagnosticDescriptors.SeparateImportSection,
			DiagnosticDescriptors.SeparateFileScopedNamespaceDeclarationFromFirstMember,
			DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors,
			DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors,
			DiagnosticDescriptors.SeparateCommentedSibling,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseMembers,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
		],

		[DiagnosticDescriptors.GatherRunOfLooseMembers, DiagnosticDescriptors.SeparateMemberGroups],

		[
			DiagnosticDescriptors.GatherRunOfLooseMembers,
			DiagnosticDescriptors.SeparateModifierGroups,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseMembers,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseMembers,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseMembers,
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseMembers,
			DiagnosticDescriptors.SeparateImportSection,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseMembers,
			DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseMembers,
			DiagnosticDescriptors.SeparateCommentedSibling,
		],

		// The keep rule joins the groups the others read, and writes the blank lines they then ask for.
		[
			DiagnosticDescriptors.KeepRunOfMembersTogether,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateMemberGroups,
			DiagnosticDescriptors.SeparateModifierGroups,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
			DiagnosticDescriptors.SeparateImportSection,
			DiagnosticDescriptors.SeparateFileScopedNamespaceDeclarationFromFirstMember,
			DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors,
			DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors,
			DiagnosticDescriptors.SeparateCommentedSibling,
		],

		[
			DiagnosticDescriptors.KeepRunOfMembersTogether,
			DiagnosticDescriptors.SeparateMemberGroups,
			DiagnosticDescriptors.SeparateModifierGroups,
		],

		[
			DiagnosticDescriptors.KeepRunOfMembersTogether,
			DiagnosticDescriptors.SeparateMemberGroups,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
		],
	];

	// A file holds statements and members, and the two loose run rules each judge a part of it.
	private static readonly DiagnosticDescriptor[][] s_looseFileRuleSets =
	[
		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.GatherRunOfLooseMembers,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
			DiagnosticDescriptors.SeparateMemberGroups,
			DiagnosticDescriptors.SeparateModifierGroups,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
			DiagnosticDescriptors.SeparateImportSection,
			DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.SeparateJumpStatement,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.GatherRunOfLooseMembers,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.GatherRunOfLooseMembers,
			DiagnosticDescriptors.SeparateImportSection,
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.GatherRunOfLooseMembers,
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
		],

		// The two keep rules each join a part of the file and write the blank lines the others then ask for.
		[
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			DiagnosticDescriptors.KeepRunOfMembersTogether,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
			DiagnosticDescriptors.SeparateMemberGroups,
			DiagnosticDescriptors.SeparateModifierGroups,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
			DiagnosticDescriptors.SeparateImportSection,
			DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.SeparateJumpStatement,
		],

		[
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			DiagnosticDescriptors.KeepRunOfMembersTogether,
		],
	];

	private static readonly DiagnosticDescriptor[] s_askingMemberRules =
	[
		DiagnosticDescriptors.SeparateDeclarationRuns,
		DiagnosticDescriptors.SeparateInvocationRuns,
		DiagnosticDescriptors.SeparateAssertionRuns,
		DiagnosticDescriptors.SeparateMemberGroups,
		DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
		DiagnosticDescriptors.SeparateMultiLineConstructs,
		DiagnosticDescriptors.SeparateImportSection,
		DiagnosticDescriptors.SeparateFileScopedNamespaceDeclarationFromFirstMember,
		DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors,
		DiagnosticDescriptors.SeparateAwaitRuns,
		DiagnosticDescriptors.SeparateEventSubscriptionRuns,
		DiagnosticDescriptors.SeparateAssignmentRuns,
		DiagnosticDescriptors.SeparateModifierGroups,
		DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors,
		DiagnosticDescriptors.SeparateCommentedSibling,
	];

	private static readonly LooseCorpus s_memberCorpus = new(
		RuleSets: s_looseMemberRuleSets,
		ShippedOn: [.. TestRuleSets.DefaultRules],
		Asking: s_askingMemberRules,
		Gathering: [DiagnosticDescriptors.GatherRunOfLooseMembers],
		GatheringAnalyzers: [new GatherRunOfLooseMembersAnalyzer()]
	);

	private static readonly LooseCorpus s_fileCorpus = new(
		RuleSets: s_looseFileRuleSets,
		ShippedOn: [.. TestRuleSets.DefaultRules],
		Asking: [.. s_askingMemberRules, DiagnosticDescriptors.SeparateJumpStatement],

		Gathering:
		[
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.GatherRunOfLooseMembers,
		],

		GatheringAnalyzers:
		[
			new GatherRunOfLooseStatementsAnalyzer(),
			new GatherRunOfLooseMembersAnalyzer(),
		]
	);

	// The rules that rewrite a token, such as a block body into an arrow, are left off so the tokens can be compared.
	private static readonly IReadOnlyDictionary<string, ReportDiagnostic> s_layoutRules =
		TestRuleSets
			.StandaloneRulesEnabled.Concat(TestRuleSets.GapRulesEnabled)
			.Where(pair => !TestRuleSets.TokenRules.Contains(pair.Key))
			.DistinctBy(pair => pair.Key)
			// A token rule writes code, so each stays off, the ones that ship on included.
			.Concat(
				TestRuleSets.TokenRules.Select(id =>
					KeyValuePair.Create(id, ReportDiagnostic.Suppress)
				)
			)
			.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

	private static readonly IReadOnlyDictionary<string, ReportDiagnostic> s_documentationRules =
		TestRuleSets.Only(
			DiagnosticDescriptors.LayOutDocumentationComment,
			DiagnosticDescriptors.MarkDocumentationParagraphs,
			DiagnosticDescriptors.RemoveDocumentationPadding,
			DiagnosticDescriptors.CapitalizeDocumentationText,
			DiagnosticDescriptors.EndDocumentationTextWithPeriod,
			DiagnosticDescriptors.CollapseWhitespaceInsideTag,
			DiagnosticDescriptors.OrderElementsOfDocumentationComment,
			DiagnosticDescriptors.KeepDocumentationCommentWithDeclaration,
			DiagnosticDescriptors.LayOutItemsOfDocumentationList,
			DiagnosticDescriptors.OrderItemsOfDocumentationList,
			DiagnosticDescriptors.WriteEmptyElementAsSelfClosingTag,
			DiagnosticDescriptors.SeparateSlashOfSelfClosingTag
		);

	// Written by hand as the rules lay it out, so the fixes are checked against a text of their own.
	private const string FormattedFile = """
		using System.Linq;
		using System.Text;

		internal sealed class Sample
		{
			private int _first;
			private int _second;

			public int Count { get; set; }

			public void Run()
			{
				var first = 1;

				DoThing();
				DoThing();

				if (first > 0)
				{
					DoThing();
				}

				Call(
					1,
					2);

				return;
			}

			public int Pick(int value)
			{
				switch (value)
				{
					case 1:
						return 1;
					default:
						return 0;
				}
			}
		}
		""";

	// The loose run rules alone must never remove a blank line that a rule they defer to would write back.
	private static async Task AssertGatheringLeavesNoWorkAsync(
		string source,
		Dictionary<string, string> options,
		LooseCorpus? corpus = null
	)
	{
		corpus ??= s_statementCorpus;

		DiagnosticDescriptor[] asking = corpus.Asking;

		// Only the loose run rules' fixes run, while the rules they defer to stay enabled, so they see their gaps.
		AnalyzerRunResult gathered = await RoslynTestHarness.RunIterativelyAsync(
			source,

			new RunOptions(corpus.GatheringAnalyzers)
			{
				DiagnosticOptions = TestRuleSets.Only([.. corpus.Gathering, .. asking]),
				AnalyzerConfigOptions = options,
			}
		);

		AnalyzerRunResult before = await RoslynTestHarness.ReportAllAsync(
			source,
			TestRuleSets.Only(asking),
			options
		);

		AnalyzerRunResult after = await RoslynTestHarness.ReportAllAsync(
			gathered.FixedText,
			TestRuleSets.Only(asking),
			options
		);

		List<string> reportedBefore = ReadReportedGaps(source, before);

		// A gap is judged by the lines around it, so one rule taking it over from another asks for nothing new.
		foreach (string gap in ReadReportedGaps(gathered.FixedText, after))
		{
			Assert.IsTrue(
				reportedBefore.Remove(gap),
				$"Gathering the loose runs made a rule report a gap it did not report before: {gap}"
			);
		}
	}

	// Gathering removes only blank lines, so a gap keeps the two lines around it, and its line numbers do not.
	private static List<string> ReadReportedGaps(string text, AnalyzerRunResult reported)
	{
		string[] lines = text.ReplaceLineEndings("\n").Split('\n');
		List<string> gaps = [];

		foreach (Diagnostic diagnostic in reported.Diagnostics)
		{
			int lineIndex = diagnostic.Location.GetLineSpan().StartLinePosition.Line;

			string above =
				lines[..lineIndex].LastOrDefault(static line => line.Trim().Length > 0)
				?? string.Empty;

			gaps.Add($"{above.Trim()} / {lines[lineIndex].Trim()}");
		}

		return gaps;
	}

	// The rules that share a gap of the statements with the loose run rules are off unless a set names them.
	private static Dictionary<string, ReportDiagnostic> OnSuppressingShippedStatementRules(
		DiagnosticDescriptor[] rules
	) => TestRuleSets.OnSuppressingOthersOf(rules, s_statementGapRules);
}
