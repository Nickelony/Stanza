using System;
using System.Collections.Generic;

namespace Nickelony.Stanza.Markup.Core.Options;

/// <summary>The closed set of names a key takes, read in any case, with what each name stands for.</summary>
/// <remarks>
/// A reader and a checker of the key both read one choice, so a new name is one edit.
/// </remarks>
internal sealed class OptionChoice<T>(params (string Name, T Value)[] entries)
	where T : struct
{
	/// <summary>What the value names, or <see langword="null"/> when it names none.</summary>
	public T? Find(string? value)
	{
		foreach ((string name, T entry) in entries)
		{
			if (string.Equals(value, name, StringComparison.OrdinalIgnoreCase))
				return entry;
		}

		return null;
	}

	/// <summary>What the value names, or the fallback when it names none.</summary>
	public T Read(string? value, T fallback) => Find(value) ?? fallback;
	public bool Names(string? value) => Find(value) is not null;

	/// <summary>Writes that the value is none of the names, as <c>neither 'a' nor 'b'</c>.</summary>
	public string DescribeNeither()
	{
		List<string> quoted = [];

		foreach ((string name, T _) in entries)
			quoted.Add($"'{name}'");

		string last = quoted[quoted.Count - 1];
		quoted.RemoveAt(quoted.Count - 1);

		return $"neither {string.Join(", ", quoted)} nor {last}";
	}
}
