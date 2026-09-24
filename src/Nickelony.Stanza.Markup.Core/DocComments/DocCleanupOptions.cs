namespace Nickelony.Stanza.Markup.Core.DocComments;

/// <summary>The clean-ups a run applies to a documentation body.</summary>
/// <remarks>
/// The constructor takes one parameter for each clean-up, since the one job is to hold them together.
/// </remarks>
internal readonly record struct DocCleanupOptions(
	bool Period,
	bool NullKeyword,
	bool EmptyElement,
	bool SlashShape,
	bool SpacedSlash,
	bool ElementOrder,
	bool ListOrder,
	bool Padding
)
{
	public static DocCleanupOptions None => default;

	public bool IsNone => !Period && !NullKeyword && !EmptyElement && !SlashShape && !ElementOrder && !ListOrder && !Padding;

	public static DocCleanupOptions From(MarkupRuleSet rules)
	{
		// A clean-up is off unless the file enables a rule it serves.
		return new(
			Period: rules.Contains(MarkupDiagnostics.EndDocumentationTextWithPeriodId),
			NullKeyword: rules.Contains(MarkupDiagnostics.MarkNullKeywordId),
			EmptyElement: rules.Contains(MarkupDiagnostics.WriteEmptyElementAsSelfClosingTagId),

			SlashShape: rules.Contains(MarkupDiagnostics.SeparateSlashOfSelfClosingTagId)
				|| rules.Contains(MarkupDiagnostics.KeepSlashOfSelfClosingTagTightId),

			SpacedSlash: rules.Contains(MarkupDiagnostics.SeparateSlashOfSelfClosingTagId)
				&& !rules.Contains(MarkupDiagnostics.KeepSlashOfSelfClosingTagTightId),

			ElementOrder: rules.Contains(MarkupDiagnostics.OrderElementsOfDocumentationCommentId),
			ListOrder: rules.Contains(MarkupDiagnostics.OrderItemsOfDocumentationListId),
			Padding: rules.Contains(MarkupDiagnostics.RemoveDocumentationPaddingId)
		);
	}
}
