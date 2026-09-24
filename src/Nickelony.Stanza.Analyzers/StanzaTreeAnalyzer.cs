using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers;

/// <summary>Base of an analyzer that judges a whole file from its syntax alone.</summary>
internal abstract class StanzaTreeAnalyzer(DiagnosticDescriptor descriptor) : StanzaAnalyzer(descriptor)
{
	protected sealed override void AddActions(AnalysisContext context) => context.RegisterSyntaxTreeAction(Analyze);
	protected abstract void Analyze(SyntaxTreeAnalysisContext context);
}
