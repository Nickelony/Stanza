using System.Collections.Immutable;
using System.IO;
using Nickelony.Stanza.Markup.Core.Formatting;
using Nickelony.Stanza.Markup.Notices;

namespace Nickelony.Stanza.Markup.Cli;

/// <summary>Writes what a run finds, in a shape for a person or for MSBuild.</summary>
internal abstract class MarkupReporter(TextWriter output, TextWriter error)
{
	protected TextWriter Output { get; } = output;

	protected TextWriter Error { get; } = error;

	/// <summary>Whether the run ends with a count line; a shape a machine reads has none.</summary>
	public abstract bool WritesSummary { get; }

	public abstract void ReportFailure(string displayPath, string message, int? line, int? column, bool isError);

	public abstract void ReportChanged(string displayPath, ImmutableArray<MarkupEdit> edits, bool write);

	/// <summary>Writes a note, a warning or an error the run goes on from.</summary>
	public abstract void ReportNotice(MarkupNotice notice);

	protected static string GetLevelName(MarkupNoticeLevel level)
	{
		return level switch
		{
			MarkupNoticeLevel.Note => "note",
			MarkupNoticeLevel.Warning => "warning",
			_ => "error",
		};
	}
}
