namespace Nickelony.Stanza.Markup.Core.Xml;

/// <summary>A markup run's options.</summary>
internal sealed record MarkupOptions(
	MarkupPolicy Policy,
	int MaxBlankLines = MarkupOptions.DefaultMaxBlankLines,
	MarkupRuleSet Rules = default,
	MarkupRuleSet DisabledRules = default
)
{
	/// <summary>The cap used when <c>stanza_markup_max_blank_lines</c> is absent or unreadable.</summary>
	public const int DefaultMaxBlankLines = 1;
}
