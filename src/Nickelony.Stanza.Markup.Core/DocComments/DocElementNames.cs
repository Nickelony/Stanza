using System;

namespace Nickelony.Stanza.Markup.Core.DocComments;

internal static class DocElementNames
{
	public static readonly DocNameSet InlineElements = new(
		"a",
		"b",
		"br",
		"c",
		"em",
		"i",
		"paramref",
		"s",
		"see",
		"strong",
		"sub",
		"sup",
		"typeparamref",
		"u"
	);

	public static readonly DocNameSet OpaqueElements = new("code", "list", "table");
	public static readonly DocNameSet ListElements = new("list", "table");

	/// <summary>Whether the element is opaque and no list, as a <c>code</c> sample is.</summary>
	public static bool IsUnreadable(string name) => OpaqueElements.Contains(name) && !ListElements.Contains(name);

	public static string GetLocalName(string name)
	{
		int colon = name.LastIndexOf(':');
		return colon < 0 ? name : name.Substring(colon + 1);
	}

	public static bool IsMember(string name) => string.Equals(GetLocalName(name), "member", StringComparison.Ordinal);

	/// <summary>
	/// Whether the element is <c>include</c> or <c>inheritdoc</c>, which take their content from elsewhere.
	/// </summary>
	public static bool TakesContentFromElsewhere(string name) =>
		string.Equals(name, "include", StringComparison.Ordinal) || string.Equals(name, "inheritdoc", StringComparison.Ordinal);

	public static bool IsParagraphMarker(string name) => string.Equals(name, "para", StringComparison.Ordinal);
}
