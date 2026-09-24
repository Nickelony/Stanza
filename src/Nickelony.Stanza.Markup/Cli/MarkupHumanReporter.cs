using System.Collections.Immutable;
using System.IO;
using Nickelony.Stanza.Markup.Core;
using Nickelony.Stanza.Markup.Core.Formatting;
using Nickelony.Stanza.Markup.Notices;
using Nickelony.Stanza.Shared;

namespace Nickelony.Stanza.Markup.Cli;

internal sealed class MarkupHumanReporter(TextWriter output, TextWriter error) : MarkupReporter(output, error)
{
	public override bool WritesSummary => true;

	public override void ReportFailure(string displayPath, string message, int? line, int? column, bool isError)
	{
		string level = GetLevelName(isError ? MarkupNoticeLevel.Error : MarkupNoticeLevel.Warning);

		// The human shape adds the position when the failure names one, so a parse failure points at its line.
		string located = line is null ? displayPath : $"{displayPath}({line},{column ?? MarkupFailure.FirstColumn})";

		Error.WriteLine($"{level}: {located} {ToolHost.ToReportLine(message)}");
	}

	public override void ReportChanged(string displayPath, ImmutableArray<MarkupEdit> edits, bool write) =>
		Output.WriteLine(write ? $"formatted {displayPath}" : $"needs formatting: {displayPath}");

	public override void ReportNotice(MarkupNotice notice)
	{
		// A notice that names a line of a file leads with it, so an .editorconfig problem points at its line as MSBuild's shape does.
		string located = notice is { DisplayPath: { } displayPath, Line: { } line } ? $"{displayPath}({line}): " : string.Empty;
		Error.WriteLine($"{GetLevelName(notice.Level)}: {located}{ToolHost.ToReportLine(notice.Message)}");
	}
}
