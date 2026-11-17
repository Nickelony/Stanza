using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Nickelony.Stanza.Analyzers.Tests.Harness;

internal sealed class AnalyzerRunResult(ImmutableArray<Diagnostic> diagnostics, string fixedText)
{
	public ImmutableArray<Diagnostic> Diagnostics => ReadDiagnostics();
	public string FixedText => ReadFixedText();

	/// <summary>The zero-based start line of each diagnostic that has a location.</summary>
	public ImmutableArray<int> DiagnosticLines => GetLines(Diagnostics);

	/// <summary>What the test read of the result, which the record keeps in its place.</summary>
	public ResultReads Reads { get; } = new();

	// The harness compares results before it hands one over, and those reads are not the test's.
	public void Track(string? source = null) =>
		Reads.Begin(fixedTheSource: source is not null && source != fixedText);

	private ImmutableArray<Diagnostic> ReadDiagnostics()
	{
		Reads.NoteDiagnosticsRead();
		return diagnostics;
	}

	private string ReadFixedText()
	{
		Reads.NoteFixedTextRead();
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
