using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Shared;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Markup.Tests;

[TestClass]
public sealed class PathComparisonTests
{
	// The probe runs only on macOS, so this pins it against the volume of each system that runs it.
	[TestMethod]
	public void TheProbeAnswersAsTheVolumeLooksUpANameWithItsCaseSwapped()
	{
		using TempDirectory directory = new(editorConfig: false);
		_ = directory.Write("probe.txt", "probe\n");

		bool sensitive = !File.Exists(Path.Combine(directory.Root, "PROBE.TXT"));

		Assert.AreEqual(sensitive, PathComparison.ReadCaseSensitivity(directory.Root));
	}

	[TestMethod]
	public void ADirectoryWithNoNameThatCaseChangesShowsNothing()
	{
		using TempDirectory directory = new(editorConfig: false);

		Assert.IsNull(PathComparison.ReadCaseSensitivity(directory.Root));

		_ = directory.Write("123", "digits\n");

		Assert.IsNull(PathComparison.ReadCaseSensitivity(directory.Root));
		Assert.IsNull(PathComparison.ReadCaseSensitivity(Path.Combine(directory.Root, "missing")));
	}

	// Only a volume that tells case apart holds both names, and there the lookup would find the twin.
	[TestMethod]
	public void AnEntryWhoseSwappedNameIsAnotherEntryIsNotProbed()
	{
		using TempDirectory directory = new(editorConfig: false);

		_ = directory.Write("Twin.txt", "one\n");
		_ = directory.Write("tWIN.TXT", "two\n");

		if (Directory.GetFiles(directory.Root).Length == 1)
			ExpectedSkip.Skip("A volume that ignores case holds the two names as one file.");

		Assert.IsNull(PathComparison.ReadCaseSensitivity(directory.Root));

		_ = directory.Write("other.txt", "other\n");

		Assert.IsTrue(PathComparison.ReadCaseSensitivity(directory.Root));
	}
}
