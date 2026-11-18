using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Nickelony.Stanza.Markup.Core.Options;

namespace Nickelony.Stanza.Analyzers.Configuration;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class UseKnownStanzaKeyAnalyzer() : StanzaAnalyzer(DiagnosticDescriptors.UseKnownStanzaKey)
{
	protected override void AddActions(AnalysisContext context) => context.RegisterCompilationStartAction(Start);

	private void Start(CompilationStartAnalysisContext start)
	{
		AnalyzerConfigOptionsProvider provider = start.Options.AnalyzerConfigOptionsProvider;

		// An analyzer cannot point at a configuration file, and a diagnostic with no location ignores the severity lines of an .editorconfig.
		// So a key is reported once, at the first token of the first file it reaches, which keeps those lines and skips generated files.
		// The path orders the files, so the report does not move with the order the trees are analyzed in.
		ConcurrentDictionary<string, SyntaxTree> firstTrees = new(StringComparer.Ordinal);

		start.RegisterSyntaxTreeAction(tree =>
		{
			foreach (string key in FindUnknownKeys(provider.GetOptions(tree.Tree)))
				_ = firstTrees.AddOrUpdate(key, tree.Tree, (_, held) => string.CompareOrdinal(tree.Tree.FilePath, held.FilePath) < 0 ? tree.Tree : held);
		});

		start.RegisterCompilationEndAction(end =>
		{
			foreach (string key in FindUnknownKeys(provider.GlobalOptions).Where(key => !firstTrees.ContainsKey(key)))
				Report(end, key, Location.None);

			foreach (KeyValuePair<string, SyntaxTree> reach in firstTrees.OrderBy(pair => pair.Key, StringComparer.Ordinal))
				Report(end, reach.Key, Location.Create(reach.Value, reach.Value.GetRoot(end.CancellationToken).GetFirstToken().Span));
		});
	}

	private void Report(CompilationAnalysisContext context, string key, Location location)
	{
		string? nearest = OptionKeySuggestions.FindNearest(key);
		string suggestion = nearest is null ? string.Empty : $"; did you mean '{nearest}'?";

		context.ReportDiagnostic(Diagnostic.Create(Descriptor, location, key, suggestion));
	}

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
