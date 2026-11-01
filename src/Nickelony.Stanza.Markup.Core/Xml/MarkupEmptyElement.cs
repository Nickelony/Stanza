namespace Nickelony.Stanza.Markup.Core.Xml;

/// <summary>An element of a start tag and an end tag with nothing between them, on one line.</summary>
/// <remarks>
/// A gap that wraps keeps its layout, so its length is zero and <see cref="HasWrappedGap"/> is set.
/// </remarks>
internal readonly record struct MarkupEmptyElement(int LineIndex, int BracketOffset, int Length, int GapLength, bool HasWrappedGap)
{
	/// <summary>The text that replaces the gap, the bracket of the start tag and the end tag.</summary>
	public string BuildTail(bool spaced) => spaced && !HasWrappedGap ? " />" : "/>";
}
