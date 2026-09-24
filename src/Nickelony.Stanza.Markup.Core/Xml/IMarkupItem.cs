namespace Nickelony.Stanza.Markup.Core.Xml;

internal interface IMarkupItem : IMarkupChild
{
	int EndLineIndex { get; }
	int StartLineIndex { get; }
}
