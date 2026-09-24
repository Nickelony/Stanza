namespace Nickelony.Stanza.Markup.Core.Formatting;

internal sealed record MarkupEdit(MarkupChange Change, int Line, string Message, string Id, int Column = MarkupFailure.FirstColumn);
