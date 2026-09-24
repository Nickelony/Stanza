using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>Lists the files git tracks or would add, leaving out any copy of the tree.</summary>
internal static class RepositoryFiles
{
	private static readonly string[] s_skippedDirectories =
	[
		".git",
		".vs",
		"bin",
		"obj",
		"artifacts",
		"node_modules",
	];

	private static readonly string[] s_listArguments =
	[
		"ls-files",
		"-z",
		"--cached",
		"--others",
		"--exclude-standard",
	];

	// Every guard waits on this one listing, and a busy run can keep git past the bound a child process has by default.
	private static readonly TimeSpan s_gitBound = TimeSpan.FromMinutes(2);

	// Publication only, so a read that failed, as on a git that answered too slowly, is tried again by the next guard.
	private static readonly Lazy<string[]> s_files = new(
		Read,
		LazyThreadSafetyMode.PublicationOnly
	);

	/// <summary>Returns the full path of each file under the directory that matches.</summary>
	internal static IEnumerable<string> Under(string directory, string pattern)
	{
		string prefix = Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar;

		return s_files.Value.Where(file =>
			file.StartsWith(prefix, StringComparison.Ordinal)
			&& FileSystemName.MatchesSimpleExpression(
				pattern,
				Path.GetFileName(file),
				ignoreCase: true
			)
		);
	}

	private static string[] Read()
	{
		string root = RepositoryPaths.Root();
		string[]? listed = ReadFromGit(root);

		// A file that git still lists after it was deleted from the disk is no file to read.
		string[] files = listed is null
			? [.. WalkDisk(root)]
			: [.. listed.Select(path => ToFullPath(root, path)).Where(File.Exists)];

		Array.Sort(files, StringComparer.Ordinal);
		Assert.IsNotEmpty(files, "No repository file is listed, so the guards would read nothing.");

		return files;
	}

	private static string ToFullPath(string root, string listed) =>
		Path.Combine(root, listed.Replace('/', Path.DirectorySeparatorChar));

	// Returns null outside a git repository, or when git lists nothing, as in a checkout a parent repository ignores,
	// so the disk walk still feeds the guards.
	private static string[]? ReadFromGit(string root)
	{
		ProcessStartInfo start = new("git")
		{
			WorkingDirectory = root,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};

		foreach (string argument in s_listArguments)
			start.ArgumentList.Add(argument);

		RunResult result = ChildProcess.Run(start, "git ls-files", s_gitBound);

		if (result.ExitCode != 0)
			return null;

		string[] listed = result.Output.Split('\0', StringSplitOptions.RemoveEmptyEntries);

		return listed.Length > 0 ? listed : null;
	}

	private static IEnumerable<string> WalkDisk(string root)
	{
		Stack<string> directories = new();
		directories.Push(root);

		while (directories.Count > 0)
		{
			string directory = directories.Pop();

			foreach (string child in Directory.EnumerateDirectories(directory))
			{
				if (
					!s_skippedDirectories.Contains(
						Path.GetFileName(child),
						StringComparer.OrdinalIgnoreCase
					)
				)
				{
					directories.Push(child);
				}
			}

			foreach (string file in Directory.EnumerateFiles(directory))
				yield return file;
		}
	}
}
