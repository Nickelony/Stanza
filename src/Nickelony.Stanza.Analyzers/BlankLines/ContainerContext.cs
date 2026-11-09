using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>A container a blank-line rule judges: its analysis context, the file's options and its siblings.</summary>
/// <remarks>
/// A struct, so a container read allocates nothing.
/// </remarks>
internal readonly record struct ContainerContext(SyntaxNodeAnalysisContext Context, StanzaOptions Options, ImmutableArray<SyntaxNode> Siblings)
{
	public SyntaxNode Node => Context.Node;
	public SyntaxKind Kind => Context.Node.Kind();
	public CancellationToken CancellationToken => Context.CancellationToken;

	/// <summary>Reads the siblings and the options of the container the context analyzes.</summary>
	public static ContainerContext From(SyntaxNodeAnalysisContext context) =>
		new(context, StanzaOptions.From(context.Options, context.Node.SyntaxTree), StanzaSyntax.ReadSiblings(context.Node));

	public static implicit operator RuleScope(ContainerContext container) => container.Context;
}
