using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using Nickelony.Stanza.Shared;

namespace Nickelony.Stanza.Format.Running;

/// <summary>Reads the working tree through git; a path is relative to the root, with forward slashes.</summary>
internal static class GitRepository
{
	private static readonly string[] s_buildOutputDirectories = ["bin", "obj"];

	/// <summary>Walks up to the root, keeping the spelling of a link the directory is read through.</summary>
	/// <remarks>
	/// Git resolves the links of the root it names, and that root stands when the walk names another directory.
	/// </remarks>
	public static string ReadRoot(string directory, CancellationToken cancellationToken)
	{
		ProcessResult found = RunTopLevel(directory, cancellationToken);

		// Git says why it found none, as when it refuses a repository another user owns.
		if (found.ExitCode != 0)
			throw new FormatRunException($"'{directory}' is not in a git repository: {found.Error.Trim()}");

		(string root, string up) = ReadTopLevel(found);
		string walked = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(directory, up)));

		// The walk from a link to a directory below the root ends elsewhere, in another repository or none,
		// and only git says which root a directory belongs to, so the walk is checked with a second call.
		return up.Length == 0 || NamesRoot(RunTopLevel(walked, cancellationToken), root) ? walked : Path.GetFullPath(root);
	}

	/// <summary>Skips a deleted file, and an untracked file below <c>bin</c> or <c>obj</c> as build output.</summary>
	/// <remarks>
	/// While HEAD names no commit, every indexed file is a change.
	/// </remarks>
	public static ImmutableArray<string> CollectChangedFiles(string root, CancellationToken cancellationToken)
	{
		string tracked = HasCommit(root, cancellationToken)
			? RunGit(root, ["diff", "--name-only", "-z", "HEAD"], cancellationToken)
			: RunGit(root, ["ls-files", "-z"], cancellationToken);

		string untracked = RunGit(root, ["ls-files", "--others", "--exclude-standard", "-z"], cancellationToken);

		return
		[
			.. ReadPaths(tracked)
				.Concat(ReadPaths(untracked).Where(static path => !IsBuildOutput(path)))
				.Distinct(PathComparison.Comparer)
				.Where(path => File.Exists(Path.Combine(root, path)))
				.Order(StringComparer.Ordinal),
		];
	}

	/// <summary>Reads each file's line endings in the index and the working tree, one NUL-ended entry each.</summary>
	public static string ReadLineEndings(string root, CancellationToken cancellationToken) =>
		RunGit(root, ["ls-files", "--eol", "-z"], cancellationToken);

	/// <summary>
	/// A line that starts with seven angle brackets marks a conflict or views <c>dotnet format</c> cannot merge.
	/// </summary>
	public static string ReadMarkedFiles(string root, CancellationToken cancellationToken)
	{
		// `[[:space:]]` matches the carriage return before the end of a CRLF line.
		ProcessResult result = ProcessRunner.Run(
			fileName: "git",
			arguments: ["--no-pager", "grep", "--untracked", "-l", "-z", "-I", "-E", "-e", "^(<{7}|>{7})([[:space:]]|$)"],
			workingDirectory: root,
			echo: null,
			cancellationToken
		);

		// Exit code 1 means no line matched, and any code above it is a search that failed.
		return result.ExitCode switch
		{
			0 => result.Output,
			1 => string.Empty,
			_ => throw new FormatRunException($"git grep could not search for merge markers (exit code {result.ExitCode}): {result.Error.Trim()}"),
		};
	}

	/// <summary>
	/// <c>-z</c> ends each path with a NUL character, so a path outside ASCII is neither quoted nor escaped.
	/// </summary>
	public static IEnumerable<string> ReadPaths(string output) => output.Split('\0', StringSplitOptions.RemoveEmptyEntries);

	private static bool IsBuildOutput(string path)
	{
		string[] segments = path.Split('/');
		return segments[..^1].Any(static segment => s_buildOutputDirectories.Contains(segment, PathComparison.Comparer));
	}

	private static ProcessResult RunTopLevel(string directory, CancellationToken cancellationToken)
	{
		return ProcessRunner.Run(
			fileName: "git",
			arguments: ["rev-parse", "--show-toplevel", "--show-cdup"],
			workingDirectory: directory,
			echo: null,
			cancellationToken
		);
	}

	/// <summary>The root git names for the directory, and the path up to it, such as <c>../../</c>.</summary>
	private static (string Root, string Up) ReadTopLevel(ProcessResult result)
	{
		string[] lines = [.. result.Output.Split('\n').Select(static line => line.TrimEnd('\r'))];
		return (lines[0], lines.Length > 1 ? lines[1] : string.Empty);
	}

	private static bool NamesRoot(ProcessResult result, string root) => result.ExitCode == 0 && ReadTopLevel(result).Root == root;

	private static bool HasCommit(string root, CancellationToken cancellationToken)
	{
		ProcessResult result = ProcessRunner.Run(
			fileName: "git",
			arguments: ["rev-parse", "--verify", "--quiet", "HEAD"],
			workingDirectory: root,
			echo: null,
			cancellationToken
		);

		return result.ExitCode == 0;
	}

	private static string RunGit(string root, IEnumerable<string> arguments, CancellationToken cancellationToken)
	{
		string[] list = [.. arguments];
		ProcessResult result = ProcessRunner.Run(fileName: "git", arguments: list, workingDirectory: root, echo: null, cancellationToken);

		if (result.ExitCode != 0)
			throw new FormatRunException($"git {string.Join(' ', list)} failed with exit code {result.ExitCode}: {result.Error.Trim()}");

		return result.Output;
	}
}
