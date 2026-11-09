using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Arguments;

/// <summary>The <c>#if</c> blocks of a tree, in the order they open, each with the block that holds it.</summary>
internal sealed class ConditionalBlockTree(int[] starts, int[] ends, int[] parents)
{
	/// <summary>The markers this thread's reads have taken; a test seam for a count without a clock.</summary>
	[ThreadStatic]
	public static long StepsTaken;

	public bool IsEmpty => starts.Length == 0;

	/// <summary>Pairs each <c>#endif</c> with the innermost open <c>#if</c>, as a stack does, in one pass.</summary>
	/// <remarks>
	/// A block no <c>#endif</c> closes runs to the end of the text, and a stray <c>#endif</c> is ignored.
	/// </remarks>
	public static ConditionalBlockTree Read(ImmutableArray<SyntaxTrivia> markers)
	{
		List<int> blockStarts = [];
		List<int> blockEnds = [];
		List<int> blockParents = [];

		// The blocks that are open, by their index.
		Stack<int> open = new();

		foreach (SyntaxTrivia marker in markers)
		{
			StepsTaken++;

			if (marker.GetStructure() is IfDirectiveTriviaSyntax)
			{
				blockParents.Add(open.Count > 0 ? open.Peek() : -1);
				blockStarts.Add(marker.SpanStart);
				blockEnds.Add(int.MaxValue);

				open.Push(blockStarts.Count - 1);
			}
			else if (open.Count > 0)
			{
				blockEnds[open.Pop()] = marker.Span.End;
			}
		}

		return new ConditionalBlockTree([.. blockStarts], [.. blockEnds], [.. blockParents]);
	}

	/// <summary>Whether a block holds the declaration or overlaps its parameter list.</summary>
	/// <remarks>
	/// Blocks nest or stand apart, so the ones that reach a position are the parents of the last to open before it.
	/// </remarks>
	public bool IsAround(TextSpan declaration, TextSpan? parameterList)
	{
		for (int block = FindLastOpenedAtOrBefore(declaration.Start); block >= 0; block = parents[block])
		{
			if (ends[block] >= declaration.End)
				return true;
		}

		if (parameterList is not { } parameters)
			return false;

		int lastBefore = FindLastOpenedAtOrBefore(parameters.Start);

		for (int block = lastBefore; block >= 0; block = parents[block])
		{
			if (Overlaps(block, parameters))
				return true;
		}

		// A block that opens inside the list overlaps it, and the first block after the list's start is the one that could.
		return lastBefore + 1 < starts.Length && Overlaps(lastBefore + 1, parameters);
	}

	private bool Overlaps(int block, TextSpan span) => TextSpan.FromBounds(starts[block], ends[block]).OverlapsWith(span);

	/// <summary>The index of the last block that opens at or before the position, or -1 for none.</summary>
	private int FindLastOpenedAtOrBefore(int position)
	{
		int index = Array.BinarySearch(starts, position);
		return index >= 0 ? index : ~index - 1;
	}
}
