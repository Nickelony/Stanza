namespace Nickelony.Stanza.Markup.Notices;

internal enum MarkupNoticeLevel
{
	/// <summary>A named file the run skips, which <c>--strict</c> never escalates.</summary>
	Note,

	Warning,
	Error,
}
