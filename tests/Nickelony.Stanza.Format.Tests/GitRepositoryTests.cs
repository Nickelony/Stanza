using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Format.Running;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Format.Tests;

[TestClass]
public sealed class GitRepositoryTests
{
	[TestMethod]
	public void BeforeTheFirstCommitTheIndexAndTheUntrackedFilesAreTheChanges()
	{
		using TempDirectory directory = new(editorConfig: false);

		_ = directory.Write("Staged.cs", "class Staged;\n");
		_ = directory.Write("New.cs", "class New;\n");

		TestGit.Init(directory.Root);
		TestGit.Run(directory.Root, "add", "Staged.cs");

		Assert.AreSequenceEqual(
			["New.cs", "Staged.cs"],
			[.. GitRepository.CollectChangedFiles(directory.Root, CancellationToken.None)]
		);
	}

	[TestMethod]
	public void AfterACommitTheChangesAreTheFilesThatDifferFromHeadAndTheUntrackedOnes()
	{
		using TempDirectory directory = new(editorConfig: false);
		_ = directory.Write(".gitignore", "Ignored.cs\n");

		foreach (string name in (string[])["Kept", "Old", "Deleted", "With space", "Café"])
			_ = directory.Write($"{name}.cs", $"// {name}\n");

		TestGit.Init(directory.Root);
		TestGit.CommitAll(directory.Root);

		TestGit.Run(directory.Root, "mv", "Old.cs", "Renamed.cs");
		File.Delete(Path.Combine(directory.Root, "Deleted.cs"));

		_ = directory.Write("With space.cs", "class Changed;\n");
		_ = directory.Write("Café.cs", "class Changed;\n");
		_ = directory.Write("New.cs", "class New;\n");
		_ = directory.Write("Ignored.cs", "class Ignored;\n");

		Assert.AreSequenceEqual(
			["Café.cs", "New.cs", "Renamed.cs", "With space.cs"],
			[.. GitRepository.CollectChangedFiles(directory.Root, CancellationToken.None)]
		);
	}

	[TestMethod]
	public void ARootFileNamedLikeTheRevisionDoesNotMakeGitRefuseTheDiff()
	{
		using TempDirectory directory = new(editorConfig: false);

		_ = directory.Write("HEAD", "a file\n");
		_ = directory.Write("Kept.cs", "// kept\n");

		TestGit.Init(directory.Root);
		TestGit.CommitAll(directory.Root);

		_ = directory.Write("Kept.cs", "class Changed;\n");

		Assert.AreSequenceEqual(
			["Kept.cs"],
			[.. GitRepository.CollectChangedFiles(directory.Root, CancellationToken.None)]
		);
	}

	[TestMethod]
	public void TheChangesSinceARefAreTheFilesTheBranchChangedAfterItLeftTheRef()
	{
		using TempDirectory directory = new(editorConfig: false);

		_ = directory.Write(".gitignore", "Ignored.cs\n");
		_ = directory.Write("Both.cs", "// both\n");
		_ = directory.Write("Tracked.cs", "// tracked\n");

		TestGit.Init(directory.Root);

		TestGit.CommitAll(directory.Root);

		TestGit.Run(directory.Root, "branch", "trunk");
		TestGit.Run(directory.Root, "checkout", "--quiet", "-b", "feature");

		_ = directory.Write("Both.cs", "// both, on the branch\n");
		_ = directory.Write("Feature.cs", "// feature\n");

		TestGit.CommitAll(directory.Root);

		// Trunk moves on, and its files are not the branch's changes.
		TestGit.Run(directory.Root, "checkout", "--quiet", "trunk");

		_ = directory.Write("Trunk.cs", "// trunk\n");

		TestGit.CommitAll(directory.Root);

		TestGit.Run(directory.Root, "checkout", "--quiet", "feature");

		_ = directory.Write("Tracked.cs", "// tracked, edited\n");
		_ = directory.Write("New.cs", "class New;\n");
		_ = directory.Write("Ignored.cs", "class Ignored;\n");

		Assert.AreSequenceEqual(
			["Both.cs", "Feature.cs", "New.cs", "Tracked.cs"],

			[
				.. GitRepository.CollectChangedFilesSince(
					directory.Root,
					"trunk",
					CancellationToken.None
				),
			]
		);

		// A branch, a tag, an abbreviated commit and a relative ref all name the same point.
		TestGit.Run(directory.Root, "tag", "start", "trunk~1");

		string abbreviated = TestGit.Read(directory.Root, "rev-parse", "--short", "trunk~1");

		foreach (string reference in (string[])["start", abbreviated, "trunk~1", "feature~1"])
		{
			Assert.AreSequenceEqual(
				["Both.cs", "Feature.cs", "New.cs", "Tracked.cs"],

				[
					.. GitRepository.CollectChangedFilesSince(
						directory.Root,
						reference,
						CancellationToken.None
					),
				],

				reference
			);
		}
	}

	[TestMethod]
	public void TheChangesSinceARefAreTheSameWhileHeadIsDetached()
	{
		using TempDirectory directory = new(editorConfig: false);
		_ = directory.Write("Base.cs", "// base\n");

		TestGit.Init(directory.Root);

		TestGit.CommitAll(directory.Root);

		TestGit.Run(directory.Root, "branch", "trunk");

		_ = directory.Write("Topic.cs", "// topic\n");

		TestGit.CommitAll(directory.Root);

		TestGit.Run(directory.Root, "checkout", "--quiet", "--detach");

		Assert.AreSequenceEqual(
			["Topic.cs"],

			[
				.. GitRepository.CollectChangedFilesSince(
					directory.Root,
					"trunk",
					CancellationToken.None
				),
			]
		);
	}

	[TestMethod]
	public void ARenamedFileCountsUnderItsNewNameAndNotItsOld()
	{
		using TempDirectory directory = new(editorConfig: false);

		_ = directory.Write(
			"Old.cs",
			"// a file that is long enough for git to see it was renamed\n"
		);

		_ = directory.Write(
			"Old café.cs",
			"// a second file that is long enough for git to see it was renamed\n"
		);

		_ = directory.Write("Kept.cs", "// kept\n");

		TestGit.Init(directory.Root);

		TestGit.CommitAll(directory.Root);

		TestGit.Run(directory.Root, "mv", "Old.cs", "Renamed.cs");
		TestGit.Run(directory.Root, "mv", "Old café.cs", "Neu café.cs");

		TestGit.CommitAll(directory.Root);

		TestGit.Run(directory.Root, "mv", "Kept.cs", "Moved on.cs");

		Assert.AreSequenceEqual(
			["Moved on.cs", "Neu café.cs", "Renamed.cs"],

			[
				.. GitRepository.CollectChangedFilesSince(
					directory.Root,
					"HEAD~1",
					CancellationToken.None
				),
			]
		);
	}

	[TestMethod]
	public void ADeletedFileIsNotAChangeSinceARef()
	{
		using TempDirectory directory = new(editorConfig: false);

		_ = directory.Write("Gone.cs", "// gone\n");
		_ = directory.Write("Stays.cs", "// stays\n");

		TestGit.Init(directory.Root);

		TestGit.CommitAll(directory.Root);

		File.Delete(Path.Combine(directory.Root, "Gone.cs"));
		_ = directory.Write("Stays.cs", "// stays, edited\n");

		Assert.AreSequenceEqual(
			["Stays.cs"],

			[
				.. GitRepository.CollectChangedFilesSince(
					directory.Root,
					"HEAD",
					CancellationToken.None
				),
			]
		);
	}

	[TestMethod]
	public void ARefThatNamesNoCommitIsRefusedByName()
	{
		using TempDirectory directory = new(editorConfig: false);
		_ = directory.Write("A.cs", "// a\n");

		TestGit.Init(directory.Root);

		TestGit.CommitAll(directory.Root);

		FormatRunException exception = Assert.ThrowsExactly<FormatRunException>(() =>
			_ = GitRepository.CollectChangedFilesSince(
				directory.Root,
				"origin/main",
				CancellationToken.None
			)
		);

		Assert.AreEqual(
			"'origin/main' names no commit in this repository. Fetch it, or name a branch, tag or commit that exists.",
			exception.Message
		);
	}

	[TestMethod]
	public void ARefThatStartsWithADashIsNeverHandedToGit()
	{
		using TempDirectory directory = new(editorConfig: false);
		_ = directory.Write("A.cs", "// a\n");

		TestGit.Init(directory.Root);

		TestGit.CommitAll(directory.Root);

		FormatRunException exception = Assert.ThrowsExactly<FormatRunException>(() =>
			_ = GitRepository.CollectChangedFilesSince(
				directory.Root,
				"--all",
				CancellationToken.None
			)
		);

		Assert.Contains("starts with a dash", exception.Message);
	}

	[TestMethod]
	public void BeforeTheFirstCommitThereIsNothingToCompareWith()
	{
		using TempDirectory directory = new(editorConfig: false);
		_ = directory.Write("A.cs", "// a\n");

		TestGit.Init(directory.Root);

		FormatRunException exception = Assert.ThrowsExactly<FormatRunException>(() =>
			_ = GitRepository.CollectChangedFilesSince(
				directory.Root,
				"HEAD",
				CancellationToken.None
			)
		);

		Assert.AreEqual(
			"HEAD names no commit, so there is no ref to compare the working tree with.",
			exception.Message
		);
	}

	[TestMethod]
	public void AShallowCloneThatLacksTheRefsHistoryAsksForMoreHistory()
	{
		using TempDirectory directory = new(editorConfig: false);
		_ = directory.Write("origin/A.cs", "// a\n");

		string origin = Path.Combine(directory.Root, "origin");

		TestGit.Init(origin);

		TestGit.CommitAll(origin);

		TestGit.Run(origin, "branch", "trunk");

		_ = directory.Write("origin/B.cs", "// b\n");

		TestGit.CommitAll(origin);

		_ = directory.Write("origin/C.cs", "// c\n");

		TestGit.CommitAll(origin);

		string clone = Path.Combine(directory.Root, "clone");

		TestGit.Run(
			directory.Root,
			"clone",
			"--quiet",
			"--depth",
			"1",
			new Uri(origin).AbsoluteUri,
			clone
		);

		// The parent commit was not fetched.
		FormatRunException missing = Assert.ThrowsExactly<FormatRunException>(() =>
			_ = GitRepository.CollectChangedFilesSince(clone, "HEAD~1", CancellationToken.None)
		);

		Assert.StartsWith("'HEAD~1' names no commit in this repository.", missing.Message);
		Assert.Contains("shallow clone", missing.Message);
		Assert.Contains("git fetch --unshallow", missing.Message);

		// The trunk is fetched, but without the history that joins it to HEAD.
		TestGit.Run(clone, "fetch", "--quiet", "--depth", "1", "origin", "trunk:trunk");

		FormatRunException unjoined = Assert.ThrowsExactly<FormatRunException>(() =>
			_ = GitRepository.CollectChangedFilesSince(clone, "trunk", CancellationToken.None)
		);

		Assert.StartsWith("'trunk' and HEAD share no history", unjoined.Message);
		Assert.Contains("shallow clone", unjoined.Message);

		// With the history back, the same ref reads.
		TestGit.Run(clone, "fetch", "--quiet", "--unshallow");

		Assert.AreSequenceEqual(
			["B.cs", "C.cs"],
			[.. GitRepository.CollectChangedFilesSince(clone, "trunk", CancellationToken.None)]
		);
	}

	[TestMethod]
	public void ARepositoryUnderAPathWithASpaceAndANonAsciiNameReadsItsChanges()
	{
		using TempDirectory directory = new(editorConfig: false);
		_ = directory.Write("my repo é/Base.cs", "// base\n");

		string root = Path.Combine(directory.Root, "my repo é");

		TestGit.Init(root);

		TestGit.CommitAll(root);

		_ = directory.Write("my repo é/src/Neu é ü.cs", "class N;\n");

		TestGit.CommitAll(root);

		_ = directory.Write("my repo é/Base.cs", "// base, edited\n");

		Assert.AreSequenceEqual(
			["Base.cs", "src/Neu é ü.cs"],
			[.. GitRepository.CollectChangedFilesSince(root, "HEAD~1", CancellationToken.None)]
		);
	}

	[TestMethod]
	public void TheRootKeepsTheSpellingOfALinkTheDirectoryIsReadThrough()
	{
		using TempDirectory directory = new(editorConfig: false);
		_ = directory.Write("repo/src/A.cs", "namespace A;\n");

		string repository = Path.Combine(directory.Root, "repo");
		string linked = Path.Combine(directory.Root, "linked");
		string below = Path.Combine(directory.Root, "below");

		TestGit.Init(repository);

		bool created =
			TempDirectory.TryCreateDirectoryLink(linked, repository)
			&& TempDirectory.TryCreateDirectoryLink(below, Path.Combine(repository, "src"));

		if (!created)
			Assert.Inconclusive("This system cannot create a directory link.");

		Assert.AreEqual(
			linked,
			GitRepository.ReadRoot(Path.Combine(linked, "src"), CancellationToken.None)
		);

		// A link to a directory below the root has no spelling of the root, so the root is git's own.
		string root = GitRepository.ReadRoot(below, CancellationToken.None);
		Assert.IsTrue(Directory.Exists(Path.Combine(root, ".git")), root);
	}

	[TestMethod]
	public void ADirectoryOutsideAnyRepositoryIsRefusedWithGitsOwnMessage()
	{
		using TempDirectory directory = new(editorConfig: false);

		FormatRunException exception = Assert.ThrowsExactly<FormatRunException>(() =>
			_ = GitRepository.ReadRoot(directory.Root, CancellationToken.None)
		);

		Assert.Contains("is not in a git repository: fatal:", exception.Message);
	}

	[TestMethod]
	public void AMergeMarkerInAFileWithANonAsciiNameIsNamedWithoutQuotes()
	{
		using TempDirectory directory = new(editorConfig: false);

		_ = directory.Write("Café notes.txt", "<<<<<<< ours\n");
		_ = directory.Write("Clean.txt", "clean\n");

		TestGit.Init(directory.Root);

		string output = GitRepository.ReadMarkedFiles(directory.Root, CancellationToken.None);

		Assert.AreSequenceEqual(["Café notes.txt"], FormatChecks.CollectMarkedFiles(output));
	}

	[TestMethod]
	[DoNotParallelize]
	[DataRow("color.ui")]
	[DataRow("color.grep")]
	public void AMergeMarkerStaysInScopeWhenTheUsersGitConfigAsksForColorAlways(string colorKey)
	{
		using TempDirectory directory = new(editorConfig: false);

		_ = directory.Write("Clean.txt", "clean\n");
		TestGit.Init(directory.Root);
		TestGit.CommitAll(directory.Root);

		_ = directory.Write("Conflict.txt", "<<<<<<< ours\n");

		string? count = Environment.GetEnvironmentVariable("GIT_CONFIG_COUNT");
		string? key = Environment.GetEnvironmentVariable("GIT_CONFIG_KEY_0");
		string? value = Environment.GetEnvironmentVariable("GIT_CONFIG_VALUE_0");

		try
		{
			// With color always on, git wraps each path of a pipe in escape codes, and a path with them is in no scope.
			Environment.SetEnvironmentVariable("GIT_CONFIG_COUNT", "1");
			Environment.SetEnvironmentVariable("GIT_CONFIG_KEY_0", colorKey);
			Environment.SetEnvironmentVariable("GIT_CONFIG_VALUE_0", "always");

			string output = GitRepository.ReadMarkedFiles(directory.Root, CancellationToken.None);

			Assert.AreSequenceEqual(
				["Conflict.txt"],
				FormatChecks.CollectMarkedFiles(output, ["Conflict.txt"])
			);
		}
		finally
		{
			Environment.SetEnvironmentVariable("GIT_CONFIG_COUNT", count);
			Environment.SetEnvironmentVariable("GIT_CONFIG_KEY_0", key);
			Environment.SetEnvironmentVariable("GIT_CONFIG_VALUE_0", value);
		}
	}

	[TestMethod]
	public void AWrongLineEndingInAFileWithANonAsciiNameIsNamedWithoutQuotes()
	{
		using TempDirectory directory = new(editorConfig: false);

		_ = directory.Write(".gitattributes", "*.txt text eol=lf\n");
		_ = directory.Write("Café notes.txt", "notes\n");
		_ = directory.Write("Fine.txt", "fine\n");

		TestGit.Init(directory.Root);
		TestGit.Run(directory.Root, "add", ".");

		_ = directory.Write("Café notes.txt", "notes\r\n");

		string listing = GitRepository.ReadLineEndings(directory.Root, CancellationToken.None);
		ImmutableArray<string> wrong = FormatChecks.CollectWrongLineEndings(listing);

		Assert.HasCount(1, wrong);
		Assert.EndsWith("\tCafé notes.txt", wrong[0]);
	}

	[TestMethod]
	public void AWrongLineEndingInAnUntrackedFileIsNamed()
	{
		using TempDirectory directory = new(editorConfig: false);

		_ = directory.Write(".gitattributes", "*.cs text eol=lf\n");

		TestGit.Init(directory.Root);
		TestGit.Run(directory.Root, "add", ".");

		_ = directory.Write("New.cs", "class New { }\r\n");

		string listing = GitRepository.ReadLineEndings(directory.Root, CancellationToken.None);
		ImmutableArray<string> wrong = FormatChecks.CollectWrongLineEndings(listing);

		Assert.HasCount(1, wrong);
		Assert.EndsWith("\tNew.cs", wrong[0]);
	}

	[TestMethod]
	public void TheLineEndingsOfUntrackedBuildOutputAreLeftOutAndThoseOfATrackedFileThereAreRead()
	{
		using TempDirectory directory = new(editorConfig: false);

		_ = directory.Write(".gitattributes", "*.cs text eol=lf\n");
		_ = directory.Write("bin/Tracked.cs", "// tracked\n");

		TestGit.Init(directory.Root);
		TestGit.Run(directory.Root, "add", ".");

		_ = directory.Write("bin/Tracked.cs", "// tracked\r\n");
		_ = directory.Write("obj/Debug/App.AssemblyInfo.cs", "// generated\r\n");
		_ = directory.Write("src/A/bin/Other.cs", "// output\r\n");

		string listing = GitRepository.ReadLineEndings(directory.Root, CancellationToken.None);
		ImmutableArray<string> wrong = FormatChecks.CollectWrongLineEndings(listing);

		Assert.HasCount(1, wrong);
		Assert.EndsWith("\tbin/Tracked.cs", wrong[0]);
	}

	[TestMethod]
	public void AnUntrackedFileBelowABinOrObjDirectoryIsBuildOutputAndNotAChange()
	{
		using TempDirectory directory = new(editorConfig: false);
		_ = directory.Write("bin/Tracked.cs", "// tracked\n");

		TestGit.Init(directory.Root);
		TestGit.CommitAll(directory.Root, message: "Commit a file below bin");

		_ = directory.Write("bin/Tracked.cs", "// changed\n");
		_ = directory.Write("obj/Debug/net8.0/App.AssemblyInfo.cs", "// generated\n");
		_ = directory.Write("src/A/obj/Stale.cs", "// stale\n");
		_ = directory.Write("src/A/bin/Debug/Other.cs", "// output\n");
		_ = directory.Write("src/A/Binary.cs", "// a source\n");

		Assert.AreSequenceEqual(
			["bin/Tracked.cs", "src/A/Binary.cs"],
			[.. GitRepository.CollectChangedFiles(directory.Root, CancellationToken.None)]
		);
	}
}
