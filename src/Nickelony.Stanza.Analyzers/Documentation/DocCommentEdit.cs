using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Documentation;

/// <summary>One change to a documentation comment, with the span it reports at.</summary>
internal readonly record struct DocCommentEdit(TextChange Change, TextSpan Anchor) : IAnchoredEdit;
