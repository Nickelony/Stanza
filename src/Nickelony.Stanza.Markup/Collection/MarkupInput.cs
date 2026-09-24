using Nickelony.Stanza.Markup.Core.DocComments;
using Nickelony.Stanza.Markup.Core.Xml;

namespace Nickelony.Stanza.Markup.Collection;

/// <summary>A file to lay out, with the length the scan listed for it, if the listing carried one.</summary>
internal sealed record MarkupInput(string Path, MarkupOptions Options, DocLayoutOptions? ReprintLayout, long? Length);
