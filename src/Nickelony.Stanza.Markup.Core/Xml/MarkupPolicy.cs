namespace Nickelony.Stanza.Markup.Core.Xml;

/// <summary>
/// A profile's policies: whether only a container counts as multi-line, and whether property elements are skipped.
/// </summary>
internal readonly record struct MarkupPolicy(bool ContainersOnly, bool SkipsPropertyElements = false);
