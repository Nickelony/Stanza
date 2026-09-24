namespace Nickelony.Stanza.Format.Planning;

/// <summary>Whether a step runs its commands or a check of the runner's own.</summary>
internal enum FormatStepKind
{
	Commands,

	/// <summary>Runs the commands of a rule check, which also fails on a diagnostic that no fix can apply.</summary>
	Diagnostics,

	/// <summary>Runs the markup check, which also fails on a warning that no format run can fix.</summary>
	Markup,
	SourceLayout,
	LineEndings,
	Markers,
}
