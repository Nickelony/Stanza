using System;
using System.Collections.Immutable;

namespace Nickelony.Stanza.Format.Planning;

/// <summary>A step of a run: its commands or a check, where they run, and the files whose change repeats it.</summary>
/// <remarks>
/// A command that exits with one of <see cref="ChangeExitCodes"/> would change a file, and any other code but 0 failed.
/// </remarks>
internal sealed record FormatStep(
	string Title,
	FormatStepKind Kind,
	ImmutableArray<ImmutableArray<string>> Commands,
	ImmutableArray<string> Watched,
	ImmutableArray<int> ChangeExitCodes,
	string WorkingDirectory
)
{
	/// <summary>The files a check reports, or every file when unset.</summary>
	public ImmutableArray<string> Scope { get; init; }

	/// <summary>The command that starts the CSharpier server of a source layout check; a test seam.</summary>
	public ImmutableArray<string> Server { get; init; } = ["csharpier", "server"];

	/// <summary>How long the server may take to answer for one source before the run fails; a test seam.</summary>
	public TimeSpan ServerTimeout { get; init; } = TimeSpan.FromMinutes(2);
}
