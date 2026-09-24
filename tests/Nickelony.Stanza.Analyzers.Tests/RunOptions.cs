using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>The analyzers, fixes, settings and checks of one harness run.</summary>
internal sealed record RunOptions(ImmutableArray<DiagnosticAnalyzer> Analyzers)
{
	public ImmutableArray<CodeFixProvider> Providers { get; init; } =
		RoslynTestHarness.AllCodeFixProviders;

	/// <summary>The compilation's own severities, as a build's NoWarn list sets them.</summary>
	public IReadOnlyDictionary<string, ReportDiagnostic>? DiagnosticOptions { get; init; }

	/// <summary>The keys the analyzers read in place of the project's config files.</summary>
	public IReadOnlyDictionary<string, string>? AnalyzerConfigOptions { get; init; }

	/// <summary>The severities an <c>.editorconfig</c> beside the snippet sets.</summary>
	public IReadOnlyDictionary<string, ReportDiagnostic>? TreeOptions { get; init; }

	/// <summary>The severities a <c>.globalconfig</c> sets for every tree.</summary>
	public IReadOnlyDictionary<string, ReportDiagnostic>? GlobalOptions { get; init; }

	/// <summary>Whether an iterative run fails on a snippet that does not compile.</summary>
	public bool RequireCompiles { get; init; } = true;

	/// <summary>Whether the run fails when its fixes leave a diagnostic standing.</summary>
	public bool AssertFixedPoint { get; init; } = true;
}
