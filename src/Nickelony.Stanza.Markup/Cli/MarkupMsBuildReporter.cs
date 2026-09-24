using System.Collections.Immutable;
using System.IO;
using Nickelony.Stanza.Markup.Core;
using Nickelony.Stanza.Markup.Core.Formatting;
using Nickelony.Stanza.Markup.Notices;
using Nickelony.Stanza.Shared;

namespace Nickelony.Stanza.Markup.Cli;

internal sealed class MarkupMsBuildReporter(TextWriter output, TextWriter error) : MarkupReporter(output, error)
{
	public override bool WritesSummary => false;

	public override void ReportFailure(string displayPath, string message, int? line, int? column, bool isError)
	{
		string level = GetLevelName(isError ? MarkupNoticeLevel.Error : MarkupNoticeLevel.Warning);

		// The layout raises a position below the first to the first,
		// so the fallback here covers only a failure that names none.
		// The shape always carries a position, or the Error List cannot navigate.
		int failureLine = line ?? MarkupFailure.FirstLine;
		int failureColumn = column ?? MarkupFailure.FirstColumn;

		Output.WriteLine(
			$"{displayPath}({failureLine},{failureColumn}): {level} {MarkupDiagnostics.ReportUnreadableFileId}: {ToolHost.ToReportLine(message)}"
		);
	}

	public override void ReportChanged(string displayPath, ImmutableArray<MarkupEdit> edits, bool write)
	{
		foreach (MarkupEdit edit in edits)
			Output.WriteLine($"{displayPath}({edit.Line},{edit.Column}): warning {edit.Id}: {ToolHost.ToReportLine(edit.Message)}");
	}

	/// <summary>MSBuild has no note level, so a note stays a plain line, which a build logs as a message.</summary>
	public override void ReportNotice(MarkupNotice notice)
	{
		string message = ToolHost.ToReportLine(notice.Message);

		if (notice.Level == MarkupNoticeLevel.Note)
		{
			Output.WriteLine($"note: {message}");
			return;
		}

		// A notice about a file or a directory takes it as the origin, and its line when it has one,
		// so the Error List can point at it.
		string origin = notice switch
		{
			{ DisplayPath: null } => "stanza-markup ",
			{ Line: null } => $"{notice.DisplayPath} ",
			_ => $"{notice.DisplayPath}({notice.Line},{MarkupFailure.FirstColumn})",
		};

		Output.WriteLine($"{origin}: {GetLevelName(notice.Level)} : {message}");
	}
}
