using System;
using System.Collections.Generic;

namespace Nickelony.Stanza.Markup.Core;

internal readonly struct MarkupRuleSet : IEquatable<MarkupRuleSet>
{
	private readonly string[]? _ids;

	/// <summary>Only <see cref="From"/> builds a set, since <see cref="Contains"/> searches the IDs it sorts.</summary>
	private MarkupRuleSet(string[] ids) => _ids = ids;

	public static MarkupRuleSet None => default;

	public static MarkupRuleSet From(IEnumerable<string> ids)
	{
		string[] sorted = [.. ids];
		Array.Sort(sorted, StringComparer.Ordinal);

		return sorted.Length == 0 ? None : new(WithoutDuplicates(sorted));
	}

	public bool Contains(string id) => _ids is not null && Array.BinarySearch(_ids, id, StringComparer.Ordinal) >= 0;

	public bool Equals(MarkupRuleSet other)
	{
		string[]? mine = _ids;
		string[]? theirs = other._ids;

		if (mine is null || theirs is null)
			return mine is null && theirs is null;

		if (mine.Length != theirs.Length)
			return false;

		for (int index = 0; index < mine.Length; index++)
		{
			if (!string.Equals(mine[index], theirs[index], StringComparison.Ordinal))
				return false;
		}

		return true;
	}

	public override bool Equals(object? obj) => obj is MarkupRuleSet other && Equals(other);

	public override int GetHashCode()
	{
		if (_ids is null)
			return 0;

		unchecked
		{
			int hash = _ids.Length;

			foreach (string id in _ids)
				hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(id);

			return hash;
		}
	}

	private static string[] WithoutDuplicates(string[] sorted)
	{
		int kept = 1;

		for (int index = 1; index < sorted.Length; index++)
		{
			if (!string.Equals(sorted[index], sorted[kept - 1], StringComparison.Ordinal))
				sorted[kept++] = sorted[index];
		}

		if (kept == sorted.Length)
			return sorted;

		string[] unique = new string[kept];
		Array.Copy(sorted, unique, kept);

		return unique;
	}
}
