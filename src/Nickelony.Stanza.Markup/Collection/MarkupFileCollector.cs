using System;
using System.Collections.Generic;
using System.IO;
using Nickelony.Stanza.Markup.Configuration;
using Nickelony.Stanza.Markup.Core.Options;
using Nickelony.Stanza.Markup.Paths;
using Nickelony.Stanza.Shared;

namespace Nickelony.Stanza.Markup.Collection;

/// <summary>Expands the path and glob arguments into the files a run reads, and notes each file it skips.</summary>
internal sealed class MarkupFileCollector(MarkupCollectionContext context)
{
	/// <summary>The full paths named so far, so a path named again in another spelling is collected once.</summary>
	private readonly HashSet<string> _named = new(PathComparison.Comparer);

	/// <summary>The globs given so far, as written, since two spellings of a glob can select different files.</summary>
	private readonly HashSet<string> _globs = new(StringComparer.Ordinal);

	private readonly HashSet<string> _collected = new(PathComparison.Comparer);
	private readonly MarkupRealPaths _realPaths = new();

	public List<MarkupInput> Collect(string argument)
	{
		// An empty argument is reported here rather than resolved, so it cannot select the working directory.
		if (string.IsNullOrWhiteSpace(argument))
		{
			context.Notices.AddError("an empty path was given.");
			return [];
		}

		string fullPath;

		try
		{
			fullPath = Path.GetFullPath(argument, context.Paths.WorkingDirectory);
		}
		catch (Exception exception) when (MarkupPaths.IsUnresolvable(exception))
		{
			// A bad path is an argument problem, not a reason to stop the run.
			context.Notices.AddError($"'{argument}' could not be resolved to a path: {exception.Message}");
			return [];
		}

		bool isNew = _named.Add(Path.TrimEndingDirectorySeparator(fullPath));

		// A link named on the command line is scanned like any other directory;
		// only a link the walk finds is never entered, which keeps a self-referencing junction from looping.
		if (Directory.Exists(fullPath))
			return isNew ? CollectScan(argument, directory: fullPath) : [];

		if (File.Exists(fullPath))
			return isNew ? CollectNamed(fullPath) : [];

		if (GlobMatcher.IsGlob(argument))
			return _globs.Add(argument) ? CollectGlob(argument) : [];

		if (isNew)
			context.Notices.AddError($"'{argument}' does not exist.");

		return [];
	}

	private List<MarkupInput> CollectNamed(string fullPath)
	{
		string displayPath = context.Paths.GetDisplayPath(fullPath);

		// A file named and excluded at once is a skip the user asked for, so it gets a note rather than silence.
		if (context.Excludes.IsExcluded(fullPath, context.Paths))
		{
			context.Notices.AddNote($"'{displayPath}' is excluded by --exclude, so it was skipped.");
			return [];
		}

		// A disabled file and a file of another type get a note, and the link policy is never read for them,
		// so neither is a link warning that --strict escalates; a hook names every changed file, links included.
		if (FindInput(new FileInfo(fullPath), out MarkupSkipReason? skipped) is { } input)
			return TryAddCollected(input) ? [input] : [];

		if (skipped is MarkupSkipReason.Disabled)
		{
			context.Notices.AddNote($"'{displayPath}' is disabled by its .editorconfig, so it was skipped.");
		}
		else if (skipped is MarkupSkipReason.Unclaimed)
		{
			context.Notices.AddNote(
				$"'{displayPath}' is not a file type any profile claims, so it was skipped. Name one with --profile or with a '{MarkupKeys.Profile}' entry."
			);
		}

		return [];
	}

	private List<MarkupInput> CollectGlob(string argument)
	{
		string searchRoot;
		GlobTarget target;

		try
		{
			searchRoot = GlobPath.GetSearchRoot(argument, context.Paths.WorkingDirectory);
			target = GlobPath.Resolve(argument, context.Paths.WorkingDirectory);
		}
		catch (Exception exception) when (MarkupPaths.IsUnresolvable(exception))
		{
			context.Notices.AddError($"'{argument}' could not be resolved to a path: {exception.Message}");
			return [];
		}

		// A pattern the dialect refuses matches nothing, so it is reported as refused rather than as a miss.
		// Like a path that does not exist, it fails the run, because a script that names it expects files.
		// The target's directory is compared as text, so only the pattern below it is validated.
		if (!GlobMatcher.TryValidate(target.Pattern, out string? refusal))
		{
			context.Notices.AddError($"the pattern '{argument}' is refused ({refusal}); it matches nothing.");
			return [];
		}

		// A missing directory part cannot match anything, and is reported like any empty glob.
		if (!Directory.Exists(searchRoot))
		{
			context.Notices.AddError($"'{argument}' does not match any file.");
			return [];
		}

		List<MarkupInput> inputs = [];
		MarkupGlobTally tally = new();
		int found = 0;

		// The target and the path are both resolved, so any spelling of the glob selects the same files.
		foreach (FileSystemInfo entry in MarkupDirectoryWalker.EnumerateEntries(directory: searchRoot, context, target.MaxDepth))
		{
			if (!target.IsMatch(entry.FullName))
				continue;

			tally.Matched++;

			if (context.Excludes.IsExcluded(entry.FullName, context.Paths))
			{
				tally.Excluded++;
				continue;
			}

			if (FindInput(entry, out MarkupSkipReason? skipped) is { } input)
			{
				found++;

				if (TryAddCollected(input))
					inputs.Add(input);
			}
			else if (skipped is MarkupSkipReason.Unclaimed)
			{
				tally.Unclaimed++;
			}
			else if (skipped is MarkupSkipReason.Disabled)
			{
				tally.Disabled++;
			}
		}

		tally.Report(argument, collected: found, context.Notices);

		return inputs;
	}

	private List<MarkupInput> CollectScan(string argument, string directory)
	{
		List<MarkupInput> inputs = [];
		int found = 0;

		foreach (FileSystemInfo entry in MarkupDirectoryWalker.EnumerateEntries(directory, context))
		{
			// An excluded file is left out before the no-file check, so a scan that excludes everything reports it.
			if (context.Excludes.IsExcluded(entry.FullName, context.Paths))
				continue;

			// A forced profile narrows a scan to the extensions it claims.
			if (context.Forced is not null && !context.Forced.ClaimsExtension(entry.Extension))
				continue;

			if (FindInput(entry, out _) is { } input)
			{
				found++;

				if (TryAddCollected(input))
					inputs.Add(input);
			}
		}

		if (found == 0)
			context.Notices.AddWarning($"'{argument}' holds no file this run inspects.", displayPath: context.Paths.GetDisplayPath(directory));

		return inputs;
	}

	/// <summary>Adds the file by its real path, and says whether no earlier argument reached it.</summary>
	private bool TryAddCollected(MarkupInput input) => _collected.Add(_realPaths.GetRealPath(input.Path));

	/// <summary>The length the listing gave the file, free where the listing carries one.</summary>
	private static long? FindListedLength(FileSystemInfo entry)
	{
		try
		{
			return (entry as FileInfo)?.Length;
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
		{
			// A file that vanished since the listing is read, and refused, by the reader.
			return null;
		}
	}

	/// <summary>Reads the input of a file that is enabled, claimed by a profile and not a link.</summary>
	/// <remarks>
	/// A link is reported after the other checks, so a disabled or unclaimed file is never inspected.
	/// </remarks>
	private MarkupInput? FindInput(FileSystemInfo entry, out MarkupSkipReason? skipped)
	{
		MarkupProfile? forced = context.Forced;
		MarkupConfiguration configuration = context.Configuration;
		string path = entry.FullName;

		skipped = null;

		if (!configuration.IsEnabled(path))
		{
			skipped = MarkupSkipReason.Disabled;
			return null;
		}

		MarkupProfile? profile = configuration.FindProfile(path, forced);

		if (profile is null)
		{
			skipped = MarkupSkipReason.Unclaimed;
			return null;
		}

		if (MarkupLinkPolicy.TryReportLink(entry, context))
			return null;

		return new(
			Path: path,
			Options: configuration.GetOptions(path, profile, forced is not null),
			ReprintLayout: configuration.ResolveReprintLayout(path),
			Length: FindListedLength(entry)
		);
	}
}
