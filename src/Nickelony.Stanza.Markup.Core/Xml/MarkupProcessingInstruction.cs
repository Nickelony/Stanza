namespace Nickelony.Stanza.Markup.Core.Xml;

internal sealed class MarkupProcessingInstruction(int startLineIndex, int endLineIndex) : IMarkupItem
{
	public int StartLineIndex { get; } = startLineIndex;
	public int EndLineIndex { get; } = endLineIndex;
}
