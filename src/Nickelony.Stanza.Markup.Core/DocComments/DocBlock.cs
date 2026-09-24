using System.Collections.Generic;

namespace Nickelony.Stanza.Markup.Core.DocComments;

internal sealed class DocBlock
{
	public List<DocParagraph> Paragraphs { get; } = [];

	public DocElement? List { get; set; }

	public bool IsProse => List is null;
}
