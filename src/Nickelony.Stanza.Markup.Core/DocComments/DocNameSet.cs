using System;
using System.Collections.Generic;
using System.Linq;

namespace Nickelony.Stanza.Markup.Core.DocComments;

/// <summary>A set of names or words that cannot change after it is built, matched ordinally.</summary>
internal sealed class DocNameSet(params string[] names) : IEquatable<DocNameSet>
{
	private readonly HashSet<string> _names = new(names, StringComparer.Ordinal);

	public bool Contains(string name) => _names.Contains(name);

	/// <summary>Reads the names a comma joins, or returns <see langword="null"/> when it joins none.</summary>
	public static DocNameSet? Parse(string joined) => joined.Length == 0 ? null : new DocNameSet(joined.Split(','));

	public bool Equals(DocNameSet? other) => other is not null && _names.SetEquals(other._names);

	public override bool Equals(object? obj) => Equals(obj as DocNameSet);

	/// <summary>Adds the ordinal hashes of the names, so the order of the names does not matter.</summary>
	public override int GetHashCode()
	{
		int hash = _names.Count;

		foreach (string name in _names)
			hash = unchecked(hash + StringComparer.Ordinal.GetHashCode(name));

		return hash;
	}

	/// <summary>Joins the names with commas, in ordinal order.</summary>
	public override string ToString() => string.Join(",", _names.OrderBy(static name => name, StringComparer.Ordinal));
}
