using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.CodeFixes;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class FixMergeTests
{
	private static TextChange Insert(int position, string text) =>
		new(new TextSpan(position, 0), text);

	private static TextChange Replace(int start, int end, string text) =>
		new(TextSpan.FromBounds(start, end), text);

	private static ImmutableArray<TextChange> Merge(
		ImmutableArray<TextChange>[] sets,
		bool insertsNest = false
	) => FixMerge.Merge(sets, insertsNest);

	[TestMethod]
	public void ASetThatConflictsWithAnEarlierOneIsDroppedWhole()
	{
		ImmutableArray<TextChange> merged = Merge([
			[Replace(0, 5, "x")],
			[Replace(3, 8, "y"), Insert(10, "z")],
		]);

		Assert.AreSequenceEqual([Replace(0, 5, "x")], merged);
	}

	[TestMethod]
	public void AChangeThatTouchesAnyAcceptedOneIsDropped()
	{
		ImmutableArray<TextChange> merged = Merge([
			[Replace(0, 2, "a")],
			[Replace(10, 20, "b")],
			[Replace(1, 11, "c")],
			[Insert(12, "d")],
			[Replace(18, 25, "e")],
			[Replace(5, 8, "f")],
			[Insert(10, "g")],
		]);

		Assert.AreSequenceEqual(
			[Replace(0, 2, "a"), Replace(5, 8, "f"), Insert(10, "g"), Replace(10, 20, "b")],
			merged
		);
	}

	[TestMethod]
	public void AReplacementAroundAnAcceptedInsertIsDropped()
	{
		ImmutableArray<TextChange> merged = Merge([
			[Insert(5, "a")],
			[Replace(3, 8, "b")],
			[Replace(5, 8, "c")],
		]);

		Assert.AreSequenceEqual([Insert(5, "a"), Replace(5, 8, "c")], merged);
	}

	// A merge that checks each set against every earlier one takes half a minute on 40,000 sets,
	// so the bound on the work it handles catches it, and no load of the other tests can move that count.
	// The merge counts what it does to the changes it holds, and the sets are read through a list that counts its reads.
	[TestMethod]
	public void ManySetsMergeInNearLinearWork()
	{
		var sets = new ImmutableArray<TextChange>[40_000];

		for (int index = 0; index < sets.Length; index++)
		{
			sets[index] =
				index % 2 == 0
					? [Insert(index * 4, "x")]
					: [Replace(index * 4, (index * 4) + 2, "y")];
		}

		CountedSets counted = new(sets);
		FixMerge.MergeSteps = 0;

		ImmutableArray<TextChange> merged = FixMerge.Merge(counted, insertsNest: false);

		long steps = FixMerge.MergeSteps + counted.Reads;

		Assert.IsGreaterThan(sets.Length, steps, "the merge counted no work.");

		// A binary search takes about 16 probes and each set makes a few, where the quadratic merge makes billions.
		Assert.IsLessThan((long)sets.Length * 100, steps, "the merge took too many steps.");

		Assert.HasCount(sets.Length, merged);
	}

	private sealed class CountedSets(ImmutableArray<TextChange>[] sets)
		: IReadOnlyList<ImmutableArray<TextChange>>
	{
		public long Reads { get; private set; }

		public int Count => sets.Length;

		public ImmutableArray<TextChange> this[int index]
		{
			get
			{
				Reads++;
				return sets[index];
			}
		}

		public IEnumerator<ImmutableArray<TextChange>> GetEnumerator()
		{
			foreach (ImmutableArray<TextChange> set in sets)
			{
				Reads++;
				yield return set;
			}
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	[TestMethod]
	public void AnIdenticalReplacementIsAppliedOnce()
	{
		ImmutableArray<TextChange> merged = Merge([
			[Replace(0, 2, "a"), Insert(5, "p")],
			[Replace(0, 2, "a"), Insert(7, "q")],
		]);

		Assert.AreSequenceEqual([Replace(0, 2, "a"), Insert(5, "p"), Insert(7, "q")], merged);
	}

	[TestMethod]
	public void AnIdenticalInsertOfAnotherSetIsKept()
	{
		ImmutableArray<TextChange> merged = Merge([
			[Insert(0, "{"), Insert(9, "}")],
			[Insert(4, "{"), Insert(9, "}")],
		]);

		Assert.AreSequenceEqual(
			[Insert(0, "{"), Insert(4, "{"), Insert(9, "}"), Insert(9, "}")],
			merged
		);
	}

	[TestMethod]
	public void ARepeatedSetIsAppliedOnce()
	{
		ImmutableArray<TextChange> merged = Merge([
			[Insert(0, "{"), Insert(9, "}")],
			[Insert(0, "{"), Insert(9, "}")],
		]);

		Assert.AreSequenceEqual([Insert(0, "{"), Insert(9, "}")], merged);
	}

	[TestMethod]
	public void NestedInsertsAtOneOffsetComeOutLatestSetFirstAndInWrittenOrderWithinASet()
	{
		ImmutableArray<TextChange> merged = Merge(
			[
				[Insert(9, "outer")],
				[Insert(9, "inner"), Insert(9, "after")],
			],

			insertsNest: true
		);

		Assert.AreSequenceEqual(
			[Insert(9, "inner"), Insert(9, "after"), Insert(9, "outer")],
			merged
		);
	}

	[TestMethod]
	public void InsertsAtOneOffsetKeepTheOrderOfTheirSetsUnlessTheyNest()
	{
		ImmutableArray<TextChange> merged = Merge([
			[Insert(9, "first")],
			[Insert(9, "second"), Insert(9, "third")],
		]);

		Assert.AreSequenceEqual(
			[Insert(9, "first"), Insert(9, "second"), Insert(9, "third")],
			merged
		);
	}
}
