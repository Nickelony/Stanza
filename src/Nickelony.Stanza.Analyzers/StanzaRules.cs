using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Nickelony.Stanza.Analyzers;

/// <summary>Registers each rule that names node kinds, a gap fix, a pair or a deferral, or is deferred to.</summary>
internal static class StanzaRules
{
	private static readonly GapPair s_accessors = new(
		First: DiagnosticDescriptors.SeparateMultiLineAccessors,
		Second: DiagnosticDescriptors.KeepMultiLineAccessorsTogether,
		DefaultHalf: DiagnosticDescriptors.KeepMultiLineAccessorsTogether,
		OwnerWhenBothEnabled: DiagnosticDescriptors.KeepMultiLineAccessorsTogether
	);

	private static readonly GapPair s_switchSections = new(
		First: DiagnosticDescriptors.SeparateMultiLineSwitchSections,
		Second: DiagnosticDescriptors.KeepMultiLineSwitchSectionsTogether,
		DefaultHalf: DiagnosticDescriptors.SeparateMultiLineSwitchSections,
		OwnerWhenBothEnabled: DiagnosticDescriptors.SeparateMultiLineSwitchSections
	);

	private static readonly GapPair s_switchExpressionArms = new(
		First: DiagnosticDescriptors.SeparateMultiLineSwitchExpressionArms,
		Second: DiagnosticDescriptors.KeepMultiLineSwitchExpressionArmsTogether,
		DefaultHalf: DiagnosticDescriptors.SeparateMultiLineSwitchExpressionArms,
		OwnerWhenBothEnabled: DiagnosticDescriptors.SeparateMultiLineSwitchExpressionArms
	);

	private static readonly GapPair s_initializerMembers = new(
		First: DiagnosticDescriptors.SeparateMultiLineInitializerMembers,
		Second: DiagnosticDescriptors.KeepMultiLineInitializerMembersTogether,
		DefaultHalf: DiagnosticDescriptors.KeepMultiLineInitializerMembersTogether,
		OwnerWhenBothEnabled: DiagnosticDescriptors.KeepMultiLineInitializerMembersTogether
	);

	private static readonly GapPair s_collectionElements = new(
		First: DiagnosticDescriptors.SeparateMultiLineCollectionElements,
		Second: DiagnosticDescriptors.KeepMultiLineCollectionElementsTogether,
		DefaultHalf: DiagnosticDescriptors.KeepMultiLineCollectionElementsTogether,
		OwnerWhenBothEnabled: DiagnosticDescriptors.KeepMultiLineCollectionElementsTogether
	);

	private static readonly GapPair s_arguments = new(
		First: DiagnosticDescriptors.SeparateMultiLineArguments,
		Second: DiagnosticDescriptors.KeepMultiLineArgumentsTogether,
		DefaultHalf: DiagnosticDescriptors.KeepMultiLineArgumentsTogether,
		OwnerWhenBothEnabled: DiagnosticDescriptors.KeepMultiLineArgumentsTogether
	);

	private static readonly GapPair s_parameters = new(
		First: DiagnosticDescriptors.SeparateMultiLineParameters,
		Second: DiagnosticDescriptors.KeepMultiLineParametersTogether,
		DefaultHalf: DiagnosticDescriptors.KeepMultiLineParametersTogether,
		OwnerWhenBothEnabled: DiagnosticDescriptors.KeepMultiLineParametersTogether
	);

	private static readonly GapPair s_declarators = new(
		First: DiagnosticDescriptors.SeparateMultiLineDeclarators,
		Second: DiagnosticDescriptors.KeepMultiLineDeclaratorsTogether,
		DefaultHalf: DiagnosticDescriptors.KeepMultiLineDeclaratorsTogether,
		OwnerWhenBothEnabled: DiagnosticDescriptors.KeepMultiLineDeclaratorsTogether
	);

	private static readonly GapPair s_clauseKeywords = new(
		First: DiagnosticDescriptors.SeparateClauseKeywordFromBraceBeforeIt,
		Second: DiagnosticDescriptors.KeepClauseKeywordsAdjacent,
		DefaultHalf: DiagnosticDescriptors.KeepClauseKeywordsAdjacent,
		OwnerWhenBothEnabled: DiagnosticDescriptors.KeepClauseKeywordsAdjacent
	);

	private static readonly GapPair s_markupElements = new(
		First: DiagnosticDescriptors.SeparateMultiLineMarkupElements,
		Second: DiagnosticDescriptors.KeepMultiLineMarkupElementsTogether,
		DefaultHalf: DiagnosticDescriptors.KeepMultiLineMarkupElementsTogether,
		OwnerWhenBothEnabled: DiagnosticDescriptors.KeepMultiLineMarkupElementsTogether
	);

	private static readonly GapPair s_arrows = new(
		First: DiagnosticDescriptors.KeepArrowOnSignatureLine,
		Second: DiagnosticDescriptors.PutArrowOnLineAfterSignature,
		DefaultHalf: DiagnosticDescriptors.KeepArrowOnSignatureLine,
		OwnerWhenBothEnabled: DiagnosticDescriptors.PutArrowOnLineAfterSignature
	);

	private static readonly GapPair s_selfClosingSlash = new(
		First: DiagnosticDescriptors.SeparateSlashOfSelfClosingTag,
		Second: DiagnosticDescriptors.KeepSlashOfSelfClosingTagTight,
		DefaultHalf: DiagnosticDescriptors.KeepSlashOfSelfClosingTagTight,
		OwnerWhenBothEnabled: DiagnosticDescriptors.KeepSlashOfSelfClosingTagTight
	);

	public static readonly StanzaRule SeparateDeclarationAndInvocationRuns = new(
		Descriptor: DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns,
		Fix: GapFix.SiblingInsertion,
		Kinds: StanzaKinds.ContainerKinds,

		DefersTo:
		[
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateImportSection,
			DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors,
		]
	);

	public static readonly StanzaRule SeparateMemberGroups = new(
		Descriptor: DiagnosticDescriptors.SeparateMemberGroups,
		Fix: GapFix.SiblingInsertion,
		Kinds: StanzaKinds.MemberContainerKinds,

		DefersTo:
		[
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateImportSection,
			DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors,
			DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns,
		]
	);

	public static readonly StanzaRule SeparateModifierGroups = new(
		Descriptor: DiagnosticDescriptors.SeparateModifierGroups,
		Fix: GapFix.SiblingInsertion,
		Kinds: StanzaKinds.MemberContainerKinds,

		DefersTo:
		[
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateImportSection,
			DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors,
			DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns,
			DiagnosticDescriptors.SeparateMemberGroups,
		]
	);

	public static readonly StanzaRule SeparateSameTypeDeclarationGroups = new(
		Descriptor: DiagnosticDescriptors.SeparateSameTypeDeclarationGroups,
		Fix: GapFix.SiblingInsertion,
		Kinds: StanzaKinds.ContainerKinds,

		DefersTo:
		[
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			DiagnosticDescriptors.SeparateImportSection,
			DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors,
			DiagnosticDescriptors.SeparateDeclarationAndInvocationRuns,
			DiagnosticDescriptors.SeparateMemberGroups,
			DiagnosticDescriptors.SeparateModifierGroups,
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

	public static readonly StanzaRule SeparateImportSection = new(
		Descriptor: DiagnosticDescriptors.SeparateImportSection,
		Fix: GapFix.SiblingInsertion,
		Kinds: StanzaKinds.ImportContainerKinds
	);

	public static readonly StanzaRule SeparateImportDirectiveGroups = new(
		Descriptor: DiagnosticDescriptors.SeparateImportDirectiveGroups,
		Fix: GapFix.SiblingInsertion,
		Kinds: StanzaKinds.ImportContainerKinds
	);

	public static readonly StanzaRule SeparateFileScopedNamespaceDeclarationFromFirstMember = new(
		Descriptor: DiagnosticDescriptors.SeparateFileScopedNamespaceDeclarationFromFirstMember,
		Fix: GapFix.SiblingInsertion,
		Kinds: [SyntaxKind.FileScopedNamespaceDeclaration]
	);

	public static readonly StanzaRule SeparateRegionMarkerFromNeighbors = new(Descriptor: DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors);

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

	public static readonly StanzaRule SeparateClauseKeywordFromBraceBeforeIt = new(
		Descriptor: DiagnosticDescriptors.SeparateClauseKeywordFromBraceBeforeIt,
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

	public static readonly StanzaRule SeparateMultiLineMarkupElements = new(
		Descriptor: DiagnosticDescriptors.SeparateMultiLineMarkupElements,
		Fix: GapFix.MarkupInsertion,
		Kinds: StanzaKinds.MarkupLiteralKinds,
		Pair: s_markupElements
	);

	/// <summary>Lists the single-line sibling only to read its state, as the two own disjoint gaps.</summary>
	public static readonly StanzaRule KeepMultiLineMarkupElementsTogether = new(
		Descriptor: DiagnosticDescriptors.KeepMultiLineMarkupElementsTogether,
		Fix: GapFix.MarkupCapRemoval,
		Kinds: StanzaKinds.MarkupLiteralKinds,
		Pair: s_markupElements,
		DefersTo: [DiagnosticDescriptors.KeepSingleLineMarkupElementsTogether]
	);

	/// <summary>Lists the multi-line sibling only to read its state, as the two own disjoint gaps.</summary>
	public static readonly StanzaRule KeepSingleLineMarkupElementsTogether = new(
		Descriptor: DiagnosticDescriptors.KeepSingleLineMarkupElementsTogether,
		Fix: GapFix.MarkupCapRemoval,
		Kinds: StanzaKinds.MarkupLiteralKinds,
		DefersTo: [DiagnosticDescriptors.KeepMultiLineMarkupElementsTogether]
	);

	public static readonly StanzaRule CollapseExtraBlankLinesBetweenMarkupElements = new(
		Descriptor: DiagnosticDescriptors.CollapseExtraBlankLinesBetweenMarkupElements,
		Fix: GapFix.MarkupExtraRemoval,
		Kinds: StanzaKinds.MarkupLiteralKinds,
		DefersTo: [DiagnosticDescriptors.KeepMultiLineMarkupElementsTogether, DiagnosticDescriptors.KeepSingleLineMarkupElementsTogether]
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

	/// <summary>Writes an arrow on the side the arrow pair chose, so it defers to the leading half.</summary>
	public static readonly StanzaRule UseExpressionBodyForSingleLineMember = new(
		Descriptor: DiagnosticDescriptors.UseExpressionBodyForSingleLineMember,
		Kinds: [SyntaxKind.Block],
		DefersTo: [DiagnosticDescriptors.PutArrowOnLineAfterSignature]
	);

	/// <summary>Writes an arrow on the side the arrow pair chose, so it defers to the leading half.</summary>
	public static readonly StanzaRule UseExpressionBodyForSingleLineValueMember = new(
		Descriptor: DiagnosticDescriptors.UseExpressionBodyForSingleLineValueMember,
		Kinds: [SyntaxKind.Block],
		DefersTo: [DiagnosticDescriptors.PutArrowOnLineAfterSignature]
	);

	public static readonly StanzaRule SeparateSlashOfSelfClosingTag = new(
		Descriptor: DiagnosticDescriptors.SeparateSlashOfSelfClosingTag,
		Pair: s_selfClosingSlash
	);

	public static readonly StanzaRule KeepSlashOfSelfClosingTagTight = new(
		Descriptor: DiagnosticDescriptors.KeepSlashOfSelfClosingTagTight,
		Pair: s_selfClosingSlash
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
	public static readonly StanzaRule MarkNullKeyword = new(
		Descriptor: DiagnosticDescriptors.MarkNullKeyword,
		DefersTo: [DiagnosticDescriptors.KeepSlashOfSelfClosingTagTight]
	);

	/// <summary>Spaces the slash of the element it writes as the pair chose, so it defers to the tight half.</summary>
	public static readonly StanzaRule WriteEmptyElementAsSelfClosingTag = new(
		Descriptor: DiagnosticDescriptors.WriteEmptyElementAsSelfClosingTag,
		DefersTo: [DiagnosticDescriptors.KeepSlashOfSelfClosingTagTight]
	);

	public static readonly ImmutableArray<StanzaRule> All =
	[
		SeparateDeclarationAndInvocationRuns,
		SeparateMemberGroups,
		SeparateModifierGroups,
		SeparateSameTypeDeclarationGroups,
		SeparateMultiLineConstructs,
		KeepTwoUnitsOfContainerTogether,
		SeparateImportSection,
		SeparateImportDirectiveGroups,
		SeparateFileScopedNamespaceDeclarationFromFirstMember,
		SeparateRegionMarkerFromNeighbors,
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
		SeparateClauseKeywordFromBraceBeforeIt,
		KeepClauseKeywordsAdjacent,
		RemoveBracePadding,
		SeparateMultiLineMarkupElements,
		KeepMultiLineMarkupElementsTogether,
		KeepSingleLineMarkupElementsTogether,
		CollapseExtraBlankLinesBetweenMarkupElements,
		KeepDocumentationCommentWithDeclaration,
		KeepArrowOnSignatureLine,
		PutArrowOnLineAfterSignature,
		BreakExpressionOffSignatureLine,
		UseExpressionBodyForSingleLineMember,
		UseExpressionBodyForSingleLineValueMember,
		SeparateSlashOfSelfClosingTag,
		KeepSlashOfSelfClosingTagTight,
		LayOutDocumentationComment,
		MarkDocumentationParagraphs,
		RemoveDocumentationPadding,
		LayOutItemsOfDocumentationList,
		MarkNullKeyword,
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
