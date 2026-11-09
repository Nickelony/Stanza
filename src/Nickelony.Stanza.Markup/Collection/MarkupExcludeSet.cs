using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Nickelony.Stanza.Markup.Notices;
using Nickelony.Stanza.Markup.Paths;
using Nickelony.Stanza.Shared;

namespace Nickelony.Stanza.Markup.Collection;

/// <summary>The <c>--exclude</c> patterns of a run, as written and as resolved.</summary>
internal sealed class MarkupExcludeSet
{
	private readonly List<ExcludePattern> _patterns;

	private MarkupExcludeSet(List<ExcludePattern> patterns, bool hasRefused)
	{
		_patterns = patterns;
		HasRefused = hasRefused;
	}

	/// <summary>A set that excludes nothing; a test seam.</summary>
	public static MarkupExcludeSet Empty { get; } = new([], hasRefused: false);

	/// <summary>Whether a pattern was refused, which the resolve already reported as an error.</summary>
	public bool HasRefused { get; }

	/// <summary>Reports each pattern that is invalid or unresolvable as an error and leaves it out.</summary>
	public static MarkupExcludeSet Resolve(ImmutableArray<string> excludes, MarkupPaths paths, MarkupNotices notices)
	{
		List<ExcludePattern> patterns = [];
		bool hasRefused = false;

		foreach (string pattern in excludes)
		{
			// A command-line exclude accepts either slash as a separator,
			// so each backslash is normalized to a slash before the pattern is validated and matched.
			// That loses an escape the glob dialect defines, such as \*, on the command line.
			string normalized = GlobMatcher.Normalize(pattern);

			bool valid = GlobPath.TryResolveValid(
				argument: normalized,
				workingDirectory: paths.WorkingDirectory,
				out GlobTarget target,
				out GlobProblem problem
			);

			// Every bad pattern is reported, and the run ends before it reads a file,
			// as a typo must never let a write run rewrite the files the exclude was meant to protect.
			// Like a refused glob, it fails the run in every mode.
			if (!valid)
			{
				string name = $"the exclude pattern '{pattern}'";
				notices.AddError(problem.Describe(unresolvedName: name, refusedName: name));
				hasRefused = true;

				continue;
			}

			patterns.Add(new(normalized, target));
		}

		return new(patterns, hasRefused);
	}

	public bool IsExcluded(string path, MarkupPaths paths)
	{
		if (_patterns.Count == 0)
			return false;

		string relativePath = Path.GetRelativePath(paths.WorkingDirectory, path);
		string fullPath = GlobMatcher.Normalize(path);

		foreach (ExcludePattern exclude in _patterns)
		{
			// An exclude is matched as resolved, against the full path, and as written, against the relative path,
			// which reaches a file above the working directory by its ../ path.
			// A leading / is the root of the file system, as in a path argument, so it is never read as written.
			bool readsAsWritten = !exclude.Pattern.StartsWith('/');

			if (exclude.Target.IsMatch(fullPath) || (readsAsWritten && GlobMatcher.IsMatch(exclude.Pattern, relativePath)))
				return true;
		}

		return CoversPath(fullPath, paths);
	}

	/// <summary>Whether an exclude covers the whole directory, so a walk skips it.</summary>
	public bool CoversPath(string path, MarkupPaths paths)
	{
		if (_patterns.Count == 0)
			return false;

		string root = GlobMatcher.Normalize(paths.WorkingDirectory).TrimEnd('/');
		string current = GlobMatcher.Normalize(path);

		while (current.Length > 0)
		{
			bool belowRoot = RelativePathScope.IsBelow(current, root);

			foreach (ExcludePattern exclude in _patterns)
			{
				// A pattern without a separator matches a name, so it is tested only below the working directory;
				// otherwise, `--exclude src` run from inside a directory named src would cover the whole run.
				if (
					CoversSubtree(exclude.Pattern)
					&& (belowRoot || exclude.Pattern.Contains('/'))
					&& (exclude.Target.IsMatch(current) || exclude.Target.IsMatch(current + "/"))
				)
				{
					return true;
				}
			}

			current = SlashPath.GetDirectory(current);
		}

		return false;
	}

	private static bool CoversSubtree(string pattern)
	{
		// Only a trailing **, a trailing / or a wildcard-free last segment covers a subtree.
		// A wildcard in the last segment matches files as readily as directories, so src/* covers no subtree.
		return pattern.EndsWith("**", StringComparison.Ordinal)
			|| pattern.EndsWith('/')
			|| GlobMatcher.FindWildcard(pattern, pattern.LastIndexOf('/') + 1) < 0;
	}

	private readonly record struct ExcludePattern(string Pattern, GlobTarget Target);
}
