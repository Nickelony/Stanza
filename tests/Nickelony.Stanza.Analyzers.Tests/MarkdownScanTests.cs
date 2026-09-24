using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class MarkdownScanTests
{
	[TestMethod]
	public void ALineInsideACodeFenceIsLeftOutAndTheFenceItselfComesBackBlank()
	{
		string[] lines = ["text", "```csharp", "[x](y)", "```", "more"];

		(int Number, string Text)[] scanned = [.. MarkdownScan.ProseLines(lines)];

		(int Number, string Text)[] expected =
		[
			(1, "text"),
			(2, string.Empty),
			(4, string.Empty),
			(5, "more"),
		];

		Assert.AreSequenceEqual(expected, scanned);
	}

	[TestMethod]
	public void AFenceThatIsNeverClosedHidesTheRestOfThePage()
	{
		string[] lines = ["text", "```", "hidden", "still hidden"];

		string[] scanned = [.. MarkdownScan.ProseLines(lines).Select(line => line.Text)];

		Assert.AreSequenceEqual(["text", string.Empty], scanned);
	}

	[TestMethod]
	public void AnIndentedFenceStillOpensAFence()
	{
		string[] lines = ["  ```", "hidden", "  ```", "shown"];

		string[] scanned = [.. MarkdownScan.ProseLines(lines).Select(line => line.Text)];

		Assert.AreSequenceEqual([string.Empty, string.Empty, "shown"], scanned);
	}
}
