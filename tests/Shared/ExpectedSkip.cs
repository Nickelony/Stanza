using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Tests.Shared;

/// <summary>Skips a test by design, in the words CI's skip check accepts.</summary>
internal static class ExpectedSkip
{
	// The script matches this text in the result file, so a change here changes it too.
	private const string Marker = "Expected skip:";

	[DoesNotReturn]
	public static void Skip(string reason) => Assert.Inconclusive($"{Marker} {reason}");
}
