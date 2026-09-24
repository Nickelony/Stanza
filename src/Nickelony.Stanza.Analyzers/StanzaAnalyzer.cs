using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers;

/// <summary>Base of every analyzer: it holds the descriptor and turns generated code off.</summary>
internal abstract class StanzaAnalyzer(DiagnosticDescriptor descriptor) : DiagnosticAnalyzer
{
	private readonly ImmutableArray<DiagnosticDescriptor> _supportedDiagnostics = [descriptor];

	protected DiagnosticDescriptor Descriptor { get; } = descriptor;

	public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => _supportedDiagnostics;

	public sealed override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		AddActions(context);
	}

	protected abstract void AddActions(AnalysisContext context);
}
