using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nickelony.Stanza.Analyzers.Limits;

/// <summary>Finds the conditional expressions that hold more <c>?</c> operators than a limit allows.</summary>
internal static class TernaryChains
{
	/// <summary>Collects the outermost conditional of each chain that holds more operators than the limit.</summary>
	/// <remarks>
	/// A lambda or an anonymous method starts a chain of its own.
	/// </remarks>
	public static ImmutableArray<ConditionalExpressionSyntax> CollectLongChains(SyntaxNode root, int limit, CancellationToken cancellationToken)
	{
		List<Chain> chains = [];
		Stack<(SyntaxNode Node, Chain? Chain)> pending = new();

		pending.Push((root, null));

		// The walk keeps its own stack, so a deep chain cannot overflow the call stack, and it visits each node once.
		while (pending.Count > 0)
		{
			cancellationToken.ThrowIfCancellationRequested();

			(SyntaxNode node, Chain? chain) = pending.Pop();

			if (node is AnonymousFunctionExpressionSyntax)
			{
				chain = null;
			}
			else if (node is ConditionalExpressionSyntax conditional)
			{
				if (chain is null)
				{
					chain = new Chain(conditional);
					chains.Add(chain);
				}

				chain.Count++;
			}

			foreach (SyntaxNode child in node.ChildNodes())
				pending.Push((child, chain));
		}

		ImmutableArray<ConditionalExpressionSyntax>.Builder longChains = ImmutableArray.CreateBuilder<ConditionalExpressionSyntax>();

		foreach (Chain chain in chains)
		{
			if (chain.Count > limit)
				longChains.Add(chain.Root);
		}

		return longChains.ToImmutable();
	}

	private sealed class Chain(ConditionalExpressionSyntax root)
	{
		public ConditionalExpressionSyntax Root { get; } = root;
		public int Count { get; set; }
	}
}
