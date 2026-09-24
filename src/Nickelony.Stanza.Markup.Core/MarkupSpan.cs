namespace Nickelony.Stanza.Markup.Core;

internal readonly record struct MarkupSpan(int Start, int Length)
{
	public int End => Start + Length;

	public bool Contains(int offset) => offset >= Start && offset < End;

	public static MarkupSpan FromBounds(int start, int end) => new(start, end - start);
}
