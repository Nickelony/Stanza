namespace Nickelony.Stanza.Markup.Notices;

/// <summary>A problem the run reports and goes on from; an error also fails the run.</summary>
internal sealed record MarkupNotice(string Message, MarkupNoticeLevel Level, string? DisplayPath = null, int? Line = null)
{
	public bool IsError => Level == MarkupNoticeLevel.Error;
}
