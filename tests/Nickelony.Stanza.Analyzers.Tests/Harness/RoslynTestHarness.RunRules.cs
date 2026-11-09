using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests.Harness;

internal static partial class RoslynTestHarness
{
	/// <summary>Fixes what the run rules find and asserts a fixed point.</summary>
	public static Task<AnalyzerRunResult> RunRunRulesAsync(
		string source,
		IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions = null,
		IReadOnlyDictionary<string, string>? analyzerConfigOptions = null,
		IReadOnlyDictionary<string, ReportDiagnostic>? treeOptions = null,
		IReadOnlyDictionary<string, ReportDiagnostic>? globalOptions = null
	)
	{
		return RunAsync(
			source,

			CreateRunRuleOptions(
				diagnosticOptions,
				analyzerConfigOptions,
				treeOptions,
				globalOptions
			)
		);
	}

	/// <summary>Reports what the run rules find and applies no fix.</summary>
	public static Task<AnalyzerRunResult> ReportRunRulesAsync(
		string source,
		IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions = null,
		IReadOnlyDictionary<string, string>? analyzerConfigOptions = null,
		IReadOnlyDictionary<string, ReportDiagnostic>? treeOptions = null,
		IReadOnlyDictionary<string, ReportDiagnostic>? globalOptions = null
	)
	{
		return ReportAsync(
			source,

			CreateRunRuleOptions(
				diagnosticOptions,
				analyzerConfigOptions,
				treeOptions,
				globalOptions
			)
		);
	}

	private static RunOptions CreateRunRuleOptions(
		IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions,
		IReadOnlyDictionary<string, string>? analyzerConfigOptions,
		IReadOnlyDictionary<string, ReportDiagnostic>? treeOptions,
		IReadOnlyDictionary<string, ReportDiagnostic>? globalOptions
	)
	{
		ImmutableArray<DiagnosticAnalyzer> analyzers =
		[
			new SeparateDeclarationRunsAnalyzer(),
			new SeparateInvocationRunsAnalyzer(),
			new SeparateAssertionRunsAnalyzer(),
			new SeparateAwaitRunsAnalyzer(),
			new SeparateEventSubscriptionRunsAnalyzer(),
			new SeparateAssignmentRunsAnalyzer(),
		];

		return new RunOptions(analyzers)
		{
			DiagnosticOptions = diagnosticOptions,
			AnalyzerConfigOptions = analyzerConfigOptions,
			TreeOptions = treeOptions,
			GlobalOptions = globalOptions,
		};
	}
}
