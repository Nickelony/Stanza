using System.Collections.Generic;

namespace Nickelony.Stanza.Markup.Core.DocComments;

internal sealed class DocContent(List<DocBlock> blocks, MarkupSpan? firstParagraphBreak, bool sawBlankLine, bool holdsUnmarkedBlankLine)
{
	public IReadOnlyList<DocBlock> Blocks { get; } = blocks;
	public MarkupSpan? FirstParagraphBreak { get; } = firstParagraphBreak;
	public bool SawBlankLine { get; } = sawBlankLine;
	public bool HoldsUnmarkedBlankLine { get; } = holdsUnmarkedBlankLine;

	public bool HoldsBlankLine => SawBlankLine || HoldsUnmarkedBlankLine;
	public bool IsProseOnly => Blocks.Count == 1 && Blocks[0].IsProse;

	public IReadOnlyList<DocParagraph> Paragraphs => Blocks[0].Paragraphs;
}
