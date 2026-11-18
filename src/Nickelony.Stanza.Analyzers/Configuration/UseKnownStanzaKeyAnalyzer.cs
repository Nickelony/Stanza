using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Nickelony.Stanza.Markup.Core.Options;

namespace Nickelony.Stanza.Analyzers.Configuration;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class UseKnownStanzaKeyAnalyzer() : StanzaAnalyzer(DiagnosticDescriptors.UseKnownStanzaKey)
{
	private readonly record struct Reach(SyntaxTree Tree, bool IsHidden);

	protected override void AddActions(AnalysisContext context) => context.RegisterCompilationStartAction(Start);

	private void Start(CompilationStartAnalysisContext start)
	{
		AnalyzerConfigOptionsProvider provider = start.Options.AnalyzerConfigOptionsProvider;

		// An analyzer cannot point at a configuration file, and a diagnostic with no location ignores the severity lines of an .editorconfig.
		// So a key is reported once, at the first token of a file it reaches, which keeps those lines and skips generated files.
		// A file whose pragma hides the report comes last, then the path orders the files, so the report does not move with the analysis order.
		ConcurrentDictionary<string, Reach> reaches = new(StringComparer.Ordinal);

		start.RegisterSyntaxTreeAction(tree =>
		{
			Reach reach = new(tree.Tree, HidesReport(tree.Tree, tree.CancellationToken));

			foreach (string key in FindUnknownKeys(provider.GetOptions(tree.Tree)))
				_ = reaches.AddOrUpdate(key, reach, (_, held) => IsBefore(reach, held) ? reach : held);
		});

		start.RegisterCompilationEndAction(end =>
		{
			foreach (string key in FindUnknownKeys(provider.GlobalOptions).Where(key => !reaches.ContainsKey(key)))
				Report(end, key, Location.None);

			foreach (KeyValuePair<string, Reach> pair in reaches.OrderBy(pair => pair.Key, StringComparer.Ordinal))
				Report(end, pair.Key, Location.Create(pair.Value.Tree, pair.Value.Tree.GetRoot(end.CancellationToken).GetFirstToken().Span));
		});
	}

	private void Report(CompilationAnalysisContext context, string key, Location location)
	{
		string? nearest = OptionKeySuggestions.FindNearest(key);
		string suggestion = nearest is null ? string.Empty : $"; the nearest known key is '{nearest}'";

		context.ReportDiagnostic(Diagnostic.Create(Descriptor, location, key, suggestion));
	}

	private static bool IsBefore(Reach reach, Reach other)
	{
		return reach.IsHidden != other.IsHidden
			? !reach.IsHidden
			: string.CompareOrdinal(reach.Tree.FilePath, other.Tree.FilePath) < 0;
	}

	/// <summary>Says whether a pragma before the first token disables the rule, by its ID or with none.</summary>
	private bool HidesReport(SyntaxTree syntaxTree, CancellationToken cancellationToken)
	{
		bool isHidden = false;

		foreach (SyntaxTrivia trivia in syntaxTree.GetRoot(cancellationToken).GetFirstToken().LeadingTrivia)
		{
			if (trivia.GetStructure() is PragmaWarningDirectiveTriviaSyntax pragma && NamesRule(pragma))
				isHidden = pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword);
		}

		return isHidden;
	}

	private bool NamesRule(PragmaWarningDirectiveTriviaSyntax pragma) =>
		pragma.ErrorCodes.Count == 0 || pragma.ErrorCodes.Any(code => code.ToString().Trim() == Descriptor.Id);

	/// <summary>Lists each <c>stanza_</c> key of the options that no reader names, in lower case.</summary>
	private static IEnumerable<string> FindUnknownKeys(AnalyzerConfigOptions options)
	{
		IEnumerable<string> keys;

		try
		{
			keys = options.Keys.ToList();
		}
		catch (NotImplementedException)
		{
			// A host whose options cannot list their keys leaves the rule nothing to judge.
			return [];
		}

		return keys
			.Select(key => key.ToLowerInvariant())
			.Where(key => key.StartsWith(OptionKeys.StanzaPrefix, StringComparison.Ordinal) && !OptionKeys.Known.Contains(key));
	}
}
