using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Preserves written gaps between tight sections of a kept run.</summary>
internal static class BlankLineSections
{
	public static bool IsSectionBreak(ContainerContext container, int boundary, Func<int, bool> isInside, Func<int, bool> isLoose)
	{
		ImmutableArray<SyntaxNode> siblings = container.Siblings;

		bool IsTight(int candidate)
		{
			return candidate > 0
				&& candidate < siblings.Length
				&& isInside(candidate)
				&& !BlankLineGaps.HasBlankLineBetween(siblings[candidate - 1], siblings[candidate])
				&& BlankLineKeptPairs.IsWhitespaceGap(siblings[candidate - 1], siblings[candidate], container.CancellationToken);
		}

		int before = IsTight(boundary - 1) ? 2 : 1;
		int after = IsTight(boundary + 1) ? 2 : 1;

		if (before == 2 && after == 2)
			return true;

		if (before == 2 && after == 1)
			return isLoose(boundary + 1) && IsTight(boundary + 2);

		return after == 2 && before == 1 && isLoose(boundary - 1) && IsTight(boundary - 2);
	}
}
