using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Collection;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Markup.Tests;

[TestClass]
public sealed class MarkupRealPathsTests
{
	[TestMethod]
	public void ADirectoryWithoutALinkKeepsItsPath()
	{
		using TempDirectory directory = new();
		string file = directory.Write("real/sample.xml", string.Empty);

		Assert.AreEqual(file, new MarkupRealPaths().GetRealPath(file));
	}

	// A link resolves to the long spelling of its target, so a path spelled with a short 8.3 name has to reach the same identity.
	[TestMethod]
	[OSCondition(OperatingSystems.Windows)]
	public void AShortNamedDirectoryAndALinkToItGiveOnePath()
	{
		using TempDirectory directory = new();

		string file = directory.Write("RealDirectoryName/sample.xml", string.Empty);
		string longDirectory = Path.GetDirectoryName(file)!;
		string shortDirectory = ToShortPath(longDirectory);

		if (string.Equals(shortDirectory, longDirectory, StringComparison.OrdinalIgnoreCase))
			Assert.Inconclusive("This volume holds no 8.3 short names, so no short spelling exists.");

		string link = Path.Combine(directory.Root, "linkdir");

		if (!TempDirectory.TryCreateDirectoryLink(link, longDirectory))
		{
			Assert.Inconclusive(
				"This machine allows neither a symbolic link nor a junction for a directory."
			);
		}

		MarkupRealPaths realPaths = new();

		string viaShortName = realPaths.GetRealPath(Path.Combine(shortDirectory, "sample.xml"));
		string viaLink = realPaths.GetRealPath(Path.Combine(link, "sample.xml"));
		string spelledLong = realPaths.GetRealPath(file);

		Assert.AreEqual(viaLink, viaShortName, "a short name and a link reach one file.");
		Assert.AreEqual(viaLink, spelledLong, "the long name reaches it too.");
	}

	private static string ToShortPath(string path)
	{
		StringBuilder buffer = new(capacity: 1_024);
		uint length = GetShortPathName(path, buffer, (uint)buffer.Capacity);

		return length == 0 || length > buffer.Capacity ? path : buffer.ToString();
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetShortPathNameW")]
	private static extern uint GetShortPathName(string longPath, StringBuilder shortPath, uint length);
}
