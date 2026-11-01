using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nickelony.Stanza.Analyzers.Arguments;

/// <summary>Tells whether the member a call binds to is declared under a conditional directive.</summary>
internal static class ArgumentDeclarations
{
	/// <summary>Whether another view may declare the member with other parameter names or order.</summary>
	/// <remarks>
	/// A conditional block that holds the declaration or the parameter list counts, and one in the body does not.
	/// </remarks>
	public static bool IsUnderAConditional(ISymbol member, CancellationToken cancellationToken)
	{
		ISymbol declared = member is IMethodSymbol { ReducedFrom: { } unreduced } ? unreduced : member;

		foreach (SyntaxReference reference in declared.OriginalDefinition.DeclaringSyntaxReferences)
		{
			ConditionalBlockTree blocks = GetBlocks(reference.SyntaxTree, cancellationToken);

			if (blocks.IsEmpty)
				continue;

			SyntaxNode declaration = reference.GetSyntax(cancellationToken);
			BaseParameterListSyntax? parameterList = declaration.ChildNodes().OfType<BaseParameterListSyntax>().FirstOrDefault();

			if (blocks.IsAround(declaration.Span, parameterList?.Span))
				return true;
		}

		return false;
	}

	private static ConditionalBlockTree GetBlocks(SyntaxTree syntaxTree, CancellationToken cancellationToken)
	{
		return s_blocks.GetOrCompute(
			syntaxTree,
			state: cancellationToken,
			static (tree, token) => ConditionalBlockTree.Read(StanzaCommentCache.GetConditionalBlockMarkers(tree, token))
		);
	}

	/// <summary>Pairs the markers of a tree once, so a lookup searches blocks and reads no marker before it.</summary>
	private static readonly WeakCache<SyntaxTree, ConditionalBlockTree> s_blocks = new();
}
