using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Nickelony.Stanza.Analyzers;

/// <summary>Registers each rule that names node kinds, a gap fix, a pair, a deferral or a read, or is named.</summary>
internal static class StanzaRules
{
	private static readonly RulePair s_accessors = new(
		first: DiagnosticDescriptors.SeparateMultiLineAccessors,
		second: DiagnosticDescriptors.KeepMultiLineAccessorsTogether,
		defaultHalf: DiagnosticDescriptors.KeepMultiLineAccessorsTogether
	);

	private static readonly RulePair s_switchSections = new(
		first: DiagnosticDescriptors.SeparateMultiLineSwitchSections,
		second: DiagnosticDescriptors.KeepMultiLineSwitchSectionsTogether,
		defaultHalf: DiagnosticDescriptors.SeparateMultiLineSwitchSections
	);

	private static readonly RulePair s_switchExpressionArms = new(
		first: DiagnosticDescriptors.SeparateMultiLineSwitchExpressionArms,
		second: DiagnosticDescriptors.KeepMultiLineSwitchExpressionArmsTogether,
		defaultHalf: DiagnosticDescriptors.SeparateMultiLineSwitchExpressionArms
	);

	private static readonly RulePair s_initializerMembers = new(
		first: DiagnosticDescriptors.SeparateMultiLineInitializerMembers,
		second: DiagnosticDescriptors.KeepMultiLineInitializerMembersTogether,
		defaultHalf: DiagnosticDescriptors.KeepMultiLineInitializerMembersTogether
	);

	private static readonly RulePair s_collectionElements = new(
		first: DiagnosticDescriptors.SeparateMultiLineCollectionElements,
		second: DiagnosticDescriptors.KeepMultiLineCollectionElementsTogether,
		defaultHalf: DiagnosticDescriptors.KeepMultiLineCollectionElementsTogether
	);

	private static readonly RulePair s_arguments = new(
		first: DiagnosticDescriptors.SeparateMultiLineArguments,
		second: DiagnosticDescriptors.KeepMultiLineArgumentsTogether,
		defaultHalf: DiagnosticDescriptors.KeepMultiLineArgumentsTogether
	);

	private static readonly RulePair s_parameters = new(
		first: DiagnosticDescriptors.SeparateMultiLineParameters,
		second: DiagnosticDescriptors.KeepMultiLineParametersTogether,
		defaultHalf: DiagnosticDescriptors.KeepMultiLineParametersTogether
	);

	private static readonly RulePair s_declarators = new(
		first: DiagnosticDescriptors.SeparateMultiLineDeclarators,
		second: DiagnosticDescriptors.KeepMultiLineDeclaratorsTogether,
		defaultHalf: DiagnosticDescriptors.KeepMultiLineDeclaratorsTogether
	);

	private static readonly RulePair s_clauseKeywords = new(
		first: DiagnosticDescriptors.SeparateClauseKeywords,
		second: DiagnosticDescriptors.KeepClauseKeywordsAdjacent,
		defaultHalf: DiagnosticDescriptors.KeepClauseKeywordsAdjacent
	);

	private static readonly RulePair s_markupElements = new(
		first: DiagnosticDescriptors.SeparateMultiLineMarkupElements,
		second: DiagnosticDescriptors.KeepMultiLineMarkupElementsTogether,
		defaultHalf: DiagnosticDescriptors.KeepMultiLineMarkupElementsTogether
	);

	private static readonly RulePair s_markupSlash = new(
		first: DiagnosticDescriptors.SeparateSlashOfSelfClosingMarkupElement,
		second: DiagnosticDescriptors.KeepSlashOfSelfClosingMarkupElementTight,
		defaultHalf: DiagnosticDescriptors.SeparateSlashOfSelfClosingMarkupElement
	);

	private static readonly RulePair s_arrows = new(
		first: DiagnosticDescriptors.KeepArrowOnSignatureLine,
		second: DiagnosticDescriptors.PutArrowOnLineAfterSignature,
		defaultHalf: DiagnosticDescriptors.KeepArrowOnSignatureLine,
		ownerWhenBothEnabled: DiagnosticDescriptors.PutArrowOnLineAfterSignature
	);

	private static readonly RulePair s_selfClosingSlash = new(
		first: DiagnosticDescriptors.SeparateSlashOfSelfClosingTag,
		second: DiagnosticDescriptors.KeepSlashOfSelfClosingTagTight,
		defaultHalf: DiagnosticDescriptors.KeepSlashOfSelfClosingTagTight
	);

	private static readonly RulePair s_trailingCommas = new(
		first: DiagnosticDescriptors.AddTrailingComma,
		second: DiagnosticDescriptors.RemoveTrailingComma,
		defaultHalf: DiagnosticDescriptors.AddTrailingComma
	);

	private static readonly RulePair s_nullChecks = new(
		first: DiagnosticDescriptors.UsePatternForNullCheck,
		second: DiagnosticDescriptors.UseEqualityOperatorForNullCheck,
		defaultHalf: DiagnosticDescriptors.UsePatternForNullCheck
	);

	/// <summary>Reads the later run kinds to know which statements are runs, and the gaps that end a run.</summary>
	public static readonly StanzaRule SeparateDeclarationRuns = new(
		Descriptor: DiagnosticDescriptors.SeparateDeclarationRuns,
		Fix: GapFix.SiblingInsertion,
		Kinds: StanzaKinds.ContainerKinds,

		DefersTo:
		[
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateImportSection,
			DiagnosticDescriptors.SeparateRegionMarker,
			DiagnosticDescriptors.SeparateConditionalBlock,
			DiagnosticDescriptors.SeparateSuppressionBlock,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.SeparateBaseCall,
		],

		Reads:
		[
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateYieldRuns,
			DiagnosticDescriptors.SeparateLocalFunctionRuns,
			DiagnosticDescriptors.SeparateReceiverGroups,
			DiagnosticDescriptors.KeepReceiverGroupTogether,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			DiagnosticDescriptors.KeepRunOfMembersTogether,
		]
	);

	/// <summary>Leaves a gap beside a declaration run to its rule, and reads the later kinds.</summary>
	public static readonly StanzaRule SeparateInvocationRuns = new(
		Descriptor: DiagnosticDescriptors.SeparateInvocationRuns,
		Fix: GapFix.SiblingInsertion,
		Kinds: StanzaKinds.ContainerKinds,

		DefersTo:
		[
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateImportSection,
			DiagnosticDescriptors.SeparateRegionMarker,
			DiagnosticDescriptors.SeparateConditionalBlock,
			DiagnosticDescriptors.SeparateSuppressionBlock,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.SeparateBaseCall,
		],

		Reads:
		[
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateYieldRuns,
			DiagnosticDescriptors.SeparateLocalFunctionRuns,
			DiagnosticDescriptors.SeparateReceiverGroups,
			DiagnosticDescriptors.KeepReceiverGroupTogether,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			DiagnosticDescriptors.KeepRunOfMembersTogether,
		]
	);

	/// <summary>Leaves a gap beside a declaration or invocation run to its rule, and reads the later kinds.</summary>
	public static readonly StanzaRule SeparateAssertionRuns = new(
		Descriptor: DiagnosticDescriptors.SeparateAssertionRuns,
		Fix: GapFix.SiblingInsertion,
		Kinds: StanzaKinds.ContainerKinds,

		DefersTo:
		[
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateImportSection,
			DiagnosticDescriptors.SeparateRegionMarker,
			DiagnosticDescriptors.SeparateConditionalBlock,
			DiagnosticDescriptors.SeparateSuppressionBlock,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.SeparateBaseCall,
		],

		Reads:
		[
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateYieldRuns,
			DiagnosticDescriptors.SeparateLocalFunctionRuns,
			DiagnosticDescriptors.SeparateReceiverGroups,
			DiagnosticDescriptors.KeepReceiverGroupTogether,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			DiagnosticDescriptors.KeepRunOfMembersTogether,
		]
	);

	/// <summary>Leaves a gap beside an earlier run to its rule, and reads the later kinds.</summary>
	public static readonly StanzaRule SeparateAwaitRuns = new(
		Descriptor: DiagnosticDescriptors.SeparateAwaitRuns,
		Fix: GapFix.SiblingInsertion,
		Kinds: StanzaKinds.ContainerKinds,

		DefersTo:
		[
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateImportSection,
			DiagnosticDescriptors.SeparateRegionMarker,
			DiagnosticDescriptors.SeparateConditionalBlock,
			DiagnosticDescriptors.SeparateSuppressionBlock,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.SeparateBaseCall,
		],

		Reads:
		[
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.SeparateYieldRuns,
			DiagnosticDescriptors.SeparateLocalFunctionRuns,
			DiagnosticDescriptors.SeparateReceiverGroups,
			DiagnosticDescriptors.KeepReceiverGroupTogether,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			DiagnosticDescriptors.KeepRunOfMembersTogether,
		]
	);

	/// <summary>Leaves a gap beside a declaration, invocation, assertion, await or yield run to those rules.</summary>
	public static readonly StanzaRule SeparateEventSubscriptionRuns = new(
		Descriptor: DiagnosticDescriptors.SeparateEventSubscriptionRuns,
		Fix: GapFix.SiblingInsertion,
		Kinds: StanzaKinds.ContainerKinds,

		DefersTo:
		[
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateImportSection,
			DiagnosticDescriptors.SeparateRegionMarker,
			DiagnosticDescriptors.SeparateConditionalBlock,
			DiagnosticDescriptors.SeparateSuppressionBlock,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateYieldRuns,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.SeparateBaseCall,
		],

		Reads:
		[
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.SeparateLocalFunctionRuns,
			DiagnosticDescriptors.SeparateReceiverGroups,
			DiagnosticDescriptors.KeepReceiverGroupTogether,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			DiagnosticDescriptors.KeepRunOfMembersTogether,
		]
	);

	/// <summary>Leaves a gap beside the run of an earlier run rule to that rule.</summary>
	public static readonly StanzaRule SeparateAssignmentRuns = new(
		Descriptor: DiagnosticDescriptors.SeparateAssignmentRuns,
		Fix: GapFix.SiblingInsertion,
		Kinds: StanzaKinds.ContainerKinds,

		DefersTo:
		[
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateImportSection,
			DiagnosticDescriptors.SeparateRegionMarker,
			DiagnosticDescriptors.SeparateConditionalBlock,
			DiagnosticDescriptors.SeparateSuppressionBlock,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateYieldRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.SeparateBaseCall,
		],

		Reads:
		[
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.SeparateLocalFunctionRuns,
			DiagnosticDescriptors.SeparateReceiverGroups,
			DiagnosticDescriptors.KeepReceiverGroupTogether,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			DiagnosticDescriptors.KeepRunOfMembersTogether,
		]
	);

	/// <summary>Leaves a gap beside an earlier run to its rule, and reads the local function rule.</summary>
	public static readonly StanzaRule SeparateYieldRuns = new(
		Descriptor: DiagnosticDescriptors.SeparateYieldRuns,
		Fix: GapFix.SiblingInsertion,
		Kinds: StanzaKinds.ContainerKinds,

		DefersTo:
		[
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateImportSection,
			DiagnosticDescriptors.SeparateRegionMarker,
			DiagnosticDescriptors.SeparateConditionalBlock,
			DiagnosticDescriptors.SeparateSuppressionBlock,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.SeparateBaseCall,
		],

		Reads:
		[
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.SeparateLocalFunctionRuns,
			DiagnosticDescriptors.SeparateReceiverGroups,
			DiagnosticDescriptors.KeepReceiverGroupTogether,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			DiagnosticDescriptors.KeepRunOfMembersTogether,
		]
	);

	/// <summary>Leaves a gap beside the run of an earlier run rule to that rule.</summary>
	public static readonly StanzaRule SeparateLocalFunctionRuns = new(
		Descriptor: DiagnosticDescriptors.SeparateLocalFunctionRuns,
		Fix: GapFix.SiblingInsertion,
		Kinds: StanzaKinds.ContainerKinds,

		DefersTo:
		[
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateImportSection,
			DiagnosticDescriptors.SeparateRegionMarker,
			DiagnosticDescriptors.SeparateConditionalBlock,
			DiagnosticDescriptors.SeparateSuppressionBlock,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateYieldRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.SeparateBaseCall,
		],

		Reads:
		[
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.SeparateReceiverGroups,
			DiagnosticDescriptors.KeepReceiverGroupTogether,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			DiagnosticDescriptors.KeepRunOfMembersTogether,
		]
	);

	public static readonly StanzaRule SeparateMemberGroups = new(
		Descriptor: DiagnosticDescriptors.SeparateMemberGroups,
		Fix: GapFix.SiblingInsertion,
		Kinds: StanzaKinds.MemberContainerKinds,

		DefersTo:
		[
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.SeparateImportSection,
			DiagnosticDescriptors.SeparateRegionMarker,
			DiagnosticDescriptors.SeparateConditionalBlock,
			DiagnosticDescriptors.SeparateSuppressionBlock,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateYieldRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
			DiagnosticDescriptors.SeparateLocalFunctionRuns,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.SeparateReceiverGroups,
			DiagnosticDescriptors.KeepReceiverGroupTogether,
		],

		Reads:
		[
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			DiagnosticDescriptors.KeepRunOfMembersTogether,
		]
	);

	public static readonly StanzaRule SeparateModifierGroups = new(
		Descriptor: DiagnosticDescriptors.SeparateModifierGroups,
		Fix: GapFix.SiblingInsertion,
		Kinds: StanzaKinds.MemberContainerKinds,

		DefersTo:
		[
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.SeparateImportSection,
			DiagnosticDescriptors.SeparateRegionMarker,
			DiagnosticDescriptors.SeparateConditionalBlock,
			DiagnosticDescriptors.SeparateSuppressionBlock,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateYieldRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
			DiagnosticDescriptors.SeparateLocalFunctionRuns,
			DiagnosticDescriptors.SeparateMemberGroups,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.SeparateReceiverGroups,
			DiagnosticDescriptors.KeepReceiverGroupTogether,
		],

		Reads:
		[
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			DiagnosticDescriptors.KeepRunOfMembersTogether,
		]
	);

	public static readonly StanzaRule SeparateSameTypeDeclarationRuns = new(
		Descriptor: DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
		Fix: GapFix.SiblingInsertion,
		Kinds: StanzaKinds.ContainerKinds,

		DefersTo:
		[
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateImportSection,
			DiagnosticDescriptors.SeparateRegionMarker,
			DiagnosticDescriptors.SeparateConditionalBlock,
			DiagnosticDescriptors.SeparateSuppressionBlock,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateYieldRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
			DiagnosticDescriptors.SeparateLocalFunctionRuns,
			DiagnosticDescriptors.SeparateMemberGroups,
			DiagnosticDescriptors.SeparateModifierGroups,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.SeparateBaseCall,
		],

		Reads:
		[
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.SeparateReceiverGroups,
			DiagnosticDescriptors.KeepReceiverGroupTogether,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			DiagnosticDescriptors.KeepRunOfMembersTogether,
		]
	);

	public static readonly StanzaRule SeparateMultiLineConstructs = new(
		Descriptor: DiagnosticDescriptors.SeparateMultiLineConstructs,
		Fix: GapFix.SiblingInsertion,
		Kinds: StanzaKinds.ContainerKinds,
		DefersTo: [DiagnosticDescriptors.SeparateImportSection]
	);

	public static readonly StanzaRule KeepTwoUnitsOfContainerTogether = new(
		Descriptor: DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
		Fix: GapFix.AdjacentRemoval,
		Kinds: StanzaKinds.ContainerKinds
	);

	public static readonly StanzaRule GatherRunOfLooseStatements = new(
		Descriptor: DiagnosticDescriptors.GatherRunOfLooseStatements,
		Fix: GapFix.RunRemoval,
		Kinds: [SyntaxKind.Block, SyntaxKind.CompilationUnit, SyntaxKind.SwitchSection],

		DefersTo:
		[
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateYieldRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
			DiagnosticDescriptors.SeparateLocalFunctionRuns,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateImportSection,
			DiagnosticDescriptors.SeparateRegionMarker,
			DiagnosticDescriptors.SeparateConditionalBlock,
			DiagnosticDescriptors.SeparateSuppressionBlock,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.SeparateBaseCall,
			DiagnosticDescriptors.SeparateJumpStatement,
			DiagnosticDescriptors.SeparateGuardClauses,
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			DiagnosticDescriptors.SeparateReceiverGroups,
			DiagnosticDescriptors.KeepReceiverGroupTogether,
			DiagnosticDescriptors.KeepDependentBindingsTogether,
		],

		Reads:
		[
			DiagnosticDescriptors.SeparateCheckedStanza,
		]
	);

	/// <summary>Judges the members of a container, so it has no jump rule to wait for.</summary>
	public static readonly StanzaRule GatherRunOfLooseMembers = new(
		Descriptor: DiagnosticDescriptors.GatherRunOfLooseMembers,
		Fix: GapFix.RunRemoval,
		Kinds: StanzaKinds.MemberContainerKinds,

		DefersTo:
		[
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateYieldRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
			DiagnosticDescriptors.SeparateLocalFunctionRuns,
			DiagnosticDescriptors.SeparateMemberGroups,
			DiagnosticDescriptors.SeparateModifierGroups,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.SeparateImportSection,
			DiagnosticDescriptors.SeparateRegionMarker,
			DiagnosticDescriptors.SeparateConditionalBlock,
			DiagnosticDescriptors.SeparateSuppressionBlock,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
			DiagnosticDescriptors.KeepRunOfMembersTogether,
			DiagnosticDescriptors.SeparateReceiverGroups,
			DiagnosticDescriptors.KeepReceiverGroupTogether,
			DiagnosticDescriptors.KeepDependentBindingsTogether,
		]
	);

	/// <summary>Judges the statement gather rule's containers, which that rule leaves to it.</summary>
	public static readonly StanzaRule KeepRunOfStatementsTogether = new(
		Descriptor: DiagnosticDescriptors.KeepRunOfStatementsTogether,
		Fix: GapFix.RunRemoval,
		Kinds: GatherRunOfLooseStatements.Kinds,
		DefersTo: GatherRunOfLooseStatements.DefersTo.Remove(DiagnosticDescriptors.KeepRunOfStatementsTogether),
		Reads: GatherRunOfLooseStatements.Reads
	);

	/// <summary>Judges the member gather rule's containers, which that rule leaves to it.</summary>
	public static readonly StanzaRule KeepRunOfMembersTogether = new(
		Descriptor: DiagnosticDescriptors.KeepRunOfMembersTogether,
		Fix: GapFix.RunRemoval,
		Kinds: GatherRunOfLooseMembers.Kinds,
		DefersTo: GatherRunOfLooseMembers.DefersTo.Remove(DiagnosticDescriptors.KeepRunOfMembersTogether)
	);

	/// <summary>
	/// Leaves a gap to the single-line tight rule of its list, and to the two-unit rule of its container.
	/// </summary>
	public static readonly StanzaRule SeparateCommentedSibling = new(
		Descriptor: DiagnosticDescriptors.SeparateCommentedSibling,
		Fix: GapFix.SiblingInsertion,
		Kinds: StanzaKinds.SiblingContainerKinds,

		DefersTo:
		[
			DiagnosticDescriptors.KeepSingleLineAccessorsTogether,
			DiagnosticDescriptors.KeepSingleLineSwitchSectionsTogether,
			DiagnosticDescriptors.KeepSingleLineSwitchExpressionArmsTogether,
			DiagnosticDescriptors.KeepSingleLineInitializerMembersTogether,
			DiagnosticDescriptors.KeepSingleLineCollectionElementsTogether,
			DiagnosticDescriptors.KeepSingleLineArgumentsTogether,
			DiagnosticDescriptors.KeepSingleLineParametersTogether,
			DiagnosticDescriptors.KeepSingleLineDeclaratorsTogether,
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
		]
	);

	public static readonly StanzaRule SeparateJumpStatement = new(
		Descriptor: DiagnosticDescriptors.SeparateJumpStatement,
		Fix: GapFix.SiblingInsertion,
		Kinds: [SyntaxKind.Block, SyntaxKind.CompilationUnit, SyntaxKind.SwitchSection],

		DefersTo:
		[
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateYieldRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
			DiagnosticDescriptors.SeparateLocalFunctionRuns,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateImportSection,
			DiagnosticDescriptors.SeparateRegionMarker,
			DiagnosticDescriptors.SeparateConditionalBlock,
			DiagnosticDescriptors.SeparateSuppressionBlock,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.SeparateBaseCall,
		],

		Reads:
		[
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.SeparateGuardClauses,
			DiagnosticDescriptors.SeparateReceiverGroups,
			DiagnosticDescriptors.KeepReceiverGroupTogether,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			DiagnosticDescriptors.KeepRunOfMembersTogether,
		]
	);

	/// <summary>Owns its gaps ahead of the run, jump and guard rules, which defer to it and end a run there.</summary>
	public static readonly StanzaRule SeparateBaseCall = new(
		Descriptor: DiagnosticDescriptors.SeparateBaseCall,
		Fix: GapFix.SiblingInsertion,
		Kinds: [SyntaxKind.Block],

		DefersTo:
		[
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
		],

		Reads:
		[
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			DiagnosticDescriptors.KeepRunOfMembersTogether,
		]
	);

	/// <summary>Leaves a gap beside a run to the run rules, and the gap above a jump to the jump rule.</summary>
	public static readonly StanzaRule SeparateGuardClauses = new(
		Descriptor: DiagnosticDescriptors.SeparateGuardClauses,
		Fix: GapFix.SiblingInsertion,
		Kinds: [SyntaxKind.Block],

		DefersTo:
		[
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateYieldRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
			DiagnosticDescriptors.SeparateLocalFunctionRuns,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateRegionMarker,
			DiagnosticDescriptors.SeparateConditionalBlock,
			DiagnosticDescriptors.SeparateSuppressionBlock,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.SeparateBaseCall,
			DiagnosticDescriptors.SeparateJumpStatement,
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
		],

		Reads:
		[
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.SeparateReceiverGroups,
			DiagnosticDescriptors.KeepReceiverGroupTogether,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			DiagnosticDescriptors.KeepRunOfMembersTogether,
		]
	);

	public static readonly StanzaRule SeparateCheckedStanza = new(
		Descriptor: DiagnosticDescriptors.SeparateCheckedStanza,
		Fix: GapFix.SiblingInsertion,
		Kinds: [SyntaxKind.Block, SyntaxKind.CompilationUnit, SyntaxKind.SwitchSection],

		DefersTo:
		[
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateYieldRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
			DiagnosticDescriptors.SeparateLocalFunctionRuns,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateImportSection,
			DiagnosticDescriptors.SeparateRegionMarker,
			DiagnosticDescriptors.SeparateConditionalBlock,
			DiagnosticDescriptors.SeparateSuppressionBlock,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.SeparateBaseCall,
			DiagnosticDescriptors.SeparateJumpStatement,
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
			DiagnosticDescriptors.SeparateGuardClauses,
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
		],

		Reads:
		[
			DiagnosticDescriptors.SeparateReceiverGroups,
			DiagnosticDescriptors.KeepReceiverGroupTogether,
			DiagnosticDescriptors.KeepRunOfMembersTogether,
		]
	);

	public static readonly StanzaRule CollapseExtraBlankLines = new(
		Descriptor: DiagnosticDescriptors.CollapseExtraBlankLines,
		Fix: GapFix.TriviaRunRemoval
	);

	/// <summary>Claims the gap between two guards, which the separating rules leave to it while it is on.</summary>
	public static readonly StanzaRule KeepGuardClausesTogether = new(
		Descriptor: DiagnosticDescriptors.KeepGuardClausesTogether,
		Fix: GapFix.AdjacentRemoval,
		Kinds: [SyntaxKind.Block, SyntaxKind.CompilationUnit, SyntaxKind.SwitchSection],

		DefersTo:
		[
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
		]
	);

	/// <summary>Claims the gap above a guard that tests what the statement above binds.</summary>
	public static readonly StanzaRule KeepGuardWithBinding = new(
		Descriptor: DiagnosticDescriptors.KeepGuardWithBinding,
		Fix: GapFix.AdjacentRemoval,
		Kinds: [SyntaxKind.Block, SyntaxKind.CompilationUnit, SyntaxKind.SwitchSection],

		DefersTo:
		[
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
		]
	);

	/// <summary>Claims the gap above a loop that builds what the statement above seeds.</summary>
	public static readonly StanzaRule KeepAccumulatorWithLoop = new(
		Descriptor: DiagnosticDescriptors.KeepAccumulatorWithLoop,
		Fix: GapFix.AdjacentRemoval,
		Kinds: [SyntaxKind.Block, SyntaxKind.CompilationUnit, SyntaxKind.SwitchSection],

		DefersTo:
		[
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
		]
	);

	/// <summary>Claims the gap above a jump that uses what the lone binding above it binds.</summary>
	public static readonly StanzaRule KeepJumpWithBinding = new(
		Descriptor: DiagnosticDescriptors.KeepJumpWithBinding,
		Fix: GapFix.AdjacentRemoval,
		Kinds: [SyntaxKind.Block, SyntaxKind.CompilationUnit, SyntaxKind.SwitchSection],

		DefersTo:
		[
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
			DiagnosticDescriptors.SeparateReceiverGroups,
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateYieldRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
			DiagnosticDescriptors.SeparateLocalFunctionRuns,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.SeparateBaseCall,
			DiagnosticDescriptors.SeparateGuardClauses,
		]
	);

	/// <summary>Claims the gap below a stanza-opening initialization and above its first receiver call.</summary>
	public static readonly StanzaRule KeepInitializationWithCalls = new(
		Descriptor: DiagnosticDescriptors.KeepInitializationWithCalls,
		Fix: GapFix.AdjacentRemoval,
		Kinds: [SyntaxKind.Block, SyntaxKind.CompilationUnit, SyntaxKind.SwitchSection],

		DefersTo:
		[
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
			DiagnosticDescriptors.SeparateReceiverGroups,
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateYieldRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
			DiagnosticDescriptors.SeparateLocalFunctionRuns,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.SeparateBaseCall,
			DiagnosticDescriptors.SeparateGuardClauses,
		]
	);

	/// <summary>Claims the gap above an assertion that narrows what the statement above binds.</summary>
	public static readonly StanzaRule KeepNarrowingAssertionWithBinding = new(
		Descriptor: DiagnosticDescriptors.KeepNarrowingAssertionWithBinding,
		Fix: GapFix.AdjacentRemoval,
		Kinds: [SyntaxKind.Block, SyntaxKind.CompilationUnit, SyntaxKind.SwitchSection],

		DefersTo:
		[
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
		]
	);

	public static readonly StanzaRule SeparateReceiverGroups = new(
		Descriptor: DiagnosticDescriptors.SeparateReceiverGroups,
		Fix: GapFix.SiblingInsertion,
		Kinds: [SyntaxKind.Block, SyntaxKind.CompilationUnit, SyntaxKind.SwitchSection],

		DefersTo:
		[
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateYieldRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
			DiagnosticDescriptors.SeparateLocalFunctionRuns,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateRegionMarker,
			DiagnosticDescriptors.SeparateConditionalBlock,
			DiagnosticDescriptors.SeparateSuppressionBlock,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.SeparateImportSection,
			DiagnosticDescriptors.SeparateBaseCall,
			DiagnosticDescriptors.SeparateJumpStatement,
			DiagnosticDescriptors.SeparateGuardClauses,
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
		],

		Reads:
		[
			DiagnosticDescriptors.KeepReceiverGroupTogether,
			DiagnosticDescriptors.KeepRunOfStatementsTogether,
			DiagnosticDescriptors.KeepRunOfMembersTogether,
		]
	);

	public static readonly StanzaRule KeepReceiverGroupTogether = new(
		Descriptor: DiagnosticDescriptors.KeepReceiverGroupTogether,
		Fix: GapFix.RunRemoval,
		Kinds: [SyntaxKind.Block, SyntaxKind.CompilationUnit, SyntaxKind.SwitchSection],

		DefersTo:
		[
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateYieldRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
			DiagnosticDescriptors.SeparateLocalFunctionRuns,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateRegionMarker,
			DiagnosticDescriptors.SeparateConditionalBlock,
			DiagnosticDescriptors.SeparateSuppressionBlock,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.SeparateImportSection,
			DiagnosticDescriptors.SeparateBaseCall,
			DiagnosticDescriptors.SeparateJumpStatement,
			DiagnosticDescriptors.SeparateGuardClauses,
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
		],

		Reads:
		[
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.SeparateReceiverGroups,
		]
	);

	public static readonly StanzaRule KeepDependentBindingsTogether = new(
		Descriptor: DiagnosticDescriptors.KeepDependentBindingsTogether,
		Fix: GapFix.RunRemoval,
		Kinds: [SyntaxKind.Block, SyntaxKind.CompilationUnit, SyntaxKind.SwitchSection],

		DefersTo:
		[
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateInvocationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns,
			DiagnosticDescriptors.SeparateAwaitRuns,
			DiagnosticDescriptors.SeparateYieldRuns,
			DiagnosticDescriptors.SeparateEventSubscriptionRuns,
			DiagnosticDescriptors.SeparateAssignmentRuns,
			DiagnosticDescriptors.SeparateLocalFunctionRuns,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateRegionMarker,
			DiagnosticDescriptors.SeparateConditionalBlock,
			DiagnosticDescriptors.SeparateSuppressionBlock,
			DiagnosticDescriptors.SeparateCommentedSibling,
			DiagnosticDescriptors.SeparateImportSection,
			DiagnosticDescriptors.SeparateBaseCall,
			DiagnosticDescriptors.SeparateJumpStatement,
			DiagnosticDescriptors.SeparateGuardClauses,
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
			DiagnosticDescriptors.SeparateReceiverGroups,
			DiagnosticDescriptors.KeepReceiverGroupTogether,
		]
	);

	public static readonly StanzaRule SeparateImportSection = new(
		Descriptor: DiagnosticDescriptors.SeparateImportSection,
		Fix: GapFix.SiblingInsertion,
		Kinds: StanzaKinds.ImportContainerKinds,
		DefersTo: [DiagnosticDescriptors.SeparateCommentedSibling]
	);

	public static readonly StanzaRule SeparateImportGroups = new(
		Descriptor: DiagnosticDescriptors.SeparateImportGroups,
		Fix: GapFix.SiblingInsertion,
		Kinds: StanzaKinds.ImportContainerKinds,
		DefersTo: [DiagnosticDescriptors.SeparateCommentedSibling]
	);

	public static readonly StanzaRule SeparateFileScopedNamespace = new(
		Descriptor: DiagnosticDescriptors.SeparateFileScopedNamespace,
		Fix: GapFix.SiblingInsertion,
		Kinds: [SyntaxKind.FileScopedNamespaceDeclaration]
	);

	public static readonly StanzaRule SeparateRegionMarker = new(Descriptor: DiagnosticDescriptors.SeparateRegionMarker);

	public static readonly StanzaRule SeparateConditionalBlock = new(
		Descriptor: DiagnosticDescriptors.SeparateConditionalBlock
	);

	public static readonly StanzaRule SeparateSuppressionBlock = new(
		Descriptor: DiagnosticDescriptors.SeparateSuppressionBlock
	);

	public static readonly StanzaRule SeparateMultiLineAccessors = new(
		Descriptor: DiagnosticDescriptors.SeparateMultiLineAccessors,
		Fix: GapFix.SiblingInsertion,
		Kinds: [SyntaxKind.AccessorList],
		Pair: s_accessors
	);

	public static readonly StanzaRule KeepMultiLineAccessorsTogether = new(
		Descriptor: DiagnosticDescriptors.KeepMultiLineAccessorsTogether,
		Fix: GapFix.AdjacentRemoval,
		Kinds: [SyntaxKind.AccessorList],
		Pair: s_accessors
	);

	public static readonly StanzaRule KeepSingleLineAccessorsTogether = new(
		Descriptor: DiagnosticDescriptors.KeepSingleLineAccessorsTogether,
		Fix: GapFix.AdjacentRemoval,
		Kinds: [SyntaxKind.AccessorList]
	);

	public static readonly StanzaRule SeparateMultiLineSwitchSections = new(
		Descriptor: DiagnosticDescriptors.SeparateMultiLineSwitchSections,
		Fix: GapFix.SiblingInsertion,
		Kinds: [SyntaxKind.SwitchStatement],
		Pair: s_switchSections
	);

	public static readonly StanzaRule KeepMultiLineSwitchSectionsTogether = new(
		Descriptor: DiagnosticDescriptors.KeepMultiLineSwitchSectionsTogether,
		Fix: GapFix.AdjacentRemoval,
		Kinds: [SyntaxKind.SwitchStatement],
		Pair: s_switchSections
	);

	public static readonly StanzaRule KeepSingleLineSwitchSectionsTogether = new(
		Descriptor: DiagnosticDescriptors.KeepSingleLineSwitchSectionsTogether,
		Fix: GapFix.AdjacentRemoval,
		Kinds: [SyntaxKind.SwitchStatement]
	);

	public static readonly StanzaRule SeparateMultiLineSwitchExpressionArms = new(
		Descriptor: DiagnosticDescriptors.SeparateMultiLineSwitchExpressionArms,
		Fix: GapFix.SiblingInsertion,
		Kinds: [SyntaxKind.SwitchExpression],
		Pair: s_switchExpressionArms
	);

	public static readonly StanzaRule KeepMultiLineSwitchExpressionArmsTogether = new(
		Descriptor: DiagnosticDescriptors.KeepMultiLineSwitchExpressionArmsTogether,
		Fix: GapFix.AdjacentRemoval,
		Kinds: [SyntaxKind.SwitchExpression],
		Pair: s_switchExpressionArms
	);

	public static readonly StanzaRule KeepSingleLineSwitchExpressionArmsTogether = new(
		Descriptor: DiagnosticDescriptors.KeepSingleLineSwitchExpressionArmsTogether,
		Fix: GapFix.AdjacentRemoval,
		Kinds: [SyntaxKind.SwitchExpression]
	);

	public static readonly StanzaRule SeparateMultiLineInitializerMembers = new(
		Descriptor: DiagnosticDescriptors.SeparateMultiLineInitializerMembers,
		Fix: GapFix.SiblingInsertion,
		Kinds: StanzaKinds.InitializerMemberKinds,
		Pair: s_initializerMembers
	);

	public static readonly StanzaRule KeepMultiLineInitializerMembersTogether = new(
		Descriptor: DiagnosticDescriptors.KeepMultiLineInitializerMembersTogether,
		Fix: GapFix.AdjacentRemoval,
		Kinds: StanzaKinds.InitializerMemberKinds,
		Pair: s_initializerMembers
	);

	public static readonly StanzaRule KeepSingleLineInitializerMembersTogether = new(
		Descriptor: DiagnosticDescriptors.KeepSingleLineInitializerMembersTogether,
		Fix: GapFix.AdjacentRemoval,
		Kinds: StanzaKinds.InitializerMemberKinds
	);

	public static readonly StanzaRule SeparateMultiLineCollectionElements = new(
		Descriptor: DiagnosticDescriptors.SeparateMultiLineCollectionElements,
		Fix: GapFix.SiblingInsertion,
		Kinds: StanzaKinds.CollectionElementKinds,
		Pair: s_collectionElements
	);

	public static readonly StanzaRule KeepMultiLineCollectionElementsTogether = new(
		Descriptor: DiagnosticDescriptors.KeepMultiLineCollectionElementsTogether,
		Fix: GapFix.AdjacentRemoval,
		Kinds: StanzaKinds.CollectionElementKinds,
		Pair: s_collectionElements
	);

	public static readonly StanzaRule KeepSingleLineCollectionElementsTogether = new(
		Descriptor: DiagnosticDescriptors.KeepSingleLineCollectionElementsTogether,
		Fix: GapFix.AdjacentRemoval,
		Kinds: StanzaKinds.CollectionElementKinds
	);

	public static readonly StanzaRule SeparateMultiLineArguments = new(
		Descriptor: DiagnosticDescriptors.SeparateMultiLineArguments,
		Fix: GapFix.SiblingInsertion,
		Kinds: StanzaKinds.ArgumentListKinds,
		Pair: s_arguments
	);

	public static readonly StanzaRule KeepMultiLineArgumentsTogether = new(
		Descriptor: DiagnosticDescriptors.KeepMultiLineArgumentsTogether,
		Fix: GapFix.AdjacentRemoval,
		Kinds: StanzaKinds.ArgumentListKinds,
		Pair: s_arguments
	);

	public static readonly StanzaRule KeepSingleLineArgumentsTogether = new(
		Descriptor: DiagnosticDescriptors.KeepSingleLineArgumentsTogether,
		Fix: GapFix.AdjacentRemoval,
		Kinds: StanzaKinds.ArgumentListKinds
	);

	public static readonly StanzaRule SeparateMultiLineParameters = new(
		Descriptor: DiagnosticDescriptors.SeparateMultiLineParameters,
		Fix: GapFix.SiblingInsertion,
		Kinds: StanzaKinds.ParameterListKinds,
		Pair: s_parameters
	);

	public static readonly StanzaRule KeepMultiLineParametersTogether = new(
		Descriptor: DiagnosticDescriptors.KeepMultiLineParametersTogether,
		Fix: GapFix.AdjacentRemoval,
		Kinds: StanzaKinds.ParameterListKinds,
		Pair: s_parameters
	);

	public static readonly StanzaRule KeepSingleLineParametersTogether = new(
		Descriptor: DiagnosticDescriptors.KeepSingleLineParametersTogether,
		Fix: GapFix.AdjacentRemoval,
		Kinds: StanzaKinds.ParameterListKinds
	);

	public static readonly StanzaRule SeparateMultiLineDeclarators = new(
		Descriptor: DiagnosticDescriptors.SeparateMultiLineDeclarators,
		Fix: GapFix.SiblingInsertion,
		Kinds: [SyntaxKind.VariableDeclaration],
		Pair: s_declarators
	);

	public static readonly StanzaRule KeepMultiLineDeclaratorsTogether = new(
		Descriptor: DiagnosticDescriptors.KeepMultiLineDeclaratorsTogether,
		Fix: GapFix.AdjacentRemoval,
		Kinds: [SyntaxKind.VariableDeclaration],
		Pair: s_declarators
	);

	public static readonly StanzaRule KeepSingleLineDeclaratorsTogether = new(
		Descriptor: DiagnosticDescriptors.KeepSingleLineDeclaratorsTogether,
		Fix: GapFix.AdjacentRemoval,
		Kinds: [SyntaxKind.VariableDeclaration]
	);

	public static readonly StanzaRule SeparateClauseKeywords = new(
		Descriptor: DiagnosticDescriptors.SeparateClauseKeywords,
		Fix: GapFix.TokenInsertion,
		Kinds: StanzaKinds.ClauseStatementKinds,
		Pair: s_clauseKeywords
	);

	public static readonly StanzaRule KeepClauseKeywordsAdjacent = new(
		Descriptor: DiagnosticDescriptors.KeepClauseKeywordsAdjacent,
		Fix: GapFix.AdjacentRemoval,
		Kinds: StanzaKinds.ClauseStatementKinds,
		AnchoredOnPrevious: true,
		Pair: s_clauseKeywords
	);

	public static readonly StanzaRule RemoveBracePadding = new(
		Descriptor: DiagnosticDescriptors.RemoveBracePadding,
		Fix: GapFix.AdjacentRemoval,
		Kinds: StanzaKinds.BraceContainerKinds,
		AnchoredOnPrevious: true
	);

	/// <summary>Leaves the gap before a block that opens a section to the brace padding rule where it is on.</summary>
	public static readonly StanzaRule RemoveSwitchLabelPadding = new(
		Descriptor: DiagnosticDescriptors.RemoveSwitchLabelPadding,
		Fix: GapFix.AdjacentRemoval,
		Kinds: [SyntaxKind.SwitchSection],
		AnchoredOnPrevious: true,
		DefersTo: [DiagnosticDescriptors.RemoveBracePadding]
	);

	public static readonly StanzaRule SeparateMultiLineMarkupElements = new(
		Descriptor: DiagnosticDescriptors.SeparateMultiLineMarkupElements,
		Fix: GapFix.MarkupInsertion,
		Kinds: StanzaKinds.MarkupLiteralKinds,
		Pair: s_markupElements
	);

	/// <summary>Reads the single-line sibling, as the two own disjoint gaps.</summary>
	public static readonly StanzaRule KeepMultiLineMarkupElementsTogether = new(
		Descriptor: DiagnosticDescriptors.KeepMultiLineMarkupElementsTogether,
		Fix: GapFix.MarkupCapRemoval,
		Kinds: StanzaKinds.MarkupLiteralKinds,
		Pair: s_markupElements,
		Reads: [DiagnosticDescriptors.KeepSingleLineMarkupElementsTogether]
	);

	/// <summary>Reads its multi-line sibling, as the two own disjoint gaps.</summary>
	public static readonly StanzaRule KeepSingleLineMarkupElementsTogether = new(
		Descriptor: DiagnosticDescriptors.KeepSingleLineMarkupElementsTogether,
		Fix: GapFix.MarkupCapRemoval,
		Kinds: StanzaKinds.MarkupLiteralKinds,
		Reads: [DiagnosticDescriptors.KeepMultiLineMarkupElementsTogether]
	);

	public static readonly StanzaRule CollapseExtraBlankLinesBetweenMarkupElements = new(
		Descriptor: DiagnosticDescriptors.CollapseExtraBlankLinesBetweenMarkupElements,
		Fix: GapFix.MarkupSurplusRemoval,
		Kinds: StanzaKinds.MarkupLiteralKinds,
		DefersTo: [DiagnosticDescriptors.KeepMultiLineMarkupElementsTogether, DiagnosticDescriptors.KeepSingleLineMarkupElementsTogether]
	);

	/// <summary>Leaves a gap between single-line elements to ZA2003 where that rule is on.</summary>
	public static readonly StanzaRule SeparateCommentedMarkupElement = new(
		Descriptor: DiagnosticDescriptors.SeparateCommentedMarkupElement,
		Fix: GapFix.MarkupInsertion,
		Kinds: StanzaKinds.MarkupLiteralKinds,
		DefersTo: [DiagnosticDescriptors.KeepSingleLineMarkupElementsTogether]
	);

	/// <summary>Judges the blank lines inside an element, which no gap between two elements shares.</summary>
	public static readonly StanzaRule RemoveMarkupElementPadding = new(
		Descriptor: DiagnosticDescriptors.RemoveMarkupElementPadding,
		Fix: GapFix.MarkupCapRemoval,
		Kinds: StanzaKinds.MarkupLiteralKinds
	);

	public static readonly StanzaRule SeparateSlashOfSelfClosingMarkupElement = new(
		Descriptor: DiagnosticDescriptors.SeparateSlashOfSelfClosingMarkupElement,
		Kinds: StanzaKinds.MarkupLiteralKinds,
		Pair: s_markupSlash
	);

	public static readonly StanzaRule KeepSlashOfSelfClosingMarkupElementTight = new(
		Descriptor: DiagnosticDescriptors.KeepSlashOfSelfClosingMarkupElementTight,
		Kinds: StanzaKinds.MarkupLiteralKinds,
		Pair: s_markupSlash
	);

	/// <summary>Writes the slash of the tag it writes as the pair chose, so it defers to the tight half.</summary>
	public static readonly StanzaRule WriteEmptyMarkupElementAsSelfClosingTag = new(
		Descriptor: DiagnosticDescriptors.WriteEmptyMarkupElementAsSelfClosingTag,
		Kinds: StanzaKinds.MarkupLiteralKinds,
		DefersTo: [DiagnosticDescriptors.KeepSlashOfSelfClosingMarkupElementTight]
	);

	public static readonly StanzaRule KeepDocumentationCommentWithDeclaration = new(
		Descriptor: DiagnosticDescriptors.KeepDocumentationCommentWithDeclaration,
		Fix: GapFix.DeclarationGapRemoval
	);

	/// <summary>Leaves the expression on its own line wherever the expression line break rule is on.</summary>
	public static readonly StanzaRule KeepArrowOnSignatureLine = new(
		Descriptor: DiagnosticDescriptors.KeepArrowOnSignatureLine,
		Kinds: [SyntaxKind.ArrowExpressionClause],
		Pair: s_arrows,
		DefersTo: [DiagnosticDescriptors.BreakExpressionOffSignatureLine]
	);

	public static readonly StanzaRule PutArrowOnLineAfterSignature = new(
		Descriptor: DiagnosticDescriptors.PutArrowOnLineAfterSignature,
		Kinds: [SyntaxKind.ArrowExpressionClause],
		Pair: s_arrows
	);

	/// <summary>Writes an arrow on the side the arrow pair chose, so it defers to the leading half.</summary>
	public static readonly StanzaRule BreakExpressionOffSignatureLine = new(
		Descriptor: DiagnosticDescriptors.BreakExpressionOffSignatureLine,
		Kinds: [SyntaxKind.ArrowExpressionClause],
		DefersTo: [DiagnosticDescriptors.PutArrowOnLineAfterSignature]
	);

	/// <summary>Writes an arrow on the side the arrow pair chose, with the gaps the multi-line rules ask.</summary>
	public static readonly StanzaRule UseExpressionBodyForSingleLineMember = new(
		Descriptor: DiagnosticDescriptors.UseExpressionBodyForSingleLineMember,
		Kinds: [SyntaxKind.Block],

		DefersTo:
		[
			DiagnosticDescriptors.PutArrowOnLineAfterSignature,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.SeparateMultiLineAccessors,
			DiagnosticDescriptors.KeepMultiLineAccessorsTogether,
		]
	);

	/// <summary>Writes an arrow on the side the arrow pair chose, with the gaps the multi-line rules ask.</summary>
	public static readonly StanzaRule UseExpressionBodyForSingleLineValueMember = new(
		Descriptor: DiagnosticDescriptors.UseExpressionBodyForSingleLineValueMember,
		Kinds: [SyntaxKind.Block],

		DefersTo:
		[
			DiagnosticDescriptors.PutArrowOnLineAfterSignature,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.SeparateMultiLineAccessors,
			DiagnosticDescriptors.KeepMultiLineAccessorsTogether,
		]
	);

	/// <summary>Writes a block body for a multi-line body, waiting for the collapse of an empty block.</summary>
	public static readonly StanzaRule UseBlockBodyForMultiLineMember = new(
		Descriptor: DiagnosticDescriptors.UseBlockBodyForMultiLineMember,
		Kinds: [SyntaxKind.ArrowExpressionClause],
		DefersTo: [DiagnosticDescriptors.CollapseEmptyBlocks]
	);

	/// <summary>Writes a block body for a multi-line value, waiting for the collapse of an empty block.</summary>
	public static readonly StanzaRule UseBlockBodyForMultiLineValueMember = new(
		Descriptor: DiagnosticDescriptors.UseBlockBodyForMultiLineValueMember,
		Kinds: [SyntaxKind.ArrowExpressionClause],
		DefersTo: [DiagnosticDescriptors.CollapseEmptyBlocks]
	);

	public static readonly StanzaRule SeparateSlashOfSelfClosingTag = new(
		Descriptor: DiagnosticDescriptors.SeparateSlashOfSelfClosingTag,
		Pair: s_selfClosingSlash
	);

	public static readonly StanzaRule KeepSlashOfSelfClosingTagTight = new(
		Descriptor: DiagnosticDescriptors.KeepSlashOfSelfClosingTagTight,
		Pair: s_selfClosingSlash
	);

	public static readonly StanzaRule AddTrailingComma = new(
		Descriptor: DiagnosticDescriptors.AddTrailingComma,
		Kinds: StanzaKinds.TrailingCommaKinds,
		Pair: s_trailingCommas
	);

	public static readonly StanzaRule RemoveTrailingComma = new(
		Descriptor: DiagnosticDescriptors.RemoveTrailingComma,
		Kinds: StanzaKinds.TrailingCommaKinds,
		Pair: s_trailingCommas
	);

	/// <summary>Owns no gap, but another rule defers to it, so its entry is read from here.</summary>
	public static readonly StanzaRule LayOutDocumentationComment = new(Descriptor: DiagnosticDescriptors.LayOutDocumentationComment);

	/// <summary>Owns no gap, but other rules defer to it, so its entry is read from here.</summary>
	public static readonly StanzaRule MarkDocumentationParagraphs = new(Descriptor: DiagnosticDescriptors.MarkDocumentationParagraphs);

	/// <summary>Owns no gap, but defers to the paragraph rule, so its deferral is read from here.</summary>
	public static readonly StanzaRule RemoveDocumentationPadding = new(
		Descriptor: DiagnosticDescriptors.RemoveDocumentationPadding,
		DefersTo: [DiagnosticDescriptors.MarkDocumentationParagraphs]
	);

	/// <summary>Owns no gap, but defers to the layout and paragraph rules, so its entry is read.</summary>
	public static readonly StanzaRule LayOutItemsOfDocumentationList = new(
		Descriptor: DiagnosticDescriptors.LayOutItemsOfDocumentationList,
		DefersTo: [DiagnosticDescriptors.LayOutDocumentationComment, DiagnosticDescriptors.MarkDocumentationParagraphs]
	);

	/// <summary>Spaces the slash of the element it writes as the pair chose, so it defers to the tight half.</summary>
	public static readonly StanzaRule MarkLanguageKeywords = new(
		Descriptor: DiagnosticDescriptors.MarkLanguageKeywords,
		DefersTo: [DiagnosticDescriptors.KeepSlashOfSelfClosingTagTight]
	);

	/// <summary>Spaces the slash of the element it writes as the pair chose, so it defers to the tight half.</summary>
	public static readonly StanzaRule WriteEmptyElementAsSelfClosingTag = new(
		Descriptor: DiagnosticDescriptors.WriteEmptyElementAsSelfClosingTag,
		DefersTo: [DiagnosticDescriptors.KeepSlashOfSelfClosingTagTight]
	);

	/// <summary>Names every argument of a call, so the rule that names the literals defers to it.</summary>
	public static readonly StanzaRule NameArgumentsOfCall = new(Descriptor: DiagnosticDescriptors.NameArgumentsOfCall, Kinds: StanzaKinds.CallKinds);

	/// <summary>Names only the literals, so it leaves a call the rule naming every argument reports.</summary>
	public static readonly StanzaRule NameLiteralArgumentsOfCall = new(
		Descriptor: DiagnosticDescriptors.NameLiteralArgumentsOfCall,
		Kinds: StanzaKinds.CallKinds,
		DefersTo: [DiagnosticDescriptors.NameArgumentsOfCall]
	);

	/// <summary>Braces a chain with a multi-line branch, so the rules that rewrite a chain wait for it.</summary>
	/// <remarks>
	/// It waits for the collapse of an empty block that alone makes the chain multi-line.
	/// </remarks>
	public static readonly StanzaRule UseBracesConsistently = new(
		Descriptor: DiagnosticDescriptors.UseBracesConsistently,
		Kinds: StanzaKinds.BraceStatementKinds,
		DefersTo: [DiagnosticDescriptors.CollapseEmptyBlocks]
	);

	/// <summary>Joins an empty block to the line of its header with the gaps the multi-line rules ask.</summary>
	public static readonly StanzaRule CollapseEmptyBlocks = new(
		Descriptor: DiagnosticDescriptors.CollapseEmptyBlocks,
		Kinds: StanzaKinds.EmptyBlockKinds,

		DefersTo:
		[
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.SeparateMultiLineAccessors,
			DiagnosticDescriptors.SeparateMultiLineSwitchSections,
			DiagnosticDescriptors.SeparateMultiLineSwitchExpressionArms,
			DiagnosticDescriptors.SeparateMultiLineInitializerMembers,
			DiagnosticDescriptors.SeparateMultiLineCollectionElements,
			DiagnosticDescriptors.SeparateMultiLineArguments,
			DiagnosticDescriptors.SeparateMultiLineDeclarators,
		]
	);

	/// <summary>Moves the body of an else out after the braces rule, with the gaps the multi-line rule asks.</summary>
	public static readonly StanzaRule RemoveElseAfterJump = new(
		Descriptor: DiagnosticDescriptors.RemoveElseAfterJump,
		Kinds: [SyntaxKind.IfStatement],
		DefersTo: [DiagnosticDescriptors.UseBracesConsistently, DiagnosticDescriptors.SeparateMultiLineConstructs]
	);

	/// <summary>Merges two nested conditions after the braces rule, with the gaps the multi-line rule asks.</summary>
	public static readonly StanzaRule MergeNestedIfStatements = new(
		Descriptor: DiagnosticDescriptors.MergeNestedIfStatements,
		Kinds: [SyntaxKind.IfStatement],
		DefersTo: [DiagnosticDescriptors.UseBracesConsistently, DiagnosticDescriptors.SeparateMultiLineConstructs]
	);

	public static readonly StanzaRule UsePatternForNullCheck = new(
		Descriptor: DiagnosticDescriptors.UsePatternForNullCheck,
		Kinds: [SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression],
		Pair: s_nullChecks
	);

	public static readonly StanzaRule UseEqualityOperatorForNullCheck = new(
		Descriptor: DiagnosticDescriptors.UseEqualityOperatorForNullCheck,
		Kinds: [SyntaxKind.IsPatternExpression],
		Pair: s_nullChecks
	);

	public static readonly ImmutableArray<StanzaRule> All =
	[
		NameArgumentsOfCall,
		NameLiteralArgumentsOfCall,
		UseBracesConsistently,
		CollapseEmptyBlocks,
		RemoveElseAfterJump,
		MergeNestedIfStatements,
		UsePatternForNullCheck,
		UseEqualityOperatorForNullCheck,
		SeparateDeclarationRuns,
		SeparateInvocationRuns,
		SeparateAssertionRuns,
		SeparateAwaitRuns,
		SeparateEventSubscriptionRuns,
		SeparateAssignmentRuns,
		SeparateYieldRuns,
		SeparateLocalFunctionRuns,
		SeparateMemberGroups,
		SeparateModifierGroups,
		SeparateSameTypeDeclarationRuns,
		SeparateMultiLineConstructs,
		KeepTwoUnitsOfContainerTogether,
		GatherRunOfLooseStatements,
		GatherRunOfLooseMembers,
		KeepRunOfStatementsTogether,
		KeepRunOfMembersTogether,
		SeparateCommentedSibling,
		SeparateJumpStatement,
		SeparateBaseCall,
		SeparateGuardClauses,
		SeparateCheckedStanza,
		SeparateReceiverGroups,
		CollapseExtraBlankLines,
		KeepGuardClausesTogether,
		KeepGuardWithBinding,
		KeepAccumulatorWithLoop,
		KeepJumpWithBinding,
		KeepNarrowingAssertionWithBinding,
		KeepInitializationWithCalls,
		KeepReceiverGroupTogether,
		KeepDependentBindingsTogether,
		SeparateImportSection,
		SeparateImportGroups,
		SeparateFileScopedNamespace,
		SeparateRegionMarker,
		SeparateConditionalBlock,
		SeparateSuppressionBlock,
		SeparateMultiLineAccessors,
		KeepMultiLineAccessorsTogether,
		KeepSingleLineAccessorsTogether,
		SeparateMultiLineSwitchSections,
		KeepMultiLineSwitchSectionsTogether,
		KeepSingleLineSwitchSectionsTogether,
		SeparateMultiLineSwitchExpressionArms,
		KeepMultiLineSwitchExpressionArmsTogether,
		KeepSingleLineSwitchExpressionArmsTogether,
		SeparateMultiLineInitializerMembers,
		KeepMultiLineInitializerMembersTogether,
		KeepSingleLineInitializerMembersTogether,
		SeparateMultiLineCollectionElements,
		KeepMultiLineCollectionElementsTogether,
		KeepSingleLineCollectionElementsTogether,
		SeparateMultiLineArguments,
		KeepMultiLineArgumentsTogether,
		KeepSingleLineArgumentsTogether,
		SeparateMultiLineParameters,
		KeepMultiLineParametersTogether,
		KeepSingleLineParametersTogether,
		SeparateMultiLineDeclarators,
		KeepMultiLineDeclaratorsTogether,
		KeepSingleLineDeclaratorsTogether,
		SeparateClauseKeywords,
		KeepClauseKeywordsAdjacent,
		RemoveBracePadding,
		RemoveSwitchLabelPadding,
		SeparateMultiLineMarkupElements,
		KeepMultiLineMarkupElementsTogether,
		KeepSingleLineMarkupElementsTogether,
		CollapseExtraBlankLinesBetweenMarkupElements,
		SeparateCommentedMarkupElement,
		RemoveMarkupElementPadding,
		SeparateSlashOfSelfClosingMarkupElement,
		KeepSlashOfSelfClosingMarkupElementTight,
		WriteEmptyMarkupElementAsSelfClosingTag,
		KeepDocumentationCommentWithDeclaration,
		KeepArrowOnSignatureLine,
		PutArrowOnLineAfterSignature,
		BreakExpressionOffSignatureLine,
		UseExpressionBodyForSingleLineMember,
		UseExpressionBodyForSingleLineValueMember,
		UseBlockBodyForMultiLineMember,
		UseBlockBodyForMultiLineValueMember,
		SeparateSlashOfSelfClosingTag,
		KeepSlashOfSelfClosingTagTight,
		AddTrailingComma,
		RemoveTrailingComma,
		LayOutDocumentationComment,
		MarkDocumentationParagraphs,
		RemoveDocumentationPadding,
		LayOutItemsOfDocumentationList,
		MarkLanguageKeywords,
		WriteEmptyElementAsSelfClosingTag,
	];

	private static readonly ImmutableDictionary<string, StanzaRule> s_byId = All.ToImmutableDictionary(
		static rule => rule.Descriptor.Id,
		StringComparer.Ordinal
	);

	public static StanzaRule? Find(string ruleId) => s_byId.TryGetValue(ruleId, out StanzaRule? rule) ? rule : null;

	/// <summary>Gets the entry of a rule, or throws when the registry has none.</summary>
	public static StanzaRule Get(DiagnosticDescriptor descriptor) =>
		Find(descriptor.Id) ?? throw new InvalidOperationException($"{descriptor.Id} has no entry in StanzaRules.");
}
