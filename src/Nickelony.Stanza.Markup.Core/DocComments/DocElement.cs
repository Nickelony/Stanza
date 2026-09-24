using System.Collections.Generic;

namespace Nickelony.Stanza.Markup.Core.DocComments;

/// <summary>An element of a documentation body, with its tags, its body and its collapsed text.</summary>
/// <remarks>
/// The constructor takes one parameter for each part the walk reads, since the one job is to hold them together.
/// </remarks>
internal sealed class DocElement(
	string name,
	string openTag,
	string? closeTag,
	bool isEmpty,
	IReadOnlyList<DocNode> body,
	string phrase,
	bool isSpacePreserved = false
) : DocNode
{
	/// <summary>The name without its namespace prefix.</summary>
	public string Name { get; } = name;
	public string OpenTag { get; } = openTag;
	public string? CloseTag { get; } = closeTag;
	public bool IsEmpty { get; } = isEmpty;
	public IReadOnlyList<DocNode> Body { get; } = body;
	public string Phrase { get; } = phrase;
	public bool IsSpacePreserved { get; } = isSpacePreserved;

	/// <summary>The whitespace before the slash of a self-closing tag, as written, when the reader knows it.</summary>
	public string? SlashGap { get; init; }
}
