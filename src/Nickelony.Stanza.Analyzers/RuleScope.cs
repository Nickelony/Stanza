using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers;

/// <summary>What a rule reads its severity from: the compilation options, the tree and a cancellation token.</summary>
internal readonly record struct RuleScope(CompilationOptions CompilationOptions, SyntaxTree Tree, CancellationToken CancellationToken)
{
	public static implicit operator RuleScope(SyntaxNodeAnalysisContext context) =>
		new(context.Compilation.Options, context.Node.SyntaxTree, context.CancellationToken);
}
