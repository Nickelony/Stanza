using System.Collections.Generic;

namespace Nickelony.Stanza.Markup.Core.DocComments;

internal sealed class DocParagraph
{
	public List<DocAtom> Atoms { get; } = [];

	public bool IsMarked { get; set; }
	public string? OpenTag { get; set; }
	public string? CloseTag { get; set; }
}
