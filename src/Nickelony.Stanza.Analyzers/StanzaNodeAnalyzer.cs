using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers;

/// <summary>Base of an analyzer that reports one rule on the node kinds it names.</summary>
internal abstract class StanzaNodeAnalyzer(DiagnosticDescriptor descriptor, ImmutableArray<SyntaxKind> kinds) : StanzaAnalyzer(descriptor)
{
	protected sealed override void AddActions(AnalysisContext context) => context.RegisterSyntaxNodeAction(Analyze, kinds);
	protected abstract void Analyze(SyntaxNodeAnalysisContext context);
}
