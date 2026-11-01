using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.CodeFixes;

/// <summary>Holds the fix helpers that more than one provider reads.</summary>
internal static class StanzaFixes
{
	/// <summary>Where a diagnostic's comment stands: its first extra location, else where it reports.</summary>
	public static int GetCommentPosition(Diagnostic diagnostic) =>
		diagnostic.AdditionalLocations.Count > 0 ? diagnostic.AdditionalLocations[0].SourceSpan.Start : diagnostic.Location.SourceSpan.Start;

	/// <summary>Offers the change of the edit a diagnostic reports at, by the span it reports.</summary>
	public static ImmutableArray<TextChange> FindChangeAt<TEdit>(IReadOnlyList<TEdit> edits, TextSpan anchor)
		where TEdit : IAnchoredEdit
	{
		foreach (TEdit edit in edits)
		{
			if (edit.Anchor == anchor)
				return [edit.Change];
		}

		return [];
	}
}
