using System.Collections.Generic;

namespace Nickelony.Stanza.Markup.Core.Xml;

/// <summary>What a measure of one document finds, with the parsed root the formatter reprints from.</summary>
internal sealed record MarkupFindings(
	IReadOnlyList<MarkupGap> Gaps,
	IReadOnlyList<MarkupRemoval> Removals,
	IReadOnlyList<MarkupCommentMarker> CommentMarkers,
	MarkupFailure? Failure,
	MarkupElement? Root
);
