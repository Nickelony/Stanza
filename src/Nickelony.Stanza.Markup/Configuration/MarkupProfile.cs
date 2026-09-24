using System;
using System.Collections.Generic;
using Nickelony.Stanza.Markup.Core.Xml;

namespace Nickelony.Stanza.Markup.Configuration;

/// <summary>
/// Each profile is an instance <see cref="MarkupProfiles"/> holds, so it is compared by identity, not by its lists.
/// </summary>
internal sealed class MarkupProfile(string name, IReadOnlyList<string> extensions, MarkupPolicy policy)
{
	public string Name { get; } = name;
	public IReadOnlyList<string> Extensions { get; } = extensions;
	public MarkupPolicy Policy { get; } = policy;

	public bool ClaimsExtension(string extension)
	{
		foreach (string candidate in Extensions)
		{
			if (string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase))
				return true;
		}

		return false;
	}
}
