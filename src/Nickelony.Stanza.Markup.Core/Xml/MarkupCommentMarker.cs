namespace Nickelony.Stanza.Markup.Core.Xml;

/// <summary>A comment marker that touches its text, with each offset counted from the start of its line.</summary>
internal readonly record struct MarkupCommentMarker(int LineIndex, int MarkerOffset, int Length, int SpaceOffset);
