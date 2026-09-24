namespace Nickelony.Stanza.Markup.Core.DocComments;

/// <summary>A run of a text and the file span it came from; a line break is one character of the text.</summary>
internal readonly record struct DocTextPiece(int Offset, MarkupSpan Span, bool IsBreak);
