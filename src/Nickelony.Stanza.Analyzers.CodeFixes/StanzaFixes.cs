using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.CodeFixes;

/// <summary>Picks the change of the edit a diagnostic reports at out of the edits a family builds for a file.</summary>
internal static class StanzaFixes
{
	/// <summary>Offers a change only when it starts where the diagnostic stands.</summary>
	public static ImmutableArray<TextChange> FindChangeAt(ImmutableArray<TextChange> changes, int position)
	{
		foreach (TextChange change in changes)
		{
			if (change.Span.Start == position)
				return [change];
		}

		return [];
	}

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

	/// <summary>Offers the change of the edit whose span starts where a diagnostic stands.</summary>
	public static ImmutableArray<TextChange> FindChangeAt<TEdit>(IReadOnlyList<TEdit> edits, int position)
		where TEdit : IAnchoredEdit
	{
		foreach (TEdit edit in edits)
		{
			if (edit.Anchor.Start == position)
				return [edit.Change];
		}

		return [];
	}
}
