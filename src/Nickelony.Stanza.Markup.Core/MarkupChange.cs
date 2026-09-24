namespace Nickelony.Stanza.Markup.Core;

internal readonly record struct MarkupChange(MarkupSpan Span, string NewText);
