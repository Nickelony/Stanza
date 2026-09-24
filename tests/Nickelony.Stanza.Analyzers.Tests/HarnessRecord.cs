using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>Records the rules that each test's harness runs drive.</summary>
internal static class HarnessRecord
{
	private static readonly AsyncLocal<RecordedTest?> s_current = new();
	private static readonly ConcurrentQueue<RecordedRun> s_runs = new();
	private static readonly ConcurrentDictionary<RecordedTest, byte> s_started = new();

	public static IReadOnlyCollection<RecordedRun> Runs => s_runs;

	public static ICollection<RecordedTest> Started => s_started.Keys;

	/// <summary>Starts a test's record.</summary>
	public static void Begin(RecordedTest test)
	{
		s_current.Value = test;
		s_started.TryAdd(test, 0);
	}

	// A run drives the rules its options enable, whether or not they reported.
	public static void Add(
		RunOptions options,
		ImmutableArray<Diagnostic> diagnostics,
		AnalyzerRunResult? result = null,
		string? source = null
	)
	{
		if (s_current.Value is not { } test)
			return;

		ImmutableHashSet<string> reported = [.. diagnostics.Select(diagnostic => diagnostic.Id)];

		ImmutableHashSet<string> rules =
		[
			.. options
				.Analyzers.SelectMany(analyzer => analyzer.SupportedDiagnostics)
				.Where(descriptor => IsEnabled(options, descriptor))
				.Select(descriptor => descriptor.Id),
		];

		result?.Track(source);

		s_runs.Enqueue(
			new RecordedRun(Test: test, Rules: rules, Reported: reported, Result: result)
		);
	}

	// The compilation's own severities beat a config file's, and a tree's beat the global config's.
	// A key of the rule's own beats the key of its category, which beats the bulk key.
	// A bulk key reaches only a rule that ships on, so it can switch such a rule off and never one that ships off on.
	private static bool IsEnabled(RunOptions options, DiagnosticDescriptor descriptor)
	{
		ReportDiagnostic? severity =
			Named(options.DiagnosticOptions, descriptor.Id)
			?? Read(options, descriptor.Id, $"dotnet_diagnostic.{descriptor.Id}.severity");

		if (severity is null && descriptor.IsEnabledByDefault)
		{
			string categoryKey =
				$"dotnet_analyzer_diagnostic.category-{descriptor.Category}.severity";

			severity = Read(options, categoryKey, categoryKey) ?? Read(options, BulkKey, BulkKey);
		}

		return severity is { } named
			? named != ReportDiagnostic.Suppress
			: descriptor.IsEnabledByDefault;
	}

	private const string BulkKey = "dotnet_analyzer_diagnostic.severity";

	// The keys of the config file beside the snippet stand with its severities, and the global config's come after.
	private static ReportDiagnostic? Read(RunOptions options, string treeKey, string fileKey)
	{
		return Named(options.TreeOptions, treeKey)
			?? ParseSeverity(options.AnalyzerConfigOptions, fileKey)
			?? Named(options.GlobalOptions, treeKey);
	}

	private static ReportDiagnostic? ParseSeverity(
		IReadOnlyDictionary<string, string>? keys,
		string key
	)
	{
		if (keys?.TryGetValue(key, out string? text) != true)
			return null;

		return text switch
		{
			"none" => ReportDiagnostic.Suppress,
			"silent" => ReportDiagnostic.Hidden,
			"suggestion" => ReportDiagnostic.Info,
			"warning" => ReportDiagnostic.Warn,
			"error" => ReportDiagnostic.Error,
			_ => null,
		};
	}

	private static ReportDiagnostic? Named(
		IReadOnlyDictionary<string, ReportDiagnostic>? severities,
		string id
	)
	{
		return
			severities?.TryGetValue(id, out ReportDiagnostic severity) == true
			&& severity != ReportDiagnostic.Default
			? severity
			: null;
	}
}

/// <summary>A test by the simple name of its class and the name of its method.</summary>
internal sealed record RecordedTest(string Class, string Method);

internal sealed record RecordedRun(
	RecordedTest Test,
	ImmutableHashSet<string> Rules,
	ImmutableHashSet<string> Reported,
	AnalyzerRunResult? Result
)
{
	// A run that hands back only diagnostics is read by the test that asked for them.
	public bool WasRead => Result?.WasRead ?? true;
}
