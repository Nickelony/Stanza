using System.Collections.Generic;

namespace Nickelony.Stanza.Markup.Core.Xml;

/// <summary>The tree and comments a document parses to, or the failure that stopped the parse.</summary>
internal sealed record MarkupParse(MarkupElement? Root, IReadOnlyList<MarkupComment> Comments, MarkupFailure? Failure);
