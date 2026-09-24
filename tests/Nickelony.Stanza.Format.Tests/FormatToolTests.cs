using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Format.Cli;
using Nickelony.Stanza.Format.Running;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Format.Tests;

[TestClass]
public sealed class FormatToolTests
{
	[TestMethod]
	public void AMultiLineErrorIsWrittenAsOneErrorLine()
	{
		using TempDirectory directory = new();

		(int exitCode, _, string error) = Run(directory.Root, "missing\ndirectory");

		Assert.AreEqual(2, exitCode);
		Assert.AreEqual("error: 'missing directory' does not exist.", error.TrimEnd());
	}

	[TestMethod]
	public void AControlCharacterInAnErrorIsWrittenAsItsCode()
	{
		using TempDirectory directory = new();

		(int exitCode, _, string error) = Run(directory.Root, "missing" + (char)0x1B + "directory");

		Assert.AreEqual(2, exitCode);
		Assert.AreEqual(@"error: 'missing\x1Bdirectory' does not exist.", error.TrimEnd());
	}

	[TestMethod]
	public void ACommandThatFailsEndsTheCheckWithoutPointingAtAWriteRun()
	{
		using TempDirectory directory = new(editorConfig: false);

		// dotnet format cannot restore a missing project, so it fails rather than finding a change.
		_ = directory.Write(
			"App.slnx",
			"""<Solution><Project Path="Missing/Missing.csproj" /></Solution>"""
		);

		RunGit(directory.Root, "init", "--quiet");

		(int exitCode, _, string error) = Run(directory.Root, "--check", "--layout", "none");

		Assert.AreEqual(2, exitCode, error);
		Assert.Contains("\nerror: Analyzers could not run", error);
		Assert.DoesNotContain("--all", error);
	}

	[TestMethod]
	public void ACheckThatFindsADiagnosticSaysToFixByHandWhatAWriteRunLeaves()
	{
		using TempDirectory directory = new();

		// CA1041 has no fix, so a write run leaves it and the next check fails on it again.
		_ = directory.Write("App.slnx", """<Solution><Project Path="A/A.csproj" /></Solution>""");

		_ = directory.Write(
			"A/A.csproj",

			"""
			<Project Sdk="Microsoft.NET.Sdk">
				<PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
			</Project>
			"""
		);

		_ = directory.Write(
			"A/Old.cs",

			"""
			namespace A;

			public static class Old
			{
				[System.Obsolete]
				public static int Value() => 1;
			}
			"""
		);

		RunGit(directory.Root, "init", "--quiet");

		(int exitCode, _, string error) = Run(
			directory.Root,
			"--check",
			"--layout",
			"none",
			"App.slnx"
		);

		Assert.AreEqual(1, exitCode, error);

		Assert.Contains(
			"Run stanza-format --all --layout none App.slnx, then fix by hand each diagnostic it leaves",
			error
		);
	}

	[TestMethod]
	public void AFailedGitCheckNamesItsOwnFixRatherThanARunThatCannotMakeIt()
	{
		using TempDirectory directory = new(editorConfig: false);
		_ = directory.Write("App.slnx", "<Solution />\n");
		_ = directory.Write(".gitattributes", "*.txt text eol=lf\n");
		_ = directory.Write("notes.txt", "notes\n");

		RunGit(directory.Root, "init", "--quiet");
		RunGit(directory.Root, "add", ".");

		// A merge marker on a CRLF line under an LF pin fails both git checks and no other step.
		_ = directory.Write("notes.txt", "<<<<<<< ours\r\n");

		(int exitCode, _, string error) = Run(directory.Root, "--check", "--layout", "none");

		Assert.AreEqual(1, exitCode, error);

		Assert.Contains(
			"These steps would change a file or hold one to fix by hand:\n  - Line endings\n  - Merge markers\n",
			error
		);

		Assert.Contains("Convert each file under Line endings", error);
		Assert.Contains("Resolve the conflict in each file under Merge markers.", error);
		Assert.DoesNotContain("--all", error);
	}

	[TestMethod]
	public void AManifestThatPinsAnOldCSharpierRefusesTheCSharpierLayout()
	{
		using TempDirectory directory = new(editorConfig: false);
		_ = directory.Write("App.slnx", "<Solution />\n");

		_ = directory.Write(
			".config/dotnet-tools.json",

			"""
			{
			  "version": 1,
			  "tools": { "csharpier": { "version": "0.30.6", "commands": ["csharpier"] } }
			}
			"""
		);

		RunGit(directory.Root, "init", "--quiet");

		string[][] refused =
		[
			["--check"],
			["--layout", "csharpier"],
		];

		foreach (string[] args in refused)
		{
			(int exitCode, _, string error) = Run(directory.Root, args);

			Assert.AreEqual(2, exitCode, error);
			Assert.Contains("pins CSharpier 0.30.6", error);
			Assert.Contains("--layout whitespace", error);
		}

		(int whitespaceCode, _, string whitespaceError) = Run(
			directory.Root,
			"--check",
			"--layout",
			"whitespace"
		);

		Assert.AreEqual(0, whitespaceCode, whitespaceError);
	}

	[TestMethod]
	public void TheCSharpierLayoutWithoutCSharpierInTheManifestIsARunFailureNotAUsageError()
	{
		using TempDirectory directory = new(editorConfig: false);
		_ = directory.Write("App.slnx", "<Solution />\n");

		RunGit(directory.Root, "init", "--quiet");

		(int exitCode, _, string error) = Run(directory.Root, "--check", "--layout", "csharpier");

		Assert.AreEqual(2, exitCode, error);
		Assert.Contains("needs CSharpier in the tool manifest", error);
		Assert.DoesNotContain("--help", error);
	}

	[TestMethod]
	public void ARunOutsideAnyRepositoryIsRefusedWithGitsOwnMessage()
	{
		using TempDirectory directory = new(editorConfig: false);
		_ = directory.Write("App.slnx", "<Solution />\n");

		(int exitCode, _, string error) = Run(directory.Root);

		Assert.AreEqual(2, exitCode, error);
		Assert.Contains("is not in a git repository: fatal:", error);
	}

	[TestMethod]
	public void ASolutionBelowTheRootReadsTheManifestNearestIt()
	{
		using TempDirectory directory = new(editorConfig: false);
		_ = directory.Write("sub/App.slnx", "<Solution />\n");

		_ = directory.Write(
			"sub/.config/dotnet-tools.json",

			"""
			{
			  "version": 1,
			  "isRoot": true,
			  "tools": { "csharpier": { "version": "0.30.6", "commands": ["csharpier"] } }
			}
			"""
		);

		RunGit(directory.Root, "init", "--quiet");

		(int exitCode, _, string error) = Run(directory.Root, "--check", "sub/App.slnx");

		Assert.AreEqual(2, exitCode, error);
		Assert.Contains("pins CSharpier 0.30.6", error);
	}

	[TestMethod]
	public void AManifestThatIsNotJsonIsReportedWithTheFileItIsIn()
	{
		using TempDirectory directory = new(editorConfig: false);
		_ = directory.Write("App.slnx", "<Solution />\n");
		_ = directory.Write(".config/dotnet-tools.json", "{ \"tools\": ");

		RunGit(directory.Root, "init", "--quiet");

		(int exitCode, _, string error) = Run(directory.Root, "--check");

		Assert.AreEqual(2, exitCode, error);
		Assert.StartsWith("error: the tool manifest '", error);
		Assert.Contains("dotnet-tools.json' is not valid JSON", error);
		Assert.DoesNotContain("JsonException", error);
	}

	[TestMethod]
	public void ADefaultRunNamesTheChangedSourcesItLeavesOutInsteadOfCountingZero()
	{
		using TempDirectory directory = new(editorConfig: false);
		_ = directory.Write("src/App.slnx", "<Solution />\n");

		RunGit(directory.Root, "init", "--quiet");
		RunGit(directory.Root, "add", ".");

		Commit(directory.Root);

		_ = directory.Write("Loose/L.cs", "class L;\n");

		(int exitCode, string output, string error) = Run(
			directory.Root,
			"--layout",
			"none",
			"src"
		);

		Assert.AreEqual(0, exitCode, error);
		Assert.Contains("==> Format 0 changed file(s) of src/App.slnx, laid out by none", output);

		Assert.Contains(
			"Skipped 1 changed source(s) outside the solution's directory that no project compiles: Loose/L.cs.",
			output
		);

		Assert.Contains("No changed source, project or markup file to format.", output);
	}

	[TestMethod]
	public void ADefaultRunCountsAChangedSourceOutsideTheSolutionsDirectoryThatAProjectCompiles()
	{
		using TempDirectory directory = new(editorConfig: false);

		_ = directory.Write(
			"src/App.slnx",
			"""<Solution><Project Path="A/A.csproj" /></Solution>"""
		);

		_ = directory.Write("Loose/L.cs", "class L;\n");

		_ = directory.Write(
			"src/A/A.csproj",

			"""
			<Project Sdk="Microsoft.NET.Sdk">
				<PropertyGroup>
					<TargetFramework>net8.0</TargetFramework>
					<EnableDefaultCompileItems>false</EnableDefaultCompileItems>
				</PropertyGroup>
				<ItemGroup>
					<Compile Include="../../Loose/L.cs" />
				</ItemGroup>
			</Project>
			"""
		);

		RunGit(directory.Root, "init", "--quiet");
		RunGit(directory.Root, "add", ".");

		Commit(directory.Root);

		_ = directory.Write("Loose/L.cs", "class  L;\n");

		(int exitCode, string output, string error) = Run(
			directory.Root,
			"--layout",
			"none",
			"src"
		);

		Assert.AreEqual(0, exitCode, error);
		Assert.Contains("==> Format 1 changed file(s) of src/App.slnx, laid out by none", output);
		Assert.DoesNotContain("Skipped", output);
	}

	[TestMethod]
	public void AWriteRunFixesTheFilesAndAFollowingCheckPasses()
	{
		using TempDirectory directory = new(editorConfig: false);

		// The IDE style rule that asks for a file-scoped namespace has a fix, which the style step writes.
		_ = directory.Write(
			".editorconfig",
			"root = true\n\n[*.cs]\ncsharp_style_namespace_declarations = file_scoped:suggestion\n"
		);

		_ = directory.Write("App.slnx", """<Solution><Project Path="A/A.csproj" /></Solution>""");

		_ = directory.Write(
			"A/A.csproj",

			"""
			<Project Sdk="Microsoft.NET.Sdk">
				<PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
			</Project>
			"""
		);

		_ = directory.Write("A/C.cs", "namespace A\n{\n\tpublic static class C\n\t{\n\t}\n}\n");

		RunGit(directory.Root, "init", "--quiet");

		(int writeCode, string output, string writeError) = Run(directory.Root, "--layout", "none");

		Assert.AreEqual(0, writeCode, writeError);
		Assert.StartsWith("namespace A;", directory.Read("A/C.cs"));
		Assert.Contains("Pass 1 changed a file, so the step runs again.", output);
		Assert.Contains("Formatting finished. Review the changes and commit them.", output);

		(int checkCode, string checkOutput, string checkError) = Run(
			directory.Root,
			"--check",
			"--layout",
			"none"
		);

		Assert.AreEqual(0, checkCode, checkError);
		Assert.Contains("The working tree is formatted.", checkOutput);
	}

	[TestMethod]
	public void AMergeMarkerFailsAWriteRunWithTheFixOfTheMarkerStep()
	{
		using TempDirectory directory = new(editorConfig: false);
		_ = directory.Write("App.slnx", "<Solution />\n");
		_ = directory.Write("conflict.txt", "<<<<<<< ours\n");

		RunGit(directory.Root, "init", "--quiet");

		(int exitCode, string output, string error) = Run(
			directory.Root,
			"--all",
			"--layout",
			"none"
		);

		Assert.AreEqual(2, exitCode, error);
		Assert.Contains("    conflict.txt", output);
		Assert.DoesNotContain("Formatting finished", output);

		Assert.AreEqual(
			"error: a step found a file to fix by hand:\n"
				+ "  - Merge markers\n"
				+ "Resolve the conflict in each file under Merge markers.\n",

			error
		);
	}

	[TestMethod]
	public void AMergeMarkerStopsAWriteRunBeforeCSharpierFailsOnIt()
	{
		using TempDirectory directory = new(editorConfig: false);
		_ = directory.Write("App.slnx", "<Solution />\n");
		_ = directory.Write(".config/dotnet-tools.json", ReadRepositoryManifest());

		_ = directory.Write(
			"Marked.cs",
			"<<<<<<< ours\nclass A { }\n=======\nclass B { }\n>>>>>>> theirs\n"
		);

		RunGit(directory.Root, "init", "--quiet");

		(int exitCode, string output, string error) = Run(directory.Root);

		Assert.AreEqual(2, exitCode, error);
		Assert.Contains("    Marked.cs", output);
		Assert.DoesNotContain("==> CSharpier", output);

		Assert.AreEqual(
			"error: a step found a file to fix by hand:\n"
				+ "  - Merge markers\n"
				+ "Resolve the conflict in each file under Merge markers.\n",

			error
		);
	}

	[TestMethod]
	public void AFileCSharpierCannotParseEndsTheCheckAsAFailureAndNotAsAChange()
	{
		using TempDirectory directory = new(editorConfig: false);
		_ = directory.Write("App.slnx", "<Solution />\n");
		_ = directory.Write(".config/dotnet-tools.json", ReadRepositoryManifest());
		_ = directory.Write("Bad.cs", "class  { \n");

		RunGit(directory.Root, "init", "--quiet");

		(int exitCode, _, string error) = Run(directory.Root, "--check");

		Assert.AreEqual(2, exitCode, error);

		Assert.StartsWith(
			"error: Source layout (CSharpier, blank lines aside) could not run, as CSharpier reported:",
			error
		);

		Assert.Contains("Bad.cs", error);
	}

	[TestMethod]
	public void ASolutionBelowTheRootRunsItsToolsFromItsDirectoryAndFormatsAFileNamedWithAnAtSign()
	{
		using TempDirectory directory = new(editorConfig: false);
		_ = directory.Write("sub/App.slnx", "<Solution />\n");
		_ = directory.Write("sub/.config/dotnet-tools.json", ReadRepositoryManifest());
		_ = directory.Write("sub/@r.cs", "class  R{}\n");
		_ = directory.Write("sub/A.cs", "class  A{}\n");

		RunGit(directory.Root, "init", "--quiet");

		(int checkCode, string checkOutput, string checkError) = Run(
			directory.Root,
			"--check",
			"sub"
		);

		Assert.AreEqual(1, checkCode, checkError);
		Assert.Contains("    sub/@r.cs", checkOutput);

		(int writeCode, _, string writeError) = Run(directory.Root, "sub");

		Assert.AreEqual(0, writeCode, writeError);
		Assert.AreEqual("class R { }", directory.Read("sub/@r.cs").Trim());
		Assert.AreEqual("class A { }", directory.Read("sub/A.cs").Trim());

		(int againCode, _, string againError) = Run(directory.Root, "--check", "sub");

		Assert.AreEqual(0, againCode, againError);
	}

	private static string ReadRepositoryManifest() =>
		File.ReadAllText(RepositoryPaths.Below(".config", "dotnet-tools.json"));

	private static void Commit(string root)
	{
		RunGit(
			root,
			"-c",
			"user.name=Stanza Tests",
			"-c",
			"user.email=tests@stanza.invalid",
			"commit",
			"--quiet",
			"-m",
			"Commit the files the test changes"
		);
	}

	private static RunResult Run(string workingDirectory, params string[] args)
	{
		using StringWriter output = new();
		using StringWriter error = new();

		int exitCode = FormatTool.Run(
			args,
			output,
			error,
			workingDirectory,
			cancellationToken: CancellationToken.None
		);

		return new(exitCode, output.ToString(), error.ToString().ReplaceLineEndings("\n"));
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
