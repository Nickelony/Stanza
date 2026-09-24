namespace Nickelony.Stanza.Markup.Cli;

internal enum MarkupFileOutcome
{
	Unchanged,
	Changed,

	/// <summary>
	/// The file could not be read, parsed, laid out or rewritten in its encoding; only <c>--strict</c> fails the run.
	/// </summary>
	Failed,

	/// <summary>The tool itself failed on the file; the run always fails.</summary>
	Defect,

	/// <summary>The file was formatted but the write failed; the run always fails.</summary>
	NotWritten,
}
