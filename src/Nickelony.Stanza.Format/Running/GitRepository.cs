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
	private const string ShallowAdvice =
		"This is a shallow clone, so fetch more history first, such as with 'git fetch --unshallow' or a fetch depth of 0 in CI.";

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
			? RunGit(root, ["diff", "--name-only", "-z", "HEAD", "--"], cancellationToken)
			: RunGit(root, ["ls-files", "-z"], cancellationToken);

		return CollectWithUntracked(root, tracked, cancellationToken);
	}

	/// <summary>The files that differ from where the branch left the ref, and the untracked ones.</summary>
	/// <remarks>
	/// The base is the merge base of the ref and HEAD, and a rename counts under its new name.
	/// </remarks>
	public static ImmutableArray<string> CollectChangedFilesSince(string root, string reference, CancellationToken cancellationToken)
	{
		string mergeBase = ReadMergeBase(root, reference, cancellationToken);
		string tracked = RunGit(root, ["diff", "--name-only", "--no-renames", "-z", mergeBase, "--"], cancellationToken);

		return CollectWithUntracked(root, tracked, cancellationToken);
	}

	/// <summary>Gives the error of a ref git would read as an option, or <see langword="null"/>.</summary>
	public static string? DescribeOptionLikeRef(string reference) =>
		reference.StartsWith('-') ? $"the ref '{reference}' starts with a dash, which git would read as an option." : null;

	/// <summary>Reads the line endings of each tracked and untracked file, one NUL-ended entry each.</summary>
	/// <remarks>
	/// An untracked file below <c>bin</c> or <c>obj</c> is build output, so it is left out.
	/// </remarks>
	public static string ReadLineEndings(string root, CancellationToken cancellationToken)
	{
		string listing = RunGit(root, ["ls-files", "--eol", "-z", "--cached", "--others", "--exclude-standard"], cancellationToken);
		return string.Join('\0', ReadPaths(listing).Where(static entry => !IsUntrackedBuildOutput(entry)));
	}

	/// <summary>
	/// A line that starts with seven angle brackets marks a conflict or views <c>dotnet format</c> cannot merge.
	/// </summary>
	public static string ReadMarkedFiles(string root, CancellationToken cancellationToken)
	{
		// `[[:space:]]` matches the carriage return before the end of a CRLF line.
		// `--no-color` is needed too, as a `color.grep` of the user's beats the `color.ui` that RunGitQuietly sets.
		ProcessResult result = RunGitQuietly(
			root,
			["--no-pager", "grep", "--no-color", "--untracked", "-l", "-z", "-I", "-E", "-e", "^(<{7}|>{7})([[:space:]]|$)"],
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

	private static ImmutableArray<string> CollectWithUntracked(string root, string tracked, CancellationToken cancellationToken)
	{
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

	/// <summary>Names the commit where the branch left the ref, or says why a ref cannot be compared with.</summary>
	private static string ReadMergeBase(string root, string reference, CancellationToken cancellationToken)
	{
		if (DescribeOptionLikeRef(reference) is { } problem)
			throw new FormatRunException(problem);

		if (!HasCommit(root, cancellationToken))
			throw new FormatRunException("HEAD names no commit, so there is no ref to compare the working tree with.");

		ProcessResult resolved = RunGitQuietly(root, ["rev-parse", "--verify", "--quiet", $"{reference}^{{commit}}"], cancellationToken);

		if (resolved.ExitCode != 0)
		{
			throw new FormatRunException(
				$"'{reference}' names no commit in this repository. "
					+ (IsShallow(root, cancellationToken) ? ShallowAdvice : "Fetch it, or name a branch, tag or commit that exists.")
			);
		}

		ProcessResult found = RunGitQuietly(root, ["merge-base", resolved.Output.Trim(), "HEAD"], cancellationToken);

		// Exit code 1 means the two commits have no common ancestor, which a clone that cut the history off causes.
		return found.ExitCode switch
		{
			0 => found.Output.Trim(),

			1 => throw new FormatRunException(
				$"'{reference}' and HEAD share no history. "
					+ (IsShallow(root, cancellationToken) ? ShallowAdvice : "Name a ref that the current branch grew from.")
			),

			_ => throw new FormatRunException($"git merge-base failed with exit code {found.ExitCode}: {found.Error.Trim()}"),
		};
	}

	private static bool IsShallow(string root, CancellationToken cancellationToken)
	{
		ProcessResult result = RunGitQuietly(root, ["rev-parse", "--is-shallow-repository"], cancellationToken);
		return result.ExitCode == 0 && result.Output.Trim() == "true";
	}

	/// <summary>Runs git with color off, as <c>color.ui=always</c> colors even a piped path.</summary>
	private static ProcessResult RunGitQuietly(string directory, string[] arguments, CancellationToken cancellationToken)
	{
		string[] uncolored = ["-c", "color.ui=never", .. arguments];
		return ProcessRunner.Run(fileName: "git", arguments: uncolored, workingDirectory: directory, echo: null, cancellationToken);
	}

	private static bool IsBuildOutput(string path)
	{
		string[] segments = path.Split('/');
		return segments[..^1].Any(static segment => s_buildOutputDirectories.Contains(segment, PathComparison.Comparer));
	}

	/// <summary>An untracked entry opens with <c>i/</c> and a space, as it has no index ending.</summary>
	private static bool IsUntrackedBuildOutput(string entry) =>
		entry.StartsWith("i/ ", StringComparison.Ordinal) && IsBuildOutput(entry[(entry.IndexOf('\t') + 1)..]);

	private static ProcessResult RunTopLevel(string directory, CancellationToken cancellationToken) =>
		RunGitQuietly(directory, ["rev-parse", "--show-toplevel", "--show-cdup"], cancellationToken);

	/// <summary>The root git names for the directory, and the path up to it, such as <c>../../</c>.</summary>
	private static (string Root, string Up) ReadTopLevel(ProcessResult result)
	{
		string[] lines = [.. result.Output.Split('\n').Select(static line => line.TrimEnd('\r'))];
		return (lines[0], lines.Length > 1 ? lines[1] : string.Empty);
	}

	private static bool NamesRoot(ProcessResult result, string root) => result.ExitCode == 0 && ReadTopLevel(result).Root == root;

	private static bool HasCommit(string root, CancellationToken cancellationToken) =>
		RunGitQuietly(root, ["rev-parse", "--verify", "--quiet", "HEAD"], cancellationToken).ExitCode == 0;

	private static string RunGit(string root, IEnumerable<string> arguments, CancellationToken cancellationToken)
	{
		string[] list = [.. arguments];
		ProcessResult result = RunGitQuietly(root, list, cancellationToken);

		if (result.ExitCode != 0)
			throw new FormatRunException($"git {string.Join(' ', list)} failed with exit code {result.ExitCode}: {result.Error.Trim()}");

		return result.Output;
	}
}
