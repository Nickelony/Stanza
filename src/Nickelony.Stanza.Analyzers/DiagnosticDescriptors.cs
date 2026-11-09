using Microsoft.CodeAnalysis;
using Nickelony.Stanza.Markup.Core;

namespace Nickelony.Stanza.Analyzers;

/// <summary>The descriptor of every Stanza rule, which holds its ID, title, message and default severity.</summary>
internal static class DiagnosticDescriptors
{
	private const string HelpRoot = "https://github.com/Nickelony/Stanza/wiki";

	public static readonly DiagnosticDescriptor SeparateMultiLineConstructs = Describe(
		id: "ZA0001",
		title: "Separate multi-line constructs",
		messageFormat: "Add a blank line before this construct",
		description: "Requires a blank line between adjacent siblings when either sibling spans multiple lines.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor SeparateDeclarationRuns = Describe(
		id: "ZA0002",
		title: "Separate declaration runs",
		messageFormat: "Add a blank line between declaration runs",
		description: "Requires blank lines around runs of two or more neighboring declarations of one kind.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor SeparateSameTypeDeclarationRuns = Describe(
		id: "ZA0003",
		title: "Separate same-type declaration runs",
		messageFormat: "Add a blank line between same-type declaration runs",
		description: "Requires a blank line around runs of two or more declarations that share the exact same type.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor SeparateInvocationRuns = Describe(
		id: "ZA0004",
		title: "Separate invocation runs",
		messageFormat: "Add a blank line between invocation runs",
		description: "Requires blank lines around runs of two or more neighboring calls of one method.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor SeparateAssertionRuns = Describe(
		id: "ZA0005",
		title: "Separate assertion runs",
		messageFormat: "Add a blank line between assertion runs",
		description: "Requires blank lines around runs of two or more neighboring assertions that share a root named by `stanza_assertion_roots`.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor SeparateAwaitRuns = Describe(
		id: "ZA0006",
		title: "Separate await runs",
		messageFormat: "Add a blank line between await runs",
		description: "Requires blank lines around runs of two or more neighboring await statements.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor SeparateEventSubscriptionRuns = Describe(
		id: "ZA0007",
		title: "Separate event subscription runs",
		messageFormat: "Add a blank line between event subscription runs",
		description: "Requires blank lines around runs of two or more neighboring event subscriptions and unsubscriptions.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor SeparateAssignmentRuns = Describe(
		id: "ZA0008",
		title: "Separate assignment runs",
		messageFormat: "Add a blank line between assignment runs",
		description: "Requires blank lines around runs of two or more neighboring assignments, compound assignments and increments.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor SeparateJumpStatement = Describe(
		id: "ZA0009",
		title: "Separate a jump statement",
		messageFormat: "Add a blank line above this jump statement",
		description: "Requires a blank line above a `return`, `break`, `continue`, `throw`, `goto` or `yield break` statement when the statements directly above it, with no blank line between them, span two or more lines.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor SeparateBaseCall = Describe(
		id: "ZA0010",
		title: "Separate the base call",
		messageFormat: "Add a blank line beside the base call",
		description: "Requires a blank line below a call to the base implementation that opens a member body, and above one that closes it.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor SeparateGuardClauses = Describe(
		id: "ZA0011",
		title: "Separate the guard clauses",
		messageFormat: "Add a blank line below the guard clauses",
		description: "Requires a blank line below the guard clauses that open a block: an `if` with no `else` whose body is a lone `return`, `throw`, `continue` or `break`, and a call of a throw helper.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor SeparateCommentedSibling = Describe(
		id: "ZA0012",
		title: "Separate a commented sibling",
		messageFormat: "Add a blank line above the comment of this sibling",
		description: "Requires a blank line above the comment that stands directly above a single-line sibling, so the comment travels with the sibling it describes.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor SeparateMemberGroups = Describe(
		id: "ZA0013",
		title: "Separate member groups",
		messageFormat: "Add a blank line between member groups",
		description: "Requires blank lines around runs of two or more neighboring members of the same kind.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor SeparateModifierGroups = Describe(
		id: "ZA0014",
		title: "Separate modifier groups",
		messageFormat: "Add a blank line between modifier groups",
		description: "Requires blank lines around runs of two or more neighboring members of one kind that share their access, static and readonly modifiers.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor SeparateImportSection = Describe(
		id: "ZA0015",
		title: "Separate the import section",
		messageFormat: "Add a blank line after the import section",
		description: "Requires a blank line between the import section of a file or namespace and the first member that follows it.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor SeparateImportDirectiveGroups = Describe(
		id: "ZA0016",
		title: "Separate the import directive groups",
		messageFormat: "Add a blank line between the import directive groups",
		description: "Requires a blank line between two adjacent import directives that are not of the same kind.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor SeparateFileScopedNamespaceDeclarationFromFirstMember = Describe(
		id: "ZA0017",
		title: "Separate a file-scoped namespace declaration from its first member",
		messageFormat: "Add a blank line after the file-scoped namespace declaration",
		description: "Requires a blank line between a file-scoped namespace declaration and the first member the declaration holds, unless an import section follows the declaration.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor KeepTwoUnitsOfContainerTogether = Describe(
		id: "ZA0051",
		title: "Keep the two units of a container together",
		messageFormat: "Remove the blank line before this statement or member",
		description: "Requires a container whose entire content is two single-line units to hold them next to each other, without a blank line between them.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor GatherRunOfLooseStatements = Describe(
		id: "ZA0052",
		title: "Gather a run of loose statements",
		messageFormat: "Remove the blank line before this statement of a loose run",
		description: "Requires the single-line statements of a block, a switch section or the top level of a file to stand together when more than `stanza_max_loose_run_size` of them are each set apart by a blank line that no other rule asks for.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor GatherRunOfLooseMembers = Describe(
		id: "ZA0053",
		title: "Gather a run of loose members",
		messageFormat: "Remove the blank line before this member of a loose run",
		description: "Requires the single-line members of a type, an enum, a namespace or a file to stand together when more than `stanza_max_loose_run_size` of them are each set apart by a blank line that no other rule asks for.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor KeepRunOfStatementsTogether = Describe(
		id: "ZA0054",
		title: "Keep a run of statements together",
		messageFormat: "Remove the blank line before this statement of a run",
		description: "Requires the single-line statements that the enabled separating rules read as one run to stand together, and writes the blank lines those rules then ask for around the run.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor KeepRunOfMembersTogether = Describe(
		id: "ZA0055",
		title: "Keep a run of members together",
		messageFormat: "Remove the blank line before this member of a run",
		description: "Requires the single-line members that the enabled member group rules read as one run to stand together, and writes the blank lines those rules then ask for around the run.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor CollapseExtraBlankLines = Describe(
		id: "ZA0056",
		title: "Collapse extra blank lines",
		messageFormat: "Remove the extra blank lines here",
		description: "Requires a run of blank lines in C# code to keep no more lines than `stanza_max_blank_lines` allows.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor SeparateMultiLineAccessors = Describe(
		id: "ZA0101",
		title: "Separate multi-line accessors",
		messageFormat: "Add a blank line before this accessor",
		description: "Requires a blank line between adjacent accessors unless both accessors fit on a single line.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor KeepMultiLineAccessorsTogether = Describe(
		id: "ZA0102",
		title: "Keep multi-line accessors together",
		messageFormat: "Remove the blank line before this accessor",
		description: "Requires adjacent accessors to sit directly next to each other when either of them spans more than one line.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor KeepSingleLineAccessorsTogether = Describe(
		id: "ZA0103",
		title: "Keep single-line accessors together",
		messageFormat: "Remove the blank line before this accessor",
		description: "Requires adjacent accessors that each fit on a single line to sit directly next to each other.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor SeparateMultiLineSwitchSections = Describe(
		id: "ZA0111",
		title: "Separate multi-line switch sections",
		messageFormat: "Add a blank line before this switch section",
		description: "Requires a blank line between adjacent switch sections unless both sections fit on a single line.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor KeepMultiLineSwitchSectionsTogether = Describe(
		id: "ZA0112",
		title: "Keep multi-line switch sections together",
		messageFormat: "Remove the blank line before this switch section",
		description: "Requires adjacent switch sections to sit directly next to each other when either of them spans more than one line.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor KeepSingleLineSwitchSectionsTogether = Describe(
		id: "ZA0113",
		title: "Keep single-line switch sections together",
		messageFormat: "Remove the blank line before this switch section",
		description: "Requires adjacent switch sections that each fit on a single line to sit directly next to each other.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor SeparateMultiLineSwitchExpressionArms = Describe(
		id: "ZA0121",
		title: "Separate multi-line switch expression arms",
		messageFormat: "Add a blank line before this switch expression arm",
		description: "Requires a blank line between adjacent switch expression arms unless both arms fit on a single line.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor KeepMultiLineSwitchExpressionArmsTogether = Describe(
		id: "ZA0122",
		title: "Keep multi-line switch expression arms together",
		messageFormat: "Remove the blank line before this switch expression arm",
		description: "Requires adjacent switch expression arms to sit directly next to each other when either of them spans more than one line.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor KeepSingleLineSwitchExpressionArmsTogether = Describe(
		id: "ZA0123",
		title: "Keep single-line switch expression arms together",
		messageFormat: "Remove the blank line before this switch expression arm",
		description: "Requires adjacent switch expression arms that each fit on a single line to sit directly next to each other.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor SeparateMultiLineInitializerMembers = Describe(
		id: "ZA0131",
		title: "Separate multi-line initializer members",
		messageFormat: "Add a blank line before this initializer member",
		description: "Requires a blank line between two members of an object initializer, a `with` initializer or an anonymous object initializer when either spans more than one line.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor KeepMultiLineInitializerMembersTogether = Describe(
		id: "ZA0132",
		title: "Keep multi-line initializer members together",
		messageFormat: "Remove the blank line before this initializer member",
		description: "Requires no blank line between two members of an object initializer, a `with` initializer or an anonymous object initializer when either spans more than one line.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor KeepSingleLineInitializerMembersTogether = Describe(
		id: "ZA0133",
		title: "Keep single-line initializer members together",
		messageFormat: "Remove the blank line before this initializer member",
		description: "Requires no blank line between two members of an object initializer, a `with` initializer or an anonymous object initializer when both fit on a single line.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor SeparateMultiLineCollectionElements = Describe(
		id: "ZA0141",
		title: "Separate multi-line collection elements",
		messageFormat: "Add a blank line before this collection element",
		description: "Requires a blank line between adjacent elements of a collection, array or complex element initializer, or of a C# 12 collection expression, unless both elements fit on a single line.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor KeepMultiLineCollectionElementsTogether = Describe(
		id: "ZA0142",
		title: "Keep multi-line collection elements together",
		messageFormat: "Remove the blank line before this collection element",
		description: "Requires adjacent elements of a collection, array or complex element initializer, or of a C# 12 collection expression, to sit directly next to each other when either of them spans more than one line.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor KeepSingleLineCollectionElementsTogether = Describe(
		id: "ZA0143",
		title: "Keep single-line collection elements together",
		messageFormat: "Remove the blank line before this collection element",
		description: "Requires adjacent elements of a collection, array or complex element initializer, or of a C# 12 collection expression, that each fit on a single line to sit directly next to each other.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor SeparateMultiLineArguments = Describe(
		id: "ZA0151",
		title: "Separate multi-line arguments",
		messageFormat: "Add a blank line before this argument",
		description: "Requires a blank line between adjacent arguments unless both arguments fit on a single line.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor KeepMultiLineArgumentsTogether = Describe(
		id: "ZA0152",
		title: "Keep multi-line arguments together",
		messageFormat: "Remove the blank line before this argument",
		description: "Requires adjacent arguments to sit directly next to each other when either of them spans more than one line.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor KeepSingleLineArgumentsTogether = Describe(
		id: "ZA0153",
		title: "Keep single-line arguments together",
		messageFormat: "Remove the blank line before this argument",
		description: "Requires adjacent arguments that each fit on a single line to sit directly next to each other.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor SeparateMultiLineParameters = Describe(
		id: "ZA0161",
		title: "Separate multi-line parameters",
		messageFormat: "Add a blank line before this parameter",
		description: "Requires a blank line between adjacent parameters unless both parameters fit on a single line.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor KeepMultiLineParametersTogether = Describe(
		id: "ZA0162",
		title: "Keep multi-line parameters together",
		messageFormat: "Remove the blank line before this parameter",
		description: "Requires adjacent parameters to sit directly next to each other when either of them spans more than one line.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor KeepSingleLineParametersTogether = Describe(
		id: "ZA0163",
		title: "Keep single-line parameters together",
		messageFormat: "Remove the blank line before this parameter",
		description: "Requires adjacent parameters that each fit on a single line to sit directly next to each other.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor SeparateMultiLineDeclarators = Describe(
		id: "ZA0171",
		title: "Separate multi-line declarators",
		messageFormat: "Add a blank line before this declarator",
		description: "Requires a blank line between adjacent declarators unless both declarators fit on a single line.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor KeepMultiLineDeclaratorsTogether = Describe(
		id: "ZA0172",
		title: "Keep multi-line declarators together",
		messageFormat: "Remove the blank line before this declarator",
		description: "Requires adjacent declarators to sit directly next to each other when either of them spans more than one line.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor KeepSingleLineDeclaratorsTogether = Describe(
		id: "ZA0173",
		title: "Keep single-line declarators together",
		messageFormat: "Remove the blank line before this declarator",
		description: "Requires adjacent declarators that each fit on a single line to sit directly next to each other.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor AddTrailingComma = Describe(
		id: "ZA0198",
		title: "Add a trailing comma",
		messageFormat: "Add a trailing comma after the last element",
		description: "Requires the last element of an initializer, enum, collection expression, switch expression, property pattern or list pattern whose closing token stands on a line of its own to end in a comma.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor RemoveTrailingComma = Describe(
		id: "ZA0199",
		title: "Remove the trailing comma",
		messageFormat: "Remove the trailing comma after the last element",
		description: "Requires the last element of an initializer, enum, collection expression, switch expression, property pattern or list pattern whose closing token stands on a line of its own to end without a comma.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor SeparateClauseKeywordFromBraceBeforeIt = Describe(
		id: "ZA0201",
		title: "Separate a clause keyword from the brace before it",
		messageFormat: "Add a blank line before this clause keyword",
		description: "Requires a blank line between the closing brace of a branch and the clause keyword that continues the same statement, when the keyword stands on its own line.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor KeepClauseKeywordsAdjacent = Describe(
		id: "ZA0202",
		title: "Keep clause keywords adjacent",
		messageFormat: "Remove the blank line before this clause keyword",
		description: "Requires a clause keyword to sit directly next to the closing brace of the branch before it, with no blank line between the two.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor RemoveBracePadding = Describe(
		id: "ZA0203",
		title: "Remove brace padding",
		messageFormat: "Remove the blank line next to this brace",
		description: "Requires that no blank line sits directly next to an opening or closing brace.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor CollapseEmptyBlocks = Describe(
		id: "ZA0204",
		title: "Collapse empty blocks",
		messageFormat: "Collapse this empty block",
		description: "Requires a pair of braces with nothing but whitespace between them to be joined onto a single line; a namespace body and a `switch` are not measured.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true,
		customTags: [WellKnownDiagnosticTags.Unnecessary]
	);

	public static readonly DiagnosticDescriptor UseBracesConsistently = Describe(
		id: "ZA0205",
		title: "Use braces consistently",
		messageFormat: "Add braces",
		description: "Requires braces on every branch of a conditional chain, and on the body of a loop, a `using`, a `lock` or a `fixed` statement, once the header or the content of that unit spans multiple lines.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor RemoveRedundantBraces = Describe(
		id: "ZA0206",
		title: "Remove redundant braces",
		messageFormat: "Remove braces",
		description: "Requires the branches of a conditional chain, and the bodies of loops, `using`, `lock` and `fixed` statements, to drop their braces when every one of them fits on a single line.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		customTags: [WellKnownDiagnosticTags.Unnecessary]
	);

	public static readonly DiagnosticDescriptor UseBlockBodyForMultiLineMember = Describe(
		id: "ZA0301",
		title: "Use a block body for a multi-line member",
		messageFormat: "Use a block body",
		description: "Requires a method, constructor, destructor, operator, conversion operator or local function whose signature or expression body spans multiple lines to use a block body.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor UseExpressionBodyForSingleLineMember = Describe(
		id: "ZA0302",
		title: "Use an expression body for a single-line member",
		messageFormat: "Use an expression body",
		description: "Requires a method, constructor, destructor, operator, conversion operator or local function whose signature and whose single statement each fit within the line width to use an expression body.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor UseBlockBodyForMultiLineValueMember = Describe(
		id: "ZA0303",
		title: "Use a block body for a multi-line value member",
		messageFormat: "Use a block body",
		description: "Requires a property, an indexer or an accessor whose signature or expression body spans multiple lines to use a block body.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor UseExpressionBodyForSingleLineValueMember = Describe(
		id: "ZA0304",
		title: "Use an expression body for a single-line value member",
		messageFormat: "Use an expression body",
		description: "Requires a property, an indexer or an accessor whose signature and whose single statement each fit within the line width to use an expression body.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor BreakExpressionOffSignatureLine = Describe(
		id: "ZA0305",
		title: "Break the expression off the signature line",
		messageFormat: "Move the expression off the signature line",
		description: "Requires the expression of an expression body to open a line of its own when the signature and the expression cannot share one within `stanza_max_line_length`.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor KeepArrowOnSignatureLine = Describe(
		id: "ZA0306",
		title: "Keep the arrow on the signature line",
		messageFormat: "Move the arrow onto the signature line",
		description: "Requires the arrow of an expression body that opens a line to close the line the signature sits on, unless the expression spans several lines.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor PutArrowOnLineAfterSignature = Describe(
		id: "ZA0307",
		title: "Put the arrow on the line after the signature",
		messageFormat: "Move the arrow onto the line after the signature",
		description: "Requires the arrow of an expression body to open the line after the signature, indented at least as far as the member, unless the signature, the arrow and the expression share one line or the expression spans several lines.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor SeparateRegionMarkerFromNeighbors = Describe(
		id: "ZA0401",
		title: "Separate the region marker from its neighbors",
		messageFormat: "Add a blank line next to this region marker",
		description: "Requires a blank line on each side of a `#region` or `#endregion` directive, except where the line beside it holds a brace of the block that holds it, a switch label above it or another directive.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor SeparateConditionalBlockFromNeighbors = Describe(
		id: "ZA0402",
		title: "Separate the conditional block from its neighbors",
		messageFormat: "Add a blank line next to this conditional directive",
		description: "Requires a blank line above an `#if` directive and below its `#endif` directive, except where the line beside it holds a brace of the block that holds it, a switch label above it or another directive.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor NameClosingRegionDirective = Describe(
		id: "ZA0403",
		title: "Name the closing region directive",
		messageFormat: "Write the name of the region in this directive",
		description: "Requires the `#endregion` directive that closes a named region to carry that name after its keyword.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor NameClosingConditionalDirective = Describe(
		id: "ZA0404",
		title: "Name the closing conditional directive",
		messageFormat: "Write the condition of the block in this directive",
		description: "Requires the `#endif` directive that closes a conditional block of `stanza_min_lines_to_name_endif` lines or more to carry the condition of its `#if` in a comment.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor RemoveEmptyRegions = Describe(
		id: "ZA0405",
		title: "Remove empty regions",
		messageFormat: "Remove this empty region",
		description: "Requires a region whose markers hold nothing but whitespace - and further empty regions - between them to be removed together with both markers.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true,
		customTags: [WellKnownDiagnosticTags.Unnecessary]
	);

	public static readonly DiagnosticDescriptor NameArgumentsOfCall = Describe(
		id: "ZA0501",
		title: "Name the arguments of a call",
		messageFormat: "Name the arguments of this call",
		description: "Requires each unnamed argument of a call that reaches the `stanza_named_argument_min_arguments` threshold to carry its parameter's name, except an identifier or an `out` declaration that already carries it and may stay unnamed under `stanza_named_argument_match`.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor NameLiteralArgumentsOfCall = Describe(
		id: "ZA0502",
		title: "Name the literal arguments of a call",
		messageFormat: "Name the literal arguments of this call",
		description: "Requires a `true`, `false`, `null` or `default` argument of a call that passes two or more arguments to carry its parameter's name, as `stanza_named_argument_literals` picks the words.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor RemoveRedundantArgumentName = Describe(
		id: "ZA0503",
		title: "Remove a redundant argument name",
		messageFormat: "Remove this argument name",
		description: "Requires the name of an argument to be removed when `stanza_named_argument_match` allows the shorthand, the value is only an identifier or an `out` declaration matching the parameter's name, and every argument up to it binds at its own position.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor RemoveElseAfterJump = Describe(
		id: "ZA0601",
		title: "Remove the else after a jump",
		messageFormat: "Remove this else, as the branch before it ends in a jump",
		description: "Requires the statements of an `else` to follow their `if` when the branch of the `if` ends in a `return`, `throw`, `break`, `continue`, `goto` or `yield break`.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor MergeNestedIfStatements = Describe(
		id: "ZA0602",
		title: "Merge nested if statements",
		messageFormat: "Merge the if statement inside this one into its condition",
		description: "Requires an `if` with no `else` whose only statement is another `if` with no `else` to test both conditions with `&&`, while the result stays within `stanza_max_logical_operators`.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor UsePatternForNullCheck = Describe(
		id: "ZA0651",
		title: "Use a pattern for a null check",
		messageFormat: "Write this null check as a pattern",
		description: "Requires a comparison with `null` through `==` or `!=` to read `is null` or `is not null` wherever the pattern means the same.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor UseEqualityOperatorForNullCheck = Describe(
		id: "ZA0652",
		title: "Use an equality operator for a null check",
		messageFormat: "Write this null check with an equality operator",
		description: "Requires an `is null`, `is not null`, `is { }` or `is not { }` pattern to read `== null` or `!= null` wherever the operator means the same.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor LayOutDocumentationComment = Describe(
		id: "ZA0901",
		title: "Lay out a documentation comment",
		messageFormat: "Lay out the documentation comment of this member",
		description: "Requires the text of a `///` or `/** */` documentation comment to wrap at the configured line length, and an element to become a block when it holds paragraph content, does not fit on one line, or `stanza_doc_block_elements` asks for one; `remarks`, `example` and `para` always become one.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor MarkDocumentationParagraphs = Describe(
		id: "ZA0902",
		title: "Mark documentation paragraphs",
		messageFormat: "Mark this paragraph break with a <para> element",
		description: "Requires a paragraph break inside a documentation comment element to be written as a `<para>` element of its own rather than as a blank line of the comment.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	// A rule the stanza-markup tool also runs takes its ID from MarkupDiagnostics, so both read one severity line.
	// The tool reports a markup gap under that ID and applies a documentation rule as a clean-up of a reprinted body.

	public static readonly DiagnosticDescriptor RemoveDocumentationPadding = Describe(
		id: MarkupDiagnostics.RemoveDocumentationPaddingId,
		title: "Remove documentation padding",
		messageFormat: "Remove this blank line",
		description: "Requires a blank line that separates nothing inside a documentation comment to be removed.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false,
		customTags: [WellKnownDiagnosticTags.Unnecessary]
	);

	public static readonly DiagnosticDescriptor LayOutItemsOfDocumentationList = Describe(
		id: "ZA0904",
		title: "Lay out the items of a documentation list",
		messageFormat: "Lay out the items of this documentation list",
		description: "Requires the items of a `<list>` or `<table>` element of a documentation comment to be laid out one per line, with everything an item holds indented past its tag by `stanza_doc_indent_size`.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor KeepDocumentationCommentWithDeclaration = Describe(
		id: "ZA0905",
		title: "Keep a documentation comment with its declaration",
		messageFormat: "Remove the blank line before this declaration",
		description: "Requires a documentation comment to stand directly before the declaration it documents.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor CollapseWhitespaceInsideTag = Describe(
		id: "ZA0906",
		title: "Collapse the whitespace inside a tag",
		messageFormat: "Use one space inside this tag",
		description: "Requires a run of two or more spaces or tabs between the tokens of a documentation tag to be a single space.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor SeparateSlashOfSelfClosingTag = Describe(
		id: MarkupDiagnostics.SeparateSlashOfSelfClosingTagId,
		title: "Separate the slash of a self-closing tag",
		messageFormat: "Use one space before this slash",
		description: "Requires the slash of a self-closing documentation tag to be separated from the name or attribute before it by exactly one space.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor KeepSlashOfSelfClosingTagTight = Describe(
		id: MarkupDiagnostics.KeepSlashOfSelfClosingTagTightId,
		title: "Keep the slash of a self-closing tag tight",
		messageFormat: "Remove the space before this slash",
		description: "Requires the slash of a self-closing documentation tag to touch the name or attribute before it.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor WriteEmptyElementAsSelfClosingTag = Describe(
		id: MarkupDiagnostics.WriteEmptyElementAsSelfClosingTagId,
		title: "Write an empty element as a self-closing tag",
		messageFormat: "Write this empty element as a self-closing tag",
		description: "Requires an element of a documentation comment that holds nothing to be written as a self-closing tag.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor CapitalizeDocumentationText = Describe(
		id: MarkupDiagnostics.CapitalizeDocumentationTextId,
		title: "Capitalize the documentation text",
		messageFormat: "Capitalize the first letter of this text",
		description: "Requires the text of a documentation comment element to start with a capital letter when its first word starts with a lowercase letter and sets no case on purpose.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor EndDocumentationTextWithPeriod = Describe(
		id: MarkupDiagnostics.EndDocumentationTextWithPeriodId,
		title: "End the documentation text with a period",
		messageFormat: "Add a period at the end of this text",
		description: "Requires the text of a documentation comment element to end with a period when its last character is a letter or a digit.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor MarkLanguageKeywords = Describe(
		id: MarkupDiagnostics.MarkLanguageKeywordsId,
		title: "Mark the language keywords",
		messageFormat: "Mark this keyword with a <see langword> element",
		description: "Requires a language keyword that `stanza_doc_langwords` lists, `null` by default, in the prose of a documentation comment to be written as a `<see langword>` element, spaced or tight as the file's self-closing tag rule asks.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor OrderElementsOfDocumentationComment = Describe(
		id: MarkupDiagnostics.OrderElementsOfDocumentationCommentId,
		title: "Order the elements of a documentation comment",
		messageFormat: "Order the elements of this documentation comment",
		description: "Requires the elements of a documentation comment to stand in the canonical order its rule page lists, with the `<typeparam>` elements, then the `<param>` elements, after the `<summary>`, each in the order the declaration lists them.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor OrderItemsOfDocumentationList = Describe(
		id: MarkupDiagnostics.OrderItemsOfDocumentationListId,
		title: "Order the items of a documentation list",
		messageFormat: "Order the items of this documentation list",
		description: "Requires the header of a `<list>` or `<table>` element of a documentation comment to stand before its items, and the term to stand before the description it belongs to.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor SeparateSingleLineCommentMarkerFromText = Describe(
		id: "ZA0951",
		title: "Separate a single-line comment marker from its text",
		messageFormat: "Add one space after the slashes of this comment",
		description: "Requires the marker that opens a single-line comment to be separated from the text after it by at least one whitespace character, except in a banner of one repeated punctuation mark.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor SeparateDelimitedCommentMarkerFromText = Describe(
		id: "ZA0952",
		title: "Separate a delimited comment marker from its text",
		messageFormat: "Add one space between this comment marker and the text",
		description: "Requires the text of a `/* */` delimited comment to be separated from the `/*` and the `*/` that bound it by at least one whitespace character.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor SeparateTrailingCommentFromCode = Describe(
		id: "ZA0953",
		title: "Separate a trailing comment from the code",
		messageFormat: "Add one space before this comment",
		description: "Requires a comment that trails code on the same line to be separated from that code by at least one whitespace character.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor ShortenLongLine = Describe(
		id: "ZA1001",
		title: "Shorten a long line",
		messageFormat: "Break or shorten this line to fit the maximum line length",
		description: "Requires a line to stay within `stanza_max_line_length` columns; a documentation comment line, and a line whose overflow starts inside a string literal or a URL in a comment, are left out.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor ShortenLongMember = Describe(
		id: "ZA1002",
		title: "Shorten a long member",
		messageFormat: "Split this member into smaller methods",
		description: "Requires the body of a method, constructor, accessor or local function to hold no more statements than `stanza_max_member_statements`, counting the statements nested in it.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor ShortenLongParameterList = Describe(
		id: "ZA1003",
		title: "Shorten a long parameter list",
		messageFormat: "Group these parameters into a type or split the method",
		description: "Requires a method, local function or delegate to take no more parameters than `stanza_max_parameters`; a constructor, an override, an explicit interface implementation and an extern method are left out.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor FlattenDeeplyNestedCode = Describe(
		id: "ZA1004",
		title: "Flatten deeply nested code",
		messageFormat: "Flatten this code with an early return or an extracted method",
		description: "Requires control flow to nest no deeper than `stanza_max_nesting_depth`, counting each `if`, loop, `switch`, `try`, `lock` and `using` around a statement.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor ShortenLongCondition = Describe(
		id: "ZA1005",
		title: "Shorten a long condition",
		messageFormat: "Move parts of this condition into named locals or a method",
		description: "Requires a logical expression to hold no more `&&` and `||` operators than `stanza_max_logical_operators`, counting every operator in its operands.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor BreakUpLongElseIfChain = Describe(
		id: "ZA1006",
		title: "Break up a long else-if chain",
		messageFormat: "Break up this else-if chain, such as into a switch or a table",
		description: "Requires an `if` chain to hold no more `if` and `else if` branches than `stanza_max_else_if_branches`.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor BreakUpLongTernaryChain = Describe(
		id: "ZA1007",
		title: "Break up a long ternary chain",
		messageFormat: "Break up this ternary chain into an if statement or a switch expression",
		description: "Requires a ternary chain to hold no more `?` operators than `stanza_max_ternary_operators`, counting every ternary nested in its branches and condition.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor SeparateMultiLineMarkupElements = Describe(
		id: MarkupDiagnostics.SeparateMultiLineMarkupElementsId,
		title: "Separate multi-line markup elements",
		messageFormat: MarkupDiagnostics.AddBlankLineMessage,
		description: "Requires a blank line between two neighboring elements of a markup container inside a C# 11 raw string literal when either of them spans multiple lines.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor KeepMultiLineMarkupElementsTogether = Describe(
		id: MarkupDiagnostics.KeepMultiLineMarkupElementsTogetherId,
		title: "Keep multi-line markup elements together",
		messageFormat: MarkupDiagnostics.RemoveBlankLineMessage,
		description: "Requires two neighboring elements of a markup container inside a C# 11 raw string literal to stand with no blank line between them when either of the two spans multiple lines.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor KeepSingleLineMarkupElementsTogether = Describe(
		id: MarkupDiagnostics.KeepSingleLineMarkupElementsTogetherId,
		title: "Keep single-line markup elements together",
		messageFormat: MarkupDiagnostics.RemoveBlankLineMessage,
		description: "Requires two neighboring elements of a markup container inside a C# 11 raw string literal to stand with no blank line between them when each of the two fits on a single line.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor SeparateCommentedMarkupElement = Describe(
		id: MarkupDiagnostics.SeparateCommentedMarkupElementId,
		title: "Separate a commented markup element",
		messageFormat: MarkupDiagnostics.AddBlankLineAboveCommentMessage,
		description: "Requires a blank line above the comment that stands directly above a single-line element of a markup container inside a C# 11 raw string literal, so the comment travels with the element it describes.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor RemoveMarkupElementPadding = Describe(
		id: MarkupDiagnostics.RemoveMarkupElementPaddingId,
		title: "Remove markup element padding",
		messageFormat: MarkupDiagnostics.RemovePaddingMessage,
		description: "Requires an element of a markup container inside a C# 11 raw string literal to hold no blank line between its start tag and its first item, or between its last item and its end tag.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor CollapseExtraBlankLinesBetweenMarkupElements = Describe(
		id: MarkupDiagnostics.CollapseExtraBlankLinesBetweenMarkupElementsId,
		title: "Collapse extra blank lines between markup elements",
		messageFormat: MarkupDiagnostics.RemoveBlankLinesMessage,
		description: "Requires the gap between two neighboring elements of a markup container inside a C# 11 raw string literal to keep no more blank lines than `stanza_markup_max_blank_lines` allows.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor SeparateMarkupCommentMarkerFromText = Describe(
		id: MarkupDiagnostics.SeparateMarkupCommentMarkerFromTextId,
		title: "Separate a markup comment marker from its text",
		messageFormat: MarkupDiagnostics.AddSpaceMessage,
		description: "Requires the marker of a comment inside a C# 11 raw string literal to be separated from the text of that comment.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor SeparateSlashOfSelfClosingMarkupElement = Describe(
		id: MarkupDiagnostics.SeparateSlashOfSelfClosingMarkupElementId,
		title: "Separate the slash of a self-closing markup element",
		messageFormat: MarkupDiagnostics.AddSlashSpaceMessage,
		description: "Requires the slash of a self-closing element inside a C# 11 raw string literal to be separated from the name or attribute before it by exactly one space.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor KeepSlashOfSelfClosingMarkupElementTight = Describe(
		id: MarkupDiagnostics.KeepSlashOfSelfClosingMarkupElementTightId,
		title: "Keep the slash of a self-closing markup element tight",
		messageFormat: MarkupDiagnostics.RemoveSlashSpaceMessage,
		description: "Requires the slash of a self-closing element inside a C# 11 raw string literal to touch the name or attribute before it.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	public static readonly DiagnosticDescriptor WriteEmptyMarkupElementAsSelfClosingTag = Describe(
		id: MarkupDiagnostics.WriteEmptyMarkupElementAsSelfClosingTagId,
		title: "Write an empty markup element as a self-closing tag",
		messageFormat: MarkupDiagnostics.WriteSelfClosingTagMessage,
		description: "Requires an element of a C# 11 raw string literal that holds nothing between its start tag and its end tag to be written as a self-closing tag.",
		severity: DiagnosticSeverity.Info,
		isEnabledByDefault: false
	);

	/// <summary>Describes a formatting rule, whose help page is its ID and its title with hyphens for spaces.</summary>
	private static DiagnosticDescriptor Describe(
		string id,
		string title,
		string messageFormat,
		string description,
		DiagnosticSeverity severity,
		bool isEnabledByDefault,
		params string[] customTags
	)
	{
		return new DiagnosticDescriptor(
			id,
			title,
			messageFormat,
			category: "Formatting",
			defaultSeverity: severity,
			isEnabledByDefault,
			description,
			helpLinkUri: $"{HelpRoot}/{id}-{title.Replace(' ', '-')}",
			customTags
		);
	}
}
