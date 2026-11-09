using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using Nickelony.Stanza.Markup.Collection;
using Nickelony.Stanza.Markup.Configuration;
using Nickelony.Stanza.Markup.Core.Formatting;
using Nickelony.Stanza.Markup.Files;
using Nickelony.Stanza.Markup.Notices;
using Nickelony.Stanza.Markup.Paths;
using Nickelony.Stanza.Shared;

namespace Nickelony.Stanza.Markup.Cli;

/// <summary>Runs a <c>stanza-markup</c> command over the files its paths select, and reports each outcome.</summary>
internal sealed class MarkupTool(TextWriter output, TextWriter error, string workingDirectory, CancellationToken cancellationToken)
{
	private const string ToolName = "stanza-markup";

	private readonly MarkupPaths _paths = new(workingDirectory);

	public static int Run(
		string[] args,
		TextWriter output,
		TextWriter error,
		string workingDirectory,
		CancellationToken cancellationToken = default
	) => ToolHost.RunGuarded(() => new MarkupTool(output, error, workingDirectory, cancellationToken).RunCommand(args), error);

	private int RunCommand(string[] args)
	{
		// An unreadable option is reported before --help or --version, so a typo cannot pass for either.
		// The line did not parse, so the error takes the plain shape of the human report.
		if (!MarkupCommandLine.TryParse(args, out MarkupCommandLine? commandLine, out string? parseError))
			return ToolHost.FailUsage(ToolName, parseError, error);

		bool answered = ToolHost.TryAnswerInformation(
			help: commandLine.Help,
			version: commandLine.Version,
			usage: MarkupCommandLine.Usage,
			assembly: typeof(MarkupTool).Assembly,
			output
		);

		if (answered)
			return 0;

		MarkupReporter reporter =
			commandLine.ReportFormat == MarkupReportFormat.MsBuild
				? new MarkupMsBuildReporter(output, error)
				: new MarkupHumanReporter(output, error);

		MarkupProfile? forced = null;

		if (commandLine.Profile is not null)
		{
			forced = MarkupProfiles.FindByName(commandLine.Profile);

			if (forced is null)
			{
				// The reporter writes the error in its own shape, so MSBuild reads it too.
				reporter.ReportNotice(
					new MarkupNotice($"unknown profile '{commandLine.Profile}'. Known profiles: {MarkupProfiles.Summary}.", MarkupNoticeLevel.Error)
				);

				return ToolHost.FailWithHint(ToolName, error);
			}
		}

		MarkupNotices notices = new(strict: commandLine.Strict);
		ImmutableArray<MarkupInput> inputs = CollectInputs(commandLine, forced, notices);

		ReportNotices(notices.Items, reporter);

		bool write = commandLine.Command == MarkupCommand.Format;
		(bool failed, int changed) = ProcessInputs(inputs, write, reporter, strict: commandLine.Strict);

		ReportSummary(changed, write, reporter);

		return GetExitCode(failed: failed || notices.Items.Any(static notice => notice.IsError), changed, write);
	}

	private ImmutableArray<MarkupInput> CollectInputs(MarkupCommandLine commandLine, MarkupProfile? forced, MarkupNotices notices)
	{
		// Problems the configuration hits while resolving go to the same notices as the scan's own, so --strict escalates them alike.
		MarkupConfiguration configuration = new(_paths, notices, reprintForced: commandLine.Reprint);

		// The exclude patterns are read before the walk,
		// so a directory one of them covers is pruned while the walk reaches it,
		// not after its entries are read.
		var excludes = MarkupExcludeSet.Resolve(commandLine.Excludes, _paths, notices);

		// A refused exclude ends the run here, before any input is collected.
		if (excludes.HasRefused)
			return [];

		MarkupCollectionContext context = new(paths: _paths, forced, configuration, excludes, notices, cancellationToken);

		MarkupFileCollector collector = new(context);
		List<MarkupInput> collected = [];

		foreach (string argument in commandLine.Paths)
			collected.AddRange(collector.Collect(argument));

		return ToSorted(collected);
	}

	private static void ReportNotices(IReadOnlyList<MarkupNotice> notices, MarkupReporter reporter)
	{
		foreach (MarkupNotice notice in notices.Where(static notice => !notice.IsError))
			reporter.ReportNotice(notice);

		// A path that could not be collected fails the run, and a refused exclude collects none;
		// otherwise the other inputs are still processed, and the error is reported here.
		foreach (MarkupNotice error in notices.Where(static notice => notice.IsError))
			reporter.ReportNotice(error);
	}

	private (bool Failed, int Changed) ProcessInputs(ImmutableArray<MarkupInput> inputs, bool write, MarkupReporter reporter, bool strict)
	{
		bool failed = false;
		int changed = 0;

		foreach (MarkupInput input in inputs)
		{
			cancellationToken.ThrowIfCancellationRequested();

			MarkupFileResult result = FormatFileGuarded(input, write, reporter, strict);

			if (result.Outcome == MarkupFileOutcome.Changed)
			{
				changed++;
				reporter.ReportChanged(result.DisplayPath, result.Edits, write);
			}
			else if (result.FailsTheRun(strict))
			{
				failed = true;
			}
		}

		return (failed, changed);
	}

	/// <summary>Writes the count line, which a failing file does not stop; the MSBuild shape has none.</summary>
	private void ReportSummary(int changed, bool write, MarkupReporter reporter)
	{
		if (changed > 0 && reporter.WritesSummary)
			output.WriteLine(write ? $"{changed} file(s) formatted." : $"{changed} file(s) need formatting.");
	}

	/// <summary>
	/// A run that writes leaves no file to format, so only a check
	/// returns <see cref="ToolHost.NeedsFormattingExitCode"/>.
	/// </summary>
	private static int GetExitCode(bool failed, int changed, bool write)
	{
		if (failed)
			return ToolHost.FailedExitCode;

		return changed == 0 || write ? 0 : ToolHost.NeedsFormattingExitCode;
	}

	/// <summary>The collector has dropped the repeats of a file, so this puts the rest in path order.</summary>
	private static ImmutableArray<MarkupInput> ToSorted(List<MarkupInput> collected) =>
		[.. collected.OrderBy(input => input.Path, PathComparison.Comparer)];

	private MarkupFileResult FormatFileGuarded(MarkupInput input, bool write, MarkupReporter reporter, bool strict)
	{
		string displayPath = _paths.GetDisplayPath(input.Path);

		try
		{
			return FormatFile(input, displayPath, write, reporter, strict);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			reporter.ReportFailure(
				displayPath,
				message: $"could not be processed: {exception.GetType().Name}: {exception.Message}",
				line: null,
				column: null,
				isError: true
			);

			return new(MarkupFileOutcome.Defect, displayPath, []);
		}
	}

	private MarkupFileResult FormatFile(MarkupInput input, string displayPath, bool write, MarkupReporter reporter, bool strict)
	{
		if (!MarkupSource.TryRead(path: input.Path, out MarkupSource? source, error: out string? readError, statedLength: input.Length))
		{
			reporter.ReportFailure(displayPath, message: readError ?? "could not be read.", line: null, column: null, isError: strict);
			return new(MarkupFileOutcome.Failed, displayPath, []);
		}

		MarkupFormatResult result = MarkupFormatter.Format(
			source: source.Text,
			options: input.Options,
			reprintLayout: input.ReprintLayout,
			cancellationToken: cancellationToken
		);

		if (result.Failure is { } failure)
		{
			reporter.ReportFailure(displayPath, failure, isDefect: result.IsDefect, strict);
			return MarkupFileResult.ForFailure(displayPath, result);
		}

		if (!result.HasEdits)
			return new(MarkupFileOutcome.Unchanged, displayPath, []);

		// A document whose declared encoding this tool cannot write back is read anyway,
		// so a run that only judges it can, and a run that has nothing to write passes it.
		if (write && source.WriteBackRefusal is { } writeBackRefusal)
		{
			reporter.ReportFailure(displayPath, message: $"will not be written back: {writeBackRefusal}", line: null, column: null, isError: strict);
			return new(MarkupFileOutcome.Failed, displayPath, []);
		}

		if (write && !source.TryWrite(input.Path, result.Text, out string? writeError))
		{
			reporter.ReportFailure(displayPath, message: $"could not be written: {writeError}", line: null, column: null, isError: true);
			return new(MarkupFileOutcome.NotWritten, displayPath, []);
		}

		return new(MarkupFileOutcome.Changed, displayPath, [.. result.Edits]);
	}
}
