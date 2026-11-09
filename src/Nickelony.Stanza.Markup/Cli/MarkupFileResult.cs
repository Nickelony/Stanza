using System.Collections.Immutable;
using Nickelony.Stanza.Markup.Core.Formatting;

namespace Nickelony.Stanza.Markup.Cli;

/// <summary>What a run did with one file, with the edits of a file it changed.</summary>
internal sealed record MarkupFileResult(MarkupFileOutcome Outcome, string DisplayPath, ImmutableArray<MarkupEdit> Edits)
{
	/// <summary>The result for a refused document, a defect of the tool or a refusal of the layout.</summary>
	public static MarkupFileResult ForFailure(string displayPath, MarkupFormatResult result) =>
		new(result.IsDefect ? MarkupFileOutcome.Defect : MarkupFileOutcome.Failed, displayPath, []);

	/// <summary>Whether the file fails the run; only a plain failure yields to the mode.</summary>
	public bool FailsTheRun(bool strict)
	{
		return Outcome switch
		{
			MarkupFileOutcome.Failed => strict,
			MarkupFileOutcome.Defect or MarkupFileOutcome.NotWritten => true,
			_ => false,
		};
	}
}
