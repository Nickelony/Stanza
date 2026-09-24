using System;
using System.Collections.Immutable;
using System.Linq;

namespace Nickelony.Stanza.Format.Running;

/// <summary>
/// What <c>csharpier check</c> reported: how many files it checked, or -1 without its summary line,
/// the files it would change, and its other lines.
/// </summary>
internal sealed record CSharpierReport(int CheckedFiles, ImmutableArray<string> Unformatted, ImmutableArray<string> Unknown)
{
	/// <summary>The other lines that are no warning, as the check exits with 1 for them as for a change.</summary>
	public ImmutableArray<string> Failures =>
		[
			.. Unknown.Where(static line =>
				!line.Contains(": warning", StringComparison.Ordinal) && !line.StartsWith("Warning", StringComparison.Ordinal)
			),
		];
}
