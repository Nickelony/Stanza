using System.Collections.Immutable;

namespace Nickelony.Stanza.Format.Planning;

/// <summary>The solution or a project, and the filter a command that formats through it passes.</summary>
internal sealed record FormatTarget(string Path, ImmutableArray<string> Filter);
