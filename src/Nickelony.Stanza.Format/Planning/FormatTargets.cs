using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Nickelony.Stanza.Shared;

namespace Nickelony.Stanza.Format.Planning;

/// <summary>The solution and projects a run formats through, and the filter each one takes.</summary>
internal static class FormatTargets
{
	/// <summary>Holds a file of several views out of the solution's pass, and formats it per view in turn.</summary>
	/// <seealso href="https://github.com/Nickelony/Stanza/wiki/Format-command-reference#shared-source"/>
	public static ImmutableArray<FormatTarget> ForSolution(FormatFacts facts)
	{
		Dictionary<string, ImmutableArray<string>> views = ReadViews(facts.CompiledFiles);
		ImmutableArray<string> shared = [.. views.Where(static view => view.Value.Length > 1).Select(static view => view.Key)];

		if (shared.IsEmpty)
			return [new FormatTarget(facts.Solution, Filter: [])];

		ImmutableArray<string> projects = CollectProjects(facts.CompiledFiles);
		ImmutableArray<string> excluded = [.. CollectExclusions(shared, views.Keys).Select(ArgumentPath.ToPrefixed)];

		// An exclusion list cannot be split across commands, so a list too long for one formats each project in turn.
		if (CommandBatches.Measure(excluded) > CommandBatches.ArgumentBudget)
			return [.. projects.Select(static project => new FormatTarget(project, Filter: []))];

		HashSet<string> owners = new(shared.SelectMany(path => views[path]), PathComparison.Comparer);
		FormatTarget solution = new(facts.Solution, Filter: ["--exclude", .. excluded]);

		return [solution, .. projects.Where(owners.Contains).Select(static project => new FormatTarget(project, Filter: []))];
	}

	/// <summary>Routes a changed source by its views, and leaves one no project compiles to the layout step.</summary>
	/// <seealso href="https://github.com/Nickelony/Stanza/wiki/Format-command-reference#shared-source"/>
	public static ImmutableArray<FormatTarget> ForChangedSources(FormatFacts facts, ImmutableArray<string> sources)
	{
		Dictionary<string, ImmutableArray<string>> views = ReadViews(facts.CompiledFiles);
		List<string> unshared = [];
		Dictionary<string, List<string>> includes = new(PathComparison.Comparer);

		foreach (string source in sources)
		{
			if (!views.TryGetValue(source, out ImmutableArray<string> owners))
				continue;

			if (owners.Length == 1)
			{
				unshared.Add(ArgumentPath.ToPrefixed(source));
				continue;
			}

			foreach (string owner in owners)
			{
				if (!includes.TryGetValue(owner, out List<string>? files))
					includes[owner] = files = [];

				files.Add(ArgumentPath.ToPrefixed(source));
			}
		}

		return
		[
			.. CommandBatches.Split(unshared).Select(batch => new FormatTarget(facts.Solution, Filter: ["--include", .. batch])),

			.. from project in CollectProjects(facts.CompiledFiles)
			where includes.ContainsKey(project)
			from batch in CommandBatches.Split(includes[project])
			select new FormatTarget(project, Filter: ["--include", .. batch]),
		];
	}

	/// <summary>Maps each compiled file to the first project in solution order of each view that compiles it.</summary>
	private static Dictionary<string, ImmutableArray<string>> ReadViews(ImmutableArray<CompiledFile> compiledFiles)
	{
		Dictionary<string, ImmutableArray<string>> views = new(PathComparison.Comparer);

		foreach (IGrouping<string, CompiledFile> copies in compiledFiles.GroupBy(static file => file.Path, PathComparison.Comparer))
			views[copies.Key] = [.. copies.GroupBy(static copy => copy.View, StringComparer.Ordinal).Select(static view => view.First().Project)];

		return views;
	}

	private static ImmutableArray<string> CollectProjects(ImmutableArray<CompiledFile> compiledFiles) =>
		[.. compiledFiles.Select(static file => file.Project).Distinct(PathComparison.Comparer)];

	/// <summary>
	/// Excludes a directory whose compiled files are all shared, and any other shared file alone, for a short list.
	/// </summary>
	private static ImmutableArray<string> CollectExclusions(ImmutableArray<string> shared, IEnumerable<string> compiled)
	{
		HashSet<string> sharedFiles = new(shared, PathComparison.Comparer);
		Dictionary<string, bool> sharedDirectories = new(PathComparison.Comparer);

		foreach (string path in compiled)
		{
			bool isShared = sharedFiles.Contains(path);

			for (string directory = RepositoryPath.GetDirectory(path); directory.Length > 0; directory = RepositoryPath.GetDirectory(directory))
				sharedDirectories[directory] = isShared && sharedDirectories.GetValueOrDefault(directory, true);
		}

		return
		[
			.. shared
				.Select(path => FindTopSharedDirectory(path, sharedDirectories) ?? path)
				.Distinct(PathComparison.Comparer)
				.Order(PathComparison.Comparer),
		];
	}

	private static string? FindTopSharedDirectory(string path, Dictionary<string, bool> sharedDirectories)
	{
		string? top = null;

		for (
			string directory = RepositoryPath.GetDirectory(path);
			directory.Length > 0 && sharedDirectories[directory];
			directory = RepositoryPath.GetDirectory(directory)
		)
		{
			top = directory;
		}

		return top;
	}
}
