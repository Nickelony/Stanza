using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers;

/// <summary>Base of an analyzer that judges a whole file with the compilation options its rule reads.</summary>
internal abstract class StanzaCompilationTreeAnalyzer(DiagnosticDescriptor descriptor) : StanzaAnalyzer(descriptor)
{
	protected sealed override void AddActions(AnalysisContext context) =>
		context.RegisterCompilationStartAction(start => start.RegisterSyntaxTreeAction(tree => Analyze(tree, start.Compilation.Options)));

	protected abstract void Analyze(SyntaxTreeAnalysisContext context, CompilationOptions compilationOptions);
}
