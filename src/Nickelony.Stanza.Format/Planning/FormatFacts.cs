using System.Collections.Immutable;

namespace Nickelony.Stanza.Format.Planning;

/// <summary>What the repository holds; each path is relative to its root and written with forward slashes.</summary>
/// <remarks>
/// A skipped source is a changed C# file outside the solution's directory that no project of the solution compiles.
/// </remarks>
internal sealed record FormatFacts(
	string Solution,
	bool HasMarkup,
	ImmutableArray<string> ChangedFiles,
	ImmutableArray<string> SkippedSources,
	ImmutableArray<CompiledFile> CompiledFiles
);
