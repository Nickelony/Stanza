using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>An analyzer crash, which a run that tolerates an unsettled result reports.</summary>
internal sealed class AnalyzerCrashException(string message) : AssertFailedException(message);

internal sealed class AnalyzerRunResult(ImmutableArray<Diagnostic> diagnostics, string fixedText)
{
	private bool _tracked;
	private string? _source;

	public ImmutableArray<Diagnostic> Diagnostics => ReadDiagnostics();

	public string FixedText => ReadFixedText();

	/// <summary>The zero-based start line of each diagnostic that has a location.</summary>
	public ImmutableArray<int> DiagnosticLines => GetLines(Diagnostics);

	/// <summary>Whether the test read the result after the harness handed it over.</summary>
	public bool WasRead { get; private set; }

	/// <summary>Whether the test read the fixed text after the harness handed it over.</summary>
	public bool FixedTextWasRead { get; private set; }

	/// <summary>Whether the fixes changed the source the run began with.</summary>
	public bool FixedTheSource => _source is not null && _source != fixedText;

	// The harness compares results before it hands one over, and those reads are not the test's.
	public void Track(string? source = null)
	{
		_tracked = true;
		_source = source;
	}

	private ImmutableArray<Diagnostic> ReadDiagnostics()
	{
		WasRead |= _tracked;
		return diagnostics;
	}

	private string ReadFixedText()
	{
		WasRead |= _tracked;
		FixedTextWasRead |= _tracked;
		return fixedText;
	}

	private static ImmutableArray<int> GetLines(ImmutableArray<Diagnostic> diagnostics)
	{
		return
		[
			.. diagnostics
				.Where(diagnostic => diagnostic.Location.SourceTree is not null)
				.Select(diagnostic => diagnostic.Location.GetLineSpan().StartLinePosition.Line),
		];
	}
}
