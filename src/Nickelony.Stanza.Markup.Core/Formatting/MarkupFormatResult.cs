using System.Collections.Generic;

namespace Nickelony.Stanza.Markup.Core.Formatting;

internal sealed record MarkupFormatResult(string Text, IReadOnlyList<MarkupEdit> Edits, MarkupFailure? Failure)
{
	public bool HasEdits => Edits.Count > 0;

	/// <summary>Whether the failure is the tool's own defect rather than a problem with the document.</summary>
	public bool IsDefect { get; init; }
}
