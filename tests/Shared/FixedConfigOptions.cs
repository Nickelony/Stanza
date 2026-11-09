using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Tests.Shared;

/// <summary>Analyzer config options that answer from a fixed set of keys.</summary>
internal sealed class FixedConfigOptions(IReadOnlyDictionary<string, string> values)
	: AnalyzerConfigOptions
{
	/// <summary>Options with no key set, so every setting reads its default.</summary>
	internal static FixedConfigOptions Empty { get; } = new(new Dictionary<string, string>());

	public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
		values.TryGetValue(key, out value);
}

/// <summary>An options provider that gives every tree and file the same options.</summary>
internal sealed class FixedConfigOptionsProvider(AnalyzerConfigOptions options)
	: AnalyzerConfigOptionsProvider
{
	public override AnalyzerConfigOptions GlobalOptions => options;

	public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => options;
	public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => options;
}
