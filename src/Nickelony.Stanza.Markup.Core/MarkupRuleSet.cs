using System;
using System.Collections.Generic;

namespace Nickelony.Stanza.Markup.Core;

/// <summary>The IDs of the rules a run judges, sorted for a binary search, which no caller compares by value.</summary>
internal readonly struct MarkupRuleSet
{
	private readonly string[]? _ids;

	private MarkupRuleSet(string[] ids) => _ids = ids;

	public static MarkupRuleSet None => default;

	public static MarkupRuleSet From(IEnumerable<string> ids)
	{
		string[] sorted = [.. ids];

		if (sorted.Length == 0)
			return None;

		Array.Sort(sorted, StringComparer.Ordinal);
		return new(sorted);
	}

	public bool Contains(string id) => _ids is not null && Array.BinarySearch(_ids, id, StringComparer.Ordinal) >= 0;
}
