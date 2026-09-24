namespace Nickelony.Stanza.Markup.Core;

internal readonly record struct MarkupLine(int LineIndex, int Start, int End, int EndIncludingLineBreak)
{
	public bool HasLineBreak => EndIncludingLineBreak > End;
}
