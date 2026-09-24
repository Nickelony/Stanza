using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Comments;

/// <summary>Where a comment gains its space, and the span of the marker or comment it reports at.</summary>
internal readonly struct CommentGap(TextSpan span, TextSpan anchor)
{
	public TextSpan Span { get; } = span;
	public TextSpan Anchor { get; } = anchor;
}
