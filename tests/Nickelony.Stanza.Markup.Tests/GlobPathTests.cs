using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Paths;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Markup.Tests;

[TestClass]
public sealed class GlobPathTests
{
	[TestMethod]
	public void ARootedGlobSearchesTheRootOfTheFileSystem()
	{
		using TempDirectory directory = new();

		string root = Path.GetPathRoot(directory.Root)!;

		Assert.AreEqual(root, GlobPath.GetSearchRoot("/**/*.xml", directory.Root));

		Assert.AreEqual(
			new GlobTarget(root.Replace('\\', '/').TrimEnd('/'), "/**/*.xml"),
			GlobPath.Resolve("/**/*.xml", directory.Root)
		);
	}

	[TestMethod]
	[OSCondition(OperatingSystems.Windows)]
	public void ADriveRootedGlobSearchesTheRootOfTheDrive()
	{
		using TempDirectory directory = new();

		string drive = Path.GetPathRoot(directory.Root)!;

		Assert.AreEqual(drive, GlobPath.GetSearchRoot(drive + "*.xml", directory.Root));

		Assert.AreEqual(
			new GlobTarget(drive.Replace('\\', '/').TrimEnd('/'), "/*.xml"),
			GlobPath.Resolve(drive + "*.xml", directory.Root)
		);
	}

	// The root is the drive on Windows and / elsewhere, and either ends in a separator that the directory drops.
	[TestMethod]
	public void AGlobRunFromTheRootStartsFromTheRoot()
	{
		using TempDirectory directory = new();

		string root = Path.GetPathRoot(directory.Root)!;

		Assert.AreEqual(
			new GlobTarget(root.Replace('\\', '/').TrimEnd('/'), "/**/*.xml"),
			GlobPath.Resolve("**/*.xml", root)
		);
	}

	// The target roots its pattern with a separator the glob never held, and that one is not part of the length.
	[TestMethod]
	public void AGlobIsMeasuredAsWrittenNotAsRooted()
	{
		using TempDirectory directory = new();

		GlobTarget atTheLimit = GlobPath.Resolve("*/" + new string('a', 1_022), directory.Root);
		GlobTarget pastIt = GlobPath.Resolve("*/" + new string('a', 1_023), directory.Root);

		Assert.IsFalse(GlobMatcher.TryValidate(atTheLimit.Pattern, out string? refusal));
		Assert.DoesNotContain("1024 characters", refusal);

		Assert.IsFalse(GlobMatcher.TryValidate(pastIt.Pattern, out string? tooLong));
		Assert.AreEqual("it is longer than 1024 characters", tooLong);
	}

	[TestMethod]
	public void ATargetMatchesAPathBelowItsDirectoryAsThePatternSays()
	{
		GlobTarget target = new("C:/Work/src", "/*.xml");

		Assert.IsTrue(target.IsMatch(@"c:\work\SRC\a.xml"));
		Assert.IsFalse(target.IsMatch("C:/Work/src/gen/a.xml"));
		Assert.IsFalse(target.IsMatch("C:/Work/srcs/a.xml"));
		Assert.IsFalse(target.IsMatch("C:/Work/srcs.xml"));
		Assert.IsFalse(target.IsMatch("C:/Work/src"));
	}

	[TestMethod]
	[DataRow("a.xml")]
	[DataRow("sub/a.xml")]
	public void AnArgumentWithNoWildcardNamesNoDirectoryToSearchFrom(string argument)
	{
		using TempDirectory directory = new();

		Assert.AreEqual(
			Path.GetFullPath(directory.Root),
			GlobPath.GetSearchRoot(argument, directory.Root)
		);
	}

	// A directory that is empty is the Unix root, which Resolve writes without its trailing separator.
	[TestMethod]
	public void ATargetWithAnEmptyDirectoryMatchesAPathBelowTheRoot()
	{
		GlobTarget target = new(string.Empty, "/*.xml");

		Assert.IsTrue(target.IsMatch("/a.xml"));
		Assert.IsFalse(target.IsMatch("/sub/a.xml"));
		Assert.IsFalse(target.IsMatch("a.xml"));
	}

	[TestMethod]
	public void ATargetWithNoDirectoryMatchesTheFileNameAtAnyDepth()
	{
		GlobTarget target = new(null, "*.xml");

		Assert.IsTrue(target.IsMatch("a.xml"));
		Assert.IsTrue(target.IsMatch("dir/deeper/a.xml"));
		Assert.IsFalse(target.IsMatch("dir/a.txt"));
	}

	// The directory is compared as text, so a bracket or a brace in it is never read as a wildcard.
	[TestMethod]
	public void ATargetWhoseDirectoryHoldsWildcardCharactersComparesItAsText()
	{
		GlobTarget target = new("/work/a[1]/b{2}", "/*.xml");

		Assert.IsTrue(target.IsMatch("/work/a[1]/b{2}/x.xml"));
		Assert.IsFalse(target.IsMatch("/work/a1/b2/x.xml"));
		Assert.IsFalse(target.IsMatch("/work/a[1]/b{2}/sub/x.xml"));
	}

	[TestMethod]
	[DataRow("src/*.xml", 1)]
	[DataRow("./*.xml", 1)]
	[DataRow("src/*/*.xml", 2)]
	[DataRow("*/*.xml", 2)]
	[DataRow("{a,b/c}/*.xml", 3)]
	[DataRow("src/**/*.xml", null)]
	[DataRow("src/{a,**}.xml", null)]
	[DataRow("*.xml", null)]
	public void AGlobReachesAsDeepAsItsSegmentsUnlessItHoldsADoubleStarOrNamesNoDirectory(
		string glob,
		int? depth
	)
	{
		using TempDirectory directory = new();
		Assert.AreEqual(depth, GlobPath.Resolve(glob, directory.Root).MaxDepth);
	}

	[TestMethod]
	public void AGlobBelowTheWorkingDirectoryKeepsItsSearchRoot()
	{
		using TempDirectory directory = new();

		Assert.AreEqual(
			Path.Combine(directory.Root, "sub"),
			GlobPath.GetSearchRoot("sub/*.xml", directory.Root)
		);

		Assert.AreEqual(
			Path.GetFullPath(directory.Root),
			GlobPath.GetSearchRoot("*.xml", directory.Root)
		);
	}

	// A path that folds back, as sub/.. does, ends in the name of the working directory, which may hold a wildcard.
	[TestMethod]
	public void APathThatFoldsBackOntoADirectoryWithAWildcardInItsNameStillMatchesIt()
	{
		using TempDirectory directory = new();

		string workingDirectory = Path.Combine(directory.Root, "stanza[1]");

		_ = Directory.CreateDirectory(Path.Combine(workingDirectory, "sub"));

		GlobTarget target = GlobPath.Resolve("sub/..", workingDirectory);

		Assert.IsTrue(target.IsMatch(workingDirectory));
	}
}
