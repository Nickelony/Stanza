namespace Nickelony.Stanza.Markup.Core.Xml;

/// <summary>The whitespace before the slash of a self-closing tag, on the one line that holds both.</summary>
internal readonly record struct MarkupSlashGap(int LineIndex, int SlashOffset, int GapLength, bool IsOneSpace)
{
	/// <summary>The length of the <c>/&gt;</c> that closes a self-closing tag.</summary>
	public const int SlashLength = 2;

	/// <summary>Whether the gap is not the one the shape asks for: one space when spaced, none when tight.</summary>
	public bool NeedsChange(bool spaced) => spaced ? !IsOneSpace : GapLength != 0;
}
