using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Format.Cli;
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

		TestGit.Init(directory.Root);

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

		TestGit.Init(directory.Root);

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

		TestGit.Init(directory.Root);
		TestGit.Run(directory.Root, "add", ".");

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

		TestGit.Init(directory.Root);

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

		TestGit.Init(directory.Root);

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

		TestGit.Init(directory.Root);

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

		TestGit.Init(directory.Root);

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

		TestGit.Init(directory.Root);
		TestGit.CommitAll(directory.Root);

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

		TestGit.Init(directory.Root);
		TestGit.CommitAll(directory.Root);

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

		TestGit.Init(directory.Root);

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
	public void AWholeRunAndItsCheckPassOnANamespaceThatDiffersFromItsProjectsDirectory()
	{
		using TempDirectory directory = new();

		// The fix of IDE0130 moves the file to the directory its namespace names,
		// which dotnet format cannot apply.
		_ = directory.Write(
			"App.slnx",
			"""<Solution><Project Path="App/App.csproj" /></Solution>"""
		);

		_ = directory.Write(
			"App/App.csproj",

			"""
			<Project Sdk="Microsoft.NET.Sdk">
				<PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
			</Project>
			"""
		);

		const string source = "namespace A;\n\npublic static class C\n{\n}\n";
		_ = directory.Write("App/C.cs", source);

		TestGit.Init(directory.Root);

		(int writeCode, string output, string writeError) = Run(
			directory.Root,
			"--all",
			"--layout",
			"none"
		);

		Assert.AreEqual(0, writeCode, writeError);
		Assert.Contains("Formatting finished.", output);
		Assert.AreEqual(source, directory.Read("App/C.cs"));

		(int checkCode, _, string checkError) = Run(directory.Root, "--check", "--layout", "none");

		Assert.AreEqual(0, checkCode, checkError);
	}

	[TestMethod]
	public void AWriteRunThatDoesNotSettleSaysSoAndFailsAsTheNextCheckWould()
	{
		using TempDirectory directory = new(editorConfig: false);

		// IDE2001 moves an empty loop body below its header, and CSharpier joins it back.
		_ = directory.Write(
			".editorconfig",

			"root = true\n\n[*.cs]\n"
				+ "csharp_style_allow_embedded_statements_on_same_line_experimental = false:warning\n"
		);

		_ = directory.Write(".config/dotnet-tools.json", ReadRepositoryManifest());
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
			"A/C.cs",

			"namespace A;\n\npublic static class C\n{\n\tpublic static void M()\n\t{\n"
				+ "\t\tfor (int i = 0; i < 1; i++) { }\n\t}\n}\n"
		);

		TestGit.Init(directory.Root);

		(int exitCode, string output, string error) = Run(directory.Root, "--all");

		Assert.AreEqual(1, exitCode, error);
		Assert.Contains("for (int i = 0; i < 1; i++) { }", directory.Read("A/C.cs"));
		Assert.DoesNotContain("Formatting finished.", output);

		// dotnet format writes the diagnostic to the error stream above the closing lines.
		Assert.Contains("IDE2001", error);

		Assert.Contains(
			"\nerror: the run did not settle, as a check of the files it formatted still finds work in these steps:\n"
				+ "  - IDE style rules\n"
				+ "Fix by hand each diagnostic the check printed, or set its severity below info;",

			error
		);
	}

	[TestMethod]
	public void AWholeRunAndItsCheckLeaveAProjectReferencedFromOutsideTheRepositoryAlone()
	{
		using TempDirectory directory = new(editorConfig: false);

		const string library = "namespace Lib\n{\n\tpublic static class L\n\t{\n\t}\n}\n";

		// The style fix that asks for a file-scoped namespace would change a source of either repository.
		_ = directory.Write(
			".editorconfig",
			"root = true\n\n[*.cs]\ncsharp_style_namespace_declarations = file_scoped:suggestion\n"
		);

		_ = directory.Write(
			"other/Lib/Lib.csproj",

			"""
			<Project Sdk="Microsoft.NET.Sdk">
				<PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
			</Project>
			"""
		);

		_ = directory.Write("other/Lib/L.cs", library);

		_ = directory.Write(
			"repo/App.slnx",
			"""<Solution><Project Path="A/A.csproj" /></Solution>"""
		);

		_ = directory.Write(
			"repo/A/A.csproj",

			"""
			<Project Sdk="Microsoft.NET.Sdk">
				<PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
				<ItemGroup><ProjectReference Include="../../other/Lib/Lib.csproj" /></ItemGroup>
			</Project>
			"""
		);

		_ = directory.Write(
			"repo/A/C.cs",
			"namespace A\n{\n\tpublic static class C\n\t{\n\t}\n}\n"
		);

		string repository = Path.Combine(directory.Root, "repo");
		TestGit.Init(repository);

		(int checkCode, _, string checkError) = Run(repository, "--check", "--layout", "none");

		// dotnet format writes each diagnostic to the error stream.
		Assert.AreEqual(1, checkCode, checkError);
		Assert.Contains(Path.Combine("repo", "A", "C.cs"), checkError);
		Assert.DoesNotContain(Path.Combine("other", "Lib", "L.cs"), checkError);

		(int writeCode, _, string writeError) = Run(repository, "--all", "--layout", "none");

		Assert.AreEqual(0, writeCode, writeError);
		Assert.StartsWith("namespace A;", directory.Read("repo/A/C.cs"));
		Assert.AreEqual(library, directory.Read("other/Lib/L.cs"));
	}

	[TestMethod]
	public void ARunFromAnotherDirectoryOnASolutionInASubdirectoryStaysInsideTheRepository()
	{
		using TempDirectory directory = new(editorConfig: false);

		const string library = "namespace Lib\n{\n\tpublic static class L\n\t{\n\t}\n}\n";

		// The style fix that asks for a file-scoped namespace would change a source of either repository.
		_ = directory.Write(
			".editorconfig",
			"root = true\n\n[*.cs]\ncsharp_style_namespace_declarations = file_scoped:suggestion\n"
		);

		_ = directory.Write(
			"other/Lib/Lib.csproj",

			"""
			<Project Sdk="Microsoft.NET.Sdk">
				<PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
			</Project>
			"""
		);

		_ = directory.Write("other/Lib/L.cs", library);

		_ = directory.Write(
			"repo/build/App.slnx",
			"""<Solution><Project Path="../src/App/App.csproj" /></Solution>"""
		);

		_ = directory.Write(
			"repo/src/App/App.csproj",

			"""
			<Project Sdk="Microsoft.NET.Sdk">
				<PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
				<ItemGroup><ProjectReference Include="../../../other/Lib/Lib.csproj" /></ItemGroup>
			</Project>
			"""
		);

		// The namespace is the project's name, as a namespace that differs from it stops dotnet format.
		_ = directory.Write(
			"repo/src/App/C.cs",
			"namespace App\n{\n\tpublic static class C\n\t{\n\t}\n}\n"
		);

		_ = directory.Write("elsewhere/notes.txt", "A directory that is not the repository.\n");

		string repository = Path.Combine(directory.Root, "repo");
		string elsewhere = Path.Combine(directory.Root, "elsewhere");
		string solution = Path.Combine(repository, "build", "App.slnx");

		TestGit.Init(repository);

		(int checkCode, _, string checkError) = Run(
			elsewhere,
			"--check",
			"--layout",
			"none",
			solution
		);

		// dotnet format writes each diagnostic to the error stream.
		Assert.AreEqual(1, checkCode, checkError);
		Assert.Contains(Path.Combine(paths: ["repo", "src", "App", "C.cs"]), checkError);
		Assert.DoesNotContain(Path.Combine("other", "Lib", "L.cs"), checkError);

		(int writeCode, _, string writeError) = Run(
			elsewhere,
			"--all",
			"--layout",
			"none",
			solution
		);

		Assert.AreEqual(0, writeCode, writeError);
		Assert.StartsWith("namespace App;", directory.Read("repo/src/App/C.cs"));
		Assert.AreEqual(library, directory.Read("other/Lib/L.cs"));

		Assert.AreEqual(
			"A directory that is not the repository.\n",
			directory.Read("elsewhere/notes.txt")
		);
	}

	[TestMethod]
	public void SinceAndAllTogetherAreAUsageError()
	{
		using TempDirectory directory = new(editorConfig: false);

		(int exitCode, _, string error) = Run(directory.Root, "--since", "main", "--all");

		Assert.AreEqual(2, exitCode, error);
		Assert.Contains("error: --since scopes a run to the changed files", error);
		Assert.Contains("Run 'stanza-format --help' for the usage.", error);
	}

	[TestMethod]
	public void ARunSinceARefOutsideAnyRepositoryIsRefusedWithGitsOwnMessage()
	{
		using TempDirectory directory = new(editorConfig: false);
		_ = directory.Write("App.slnx", "<Solution />\n");

		(int exitCode, _, string error) = Run(directory.Root, "--check", "--since", "main");

		Assert.AreEqual(2, exitCode, error);
		Assert.Contains("is not in a git repository: fatal:", error);
	}

	[TestMethod]
	public void ARunSinceARefThatNamesNoCommitIsRefusedBeforeAnyStepRuns()
	{
		using TempDirectory directory = new(editorConfig: false);
		_ = directory.Write("App.slnx", "<Solution />\n");

		TestGit.Init(directory.Root);
		TestGit.CommitAll(directory.Root);

		(int exitCode, string output, string error) = Run(
			directory.Root,
			"--since",
			"origin/main",
			"--layout",
			"none"
		);

		Assert.AreEqual(2, exitCode, error);
		Assert.StartsWith("error: 'origin/main' names no commit in this repository.", error);
		Assert.DoesNotContain("==>", output);
	}

	[TestMethod]
	public void AWriteRunSinceARefFormatsTheFilesCommittedAfterItAndLeavesTheOthers()
	{
		using TempDirectory directory = new(editorConfig: false);
		WriteSinceFixture(directory);

		(int exitCode, string output, string error) = Run(
			directory.Root,
			"--since",
			"HEAD~1",
			"--layout",
			"none"
		);

		Assert.AreEqual(0, exitCode, error);

		Assert.Contains(
			"==> Format 2 file(s) changed since HEAD~1 of App.slnx, laid out by none",
			output
		);

		Assert.StartsWith("namespace A;", directory.Read("A/New file.cs"));
		Assert.StartsWith("namespace A;", directory.Read("A/Renamed café.cs"));
		Assert.StartsWith("namespace A\n", directory.Read("A/Old.cs"));
	}

	[TestMethod]
	public void ACheckSinceARefReadsOnlyTheFilesChangedAfterItAndNamesTheRunThatFixesThem()
	{
		using TempDirectory directory = new(editorConfig: false);
		WriteSinceFixture(directory);

		(int exitCode, string output, string error) = Run(
			directory.Root,
			"--check",
			"--since",
			"HEAD~1",
			"--layout",
			"none"
		);

		Assert.AreEqual(1, exitCode, error);

		Assert.Contains(
			"==> Check 2 file(s) changed since HEAD~1 of App.slnx, laid out by none",
			output
		);

		Assert.Contains("New file.cs", output);
		Assert.DoesNotContain("Old.cs", output);

		Assert.Contains(
			"Run stanza-format --since HEAD~1 --layout none, then fix by hand each diagnostic it leaves",
			error
		);

		(int writeCode, _, string writeError) = Run(
			directory.Root,
			"--since",
			"HEAD~1",
			"--layout",
			"none"
		);

		Assert.AreEqual(0, writeCode, writeError);

		(int againCode, string againOutput, string againError) = Run(
			directory.Root,
			"--check",
			"--since",
			"HEAD~1",
			"--layout",
			"none"
		);

		Assert.AreEqual(0, againCode, againError);
		Assert.Contains("The working tree is formatted.", againOutput);

		// The old file is still unformatted, which a whole check finds.
		(int wholeCode, _, _) = Run(directory.Root, "--check", "--layout", "none");

		Assert.AreEqual(1, wholeCode);
	}

	[TestMethod]
	public void ACheckSinceARefWithNothingChangedFormatsNothingAndPasses()
	{
		using TempDirectory directory = new(editorConfig: false);
		WriteSinceFixture(directory);

		(int exitCode, string output, string error) = Run(
			directory.Root,
			"--check",
			"--since",
			"HEAD",
			"--layout",
			"none"
		);

		Assert.AreEqual(0, exitCode, error);
		Assert.Contains("No changed source, project or markup file to format.", output);
	}

	[TestMethod]
	public void ACheckSinceARefReadsTheMergeMarkersOfTheChangedFilesOnly()
	{
		using TempDirectory directory = new(editorConfig: false);

		_ = directory.Write("App.slnx", "<Solution />\n");
		_ = directory.Write("old.txt", "<<<<<<< ours\n");

		TestGit.Init(directory.Root);
		TestGit.CommitAll(directory.Root);

		_ = directory.Write("new.txt", "<<<<<<< theirs\n");
		TestGit.CommitAll(directory.Root);

		(int exitCode, string output, string error) = Run(
			directory.Root,
			"--check",
			"--since",
			"HEAD~1",
			"--layout",
			"none"
		);

		Assert.AreEqual(1, exitCode, error);
		Assert.Contains("    new.txt", output);
		Assert.DoesNotContain("old.txt", output);
	}

	[TestMethod]
	public void ACheckSinceARefLaysOutOnlyTheChangedFilesWhateverTheirNames()
	{
		using TempDirectory directory = new(editorConfig: false);

		_ = directory.Write("my solution/App.slnx", "<Solution />\n");
		_ = directory.Write("my solution/.config/dotnet-tools.json", ReadRepositoryManifest());
		_ = directory.Write("my solution/Old.cs", "class  Old{}\n");

		TestGit.Init(directory.Root);
		TestGit.CommitAll(directory.Root);

		_ = directory.Write("my solution/@New café.cs", "class  New{}\n");
		TestGit.CommitAll(directory.Root);

		(int checkCode, string checkOutput, string checkError) = Run(
			directory.Root,
			"--check",
			"--since",
			"HEAD~1",
			"my solution"
		);

		Assert.AreEqual(1, checkCode, checkError);
		Assert.Contains("    my solution/@New café.cs", checkOutput);
		Assert.DoesNotContain("Old.cs", checkOutput);
		Assert.Contains("Run stanza-format --since HEAD~1 \"my solution\"", checkError);

		(int writeCode, _, string writeError) = Run(
			directory.Root,
			"--since",
			"HEAD~1",
			"my solution"
		);

		Assert.AreEqual(0, writeCode, writeError);
		Assert.AreEqual("class New { }", directory.Read("my solution/@New café.cs").Trim());
		Assert.AreEqual("class  Old{}", directory.Read("my solution/Old.cs").Trim());

		(int againCode, _, string againError) = Run(
			directory.Root,
			"--check",
			"--since",
			"HEAD~1",
			"my solution"
		);

		Assert.AreEqual(0, againCode, againError);

		(int wholeCode, _, _) = Run(directory.Root, "--check", "my solution");

		Assert.AreEqual(1, wholeCode);
	}

	[TestMethod]
	public void ACheckSinceARefNamesAnUntrackedFileWithTheWrongLineEnding()
	{
		using TempDirectory directory = new(editorConfig: false);

		_ = directory.Write("App.slnx", "<Solution />\n");
		_ = directory.Write(".gitattributes", "*.txt text eol=lf\n");

		TestGit.Init(directory.Root);
		TestGit.CommitAll(directory.Root);

		_ = directory.Write("new.txt", "notes\r\n");

		(int exitCode, string output, string error) = Run(
			directory.Root,
			"--check",
			"--since",
			"HEAD",
			"--layout",
			"none"
		);

		Assert.AreEqual(1, exitCode, error);
		Assert.Contains("new.txt", output);
	}

	[TestMethod]
	public void AWholeCheckNamesAnUntrackedFileWithTheWrongLineEnding()
	{
		using TempDirectory directory = new(editorConfig: false);

		_ = directory.Write("App.slnx", "<Solution />\n");
		_ = directory.Write(".gitattributes", "*.txt text eol=lf\n");

		TestGit.Init(directory.Root);
		TestGit.CommitAll(directory.Root);

		_ = directory.Write("new.txt", "notes\r\n");

		(int exitCode, string output, string error) = Run(
			directory.Root,
			"--check",
			"--layout",
			"none"
		);

		Assert.AreEqual(1, exitCode, error);
		Assert.Contains("new.txt", output);
	}

	[TestMethod]
	public void ASinceRunIsNotStoppedByAMergeMarkerInAFileItLeavesAlone()
	{
		using TempDirectory directory = new(editorConfig: false);
		WriteSinceFixture(directory, markerFile: "elsewhere.txt");

		(int exitCode, _, string error) = Run(
			directory.Root,
			"--since",
			"HEAD~1",
			"--layout",
			"none"
		);

		Assert.AreEqual(0, exitCode, error);
	}

	[TestMethod]
	public void ADefaultRunIsNotStoppedByAMergeMarkerInAFileItLeavesAlone()
	{
		using TempDirectory directory = new(editorConfig: false);
		WriteSinceFixture(directory, markerFile: "elsewhere.txt");

		_ = directory.Write("A/New file.cs", directory.Read("A/New file.cs") + "// edited\n");

		(int exitCode, _, string error) = Run(directory.Root, "--layout", "none");

		Assert.AreEqual(0, exitCode, error);
	}

	[TestMethod]
	public void AWholeRunStopsOnAMergeMarkerAnywhereInTheRepository()
	{
		using TempDirectory directory = new(editorConfig: false);
		WriteSinceFixture(directory, markerFile: "elsewhere.txt");

		(int exitCode, string output, _) = Run(directory.Root, "--all", "--layout", "none");

		Assert.AreEqual(2, exitCode);
		Assert.Contains("    elsewhere.txt", output);
	}

	private static void WriteSinceFixture(TempDirectory directory, string? markerFile = null)
	{
		if (markerFile is not null)
			_ = directory.Write(markerFile, "<<<<<<< ours\n");

		// The IDE style rule that asks for a file-scoped namespace has a fix, so a file in the old style needs one.
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

		const string oldStyle = "namespace A\n{\n\tpublic static class C\n\t{\n\t}\n}\n";

		_ = directory.Write("A/Old.cs", oldStyle);
		_ = directory.Write("A/Born café.cs", oldStyle.Replace("class C", "class B"));

		TestGit.Init(directory.Root);
		TestGit.CommitAll(directory.Root);

		TestGit.Run(directory.Root, "mv", "A/Born café.cs", "A/Renamed café.cs");
		_ = directory.Write("A/New file.cs", oldStyle.Replace("class C", "class N"));

		TestGit.CommitAll(directory.Root);
	}

	[TestMethod]
	public void AMergeMarkerFailsAWriteRunWithTheFixOfTheMarkerStep()
	{
		using TempDirectory directory = new(editorConfig: false);

		_ = directory.Write("App.slnx", "<Solution />\n");
		_ = directory.Write("conflict.txt", "<<<<<<< ours\n");

		TestGit.Init(directory.Root);

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
	public void AMergeMarkerInAChangedFileNoStepFormatsFailsADefaultRun()
	{
		using TempDirectory directory = new(editorConfig: false);

		_ = directory.Write("App.slnx", "<Solution />\n");
		_ = directory.Write("notes.txt", "notes\n");

		TestGit.Init(directory.Root);
		TestGit.CommitAll(directory.Root);

		_ = directory.Write("notes.txt", "<<<<<<< ours\n");

		(int exitCode, string output, string error) = Run(directory.Root, "--layout", "none");

		Assert.AreEqual(2, exitCode, error);
		Assert.Contains("    notes.txt", output);
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

		TestGit.Init(directory.Root);

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

		TestGit.Init(directory.Root);

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

		TestGit.Init(directory.Root);

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
}
