using System.Collections.Generic;

namespace Nickelony.Stanza.Markup.Core.Xml;

/// <summary>What a measure of one document finds, with the parsed root the formatter reprints from.</summary>
/// <remarks>
/// One parse yields every part at once. A commented gap lies above the comment that leads a single-line element.
/// </remarks>
internal sealed record MarkupFindings(
	IReadOnlyList<MarkupGap> Gaps,
	IReadOnlyList<MarkupGap> CommentedGaps,
	IReadOnlyList<MarkupRemoval> Removals,
	IReadOnlyList<MarkupCommentMarker> CommentMarkers,
	IReadOnlyList<MarkupSlashGap> SlashGaps,
	IReadOnlyList<MarkupEmptyElement> EmptyElements,
	MarkupFailure? Failure,
	MarkupElement? Root
);
