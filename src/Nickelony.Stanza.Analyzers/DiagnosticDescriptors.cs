using Microsoft.CodeAnalysis;
using Nickelony.Stanza.Markup.Core;

namespace Nickelony.Stanza.Analyzers;

/// <summary>The descriptor of every Stanza rule, which holds its ID, title, message and default severity.</summary>
internal static class DiagnosticDescriptors
{
	private const string HelpRoot = "https://github.com/Nickelony/Stanza/wiki";

	public static readonly DiagnosticDescriptor SeparateDeclarationAndInvocationRuns = new(
		id: "ZA0001",
		title: "Separate declaration and invocation runs",
		messageFormat: "Add a blank line between declaration and invocation runs",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Requires blank lines around runs of neighboring declarations or invocations that group together.",
		helpLinkUri: $"{HelpRoot}/ZA0001-Separate-declaration-and-invocation-runs"
	);

	public static readonly DiagnosticDescriptor SeparateMemberGroups = new(
		id: "ZA0002",
		title: "Separate member groups",
		messageFormat: "Add a blank line between member groups",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Requires blank lines around runs of two or more neighboring members of the same kind.",
		helpLinkUri: $"{HelpRoot}/ZA0002-Separate-member-groups"
	);

	public static readonly DiagnosticDescriptor SeparateModifierGroups = new(
		id: "ZA0003",
		title: "Separate modifier groups",
		messageFormat: "Add a blank line between modifier groups",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires blank lines around runs of two or more neighboring members of one kind that share their static and readonly modifiers.",
		helpLinkUri: $"{HelpRoot}/ZA0003-Separate-modifier-groups"
	);

	public static readonly DiagnosticDescriptor SeparateSameTypeDeclarationGroups = new(
		id: "ZA0004",
		title: "Separate same-type declaration groups",
		messageFormat: "Add a blank line between same-type declaration groups",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Requires a blank line around groups of two or more declarations that share the exact same type.",
		helpLinkUri: $"{HelpRoot}/ZA0004-Separate-same-type-declaration-groups"
	);

	public static readonly DiagnosticDescriptor SeparateMultiLineConstructs = new(
		id: "ZA0005",
		title: "Separate multi-line constructs",
		messageFormat: "Add a blank line before this construct",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Requires a blank line between adjacent siblings when either sibling spans multiple lines.",
		helpLinkUri: $"{HelpRoot}/ZA0005-Separate-multi-line-constructs"
	);

	public static readonly DiagnosticDescriptor KeepTwoUnitsOfContainerTogether = new(
		id: "ZA0006",
		title: "Keep the two units of a container together",
		messageFormat: "Remove the blank line before this statement or member",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires a container whose entire content is two single-line units to hold them next to each other, without a blank line between them.",
		helpLinkUri: $"{HelpRoot}/ZA0006-Keep-the-two-units-of-a-container-together"
	);

	public static readonly DiagnosticDescriptor SeparateImportSection = new(
		id: "ZA0007",
		title: "Separate the import section",
		messageFormat: "Add a blank line after the import section",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Requires a blank line between the import section of a file or namespace and the first member that follows it.",
		helpLinkUri: $"{HelpRoot}/ZA0007-Separate-the-import-section"
	);

	public static readonly DiagnosticDescriptor SeparateImportDirectiveGroups = new(
		id: "ZA0008",
		title: "Separate the import directive groups",
		messageFormat: "Add a blank line between the import directive groups",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires a blank line between two adjacent import directives that are not of the same kind.",
		helpLinkUri: $"{HelpRoot}/ZA0008-Separate-the-import-directive-groups"
	);

	public static readonly DiagnosticDescriptor SeparateFileScopedNamespaceDeclarationFromFirstMember = new(
		id: "ZA0009",
		title: "Separate a file-scoped namespace declaration from its first member",
		messageFormat: "Add a blank line after the file-scoped namespace declaration",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Requires a blank line between a file-scoped namespace declaration and the first member the declaration holds, unless an import section follows the declaration.",
		helpLinkUri: $"{HelpRoot}/ZA0009-Separate-a-file-scoped-namespace-declaration-from-its-first-member"
	);

	public static readonly DiagnosticDescriptor SeparateMultiLineAccessors = new(
		id: "ZA0010",
		title: "Separate multi-line accessors",
		messageFormat: "Add a blank line before this accessor",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires a blank line between adjacent accessors unless both accessors fit on a single line.",
		helpLinkUri: $"{HelpRoot}/ZA0010-Separate-multi-line-accessors"
	);

	public static readonly DiagnosticDescriptor KeepMultiLineAccessorsTogether = new(
		id: "ZA0011",
		title: "Keep multi-line accessors together",
		messageFormat: "Remove the blank line before this accessor",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires adjacent accessors to sit directly next to each other when either of them spans more than one line.",
		helpLinkUri: $"{HelpRoot}/ZA0011-Keep-multi-line-accessors-together"
	);

	public static readonly DiagnosticDescriptor KeepSingleLineAccessorsTogether = new(
		id: "ZA0012",
		title: "Keep single-line accessors together",
		messageFormat: "Remove the blank line before this accessor",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires adjacent accessors that each fit on a single line to sit directly next to each other.",
		helpLinkUri: $"{HelpRoot}/ZA0012-Keep-single-line-accessors-together"
	);

	public static readonly DiagnosticDescriptor SeparateMultiLineSwitchSections = new(
		id: "ZA0013",
		title: "Separate multi-line switch sections",
		messageFormat: "Add a blank line before this switch section",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Requires a blank line between adjacent switch sections unless both sections fit on a single line.",
		helpLinkUri: $"{HelpRoot}/ZA0013-Separate-multi-line-switch-sections"
	);

	public static readonly DiagnosticDescriptor KeepMultiLineSwitchSectionsTogether = new(
		id: "ZA0014",
		title: "Keep multi-line switch sections together",
		messageFormat: "Remove the blank line before this switch section",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires adjacent switch sections to sit directly next to each other when either of them spans more than one line.",
		helpLinkUri: $"{HelpRoot}/ZA0014-Keep-multi-line-switch-sections-together"
	);

	public static readonly DiagnosticDescriptor KeepSingleLineSwitchSectionsTogether = new(
		id: "ZA0015",
		title: "Keep single-line switch sections together",
		messageFormat: "Remove the blank line before this switch section",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires adjacent switch sections that each fit on a single line to sit directly next to each other.",
		helpLinkUri: $"{HelpRoot}/ZA0015-Keep-single-line-switch-sections-together"
	);

	public static readonly DiagnosticDescriptor SeparateMultiLineSwitchExpressionArms = new(
		id: "ZA0016",
		title: "Separate multi-line switch expression arms",
		messageFormat: "Add a blank line before this switch expression arm",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Requires a blank line between adjacent switch expression arms unless both arms fit on a single line.",
		helpLinkUri: $"{HelpRoot}/ZA0016-Separate-multi-line-switch-expression-arms"
	);

	public static readonly DiagnosticDescriptor KeepMultiLineSwitchExpressionArmsTogether = new(
		id: "ZA0017",
		title: "Keep multi-line switch expression arms together",
		messageFormat: "Remove the blank line before this switch expression arm",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires adjacent switch expression arms to sit directly next to each other when either of them spans more than one line.",
		helpLinkUri: $"{HelpRoot}/ZA0017-Keep-multi-line-switch-expression-arms-together"
	);

	public static readonly DiagnosticDescriptor KeepSingleLineSwitchExpressionArmsTogether = new(
		id: "ZA0018",
		title: "Keep single-line switch expression arms together",
		messageFormat: "Remove the blank line before this switch expression arm",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires adjacent switch expression arms that each fit on a single line to sit directly next to each other.",
		helpLinkUri: $"{HelpRoot}/ZA0018-Keep-single-line-switch-expression-arms-together"
	);

	public static readonly DiagnosticDescriptor SeparateMultiLineInitializerMembers = new(
		id: "ZA0019",
		title: "Separate multi-line initializer members",
		messageFormat: "Add a blank line before this initializer member",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires a blank line between two members of an object initializer, a `with` initializer or an anonymous object initializer when either spans more than one line.",
		helpLinkUri: $"{HelpRoot}/ZA0019-Separate-multi-line-initializer-members"
	);

	public static readonly DiagnosticDescriptor KeepMultiLineInitializerMembersTogether = new(
		id: "ZA0020",
		title: "Keep multi-line initializer members together",
		messageFormat: "Remove the blank line before this initializer member",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires no blank line between two members of an object initializer, a `with` initializer or an anonymous object initializer when either spans more than one line.",
		helpLinkUri: $"{HelpRoot}/ZA0020-Keep-multi-line-initializer-members-together"
	);

	public static readonly DiagnosticDescriptor KeepSingleLineInitializerMembersTogether = new(
		id: "ZA0021",
		title: "Keep single-line initializer members together",
		messageFormat: "Remove the blank line before this initializer member",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires no blank line between two members of an object initializer, a `with` initializer or an anonymous object initializer when both fit on a single line.",
		helpLinkUri: $"{HelpRoot}/ZA0021-Keep-single-line-initializer-members-together"
	);

	public static readonly DiagnosticDescriptor SeparateMultiLineCollectionElements = new(
		id: "ZA0022",
		title: "Separate multi-line collection elements",
		messageFormat: "Add a blank line before this collection element",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires a blank line between adjacent elements of a collection, array or complex element initializer, or of a C# 12 collection expression, unless both elements fit on a single line.",
		helpLinkUri: $"{HelpRoot}/ZA0022-Separate-multi-line-collection-elements"
	);

	public static readonly DiagnosticDescriptor KeepMultiLineCollectionElementsTogether = new(
		id: "ZA0023",
		title: "Keep multi-line collection elements together",
		messageFormat: "Remove the blank line before this collection element",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires adjacent elements of a collection, array or complex element initializer, or of a C# 12 collection expression, to sit directly next to each other when either of them spans more than one line.",
		helpLinkUri: $"{HelpRoot}/ZA0023-Keep-multi-line-collection-elements-together"
	);

	public static readonly DiagnosticDescriptor KeepSingleLineCollectionElementsTogether = new(
		id: "ZA0024",
		title: "Keep single-line collection elements together",
		messageFormat: "Remove the blank line before this collection element",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires adjacent elements of a collection, array or complex element initializer, or of a C# 12 collection expression, that each fit on a single line to sit directly next to each other.",
		helpLinkUri: $"{HelpRoot}/ZA0024-Keep-single-line-collection-elements-together"
	);

	public static readonly DiagnosticDescriptor SeparateMultiLineArguments = new(
		id: "ZA0025",
		title: "Separate multi-line arguments",
		messageFormat: "Add a blank line before this argument",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires a blank line between adjacent arguments unless both arguments fit on a single line.",
		helpLinkUri: $"{HelpRoot}/ZA0025-Separate-multi-line-arguments"
	);

	public static readonly DiagnosticDescriptor KeepMultiLineArgumentsTogether = new(
		id: "ZA0026",
		title: "Keep multi-line arguments together",
		messageFormat: "Remove the blank line before this argument",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires adjacent arguments to sit directly next to each other when either of them spans more than one line.",
		helpLinkUri: $"{HelpRoot}/ZA0026-Keep-multi-line-arguments-together"
	);

	public static readonly DiagnosticDescriptor KeepSingleLineArgumentsTogether = new(
		id: "ZA0027",
		title: "Keep single-line arguments together",
		messageFormat: "Remove the blank line before this argument",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires adjacent arguments that each fit on a single line to sit directly next to each other.",
		helpLinkUri: $"{HelpRoot}/ZA0027-Keep-single-line-arguments-together"
	);

	public static readonly DiagnosticDescriptor SeparateMultiLineParameters = new(
		id: "ZA0028",
		title: "Separate multi-line parameters",
		messageFormat: "Add a blank line before this parameter",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires a blank line between adjacent parameters unless both parameters fit on a single line.",
		helpLinkUri: $"{HelpRoot}/ZA0028-Separate-multi-line-parameters"
	);

	public static readonly DiagnosticDescriptor KeepMultiLineParametersTogether = new(
		id: "ZA0029",
		title: "Keep multi-line parameters together",
		messageFormat: "Remove the blank line before this parameter",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires adjacent parameters to sit directly next to each other when either of them spans more than one line.",
		helpLinkUri: $"{HelpRoot}/ZA0029-Keep-multi-line-parameters-together"
	);

	public static readonly DiagnosticDescriptor KeepSingleLineParametersTogether = new(
		id: "ZA0030",
		title: "Keep single-line parameters together",
		messageFormat: "Remove the blank line before this parameter",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires adjacent parameters that each fit on a single line to sit directly next to each other.",
		helpLinkUri: $"{HelpRoot}/ZA0030-Keep-single-line-parameters-together"
	);

	public static readonly DiagnosticDescriptor SeparateMultiLineDeclarators = new(
		id: "ZA0031",
		title: "Separate multi-line declarators",
		messageFormat: "Add a blank line before this declarator",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires a blank line between adjacent declarators unless both declarators fit on a single line.",
		helpLinkUri: $"{HelpRoot}/ZA0031-Separate-multi-line-declarators"
	);

	public static readonly DiagnosticDescriptor KeepMultiLineDeclaratorsTogether = new(
		id: "ZA0032",
		title: "Keep multi-line declarators together",
		messageFormat: "Remove the blank line before this declarator",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires adjacent declarators to sit directly next to each other when either of them spans more than one line.",
		helpLinkUri: $"{HelpRoot}/ZA0032-Keep-multi-line-declarators-together"
	);

	public static readonly DiagnosticDescriptor KeepSingleLineDeclaratorsTogether = new(
		id: "ZA0033",
		title: "Keep single-line declarators together",
		messageFormat: "Remove the blank line before this declarator",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires adjacent declarators that each fit on a single line to sit directly next to each other.",
		helpLinkUri: $"{HelpRoot}/ZA0033-Keep-single-line-declarators-together"
	);

	public static readonly DiagnosticDescriptor SeparateClauseKeywordFromBraceBeforeIt = new(
		id: "ZA0034",
		title: "Separate a clause keyword from the brace before it",
		messageFormat: "Add a blank line before this clause keyword",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires a blank line between the closing brace of a branch and the clause keyword that continues the same statement, when the keyword stands on its own line.",
		helpLinkUri: $"{HelpRoot}/ZA0034-Separate-a-clause-keyword-from-the-brace-before-it"
	);

	public static readonly DiagnosticDescriptor KeepClauseKeywordsAdjacent = new(
		id: "ZA0035",
		title: "Keep clause keywords adjacent",
		messageFormat: "Remove the blank line before this clause keyword",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: true,
		description: "Requires a clause keyword to sit directly next to the closing brace of the branch before it, with no blank line between the two.",
		helpLinkUri: $"{HelpRoot}/ZA0035-Keep-clause-keywords-adjacent"
	);

	public static readonly DiagnosticDescriptor RemoveBracePadding = new(
		id: "ZA0036",
		title: "Remove brace padding",
		messageFormat: "Remove the blank line next to this brace",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: true,
		description: "Requires that no blank line sits directly next to an opening or closing brace.",
		helpLinkUri: $"{HelpRoot}/ZA0036-Remove-brace-padding"
	);

	public static readonly DiagnosticDescriptor CollapseEmptyBlocks = new(
		id: "ZA0037",
		title: "Collapse empty blocks",
		messageFormat: "Collapse this empty block",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: true,
		description: "Requires a pair of braces with nothing but whitespace between them to be joined onto a single line; a namespace body and a `switch` are not measured.",
		helpLinkUri: $"{HelpRoot}/ZA0037-Collapse-empty-blocks",
		customTags: [WellKnownDiagnosticTags.Unnecessary]
	);

	public static readonly DiagnosticDescriptor UseBracesConsistently = new(
		id: "ZA0038",
		title: "Use braces consistently",
		messageFormat: "Add braces",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: true,
		description: "Requires braces on every branch of a conditional chain, and on the body of a loop, a `using`, a `lock` or a `fixed` statement, once the header or the content of that unit spans multiple lines.",
		helpLinkUri: $"{HelpRoot}/ZA0038-Use-braces-consistently"
	);

	public static readonly DiagnosticDescriptor RemoveRedundantBraces = new(
		id: "ZA0039",
		title: "Remove redundant braces",
		messageFormat: "Remove braces",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires the branches of a conditional chain, and the bodies of loops, `using`, `lock` and `fixed` statements, to drop their braces when every one of them fits on a single line.",
		helpLinkUri: $"{HelpRoot}/ZA0039-Remove-redundant-braces",
		customTags: [WellKnownDiagnosticTags.Unnecessary]
	);

	public static readonly DiagnosticDescriptor UseBlockBodyForMultiLineMember = new(
		id: "ZA0040",
		title: "Use a block body for a multi-line member",
		messageFormat: "Use a block body",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires a method, constructor, destructor, operator, conversion operator or local function whose signature or expression body spans multiple lines to use a block body.",
		helpLinkUri: $"{HelpRoot}/ZA0040-Use-a-block-body-for-a-multi-line-member"
	);

	public static readonly DiagnosticDescriptor UseExpressionBodyForSingleLineMember = new(
		id: "ZA0041",
		title: "Use an expression body for a single-line member",
		messageFormat: "Use an expression body",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires a method, constructor, destructor, operator, conversion operator or local function whose signature and whose single statement each fit within the line width to use an expression body.",
		helpLinkUri: $"{HelpRoot}/ZA0041-Use-an-expression-body-for-a-single-line-member"
	);

	public static readonly DiagnosticDescriptor UseBlockBodyForMultiLineValueMember = new(
		id: "ZA0042",
		title: "Use a block body for a multi-line value member",
		messageFormat: "Use a block body",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires a property, an indexer or an accessor whose signature or expression body spans multiple lines to use a block body.",
		helpLinkUri: $"{HelpRoot}/ZA0042-Use-a-block-body-for-a-multi-line-value-member"
	);

	public static readonly DiagnosticDescriptor UseExpressionBodyForSingleLineValueMember = new(
		id: "ZA0043",
		title: "Use an expression body for a single-line value member",
		messageFormat: "Use an expression body",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires a property, an indexer or an accessor whose signature and whose single statement each fit within the line width to use an expression body.",
		helpLinkUri: $"{HelpRoot}/ZA0043-Use-an-expression-body-for-a-single-line-value-member"
	);

	public static readonly DiagnosticDescriptor BreakExpressionOffSignatureLine = new(
		id: "ZA0044",
		title: "Break the expression off the signature line",
		messageFormat: "Move the expression off the signature line",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: true,
		description: "Requires the expression of an expression body to open a line of its own when the signature and the expression cannot share one within `stanza_max_line_length`.",
		helpLinkUri: $"{HelpRoot}/ZA0044-Break-the-expression-off-the-signature-line"
	);

	public static readonly DiagnosticDescriptor KeepArrowOnSignatureLine = new(
		id: "ZA0045",
		title: "Keep the arrow on the signature line",
		messageFormat: "Move the arrow onto the signature line",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires the arrow of an expression body that opens a line to close the line the signature sits on, unless the expression spans several lines.",
		helpLinkUri: $"{HelpRoot}/ZA0045-Keep-the-arrow-on-the-signature-line"
	);

	public static readonly DiagnosticDescriptor PutArrowOnLineAfterSignature = new(
		id: "ZA0046",
		title: "Put the arrow on the line after the signature",
		messageFormat: "Move the arrow onto the line after the signature",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires the arrow of an expression body to open the line after the signature, indented at least as far as the member, unless the signature, the arrow and the expression share one line or the expression spans several lines.",
		helpLinkUri: $"{HelpRoot}/ZA0046-Put-the-arrow-on-the-line-after-the-signature"
	);

	public static readonly DiagnosticDescriptor SeparateRegionMarkerFromNeighbors = new(
		id: "ZA0047",
		title: "Separate the region marker from its neighbors",
		messageFormat: "Add a blank line next to this region marker",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: true,
		description: "Requires a blank line on each side of a `#region` or `#endregion` directive, except where the line beside it holds a brace or another directive.",
		helpLinkUri: $"{HelpRoot}/ZA0047-Separate-the-region-marker-from-its-neighbors"
	);

	public static readonly DiagnosticDescriptor NameClosingRegionDirective = new(
		id: "ZA0048",
		title: "Name the closing region directive",
		messageFormat: "Write the name of the region in this directive",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires the `#endregion` directive that closes a named region to carry that name after its keyword.",
		helpLinkUri: $"{HelpRoot}/ZA0048-Name-the-closing-region-directive"
	);

	public static readonly DiagnosticDescriptor RemoveEmptyRegions = new(
		id: "ZA0049",
		title: "Remove empty regions",
		messageFormat: "Remove this empty region",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires a region whose markers hold nothing but whitespace - and further empty regions - between them to be removed together with both markers.",
		helpLinkUri: $"{HelpRoot}/ZA0049-Remove-empty-regions",
		customTags: [WellKnownDiagnosticTags.Unnecessary]
	);

	public static readonly DiagnosticDescriptor NameArgumentsOfCall = new(
		id: "ZA0050",
		title: "Name the arguments of a call",
		messageFormat: "Name the arguments of this call",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires each unnamed argument of a call that reaches the `stanza_named_argument_count` threshold to carry its parameter's name, except an identifier or an `out` declaration that already carries it and may stay unnamed under `stanza_named_argument_match`.",
		helpLinkUri: $"{HelpRoot}/ZA0050-Name-the-arguments-of-a-call"
	);

	public static readonly DiagnosticDescriptor RemoveRedundantArgumentName = new(
		id: "ZA0051",
		title: "Remove a redundant argument name",
		messageFormat: "Remove this argument name",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires the name of an argument to be removed when `stanza_named_argument_match` allows the shorthand, the value is only an identifier or an `out` declaration matching the parameter's name, and every argument up to it binds at its own position.",
		helpLinkUri: $"{HelpRoot}/ZA0051-Remove-a-redundant-argument-name"
	);

	// A rule the stanza-markup tool also runs takes its ID from MarkupDiagnostics, so both read one severity line.
	// The tool reports a markup gap under that ID and applies a documentation rule as a clean-up of a reprinted body.

	public static readonly DiagnosticDescriptor SeparateMultiLineMarkupElements = new(
		id: MarkupDiagnostics.SeparateMultiLineMarkupElementsId,
		title: "Separate multi-line markup elements",
		messageFormat: MarkupDiagnostics.AddBlankLineMessage,
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: false,
		description: "Requires a blank line between two neighboring elements of a markup container inside a C# 11 raw string literal when either of them spans multiple lines.",
		helpLinkUri: $"{HelpRoot}/ZA0101-Separate-multi-line-markup-elements"
	);

	public static readonly DiagnosticDescriptor KeepMultiLineMarkupElementsTogether = new(
		id: MarkupDiagnostics.KeepMultiLineMarkupElementsTogetherId,
		title: "Keep multi-line markup elements together",
		messageFormat: MarkupDiagnostics.RemoveBlankLineMessage,
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: false,
		description: "Requires two neighboring elements of a markup container inside a C# 11 raw string literal to stand with no blank line between them when either of the two spans multiple lines.",
		helpLinkUri: $"{HelpRoot}/ZA0102-Keep-multi-line-markup-elements-together"
	);

	public static readonly DiagnosticDescriptor KeepSingleLineMarkupElementsTogether = new(
		id: MarkupDiagnostics.KeepSingleLineMarkupElementsTogetherId,
		title: "Keep single-line markup elements together",
		messageFormat: MarkupDiagnostics.RemoveBlankLineMessage,
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: false,
		description: "Requires two neighboring elements of a markup container inside a C# 11 raw string literal to stand with no blank line between them when each of the two fits on a single line.",
		helpLinkUri: $"{HelpRoot}/ZA0103-Keep-single-line-markup-elements-together"
	);

	public static readonly DiagnosticDescriptor CollapseExtraBlankLinesBetweenMarkupElements = new(
		id: MarkupDiagnostics.CollapseExtraBlankLinesBetweenMarkupElementsId,
		title: "Collapse extra blank lines between markup elements",
		messageFormat: MarkupDiagnostics.RemoveBlankLinesMessage,
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: false,
		description: "Requires the gap between two neighboring elements of a markup container inside a C# 11 raw string literal to keep no more blank lines than `stanza_markup_max_blank_lines` allows.",
		helpLinkUri: $"{HelpRoot}/ZA0104-Collapse-extra-blank-lines-between-markup-elements"
	);

	public static readonly DiagnosticDescriptor SeparateMarkupCommentMarkerFromText = new(
		id: MarkupDiagnostics.SeparateMarkupCommentMarkerFromTextId,
		title: "Separate a markup comment marker from its text",
		messageFormat: MarkupDiagnostics.AddSpaceMessage,
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: false,
		description: "Requires the marker of a comment inside a C# 11 raw string literal to be separated from the text of that comment.",
		helpLinkUri: $"{HelpRoot}/ZA0105-Separate-a-markup-comment-marker-from-its-text"
	);

	public static readonly DiagnosticDescriptor LayOutDocumentationComment = new(
		id: "ZA0201",
		title: "Lay out a documentation comment",
		messageFormat: "Lay out the documentation comment of this member",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires the text of a `///` or `/** */` documentation comment to wrap at the configured line length, and an element to become a block when it holds paragraph content, does not fit on one line, or `stanza_doc_expand` asks for one; `remarks`, `example` and `para` always become one.",
		helpLinkUri: $"{HelpRoot}/ZA0201-Lay-out-a-documentation-comment"
	);

	public static readonly DiagnosticDescriptor MarkDocumentationParagraphs = new(
		id: "ZA0202",
		title: "Mark documentation paragraphs",
		messageFormat: "Mark this paragraph break with a <para> element",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires a paragraph break inside a documentation comment element to be written as a `<para>` element of its own rather than as a blank line of the comment.",
		helpLinkUri: $"{HelpRoot}/ZA0202-Mark-documentation-paragraphs"
	);

	public static readonly DiagnosticDescriptor RemoveDocumentationPadding = new(
		id: MarkupDiagnostics.RemoveDocumentationPaddingId,
		title: "Remove documentation padding",
		messageFormat: "Remove this blank line",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires a blank line that separates nothing inside a documentation comment to be removed.",
		helpLinkUri: $"{HelpRoot}/ZA0203-Remove-documentation-padding",
		customTags: [WellKnownDiagnosticTags.Unnecessary]
	);

	public static readonly DiagnosticDescriptor LayOutItemsOfDocumentationList = new(
		id: "ZA0204",
		title: "Lay out the items of a documentation list",
		messageFormat: "Lay out the items of this documentation list",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires the items of a `<list>` or `<table>` element of a documentation comment to be laid out one per line, with everything an item holds indented past its tag by `stanza_doc_indent_size`.",
		helpLinkUri: $"{HelpRoot}/ZA0204-Lay-out-the-items-of-a-documentation-list"
	);

	public static readonly DiagnosticDescriptor KeepDocumentationCommentWithDeclaration = new(
		id: "ZA0205",
		title: "Keep a documentation comment with its declaration",
		messageFormat: "Remove the blank line before this declaration",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires a documentation comment to stand directly before the declaration it documents.",
		helpLinkUri: $"{HelpRoot}/ZA0205-Keep-a-documentation-comment-with-its-declaration"
	);

	public static readonly DiagnosticDescriptor CollapseWhitespaceInsideTag = new(
		id: "ZA0206",
		title: "Collapse the whitespace inside a tag",
		messageFormat: "Use one space inside this tag",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires a run of two or more spaces or tabs between the tokens of a documentation tag to be a single space.",
		helpLinkUri: $"{HelpRoot}/ZA0206-Collapse-the-whitespace-inside-a-tag"
	);

	public static readonly DiagnosticDescriptor SeparateSlashOfSelfClosingTag = new(
		id: MarkupDiagnostics.SeparateSlashOfSelfClosingTagId,
		title: "Separate the slash of a self-closing tag",
		messageFormat: "Use one space before this slash",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires the slash of a self-closing documentation tag to be separated from the name or attribute before it by exactly one space.",
		helpLinkUri: $"{HelpRoot}/ZA0207-Separate-the-slash-of-a-self-closing-tag"
	);

	public static readonly DiagnosticDescriptor KeepSlashOfSelfClosingTagTight = new(
		id: MarkupDiagnostics.KeepSlashOfSelfClosingTagTightId,
		title: "Keep the slash of a self-closing tag tight",
		messageFormat: "Remove the space before this slash",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires the slash of a self-closing documentation tag to touch the name or attribute before it.",
		helpLinkUri: $"{HelpRoot}/ZA0208-Keep-the-slash-of-a-self-closing-tag-tight"
	);

	public static readonly DiagnosticDescriptor WriteEmptyElementAsSelfClosingTag = new(
		id: MarkupDiagnostics.WriteEmptyElementAsSelfClosingTagId,
		title: "Write an empty element as a self-closing tag",
		messageFormat: "Write this empty element as a self-closing tag",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires an element of a documentation comment that holds nothing to be written as a self-closing tag.",
		helpLinkUri: $"{HelpRoot}/ZA0209-Write-an-empty-element-as-a-self-closing-tag"
	);

	public static readonly DiagnosticDescriptor EndDocumentationTextWithPeriod = new(
		id: MarkupDiagnostics.EndDocumentationTextWithPeriodId,
		title: "End the documentation text with a period",
		messageFormat: "Add a period at the end of this text",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires the text of a documentation comment element to end with a period when its last character is a letter or a digit.",
		helpLinkUri: $"{HelpRoot}/ZA0210-End-the-documentation-text-with-a-period"
	);

	public static readonly DiagnosticDescriptor MarkNullKeyword = new(
		id: MarkupDiagnostics.MarkNullKeywordId,
		title: "Mark the null keyword",
		messageFormat: "Mark this null with a <see langword> element",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires the null keyword in the prose of a documentation comment to be written as a `<see langword=\"null\"/>` element, spaced or tight as the file's self-closing tag rule asks.",
		helpLinkUri: $"{HelpRoot}/ZA0211-Mark-the-null-keyword"
	);

	public static readonly DiagnosticDescriptor OrderElementsOfDocumentationComment = new(
		id: MarkupDiagnostics.OrderElementsOfDocumentationCommentId,
		title: "Order the elements of a documentation comment",
		messageFormat: "Order the elements of this documentation comment",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires the elements of a documentation comment to stand in the canonical order its rule page lists, with the `<typeparam>` elements, then the `<param>` elements, after the `<summary>`, each in the order the declaration lists them.",
		helpLinkUri: $"{HelpRoot}/ZA0212-Order-the-elements-of-a-documentation-comment"
	);

	public static readonly DiagnosticDescriptor OrderItemsOfDocumentationList = new(
		id: MarkupDiagnostics.OrderItemsOfDocumentationListId,
		title: "Order the items of a documentation list",
		messageFormat: "Order the items of this documentation list",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires the header of a `<list>` or `<table>` element of a documentation comment to stand before its items, and the term to stand before the description it belongs to.",
		helpLinkUri: $"{HelpRoot}/ZA0213-Order-the-items-of-a-documentation-list"
	);

	public static readonly DiagnosticDescriptor SeparateSingleLineCommentMarkerFromText = new(
		id: "ZA0214",
		title: "Separate a single-line comment marker from its text",
		messageFormat: "Add one space after the slashes of this comment",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires the marker that opens a single-line comment to be separated from the text after it by at least one whitespace character.",
		helpLinkUri: $"{HelpRoot}/ZA0214-Separate-a-single-line-comment-marker-from-its-text"
	);

	public static readonly DiagnosticDescriptor SeparateDelimitedCommentMarkerFromText = new(
		id: "ZA0215",
		title: "Separate a delimited comment marker from its text",
		messageFormat: "Add one space between this comment marker and the text",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires the text of a `/* */` delimited comment to be separated from the `/*` and the `*/` that bound it by at least one whitespace character.",
		helpLinkUri: $"{HelpRoot}/ZA0215-Separate-a-delimited-comment-marker-from-its-text"
	);

	public static readonly DiagnosticDescriptor SeparateTrailingCommentFromCode = new(
		id: "ZA0216",
		title: "Separate a trailing comment from the code",
		messageFormat: "Add one space before this comment",
		category: "Formatting",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		description: "Requires a comment that trails code on the same line to be separated from that code by at least one whitespace character.",
		helpLinkUri: $"{HelpRoot}/ZA0216-Separate-a-trailing-comment-from-the-code"
	);
}
