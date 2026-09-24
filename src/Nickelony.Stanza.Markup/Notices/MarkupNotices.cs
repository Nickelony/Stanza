using System.Collections.Generic;

namespace Nickelony.Stanza.Markup.Notices;

/// <summary>Gathers the notices of a run, and raises a warning to an error under <c>--strict</c>.</summary>
internal sealed class MarkupNotices(bool strict)
{
	private readonly List<MarkupNotice> _items = [];

	public IReadOnlyList<MarkupNotice> Items => _items;

	public void AddError(string message) => _items.Add(new(Message: message, Level: MarkupNoticeLevel.Error));

	/// <summary>Adds a problem that fails the run only under <c>--strict</c>.</summary>
	public void AddWarning(string message, string? displayPath = null, int? line = null)
	{
		MarkupNoticeLevel level = strict ? MarkupNoticeLevel.Error : MarkupNoticeLevel.Warning;
		_items.Add(new(Message: message, Level: level, DisplayPath: displayPath, Line: line));
	}

	public void AddNote(string message) => _items.Add(new(Message: message, Level: MarkupNoticeLevel.Note));
}
