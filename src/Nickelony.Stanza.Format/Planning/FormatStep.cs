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
);
