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

		RunGit(directory.Root, "init", "--quiet");
		RunGit(directory.Root, "add", "Staged.cs");

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

		RunGit(directory.Root, "init", "--quiet");
		RunGit(directory.Root, "add", ".");

		RunGit(
			directory.Root,
			"-c",
			"user.name=Stanza Tests",
			"-c",
			"user.email=tests@stanza.invalid",
			"commit",
			"--quiet",
			"-m",
			"Commit the files the test changes"
		);

		RunGit(directory.Root, "mv", "Old.cs", "Renamed.cs");
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
	public void TheRootKeepsTheSpellingOfALinkTheDirectoryIsReadThrough()
	{
		using TempDirectory directory = new(editorConfig: false);
		_ = directory.Write("repo/src/A.cs", "namespace A;\n");

		string repository = Path.Combine(directory.Root, "repo");
		string linked = Path.Combine(directory.Root, "linked");
		string below = Path.Combine(directory.Root, "below");

		RunGit(repository, "init", "--quiet");

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

		RunGit(directory.Root, "init", "--quiet");

		string output = GitRepository.ReadMarkedFiles(directory.Root, CancellationToken.None);

		Assert.AreSequenceEqual(["Café notes.txt"], FormatChecks.CollectMarkedFiles(output));
	}

	[TestMethod]
	public void AWrongLineEndingInAFileWithANonAsciiNameIsNamedWithoutQuotes()
	{
		using TempDirectory directory = new(editorConfig: false);
		_ = directory.Write(".gitattributes", "*.txt text eol=lf\n");
		_ = directory.Write("Café notes.txt", "notes\n");
		_ = directory.Write("Fine.txt", "fine\n");

		RunGit(directory.Root, "init", "--quiet");
		RunGit(directory.Root, "add", ".");

		_ = directory.Write("Café notes.txt", "notes\r\n");

		string listing = GitRepository.ReadLineEndings(directory.Root, CancellationToken.None);
		ImmutableArray<string> wrong = FormatChecks.CollectWrongLineEndings(listing);

		Assert.HasCount(1, wrong);
		Assert.EndsWith("\tCafé notes.txt", wrong[0]);
	}

	[TestMethod]
	public void AnUntrackedFileBelowABinOrObjDirectoryIsBuildOutputAndNotAChange()
	{
		using TempDirectory directory = new(editorConfig: false);
		_ = directory.Write("bin/Tracked.cs", "// tracked\n");

		RunGit(directory.Root, "init", "--quiet");
		RunGit(directory.Root, "add", ".");

		RunGit(
			directory.Root,
			"-c",
			"user.name=Stanza Tests",
			"-c",
			"user.email=tests@stanza.invalid",
			"commit",
			"--quiet",
			"-m",
			"Commit a file below bin"
		);

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

	private static void RunGit(string root, params string[] arguments)
	{
		ProcessResult result = ProcessRunner.Run(
			fileName: "git",
			arguments,
			workingDirectory: root,
			echo: null,
			cancellationToken: CancellationToken.None
		);

		Assert.AreEqual(0, result.ExitCode, result.Error);
	}
}
