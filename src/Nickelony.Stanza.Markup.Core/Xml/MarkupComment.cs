namespace Nickelony.Stanza.Markup.Core.Xml;

internal sealed class MarkupComment(int startLineIndex, int endLineIndex, int start, int closeStart, bool isSpacePreserved, bool isInPropertyElement)
	: IMarkupItem
{
	/// <summary>The length of the <c>&lt;!--</c> that opens a comment.</summary>
	public const int OpenerLength = 4;

	/// <summary>The length of the <c>--&gt;</c> that closes a comment.</summary>
	public const int CloserLength = 3;

	public int StartLineIndex { get; } = startLineIndex;
	public int EndLineIndex { get; } = endLineIndex;

	public int Start { get; } = start;
	public int CloseStart { get; } = closeStart;

	public bool IsSpacePreserved { get; } = isSpacePreserved;
	public bool IsInPropertyElement { get; } = isInPropertyElement;
}
