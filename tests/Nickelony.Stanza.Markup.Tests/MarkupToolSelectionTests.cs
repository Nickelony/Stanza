using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Files;
using Nickelony.Stanza.Tests.Shared;

using static Nickelony.Stanza.Markup.Tests.MarkupFixtures;
using static Nickelony.Stanza.Tests.Shared.MarkupFormatFixtures;

namespace Nickelony.Stanza.Markup.Tests;

[TestClass]
public sealed class MarkupToolSelectionTests
{
	private const string Project = """
		<Project Sdk="Microsoft.NET.Sdk">
		  <PropertyGroup>
		    <TargetFramework>net8.0</TargetFramework>
		  </PropertyGroup>
		  <ItemGroup>
		    <PackageReference Include="a" />
		  </ItemGroup>
		</Project>

		""";

	private const string ProjectFormatted = """
		<Project Sdk="Microsoft.NET.Sdk">
		  <PropertyGroup>
		    <TargetFramework>net8.0</TargetFramework>
		  </PropertyGroup>

		  <ItemGroup>
		    <PackageReference Include="a" />
		  </ItemGroup>
		</Project>

		""";

	public static IEnumerable<object?[]> GetProblemRows()
	{
		return
		[
			[
				"an EditorConfig value that is not a number",
				new[] { "sample.xml" },
				Formatted,
				"[*.xml]\nstanza_markup_max_blank_lines = many\n",
				new[] { "sample.xml" },
				".editorconfig(4): '.editorconfig' sets stanza_markup_max_blank_lines",
			],

			[
				"a scan that excludes every file",
				new[] { "src/sample.xml" },
				Source,
				null,
				new[] { "--exclude", "src/**", "src" },
				"'src' holds no file this run inspects",
			],

			[
				"a scan that collects nothing",
				new[] { "empty/sample.txt" },
				Source,
				null,
				new[] { "empty" },
				"'empty' holds no file this run inspects",
			],

			[
				"a glob whose matches no profile claims",
				new[] { "a.txt", "b.txt" },
				Source,
				null,
				new[] { "*.txt" },
				"'*.txt' matched 2 file(s), but none of them is claimed",
			],

			[
				"a glob whose matches are all disabled",
				new[] { "a.xml", "b.xml" },
				Source,
				"[*.xml]\nstanza_markup_enabled = false\n",
				new[] { "*.xml" },
				"'*.xml' matched 2 file(s), but 2 of them are disabled in their .editorconfig",
			],

			[
				"an exclude pattern the dialect refuses",
				new[] { "sample.xml" },
				Formatted,
				null,
				new[] { "--exclude", "file[z-a].xml", "sample.xml" },
				"the exclude pattern 'file[z-a].xml' is refused",
			],
		];
	}

	[TestMethod]
	public void ADirectoryIsScannedRecursively()
	{
		using TempDirectory directory = new();
		_ = directory.Write("nested/deeper/sample.xml", LineFeed(Source));
		_ = directory.Write("nested/other.txt", LineFeed(Source));

		RunResult result = Run(directory, "format", "nested");

		Assert.AreEqual(0, result.ExitCode);
		Assert.Contains("1 file(s) formatted.", result.Output);

		Assert.AreEqual(LineFeed(Formatted), directory.Read("nested/deeper/sample.xml"));
	}

	[TestMethod]
	public void AGlobSelectsTheFilesItMatches()
	{
		using TempDirectory directory = new();
		_ = directory.Write("nested/sample.xml", LineFeed(Source));
		_ = directory.Write("nested/sample.config", LineFeed(Source));

		RunResult result = Run(directory, "format", "nested/*.xml");

		Assert.AreEqual(0, result.ExitCode);
		Assert.Contains("1 file(s) formatted.", result.Output);
		Assert.AreEqual(LineFeed(Source), directory.Read("nested/sample.config"));
	}

	[TestMethod]
	public void APrefixedOrAbsoluteGlobSelectsTheSameFiles()
	{
		using TempDirectory directory = new();
		_ = directory.Write("nested/sample.xml", LineFeed(Source));

		RunResult relative = Run(directory, "format", "nested/*.xml");

		Assert.AreEqual(0, relative.ExitCode);
		Assert.Contains("1 file(s) formatted.", relative.Output);

		_ = directory.Write("nested/sample.xml", LineFeed(Source));

		RunResult prefixed = Run(directory, "format", "./nested/*.xml");

		Assert.AreEqual(0, prefixed.ExitCode);
		Assert.AreEqual(LineFeed(Formatted), directory.Read("nested/sample.xml"));

		_ = directory.Write("nested/sample.xml", LineFeed(Source));

		RunResult absolute = Run(
			directory,
			"format",
			Path.Combine(directory.Root, "nested", "*.xml")
		);

		Assert.AreEqual(0, absolute.ExitCode);
		Assert.AreEqual(LineFeed(Formatted), directory.Read("nested/sample.xml"));
	}

	[TestMethod]
	public void ABraceGlobSelectsTheFilesItLists()
	{
		using TempDirectory directory = new();
		_ = directory.Write("a.xml", LineFeed(Source));
		_ = directory.Write("b.xml", LineFeed(Source));
		_ = directory.Write("c.xml", LineFeed(Source));

		RunResult result = Run(directory, "format", "{a,b}.xml");

		Assert.AreEqual(0, result.ExitCode);
		Assert.Contains("2 file(s) formatted.", result.Output);
		Assert.AreEqual(LineFeed(Formatted), directory.Read("a.xml"));
		Assert.AreEqual(LineFeed(Formatted), directory.Read("b.xml"));
		Assert.AreEqual(LineFeed(Source), directory.Read("c.xml"));
	}

	[TestMethod]
	public void ANamedFileTheExcludeCoversIsANote()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.xml", LineFeed(Source));

		RunResult result = Run(
			directory,
			"check",
			"--strict",
			"--exclude",
			"sample.xml",
			"sample.xml"
		);

		Assert.AreEqual(0, result.ExitCode);

		Assert.Contains(
			"note: 'sample.xml' is excluded by --exclude, so it was skipped.",
			result.Error
		);
	}

	[TestMethod]
	[DataRow("src/gen/*.xml")]
	[DataRow("src/gen")]
	public void AGlobAndAnExcludeMatchBelowALongWorkingDirectory(string exclude)
	{
		using TempDirectory directory = new();

		// Joined into the pattern, a working directory this long passed the size limit of the matcher, which then matched nothing.
		string longName = new('w', 140);
		_ = directory.Write($"{longName}/src/kept.xml", LineFeed(Source));
		_ = directory.Write($"{longName}/src/gen/dropped.xml", LineFeed(Source));

		RunResult result = RunIn(
			Path.Combine(directory.Root, longName),
			"format",
			"--exclude",
			exclude,
			"src/**/*.xml"
		);

		Assert.AreEqual(0, result.ExitCode, result.Error);
		Assert.Contains("1 file(s) formatted.", result.Output);

		Assert.AreEqual(LineFeed(Formatted), directory.Read($"{longName}/src/kept.xml"));

		Assert.AreEqual(LineFeed(Source), directory.Read($"{longName}/src/gen/dropped.xml"));
	}

	[TestMethod]
	public void AnAbsoluteGlobAndExcludeMatchBelowALongDirectory()
	{
		using TempDirectory directory = new();

		// Validated whole, a directory this long passed the size limit of the matcher, so the glob was refused.
		string name = new('w', 180);
		string root = Path.Combine(directory.Root, name).Replace('\\', '/');

		_ = directory.Write($"{name}/kept.xml", LineFeed(Source));
		_ = directory.Write($"{name}/gen/dropped.xml", LineFeed(Source));

		RunResult result = Run(
			directory,
			"format",
			"--exclude",
			$"{root}/gen/*.xml",
			$"{root}/**/*.xml"
		);

		Assert.AreEqual(0, result.ExitCode, result.Error);
		Assert.Contains("1 file(s) formatted.", result.Output);
		Assert.AreEqual(LineFeed(Formatted), directory.Read($"{name}/kept.xml"));
		Assert.AreEqual(LineFeed(Source), directory.Read($"{name}/gen/dropped.xml"));
	}

	// A refusal names the pattern the user wrote, and the long directory above it is never part of what is validated.
	[TestMethod]
	[DataRow("{1..3}.xml", false)]
	[DataRow("[^a].xml", false)]
	[DataRow("{1..3}.xml", true)]
	[DataRow("[^a].xml", true)]
	public void ARefusedGlobBelowALongDirectoryIsReportedByItsOwnText(string glob, bool strict)
	{
		using TempDirectory directory = new();

		string longDirectory = Directory
			.CreateDirectory(Path.Combine(directory.Root, new string('w', 180)))
			.FullName;

		string[] mode = strict ? ["--strict"] : [];

		RunResult result = RunIn(longDirectory, ["check", .. mode, glob]);

		Assert.AreEqual(2, result.ExitCode, result.Error);
		Assert.StartsWith($"error: the pattern '{glob}' is refused (", result.Error);
	}

	[TestMethod]
	[DataRow("{1..3}.xml", false, 0, "warning")]
	[DataRow("[^a].xml", false, 0, "warning")]
	[DataRow("{1..3}.xml", true, 2, "error")]
	[DataRow("[^a].xml", true, 2, "error")]
	public void ARefusedExcludeBelowALongDirectoryIsAWarningThatStrictRaises(
		string exclude,
		bool strict,
		int exitCode,
		string level
	)
	{
		using TempDirectory directory = new();

		string longDirectory = Directory
			.CreateDirectory(Path.Combine(directory.Root, new string('w', 180)))
			.FullName;

		File.WriteAllText(Path.Combine(longDirectory, "a.xml"), LineFeed(Formatted));

		string[] mode = strict ? ["--strict"] : [];

		RunResult result = RunIn(longDirectory, ["check", .. mode, "--exclude", exclude, "a.xml"]);

		Assert.AreEqual(exitCode, result.ExitCode, result.Error);
		Assert.StartsWith($"{level}: the exclude pattern '{exclude}' is refused (", result.Error);
	}

	[TestMethod]
	[DataRow("src/dropped")]
	[DataRow("./src/dropped")]
	public void AnExcludePatternNamingADirectoryDropsItsSubtree(string pattern)
	{
		using TempDirectory directory = new();
		_ = directory.Write("src/kept.xml", LineFeed(Source));
		_ = directory.Write("src/dropped/deep.xml", LineFeed(Source));

		RunResult result = Run(directory, "format", "--exclude", pattern, "src");

		Assert.AreEqual(0, result.ExitCode);
		Assert.Contains("1 file(s) formatted.", result.Output);
		Assert.AreEqual(LineFeed(Formatted), directory.Read("src/kept.xml"));
		Assert.AreEqual(LineFeed(Source), directory.Read("src/dropped/deep.xml"));
	}

	[TestMethod]
	public void APlainNameExcludeSkipsNoDirectoryAboveTheWorkingDirectory()
	{
		using TempDirectory directory = new();
		_ = directory.Write("test/src/kept.xml", LineFeed(Source));
		_ = directory.Write("test/src/src/dropped.xml", LineFeed(Source));

		string inside = Path.Combine(directory.Root, "test", "src");

		RunResult scan = RunIn(inside, "format", ".", "--exclude", "src", "--exclude", "test");

		Assert.AreEqual(0, scan.ExitCode, scan.Error);
		Assert.Contains("1 file(s) formatted.", scan.Output);
		Assert.AreEqual(LineFeed(Formatted), directory.Read("test/src/kept.xml"));

		Assert.AreEqual(LineFeed(Source), directory.Read("test/src/src/dropped.xml"));

		_ = directory.Write("test/src/kept.xml", LineFeed(Source));

		RunResult named = RunIn(inside, "format", "kept.xml", "--exclude", "src");

		Assert.AreEqual(0, named.ExitCode, named.Error);
		Assert.AreEqual(LineFeed(Formatted), directory.Read("test/src/kept.xml"));
	}

	// A file outside the working directory shows its full path with forward slashes, in either report form.
	[TestMethod]
	[DataRow("../other/a.xml")]
	[DataRow("../other/*.xml")]
	public void AFileOutsideTheWorkingDirectoryShowsItsFullPath(string input)
	{
		using TempDirectory directory = new();

		string file = directory.Write("other/a.xml", LineFeed(Source));
		string work = Directory.CreateDirectory(Path.Combine(directory.Root, "work")).FullName;
		string shown = file.Replace('\\', '/');

		RunResult human = RunIn(work, "check", input);

		Assert.AreEqual(1, human.ExitCode, human.Error);
		Assert.Contains($"needs formatting: {shown}", human.Output);

		RunResult msbuild = RunIn(work, "check", input, "--format", "msbuild");

		Assert.AreEqual(1, msbuild.ExitCode, msbuild.Error);
		Assert.Contains($"{shown}(", msbuild.Output);
	}

	[TestMethod]
	[DataRow("../other/a.xml")]
	[DataRow("../other/a.*")]
	[DataRow("**/a.xml")]
	public void AnExcludeDropsAFileAboveTheWorkingDirectoryByItsParentPath(string pattern)
	{
		using TempDirectory directory = new();

		string kept = directory.Write("other/b.xml", LineFeed(Source));
		_ = directory.Write("other/a.xml", LineFeed(Source));

		string work = Directory.CreateDirectory(Path.Combine(directory.Root, "work")).FullName;

		RunResult result = RunIn(work, "check", "../other/*.xml", "--exclude", pattern);

		Assert.AreEqual(1, result.ExitCode, result.Error);
		Assert.Contains($"needs formatting: {kept.Replace('\\', '/')}", result.Output);
		Assert.Contains("1 file(s) need formatting.", result.Output);
	}

	[TestMethod]
	public void AWildcardExcludeDoesNotPruneADirectoryItOnlyPartlyMatches()
	{
		using TempDirectory directory = new();
		_ = directory.Write("src/kept.xml", LineFeed(Source));
		_ = directory.Write("src/deep/deep.xml", LineFeed(Source));

		RunResult result = Run(directory, "format", "--exclude", "src/*", "src");

		Assert.AreEqual(0, result.ExitCode);
		Assert.Contains("1 file(s) formatted.", result.Output);
		Assert.AreEqual(LineFeed(Source), directory.Read("src/kept.xml"));
		Assert.AreEqual(LineFeed(Formatted), directory.Read("src/deep/deep.xml"));
	}

	[TestMethod]
	public void ATrailingDoubleStarPrunesTheDirectoryItCovers()
	{
		using TempDirectory directory = new();
		_ = directory.Write("kept.xml", LineFeed(Source));
		_ = directory.Write("dropped/deep.xml", LineFeed(Source));

		RunResult result = Run(directory, "format", "--exclude", "**/dropped/**", ".");

		Assert.AreEqual(0, result.ExitCode);
		Assert.Contains("1 file(s) formatted.", result.Output);
		Assert.AreEqual(LineFeed(Formatted), directory.Read("kept.xml"));
		Assert.AreEqual(LineFeed(Source), directory.Read("dropped/deep.xml"));
	}

	[TestMethod]
	public void AnExcludeMayBeRepeatedAndEveryOneApplies()
	{
		using TempDirectory directory = new();
		_ = directory.Write("a.xml", LineFeed(Source));
		_ = directory.Write("b.xml", LineFeed(Source));
		_ = directory.Write("c.xml", LineFeed(Source));

		RunResult result = Run(
			directory,
			"format",
			"--exclude",
			"a.xml",
			"--exclude",
			"b.xml",
			"*.xml"
		);

		Assert.AreEqual(0, result.ExitCode);

		Assert.Contains(
			"1 file(s) formatted.",
			result.Output,
			"one file must survive both excludes."
		);

		Assert.AreEqual(LineFeed(Source), directory.Read("a.xml"));
		Assert.AreEqual(LineFeed(Source), directory.Read("b.xml"));
		Assert.AreEqual(LineFeed(Formatted), directory.Read("c.xml"));
	}

	[TestMethod]
	public void ATrailingBackslashIsASeparatorInAnExcludePattern()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sub/sample.xml", LineFeed(Source));

		RunResult result = Run(directory, "format", "--exclude", @"sub\", "sub/*.xml");

		Assert.AreEqual(0, result.ExitCode);

		Assert.DoesNotContain(
			"is refused",
			result.Error,
			"a command line backslash separates rather than escapes."
		);

		Assert.AreEqual(LineFeed(Source), directory.Read("sub/sample.xml"));
	}

	[TestMethod]
	public void AnExcludePatternCoveringADirectoryDropsAFileNamedOnTheCommandLine()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sub/sample.xml", LineFeed(Source));

		RunResult result = Run(directory, "format", "--exclude", "sub/", "sub/sample.xml");

		Assert.AreEqual(0, result.ExitCode);
		Assert.AreEqual(LineFeed(Source), directory.Read("sub/sample.xml"));
	}

	[TestMethod]
	public void AnExcludedFileNamedOnTheCommandLineIsNotRefusedForItsType()
	{
		using TempDirectory directory = new();
		_ = directory.Write("notes.txt", "plain text");

		RunResult result = Run(directory, "format", "--exclude", "notes.txt", "notes.txt");

		Assert.AreEqual(0, result.ExitCode);
		Assert.Contains("is excluded by --exclude", result.Error);
		Assert.DoesNotContain("not a file type", result.Error);
	}

	[TestMethod]
	[DataRow("bin", "obj")]
	[DataRow("BIN", "Node_Modules")]
	public void ADirectoryScanPrunesASkippedDirectoryInAnyLetterCase(string first, string second)
	{
		using TempDirectory directory = new();
		_ = directory.Write($"{first}/generated.props", LineFeed(Project));
		_ = directory.Write($"{second}/generated.props", LineFeed(Project));
		_ = directory.Write("generated.props", LineFeed(Project));

		RunResult result = Run(directory, "format", ".");

		Assert.AreEqual(0, result.ExitCode);

		Assert.Contains(
			"1 file(s) formatted.",
			result.Output,
			"only the root file may be scanned."
		);

		Assert.AreEqual(LineFeed(Project), directory.Read($"{first}/generated.props"));

		Assert.AreEqual(LineFeed(Project), directory.Read($"{second}/generated.props"));

		Assert.AreEqual(LineFeed(ProjectFormatted), directory.Read("generated.props"));
	}

	[TestMethod]
	[DynamicData(
		nameof(GetProblemRows),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public void AProblemIsOneWarningThatStrictRaisesToAnError(
		string name,
		string[] files,
		string content,
		string? editorConfig,
		string[] inputs,
		string notice
	)
	{
		using TempDirectory directory = new();

		foreach (string file in files)
			_ = directory.Write(file, LineFeed(content));

		if (editorConfig is not null)
			_ = directory.Write(".editorconfig", MarkupFixtures.EditorConfig(editorConfig));

		RunResult relaxed = Run(directory, ["check", .. inputs]);

		Assert.AreEqual(0, relaxed.ExitCode, $"{name}: {relaxed.Error}");
		Assert.StartsWith($"warning: {notice}", relaxed.Error, name);
		Assert.HasCount(1, relaxed.Error.Split('\n', StringSplitOptions.RemoveEmptyEntries), name);

		RunResult strict = Run(directory, ["check", "--strict", .. inputs]);

		Assert.AreEqual(2, strict.ExitCode, name);
		Assert.StartsWith($"error: {notice}", strict.Error, name);
	}

	[TestMethod]
	public void AFileThatIsDisabledByNameIsNotedAndPassesUnderStrict()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.xml", LineFeed(Source));

		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[sample.xml]\nstanza_markup_enabled = false\n")
		);

		RunResult relaxed = Run(directory, "check", "sample.xml");

		Assert.AreEqual(0, relaxed.ExitCode);
		Assert.Contains("note: 'sample.xml' is disabled", relaxed.Error);

		RunResult strict = Run(directory, "check", "--strict", "sample.xml");

		Assert.AreEqual(0, strict.ExitCode, strict.Error);
		Assert.Contains("note: 'sample.xml' is disabled", strict.Error);
	}

	[TestMethod]
	public void ARefusedSectionGlobIsReportedAndMatchesNothing()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.xml", LineFeed(Source));

		// A section that matched would take the file out of the run, so the check would pass.
		_ = directory.Write(
			".editorconfig",
			MarkupFixtures.EditorConfig("[file[z-a].xml]\nstanza_markup_enabled = false\n")
		);

		RunResult result = Run(directory, "check", "sample.xml");

		Assert.AreEqual(1, result.ExitCode);
		Assert.Contains("whose pattern is refused", result.Error);
	}

	[TestMethod]
	public void AnAbsolutePatternScansItsOwnDirectory()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.xml", LineFeed(Source));
		_ = directory.Write("nested/other.xml", LineFeed(Source));

		RunResult result = Run(directory, "format", directory.Root.Replace('\\', '/') + "/*.xml");

		Assert.AreEqual(0, result.ExitCode, "an absolute pattern must not crash the run.");
		Assert.Contains("1 file(s) formatted.", result.Output, "one file must be selected.");
		Assert.AreEqual(LineFeed(Formatted), directory.Read("sample.xml"));
		Assert.AreEqual(LineFeed(Source), directory.Read("nested/other.xml"));
	}

	[TestMethod]
	public void ARootRelativeExcludePatternIsAppliedInsteadOfCrashing()
	{
		using TempDirectory directory = new();
		_ = directory.Write("drop.xml", LineFeed(Source));
		_ = directory.Write("keep.xml", LineFeed(Source));

		RunResult result = Run(directory, "format", "--exclude", "/**/drop.xml", "*.xml");

		Assert.AreEqual(0, result.ExitCode, "a rooted exclude must not crash the run.");
		Assert.Contains("1 file(s) formatted.", result.Output, "the excluded file must stay out.");
		Assert.AreEqual(LineFeed(Source), directory.Read("drop.xml"));
		Assert.AreEqual(LineFeed(Formatted), directory.Read("keep.xml"));
	}

	[TestMethod]
	public void ACharacterClassInAnInputPathSelectsTheFilesItMatches()
	{
		using TempDirectory directory = new();
		_ = directory.Write("nested/file1.xml", LineFeed(Source));
		_ = directory.Write("nested/filea.xml", LineFeed(Source));

		RunResult result = Run(directory, "format", "nested/file[0-9].xml");

		Assert.AreEqual(0, result.ExitCode, "the class pattern must be accepted.");
		Assert.Contains("1 file(s) formatted.", result.Output, "only the match may be formatted.");
		Assert.AreEqual(LineFeed(Formatted), directory.Read("nested/file1.xml"));
		Assert.AreEqual(LineFeed(Source), directory.Read("nested/filea.xml"));
	}

	[TestMethod]
	public void ACharacterClassInAnExcludePatternIsHonored()
	{
		using TempDirectory directory = new();
		_ = directory.Write("file1.xml", LineFeed(Source));
		_ = directory.Write("filea.xml", LineFeed(Source));

		RunResult result = Run(directory, "format", "--exclude", "file[0-9].xml", "*.xml");

		Assert.AreEqual(0, result.ExitCode, "the class exclude must be accepted.");
		Assert.Contains("1 file(s) formatted.", result.Output, "the class match must be excluded.");
		Assert.AreEqual(LineFeed(Source), directory.Read("file1.xml"));
		Assert.AreEqual(LineFeed(Formatted), directory.Read("filea.xml"));
	}

	[TestMethod]
	public void AFileNamedWithGlobCharactersIsTakenLiterally()
	{
		using TempDirectory directory = new();
		_ = directory.Write("a[1].xml", LineFeed(Source));
		_ = directory.Write("a1.xml", LineFeed(Source));
		_ = directory.Write("b{x,y}.xml", LineFeed(Source));
		_ = directory.Write("bx.xml", LineFeed(Source));

		RunResult result = Run(directory, "format", "a[1].xml", "b{x,y}.xml");

		Assert.AreEqual(0, result.ExitCode);
		Assert.Contains("2 file(s) formatted.", result.Output);
		Assert.AreEqual(LineFeed(Formatted), directory.Read("a[1].xml"));
		Assert.AreEqual(LineFeed(Formatted), directory.Read("b{x,y}.xml"));
		Assert.AreEqual(LineFeed(Source), directory.Read("a1.xml"));
		Assert.AreEqual(LineFeed(Source), directory.Read("bx.xml"));
	}

	[TestMethod]
	[OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
	public void AFileNamedWithAStarIsTakenLiterally()
	{
		using TempDirectory directory = new();
		_ = directory.Write("a*.xml", LineFeed(Source));
		_ = directory.Write("ab.xml", LineFeed(Source));

		RunResult result = Run(directory, "format", "a*.xml");

		Assert.AreEqual(0, result.ExitCode);
		Assert.AreEqual(LineFeed(Formatted), directory.Read("a*.xml"));
		Assert.AreEqual(LineFeed(Source), directory.Read("ab.xml"));
	}

	[TestMethod]
	public void AMissingPathDoesNotStopThePathsThatExist()
	{
		using TempDirectory directory = new();
		_ = directory.Write("src/sample.xml", LineFeed(Source));

		RunResult check = Run(directory, "check", "stale.xml", "src");

		Assert.AreEqual(2, check.ExitCode, "the missing path must still fail the run.");
		Assert.Contains("'stale.xml' does not exist", check.Error, "the path must be reported.");
		Assert.Contains("src/sample.xml", check.Output, "the found file must be reported.");

		RunResult format = Run(directory, "format", "stale.xml", "src");

		Assert.AreEqual(2, format.ExitCode, "the missing path must still fail the run.");
		Assert.AreEqual(LineFeed(Formatted), directory.Read("src/sample.xml"));
	}

	[TestMethod]
	public void AGlobWhoseFirstSegmentIsAWildcardSelectsFiles()
	{
		using TempDirectory directory = new();

		_ = directory.Write("a/sample.xml", LineFeed(Source));
		_ = directory.Write("b/sample.xml", LineFeed(Source));

		RunResult braced = Run(directory, "check", "{a,b}/*.xml");

		Assert.AreEqual(1, braced.ExitCode);
		Assert.Contains("2 file(s) need formatting.", braced.Output);
		Assert.DoesNotContain("does not match any file", braced.Error);

		RunResult starred = Run(directory, "check", "*/sample.xml");

		Assert.AreEqual(1, starred.ExitCode);
		Assert.Contains("2 file(s) need formatting.", starred.Output);
	}

	// The configuration raises its problem while the first argument is collected, so it sits before a later argument's notice.
	[TestMethod]
	public void AnEditorConfigProblemKeepsItsPlaceAmongTheNoticesOfTheScan()
	{
		using TempDirectory directory = new();
		_ = directory.Write("a/sample.xml", LineFeed(Formatted));
		_ = directory.Write("a/.editorconfig", "[*.xml]\nstanza_markup_max_blank_lines = many\n");

		_ = Directory.CreateDirectory(Path.Combine(directory.Root, "empty"));

		RunResult result = Run(directory, "check", "a", "empty");

		int configuration = result.Error.IndexOf(
			"warning: a/.editorconfig(2): ",
			StringComparison.Ordinal
		);

		int collection = result.Error.IndexOf(
			"warning: 'empty' holds no file",
			StringComparison.Ordinal
		);

		Assert.IsGreaterThanOrEqualTo(0, configuration, result.Error);
		Assert.IsGreaterThan(configuration, collection, result.Error);
	}

	[TestMethod]
	public void AForcedProfileLaysOutEveryGlobMatch()
	{
		using TempDirectory directory = new();
		_ = directory.Write("src/sample.txt", LineFeed(Source));

		RunResult result = Run(directory, "format", "--profile", "xml", "src/**/*.txt");

		Assert.AreEqual(0, result.ExitCode);
		Assert.Contains("1 file(s) formatted.", result.Output);
		Assert.AreEqual(LineFeed(Formatted), directory.Read("src/sample.txt"));
	}

	[TestMethod]
	public void AGlobThatExcludesEveryMatchReportsThatItInspectedNothing()
	{
		using TempDirectory directory = new();
		_ = directory.Write("src/sample.xml", LineFeed(Source));

		RunResult relaxed = Run(directory, "check", "--exclude", "src/**", "src/*.xml");

		Assert.AreEqual(0, relaxed.ExitCode);
		Assert.Contains("all of them are excluded, so nothing was inspected", relaxed.Error);
	}

	[TestMethod]
	public void ADirectoryScanKeepsTheExtensionNarrowing()
	{
		using TempDirectory directory = new();
		_ = directory.Write("src/sample.txt", LineFeed(Source));

		RunResult result = Run(directory, "format", "--profile", "xml", "src");

		Assert.AreEqual(0, result.ExitCode);
		Assert.Contains("holds no file this run inspects", result.Error);
		Assert.AreEqual(LineFeed(Source), directory.Read("src/sample.txt"));
	}

	// A hook can name a directory and a changed file inside it, and the file is still reported once.
	[TestMethod]
	[DataRow(".", ".")]
	[DataRow(".", "sample.xml")]
	public void AFileThatTwoInputsReachIsOnlyInspectedOnce(string first, string second)
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.xml", LineFeed(Source));

		RunResult checkResult = Run(directory, "check", first, second);

		int reports = checkResult.Output.Split("needs formatting: ").Length - 1;

		Assert.AreEqual(1, checkResult.ExitCode);
		Assert.AreEqual(1, reports, "The file is named once, however often the path repeats.");
		Assert.Contains("1 file(s) need formatting.", checkResult.Output);

		RunResult result = Run(directory, "format", first, second);

		Assert.AreEqual(0, result.ExitCode);
		Assert.Contains("1 file(s) formatted.", result.Output);
		Assert.AreEqual(LineFeed(Formatted), directory.Read("sample.xml"));
	}

	[TestMethod]
	public void ARepeatedDirectoryThatHoldsNoFileIsReportedOnce()
	{
		using TempDirectory directory = new();

		// The collection warns each time it scans a path, so only the repeat check keeps the warning to one line.
		Directory.CreateDirectory(Path.Combine(directory.Root, "empty"));

		RunResult empty = Run(directory, "check", "empty", "empty");

		Assert.AreEqual(
			1,
			empty.Error.Split("holds no file this run inspects").Length - 1,
			empty.Error
		);
	}

	[TestMethod]
	[DataRow("missing/**/*.xml", "'missing/**/*.xml' does not match any file.")]
	[DataRow("nested/*.config", "'nested/*.config' does not match any file.")]
	[DataRow("sample[z-a].xml", "the pattern 'sample[z-a].xml' is refused")]
	public void AGlobThatMatchesNoFileOrIsRefusedFailsTheRunWhateverTheMode(
		string glob,
		string message
	)
	{
		using TempDirectory directory = new();
		_ = directory.Write("nested/sample.xml", LineFeed(Source));
		_ = directory.Write("sample1.xml", LineFeed(Source));

		RunResult relaxed = Run(directory, "check", glob, "sample1.xml");

		Assert.AreEqual(2, relaxed.ExitCode, relaxed.Error);
		Assert.StartsWith($"error: {message}", relaxed.Error);
		Assert.HasCount(1, relaxed.Error.Split('\n', StringSplitOptions.RemoveEmptyEntries));
		Assert.Contains("needs formatting: sample1.xml", relaxed.Output);

		RunResult strict = Run(directory, "check", "--strict", glob, "sample1.xml");

		Assert.AreEqual(2, strict.ExitCode, strict.Error);
		Assert.StartsWith($"error: {message}", strict.Error);
	}

	[TestMethod]
	public void AGlobThatMatchesNoFileIsReportedOnceHoweverOftenItRepeats()
	{
		using TempDirectory directory = new();

		RunResult result = Run(directory, "check", "missing/*.xml", "missing/*.xml");

		Assert.AreEqual(2, result.ExitCode);
		Assert.AreEqual(1, result.Error.Split("does not match any file").Length - 1, result.Error);
	}

	[TestMethod]
	public void AGlobThatEachSpellingReadsDifferentlyIsCollectedForBoth()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.xml", LineFeed(Source));
		_ = directory.Write("nested/sample.xml", LineFeed(Source));

		// A glob without a separator matches the file name in every directory, which "./*.xml" does not.
		RunResult result = Run(directory, "check", "./*.xml", "*.xml");

		Assert.AreEqual(1, result.ExitCode, result.Error);
		Assert.Contains("needs formatting: nested/sample.xml", result.Output);
		Assert.Contains("2 file(s) need formatting.", result.Output);
	}

	[TestMethod]
	[DataRow("/sub/b.xml")]
	[DataRow("/sub")]
	[DataRow("/sub/*.xml")]
	public void ALeadingSlashInAnExcludeIsTheRootOfTheFileSystem(string pattern)
	{
		using TempDirectory directory = new();
		_ = directory.Write("sub/b.xml", LineFeed(Source));

		RunResult result = Run(directory, "format", "--exclude", pattern, "sub");

		Assert.AreEqual(0, result.ExitCode, result.Error);
		Assert.Contains("1 file(s) formatted.", result.Output);
		Assert.AreEqual(LineFeed(Formatted), directory.Read("sub/b.xml"));
	}

	[TestMethod]
	public void AnAbsoluteExcludeNamingAFileDropsIt()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sub/b.xml", LineFeed(Source));

		string absolute = Path.Combine(directory.Root, "sub", "b.xml").Replace('\\', '/');

		RunResult result = Run(directory, "format", "--exclude", absolute, "sub");

		Assert.AreEqual(0, result.ExitCode, result.Error);
		Assert.AreEqual(LineFeed(Source), directory.Read("sub/b.xml"));
	}

	[TestMethod]
	public void ANoteNamesTheFileAsTheReportsDo()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sub/b.xml", LineFeed(Source));
		_ = directory.Write("sub/notes.txt", "plain text");

		RunResult excluded = Run(directory, "check", "--exclude", "sub/b.xml", "./sub/b.xml");

		Assert.Contains("note: 'sub/b.xml' is excluded by --exclude", excluded.Error);

		RunResult unclaimed = Run(directory, "check", "./sub/notes.txt");

		Assert.Contains(
			"note: 'sub/notes.txt' is not a file type any profile claims",
			unclaimed.Error
		);
	}

	// A hard kill between the write and the replace leaves this file, and a scan passes it by as any file no profile claims.
	[TestMethod]
	public void ALeftoverTempFileOfAKilledWriteIsNotScanned()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.xml", LineFeed(Formatted));

		_ = directory.Write(
			$"sample.xml.0123456789ABCDEF{MarkupAtomicWrite.TemporarySuffix}",
			LineFeed(Source)
		);

		RunResult result = Run(directory, "check", ".");

		Assert.AreEqual(0, result.ExitCode, result.Error);
		Assert.AreEqual(string.Empty, result.Output + result.Error);
	}

	[TestMethod]
	public void AGlobWithoutADoubleStarSelectsOnlyAsDeepAsItsSegmentsReach()
	{
		using TempDirectory directory = new();
		_ = directory.Write("src/a.xml", LineFeed(Source));
		_ = directory.Write("src/deep/b.xml", LineFeed(Source));

		RunResult shallow = Run(directory, "check", "src/*.xml");

		Assert.Contains("needs formatting: src/a.xml", shallow.Output);
		Assert.Contains("1 file(s) need formatting.", shallow.Output);

		RunResult deep = Run(directory, "check", "src/*/*.xml");

		Assert.Contains("needs formatting: src/deep/b.xml", deep.Output);
		Assert.Contains("1 file(s) need formatting.", deep.Output);
	}

	[TestMethod]
	public void AGlobWithoutADoubleStarNeverEntersADirectoryBelowItsReach()
	{
		using TempDirectory directory = new();
		_ = directory.Write("src/a.xml", LineFeed(Source));
		_ = directory.Write("src/locked/b.xml", LineFeed(Source));

		using var denied = DeniedDirectory.DenyOrSkip(
			Path.Combine(directory.Root, "src", "locked")
		);

		RunResult shallow = Run(directory, "check", "src/*.xml");

		Assert.AreEqual(1, shallow.ExitCode, shallow.Error);
		Assert.DoesNotContain("could not be scanned", shallow.Error);

		// The same walk with a pattern that can reach the directory meets it.
		RunResult reaching = Run(directory, "check", "src/**/*.xml");

		Assert.Contains("'src/locked' could not be scanned", reaching.Error);
	}

	[TestMethod]
	[DataRow("a\0b.xml")]
	[DataRow("a\0b/*.xml")]
	public void APathOrGlobThatCannotBeResolvedIsAnErrorAndTheOthersStillRun(string argument)
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.xml", LineFeed(Source));

		RunResult result = Run(directory, "check", argument, "sample.xml");

		Assert.AreEqual(2, result.ExitCode, result.Error);

		Assert.Contains(
			$"error: '{argument.Replace("\0", @"\x00")}' could not be resolved to a path",
			result.Error
		);

		Assert.Contains("needs formatting: sample.xml", result.Output);
	}

	[TestMethod]
	public void AnExcludePatternThatCannotBeResolvedIsReported()
	{
		using TempDirectory directory = new();
		_ = directory.Write("sample.xml", LineFeed(Source));

		RunResult result = Run(directory, "format", "--exclude", "a\0b/sample.xml", "sample.xml");

		Assert.AreEqual(0, result.ExitCode, "one unusable pattern is that pattern's problem.");
		Assert.Contains("could not be resolved to a path", result.Error);
		Assert.AreEqual(LineFeed(Formatted), directory.Read("sample.xml"));
	}
}
