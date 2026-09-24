using System;
using System.Collections.Generic;

namespace Nickelony.Stanza.Markup.Core.DocComments;

internal static class DocElementOrder
{
	public static readonly IReadOnlyList<string> ElementOrder =
	[
		"summary",
		"typeparam",
		"param",
		"returns",
		"value",
		"exception",
		"remarks",
		"example",
		"seealso",
	];

	public static readonly IReadOnlyList<string> ItemOrder = ["listheader", "item", "term", "description"];

	public static int FindRank(IReadOnlyList<string> order, string name)
	{
		for (int index = 0; index < order.Count; index++)
		{
			if (string.Equals(order[index], name, StringComparison.Ordinal))
				return index;
		}

		return -1;
	}

	public static bool IsRow(string name) => name is "item" or "listheader";

	public static List<(int Start, int End)> FindRuns(IReadOnlyList<int> ranks, int minLength = 2)
	{
		List<(int Start, int End)> runs = [];
		int start = 0;

		for (int index = 0; index <= ranks.Count; index++)
		{
			// An element the order does not name ends the run around it.
			if (index < ranks.Count && ranks[index] >= 0)
				continue;

			if (index - start >= minLength)
				runs.Add((start, index));

			start = index + 1;
		}

		return runs;
	}

	public static int[] ArrangeRanks(IReadOnlyList<int> ranks, int start, int end) =>
		Arrange(count: end - start, compare: (left, right) => ranks[start + left].CompareTo(ranks[start + right]));

	/// <summary>Finds each run that stands out of order, with the order that sorts it.</summary>
	/// <remarks>
	/// Ties of one rank go to <paramref name="compareWithinRank"/>, called with two indices.
	/// </remarks>
	public static List<DocMisorderedRun> FindMisorderedRuns(
		IReadOnlyList<int> ranks,
		int minLength = 2,
		Func<int, int, int>? compareWithinRank = null
	)
	{
		List<DocMisorderedRun> misordered = [];

		foreach ((int start, int end) in FindRuns(ranks, minLength))
		{
			int[] arrangement = Arrange(
				count: end - start,

				compare: (left, right) =>
				{
					int comparison = ranks[start + left].CompareTo(ranks[start + right]);
					return comparison != 0 || compareWithinRank is null ? comparison : compareWithinRank(start + left, start + right);
				}
			);

			if (FindMisplacedIndex(arrangement) is { } misplaced)
				misordered.Add(new DocMisorderedRun(Start: start, End: end, Arrangement: arrangement, Misplaced: misplaced));
		}

		return misordered;
	}

	/// <summary>Sorts the indices of a run stably, so entries that compare equal keep their order.</summary>
	public static int[] Arrange(int count, Func<int, int, int> compare)
	{
		int[] arrangement = new int[count];

		for (int index = 0; index < count; index++)
			arrangement[index] = index;

		for (int index = 1; index < count; index++)
		{
			int current = arrangement[index];
			int previous = index - 1;

			while (previous >= 0 && compare(arrangement[previous], current) > 0)
			{
				arrangement[previous + 1] = arrangement[previous];
				previous--;
			}

			arrangement[previous + 1] = current;
		}

		return arrangement;
	}

	public static int? FindMisplacedIndex(IReadOnlyList<int> order)
	{
		for (int index = 0; index < order.Count; index++)
		{
			if (order[index] != index)
				return index;
		}

		return null;
	}
}
