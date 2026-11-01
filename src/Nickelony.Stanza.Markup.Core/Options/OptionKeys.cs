using System;
using System.Collections.Generic;

namespace Nickelony.Stanza.Markup.Core.Options;

/// <summary>The <c>.editorconfig</c> keys the analyzers and the tools read, held once.</summary>
/// <remarks>
/// Every <c>stanza_</c> key is in <see cref="Known"/>, which a guard checks against the option pages.
/// </remarks>
internal static class OptionKeys
{
	public const string StanzaPrefix = "stanza_";
	public const string SeverityPrefix = "dotnet_diagnostic.";
	public const string SeveritySuffix = ".severity";

	public const string MarkupEnabled = "stanza_markup_enabled";
	public const string MarkupProfile = "stanza_markup_profile";
	public const string MarkupScope = "stanza_markup_scope";
	public const string MarkupMaxBlankLines = "stanza_markup_max_blank_lines";
	public const string MarkupDocReprint = "stanza_markup_doc_reprint";
	public const string MarkupSeparatedElements = "stanza_markup_separated_elements";
	public const string MarkupLiterals = "stanza_markup_literals";

	public const string MaxLineLength = "stanza_max_line_length";
	public const string DocMaxLineLength = "stanza_doc_max_line_length";
	public const string DocBlockElements = "stanza_doc_block_elements";
	public const string DocMinTailWords = "stanza_doc_min_tail_words";
	public const string DocMinLineWords = "stanza_doc_min_line_words";
	public const string DocMaxOverflowColumns = "stanza_doc_max_overflow_columns";
	public const string DocWeakLineEndWords = "stanza_doc_weak_line_end_words";
	public const string DocIndentSize = "stanza_doc_indent_size";
	public const string DocTextIndent = "stanza_doc_text_indent";
	public const string DocLangwords = "stanza_doc_langwords";

	public const string AssertionRoots = "stanza_assertion_roots";
	public const string GuardRoots = "stanza_guard_roots";
	public const string AssignmentTargets = "stanza_assignment_targets";
	public const string CommentBannerMinRun = "stanza_comment_banner_min_run";
	public const string MaxBlankLines = "stanza_max_blank_lines";
	public const string MaxElseIfBranches = "stanza_max_else_if_branches";
	public const string MaxLogicalOperators = "stanza_max_logical_operators";
	public const string MaxLooseRunSize = "stanza_max_loose_run_size";
	public const string MaxMemberStatements = "stanza_max_member_statements";
	public const string MaxNestingDepth = "stanza_max_nesting_depth";
	public const string MaxParameters = "stanza_max_parameters";
	public const string MaxTernaryOperators = "stanza_max_ternary_operators";
	public const string MinLinesToNameEndif = "stanza_min_lines_to_name_endif";
	public const string MultiLineMemberPart = "stanza_multi_line_member_part";
	public const string NamedArgumentCase = "stanza_named_argument_case";
	public const string NamedArgumentLiterals = "stanza_named_argument_literals";
	public const string NamedArgumentMatch = "stanza_named_argument_match";
	public const string NamedArgumentMinArguments = "stanza_named_argument_min_arguments";

	public const string StandardMaxLineLength = "max_line_length";
	public const string TabWidth = "tab_width";
	public const string IndentSize = "indent_size";

	/// <summary>Every <c>stanza_</c> key a reader names, so a key outside it is read by nothing.</summary>
	public static IReadOnlyCollection<string> Known => s_known;

	private static readonly HashSet<string> s_known = new(StringComparer.Ordinal)
	{
		MarkupEnabled,
		MarkupProfile,
		MarkupScope,
		MarkupMaxBlankLines,
		MarkupDocReprint,
		MarkupSeparatedElements,
		MarkupLiterals,
		MaxLineLength,
		DocMaxLineLength,
		DocBlockElements,
		DocMinTailWords,
		DocMinLineWords,
		DocMaxOverflowColumns,
		DocWeakLineEndWords,
		DocIndentSize,
		DocTextIndent,
		DocLangwords,
		AssertionRoots,
		GuardRoots,
		AssignmentTargets,
		CommentBannerMinRun,
		MaxBlankLines,
		MaxElseIfBranches,
		MaxLogicalOperators,
		MaxLooseRunSize,
		MaxMemberStatements,
		MaxNestingDepth,
		MaxParameters,
		MaxTernaryOperators,
		MinLinesToNameEndif,
		MultiLineMemberPart,
		NamedArgumentCase,
		NamedArgumentLiterals,
		NamedArgumentMatch,
		NamedArgumentMinArguments,
	};
}
