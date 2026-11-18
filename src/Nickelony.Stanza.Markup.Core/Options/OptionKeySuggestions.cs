using System;

namespace Nickelony.Stanza.Markup.Core.Options;

/// <summary>Finds the known key that a misspelled <c>stanza_</c> key most likely means.</summary>
internal static class OptionKeySuggestions
{
	private const int MaxDistance = 3;

	/// <summary>The known key within a few edits of the key, or <see langword="null"/> when none is.</summary>
	public static string? FindNearest(string key)
	{
		string? nearest = null;
		int nearestDistance = 0;

		foreach (string known in OptionKeys.Known)
		{
			int distance = CountEdits(key, known);

			if (distance > MaxDistance)
				continue;

			if (nearest is null || distance < nearestDistance || (distance == nearestDistance && string.CompareOrdinal(known, nearest) < 0))
			{
				nearest = known;
				nearestDistance = distance;
			}
		}

		return nearest;
	}

	/// <summary>Counts the insertions, deletions, substitutions and neighbor swaps between two texts.</summary>
	private static int CountEdits(string from, string to)
	{
		int[,] distances = new int[from.Length + 1, to.Length + 1];

		for (int row = 0; row <= from.Length; row++)
			distances[row, 0] = row;

		for (int column = 0; column <= to.Length; column++)
			distances[0, column] = column;

		for (int row = 1; row <= from.Length; row++)
		{
			for (int column = 1; column <= to.Length; column++)
			{
				int cost = from[row - 1] == to[column - 1] ? 0 : 1;

				int best = Math.Min(
					Math.Min(distances[row - 1, column] + 1, distances[row, column - 1] + 1),
					distances[row - 1, column - 1] + cost
				);

				if (row > 1 && column > 1 && from[row - 1] == to[column - 2] && from[row - 2] == to[column - 1])
					best = Math.Min(best, distances[row - 2, column - 2] + 1);

				distances[row, column] = best;
			}
		}

		return distances[from.Length, to.Length];
	}
}
