namespace Nickelony.Stanza.Markup.Core.Xml;

internal sealed class MarkupText(string text) : IMarkupBodyNode
{
	public string Text { get; } = text;
}
