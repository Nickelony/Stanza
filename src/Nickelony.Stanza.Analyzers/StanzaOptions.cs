using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Nickelony.Stanza.Markup.Core.DocComments;
using Nickelony.Stanza.Markup.Core.Options;
using Nickelony.Stanza.Markup.Core.Xml;

namespace Nickelony.Stanza.Analyzers;

/// <summary>The options a rule reads from the analyzer configuration of a file.</summary>
/// <remarks>
/// The constructor takes one parameter for each option, since the one job is to hold them together.
/// </remarks>
internal sealed record StanzaOptions(
	ImmutableHashSet<string> AssertionRoots,
	MarkupOptions Markup,
	bool? PreferBraces,
	bool PreserveSingleLineBlocks,
	int MaxLineLength,
	int TabColumns,
	int IndentStepColumns,
	MultiLineTrigger MultiLineTrigger,
	int NamedArgumentCount,
	bool AllowsNamedArgumentShorthand,
	StringComparison NamedArgumentComparison,
	int MaxLooseStatements,
	DocLayoutOptions? DocLayout,
	ImmutableHashSet<string> RulesOffUnlessEnabled
)
{
	private const int DefaultMaxLineLength = MarkupOptionReader.DefaultMaxLineLength;
	private const int DefaultNamedArgumentCount = 4;
	private const int DefaultMaxLooseStatements = 3;

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
		var assertionRoots = new HashSet<string>(StringComparer.Ordinal) { "Assert", "Expect", "Should" };

		if (ReadOptionValue(options, "stanza_assertion_roots") is { } configuredRoots)
		{
			// Only a value listing at least one root replaces the defaults,
			// so a stray `stanza_assertion_roots =` cannot quietly disable the assertion grouping.
			string[] roots =
			[
				.. configuredRoots.Split([','], StringSplitOptions.RemoveEmptyEntries).Select(root => root.Trim()).Where(root => root.Length > 0),
			];

			if (roots.Length > 0)
			{
				assertionRoots.Clear();
				assertionRoots.UnionWith(roots);
			}
		}

		// One reader for the file's keys: it drops the severity suffix a style option may carry.
		// The documentation layout reads through it too,
		// so the rules and the layout resolve the same numbers from the same keys.
		string? ReadKey(string key) => ReadOptionValue(options, key);

		// A scope other than `containers` reads as `all`.
		bool containersOnly = MarkupOptionReader.Names(ReadKey(MarkupKeys.Scope), "containers");

		int maxBlankLines = MarkupOptionReader.ReadCount(ReadKey, MarkupKeys.MaxBlankLines, MarkupOptions.DefaultMaxBlankLines);

		return new StanzaOptions(
			AssertionRoots: assertionRoots.ToImmutableHashSet(StringComparer.Ordinal),
			Markup: new MarkupOptions(new MarkupPolicy(containersOnly), maxBlankLines),
			PreferBraces: ReadPreferBraces(options),
			PreserveSingleLineBlocks: ReadPreserveSingleLineBlocks(options),
			MaxLineLength: MarkupOptionReader.ReadCount(ReadKey, MarkupKeys.MaxLineLength, DefaultMaxLineLength),
			TabColumns: MarkupOptionReader.ResolveTabColumns(ReadKey),
			IndentStepColumns: ReadIndentStepColumns(ReadKey),
			MultiLineTrigger: ReadMultiLineBodyTrigger(options),
			NamedArgumentCount: MarkupOptionReader.ReadCount(ReadKey, "stanza_named_argument_count", DefaultNamedArgumentCount),
			AllowsNamedArgumentShorthand: ReadNamedArgumentShorthand(options),
			NamedArgumentComparison: ReadNamedArgumentComparison(options),
			MaxLooseStatements: MarkupOptionReader.ReadCount(ReadKey, "stanza_max_loose_statements", DefaultMaxLooseStatements),
			DocLayout: MarkupOptionReader.ResolveDocLayout(ReadKey),
			RulesOffUnlessEnabled: ReadRulesOffUnlessEnabled(options)
		);
	}

	private static ImmutableHashSet<string> ReadRulesOffUnlessEnabled(AnalyzerConfigOptions options)
	{
		ImmutableHashSet<string>.Builder off = ImmutableHashSet.CreateBuilder(StringComparer.Ordinal);

		// Each rule whose state another rule reads has an entry, so the entries are the rules this set answers for.
		foreach (StanzaRule rule in StanzaRules.All)
		{
			if (IsOffUnlessEnabled(options, rule.Descriptor))
				off.Add(rule.Descriptor.Id);
		}

		return off.ToImmutable();
	}

	/// <summary>Whether the bulk keys leave the rule off unless its own key sets it.</summary>
	/// <remarks>
	/// Roslyn applies them only to a rule that ships on, and <see cref="StanzaRuleState.IsRuleOff"/> reads its own key.
	/// </remarks>
	private static bool IsOffUnlessEnabled(AnalyzerConfigOptions options, DiagnosticDescriptor descriptor) =>
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

	private static bool? ReadPreferBraces(AnalyzerConfigOptions options)
	{
		string? value = ReadOptionValue(options, "csharp_prefer_braces");

		if (MarkupOptionReader.Names(value, "true"))
			return true;

		if (MarkupOptionReader.Names(value, "false"))
			return false;

		return null;
	}

	private static bool ReadPreserveSingleLineBlocks(AnalyzerConfigOptions options) =>
		!MarkupOptionReader.Names(ReadOptionValue(options, "csharp_preserve_single_line_blocks"), "false");

	/// <summary>
	/// Reads <c>indent_size</c> before <c>tab_width</c>, unlike <see cref="MarkupOptionReader.ResolveTabColumns"/>.
	/// </summary>
	private static int ReadIndentStepColumns(Func<string, string?> read)
	{
		int indentSize = MarkupOptionReader.ReadCount(read, MarkupKeys.IndentSize, 0);

		if (indentSize > 0)
			return indentSize;

		int tabWidth = MarkupOptionReader.ReadCount(read, MarkupKeys.TabWidth, 0);
		return tabWidth > 0 ? tabWidth : StanzaText.DefaultIndentStepColumns;
	}

	private static MultiLineTrigger ReadMultiLineBodyTrigger(AnalyzerConfigOptions options)
	{
		string? value = ReadOptionValue(options, "stanza_multiline_body_trigger");

		if (MarkupOptionReader.Names(value, "body"))
			return MultiLineTrigger.Body;

		if (MarkupOptionReader.Names(value, "signature"))
			return MultiLineTrigger.Signature;

		if (MarkupOptionReader.Names(value, "both"))
			return MultiLineTrigger.Both;

		return MultiLineTrigger.Either;
	}

	private static bool ReadNamedArgumentShorthand(AnalyzerConfigOptions options) =>
		!MarkupOptionReader.Names(ReadOptionValue(options, "stanza_named_argument_match"), "require");

	private static StringComparison ReadNamedArgumentComparison(AnalyzerConfigOptions options)
	{
		return MarkupOptionReader.Names(ReadOptionValue(options, "stanza_named_argument_case"), "insensitive")
			? StringComparison.OrdinalIgnoreCase
			: StringComparison.Ordinal;
	}

	/// <summary>Parses the options once per tree, taking an unconfigured Tomb-Editor from 10.0 s to 4.5 s.</summary>
	private static readonly WeakCache<AnalyzerConfigOptionsProvider, WeakCache<SyntaxTree, StanzaOptions>> s_cache = new();
}
