namespace Nickelony.Stanza.Markup.Core.DocComments;

/// <summary>The clean-ups whose changes the analyzer reads, one at a time.</summary>
internal enum DocCleanupKind
{
	Padding,
	NullKeyword,
	Period,
	EmptyElement,
	SlashShape,
}
