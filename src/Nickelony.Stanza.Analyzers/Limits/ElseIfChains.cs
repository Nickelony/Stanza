using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nickelony.Stanza.Analyzers.Limits;

/// <summary>Finds the <c>if</c> chains that hold more branches than a limit allows.</summary>
internal static class ElseIfChains
{
	/// <summary>Collects the first <c>if</c> of each chain that holds more branches than the limit.</summary>
	/// <remarks>
	/// The closing <c>else</c> is not a branch, and an inner <c>if</c> starts a chain of its own.
	/// </remarks>
	public static ImmutableArray<IfStatementSyntax> CollectLongChains(SyntaxNode root, int limit, CancellationToken cancellationToken)
	{
		ImmutableArray<IfStatementSyntax>.Builder longChains = ImmutableArray.CreateBuilder<IfStatementSyntax>();

		foreach (SyntaxNode node in root.DescendantNodes())
		{
			cancellationToken.ThrowIfCancellationRequested();

			// An else if belongs to the chain above it, so only the first if of a chain walks it.
			if (node is not IfStatementSyntax head || head.Parent is ElseClauseSyntax)
				continue;

			int branches = 1;
			IfStatementSyntax current = head;

			while (current.Else?.Statement is IfStatementSyntax next)
			{
				branches++;
				current = next;
			}

			if (branches > limit)
				longChains.Add(head);
		}

		return longChains.ToImmutable();
	}
}
