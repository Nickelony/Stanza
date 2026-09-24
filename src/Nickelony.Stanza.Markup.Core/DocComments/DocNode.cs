namespace Nickelony.Stanza.Markup.Core.DocComments;

internal abstract class DocNode
{
	/// <summary>Where the node stands in the file it was read from, when its reader knows.</summary>
	public MarkupSpan? Source { get; init; }
}
