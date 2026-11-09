namespace Nickelony.Stanza.Markup.Core.DocComments;

/// <summary>The clean-ups a run applies to a documentation body.</summary>
/// <remarks>
/// The constructor takes one parameter for each clean-up, since the one job is to hold them together.
/// </remarks>
internal readonly record struct DocCleanupOptions(
	MarkupNameSet? Capital,
	bool Period,
	MarkupNameSet? Keywords,
	bool EmptyElement,
	bool SlashShape,
	bool SpacedSlash,
	bool ElementOrder,
	bool ListOrder,
	bool Padding
)
{
	public static DocCleanupOptions None => default;

	public bool IsNone => !RewritesContent && !RewritesLayout;

	private bool RewritesContent => Capital is not null || Period || Keywords is not null || EmptyElement;
	private bool RewritesLayout => SlashShape || ElementOrder || ListOrder || Padding;

	/// <summary>Reads the clean-ups the rules enable; no list of keywords marks <c>null</c> alone.</summary>
	/// <remarks>
	/// <see cref="Capital"/> holds the listed keywords too, since a keyword at the start of a text keeps its case.
	/// </remarks>
	public static DocCleanupOptions From(MarkupRuleSet rules, MarkupNameSet? keywords = null)
	{
		// A clean-up is off unless the file enables a rule it serves.
		return new(
			Capital: rules.Contains(MarkupDiagnostics.CapitalizeDocumentationTextId) ? keywords ?? DocTextRules.DefaultKeywords : null,
			Period: rules.Contains(MarkupDiagnostics.EndDocumentationTextWithPeriodId),
			Keywords: rules.Contains(MarkupDiagnostics.MarkLanguageKeywordsId) ? keywords ?? DocTextRules.DefaultKeywords : null,
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
