using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.CodeFixes;

/// <summary>Merges the change sets of a Fix All, dropping each set that conflicts with an earlier one.</summary>
internal static class FixMerge
{
	/// <summary>The work of this thread's merges, counted as a test seam that bounds it without a clock.</summary>
	[ThreadStatic]
	public static long MergeSteps;

	/// <summary>Merges the change sets of several fixes into one list in the order the text takes them.</summary>
	/// <remarks>
	/// When <paramref name="insertsNest"/> holds, inserts at one position come out latest set first.
	/// </remarks>
	public static ImmutableArray<TextChange> Merge(IReadOnlyList<ImmutableArray<TextChange>> sets, bool insertsNest)
	{
		var accepted = new AcceptedChanges();
		var merged = new HashSet<ImmutableArray<TextChange>>(SetComparer.Instance);
		var taken = new TakenSpans();

		// A set that conflicts with an earlier one is dropped whole, and a set equal to an earlier one is a repeat.
		// An identical replacement is applied once, but each set's inserts stay: two flat nested statements each need their brace.
		for (int setIndex = 0; setIndex < sets.Count; setIndex++)
		{
			ImmutableArray<TextChange> set = sets[setIndex];
			MergeSteps++;

			if (merged.Contains(set) || set.Any(taken.Conflicts))
				continue;

			merged.Add(set);

			foreach (TextChange change in set)
			{
				if (taken.TryAdd(change))
					accepted.Add(change, setIndex);
			}
		}

		// An insert comes before the replacement that starts at the same position, and the sort is stable,
		// so the inserts of one fix keep the order it wrote them in.
		return
		[
			.. accepted
				.OrderBy(static entry => entry.Change.Span.Start)
				.ThenBy(static entry => entry.Change.Span.IsEmpty ? 0 : 1)
				.ThenBy(entry => entry.Change.Span.IsEmpty && insertsNest ? -entry.Set : entry.Set)
				.Select(static entry => entry.Change),
		];
	}

	/// <summary>The changes accepted, each with its set; a pass over them counts as a step for each one.</summary>
	private sealed class AcceptedChanges : IEnumerable<(TextChange Change, int Set)>
	{
		private readonly List<(TextChange Change, int Set)> _entries = [];

		public void Add(TextChange change, int set)
		{
			MergeSteps++;
			_entries.Add((change, set));
		}

		public IEnumerator<(TextChange Change, int Set)> GetEnumerator()
		{
			foreach ((TextChange Change, int Set) entry in _entries)
			{
				MergeSteps++;
				yield return entry;
			}
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	/// <summary>The changes accepted so far, in position order.</summary>
	private sealed class TakenSpans
	{
		private readonly List<TextChange> _replacements = [];
		private readonly SortedSet<int> _inserts = [];

		/// <summary>
		/// Whether the change clashes with one taken: a replacement over an insert, or two different ones that overlap.
		/// </summary>
		public bool Conflicts(TextChange change)
		{
			TextSpan span = change.Span;
			MergeSteps++;

			if (span.IsEmpty)
				return FindLastStartingBefore(span.Start) is { } replacement && span.Start < replacement.Span.End;

			if (span.Length > 1 && _inserts.GetViewBetween(span.Start + 1, span.End - 1).Count > 0)
				return true;

			// The accepted replacements never overlap, since a set that would overlap one is dropped,
			// so the one that starts last before a change's end is the only one that can overlap it.
			return FindLastStartingBefore(span.End) is { } other && other.Span.End > span.Start && !IsSame(change, other);
		}

		/// <summary>An identical replacement is taken once, and an insert is always taken.</summary>
		public bool TryAdd(TextChange change)
		{
			if (change.Span.IsEmpty)
			{
				_inserts.Add(change.Span.Start);
				return true;
			}

			int index = FindIndexStartingBefore(change.Span.End);

			if (index >= 0 && IsSame(change, _replacements[index]))
				return false;

			MergeSteps += _replacements.Count - (index + 1);
			_replacements.Insert(index + 1, change);
			return true;
		}

		private TextChange? FindLastStartingBefore(int position) =>
			FindIndexStartingBefore(position) is int index and >= 0 ? _replacements[index] : null;

		/// <summary>The index of the last replacement that starts before the position, or -1 when none does.</summary>
		private int FindIndexStartingBefore(int position)
		{
			int low = 0;
			int high = _replacements.Count - 1;

			while (low <= high)
			{
				MergeSteps++;

				int middle = low + ((high - low) / 2);

				if (_replacements[middle].Span.Start < position)
					low = middle + 1;
				else
					high = middle - 1;
			}

			return high;
		}

		private static bool IsSame(TextChange first, TextChange second) => first.Span == second.Span && first.NewText == second.NewText;
	}

	/// <summary>A set repeats an earlier one when it holds the same changes in the same order.</summary>
	private sealed class SetComparer : IEqualityComparer<ImmutableArray<TextChange>>
	{
		public static SetComparer Instance { get; } = new();

		public bool Equals(ImmutableArray<TextChange> x, ImmutableArray<TextChange> y)
		{
			MergeSteps += x.Length + 1;
			return x.SequenceEqual(y);
		}

		public int GetHashCode(ImmutableArray<TextChange> obj)
		{
			int hash = obj.Length;
			MergeSteps += obj.Length + 1;

			foreach (TextChange change in obj)
				hash = (hash * 31) + change.GetHashCode();

			return hash;
		}
	}
}
