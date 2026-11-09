using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Nickelony.Stanza.Markup.Core.DocComments;
using Nickelony.Stanza.Markup.Core.Options;

namespace Nickelony.Stanza.Analyzers;

/// <summary>The options a rule reads from the analyzer configuration of a file.</summary>
/// <remarks>
/// The constructor takes one parameter for each option, since the one job is to hold them together.
/// </remarks>
internal sealed record StanzaOptions(
	ImmutableHashSet<string> AssertionRoots,
	ImmutableHashSet<string> GuardRoots,
	MarkupOptions Markup,
	MarkupLiteralReading MarkupLiterals,
	bool? PreferBraces,
	bool PreserveSingleLineBlocks,
	int MaxLineLength,
	int TabColumns,
	int IndentStepColumns,
	MultiLineMemberPart MultiLineMemberPart,
	int NamedArgumentMinArguments,
	bool AllowsNamedArgumentShorthand,
	StringComparison NamedArgumentComparison,
	bool NamesNullAndDefaultLiterals,
	int MaxLooseRunSize,
	int MaxTernaryOperators,
	int MaxLogicalOperators,
	int MaxNestingDepth,
	int MaxMemberStatements,
	int MaxParameters,
	int MaxElseIfBranches,
	DocLayoutOptions? DocLayout,
	int MaxBlankLines,
	int MinBannerRun,
	int MinLinesToNameEndif,
	bool SplitsAssignmentTargets,
	ImmutableHashSet<string> RulesDisabledUnlessEnabled
)
{
	private const int DefaultMinBannerRun = 3;
	private const int DefaultMinLinesToNameEndif = 10;
	private const int DefaultNamedArgumentMinArguments = 4;
	private const int DefaultMaxLooseRunSize = 3;
	private const int DefaultMaxBlankLines = 1;
	private const int DefaultMaxTernaryOperators = 2;
	private const int DefaultMaxLogicalOperators = 4;
	private const int DefaultMaxNestingDepth = 3;
	private const int DefaultMaxMemberStatements = 40;
	private const int DefaultMaxParameters = 7;
	private const int DefaultMaxElseIfBranches = 4;

	/// <summary>Whether a width bounds the code, since a <c>stanza_max_line_length</c> of zero turns it off.</summary>
	public bool HasLineLimit => MaxLineLength > 0;

	/// <summary>Whether a bulk key's severity turns the rules it covers off, which only <c>none</c> does.</summary>
	private static bool IsDisabledSeverity(string severity) => MarkupOptionReader.Names(severity, "none");

	public static StanzaOptions From(AnalyzerOptions analyzerOptions, SyntaxTree syntaxTree)
	{
		AnalyzerConfigOptionsProvider provider = analyzerOptions.AnalyzerConfigOptionsProvider;

		WeakCache<SyntaxTree, StanzaOptions> treeCache = s_cache.GetOrCompute(provider, static _ => new WeakCache<SyntaxTree, StanzaOptions>());

		return treeCache.GetOrCompute(syntaxTree, state: provider, static (tree, provider) => From(provider.GetOptions(tree)));
	}

	public static StanzaOptions From(AnalyzerConfigOptions options)
	{
		// One reader for the file's keys: it drops the severity suffix a style option may carry.
		// The documentation layout reads through it too,
		// so the rules and the layout resolve the same numbers from the same keys.
		string? ReadKey(string key) => ReadOptionValue(options, key);

		return new StanzaOptions(
			AssertionRoots: ReadRoots(options, OptionKeys.AssertionRoots, s_defaultAssertionRoots),
			GuardRoots: ReadRoots(options, OptionKeys.GuardRoots, []),
			// A scope other than `containers` reads as `all`.
			Markup: MarkupOptionReader.ReadOptions(ReadKey, fallbackPolicy: new MarkupPolicy(ContainersOnly: false)),
			MarkupLiterals: OptionValues.MarkupLiterals.Read(ReadKey(OptionKeys.MarkupLiterals), fallback: MarkupLiteralReading.Marked),
			PreferBraces: ReadPreferBraces(options),
			PreserveSingleLineBlocks: ReadPreserveSingleLineBlocks(options),
			MaxLineLength: MarkupOptionReader.ResolveMaxLineLength(ReadKey),
			TabColumns: MarkupOptionReader.ResolveTabColumns(ReadKey),
			IndentStepColumns: ReadIndentStepColumns(ReadKey),
			MultiLineMemberPart: OptionValues.MultiLineMemberPart.Read(ReadKey(OptionKeys.MultiLineMemberPart), fallback: MultiLineMemberPart.Either),
			NamedArgumentMinArguments: MarkupOptionReader.ReadCount(ReadKey, OptionKeys.NamedArgumentMinArguments, DefaultNamedArgumentMinArguments),
			AllowsNamedArgumentShorthand: OptionValues.NamedArgumentMatch.Read(ReadKey(OptionKeys.NamedArgumentMatch), fallback: true),
			NamedArgumentComparison: OptionValues.NamedArgumentCase.Read(ReadKey(OptionKeys.NamedArgumentCase), fallback: StringComparison.Ordinal),
			NamesNullAndDefaultLiterals: OptionValues.NamedArgumentLiterals.Read(ReadKey(OptionKeys.NamedArgumentLiterals), fallback: true),
			MaxLooseRunSize: MarkupOptionReader.ReadCount(ReadKey, OptionKeys.MaxLooseRunSize, DefaultMaxLooseRunSize),
			MaxTernaryOperators: MarkupOptionReader.ReadCount(ReadKey, OptionKeys.MaxTernaryOperators, DefaultMaxTernaryOperators),
			MaxLogicalOperators: MarkupOptionReader.ReadCount(ReadKey, OptionKeys.MaxLogicalOperators, DefaultMaxLogicalOperators),
			MaxNestingDepth: MarkupOptionReader.ReadCount(ReadKey, OptionKeys.MaxNestingDepth, DefaultMaxNestingDepth),
			MaxMemberStatements: MarkupOptionReader.ReadCount(ReadKey, OptionKeys.MaxMemberStatements, DefaultMaxMemberStatements),
			MaxParameters: MarkupOptionReader.ReadCount(ReadKey, OptionKeys.MaxParameters, DefaultMaxParameters),
			MaxElseIfBranches: MarkupOptionReader.ReadCount(ReadKey, OptionKeys.MaxElseIfBranches, DefaultMaxElseIfBranches),
			DocLayout: MarkupOptionReader.ResolveDocLayout(ReadKey),
			MaxBlankLines: MarkupOptionReader.ReadCount(ReadKey, OptionKeys.MaxBlankLines, DefaultMaxBlankLines),
			MinBannerRun: MarkupOptionReader.ReadCount(ReadKey, OptionKeys.CommentBannerMinRun, DefaultMinBannerRun),
			MinLinesToNameEndif: MarkupOptionReader.ReadCount(ReadKey, OptionKeys.MinLinesToNameEndif, DefaultMinLinesToNameEndif),
			SplitsAssignmentTargets: OptionValues.AssignmentTargets.Read(ReadKey(OptionKeys.AssignmentTargets), fallback: false),
			RulesDisabledUnlessEnabled: ReadRulesDisabledUnlessEnabled(options)
		);
	}

	private static ImmutableHashSet<string> ReadRulesDisabledUnlessEnabled(AnalyzerConfigOptions options)
	{
		ImmutableHashSet<string>.Builder disabled = ImmutableHashSet.CreateBuilder(StringComparer.Ordinal);

		// Each rule whose state another rule reads has an entry, so the entries are the rules this set answers for.
		foreach (StanzaRule rule in StanzaRules.All)
		{
			if (IsDisabledUnlessEnabled(options, rule.Descriptor))
				disabled.Add(rule.Descriptor.Id);
		}

		return disabled.ToImmutable();
	}

	/// <summary>Whether the bulk keys leave the rule disabled unless its own key sets it.</summary>
	/// <remarks>
	/// Roslyn applies them only to a rule shipped on; <see cref="StanzaRuleState.IsRuleDisabled"/> reads its own key.
	/// </remarks>
	private static bool IsDisabledUnlessEnabled(AnalyzerConfigOptions options, DiagnosticDescriptor descriptor) =>
		!descriptor.IsEnabledByDefault || (ReadBulkSeverity(options, descriptor) is { } severity && IsDisabledSeverity(severity));

	/// <summary>
	/// Follows Roslyn: the category key when it names a severity, <c>default</c> included, else the analyzer-wide key.
	/// </summary>
	private static string? ReadBulkSeverity(AnalyzerConfigOptions options, DiagnosticDescriptor descriptor)
	{
		string? category = ReadOptionValue(options, $"dotnet_analyzer_diagnostic.category-{descriptor.Category}.severity");

		if (MarkupOptionReader.NamesASeverity(category))
			return category;

		string? all = ReadOptionValue(options, "dotnet_analyzer_diagnostic.severity");
		return MarkupOptionReader.NamesASeverity(all) ? all : null;
	}

	private static string? ReadOptionValue(AnalyzerConfigOptions options, string key) =>
		options.TryGetValue(key, out string? value) ? MarkupOptionReader.StripCommentAndSeverity(key, value) : null;

	/// <summary>Reads the names a comma separated key lists, the fallback when it lists none.</summary>
	/// <remarks>
	/// Only the lowercase <c>none</c> as the whole value lists no name; an empty value lists the fallback.
	/// </remarks>
	private static ImmutableHashSet<string> ReadRoots(AnalyzerConfigOptions options, string key, ImmutableHashSet<string> fallback)
	{
		if (ReadOptionValue(options, key) is not { } value)
			return fallback;

		if (value == "none")
			return [];

		ImmutableHashSet<string> roots =
		[
			.. value.Split([','], StringSplitOptions.RemoveEmptyEntries).Select(static root => root.Trim()).Where(static root => root.Length > 0),
		];

		return roots.IsEmpty ? fallback : roots;
	}

	private static bool? ReadPreferBraces(AnalyzerConfigOptions options) =>
		OptionValues.Boolean.Find(ReadOptionValue(options, "csharp_prefer_braces"));

	private static bool ReadPreserveSingleLineBlocks(AnalyzerConfigOptions options) =>
		OptionValues.Boolean.Read(ReadOptionValue(options, "csharp_preserve_single_line_blocks"), fallback: true);

	/// <summary>
	/// Reads <c>indent_size</c> before <c>tab_width</c>, unlike <see cref="MarkupOptionReader.ResolveTabColumns"/>.
	/// </summary>
	private static int ReadIndentStepColumns(Func<string, string?> read)
	{
		int indentSize = MarkupOptionReader.ReadCount(read, OptionKeys.IndentSize, 0);

		if (indentSize > 0)
			return indentSize;

		int tabWidth = MarkupOptionReader.ReadCount(read, OptionKeys.TabWidth, 0);
		return tabWidth > 0 ? tabWidth : StanzaText.DefaultIndentStepColumns;
	}

	private static readonly ImmutableHashSet<string> s_defaultAssertionRoots = ["Assert", "Expect", "Should"];

	/// <summary>Parses the options once per tree, however many rules and nodes read them.</summary>
	private static readonly WeakCache<AnalyzerConfigOptionsProvider, WeakCache<SyntaxTree, StanzaOptions>> s_cache = new();
}
