using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Nickelony.Stanza.Markup.Core.DocComments;
using Nickelony.Stanza.Markup.Core.Xml;

namespace Nickelony.Stanza.Markup.Core.Formatting;

/// <summary>Formats a markup document: its spacing and removal edits, then the reprint of its member bodies.</summary>
internal static class MarkupFormatter
{
	/// <summary>
	/// Applies edits to the source, so text outside each edit keeps its bytes, attributes and entities included.
	/// </summary>
	public static MarkupFormatResult Format(
		string source,
		MarkupOptions options,
		DocLayoutOptions? reprintLayout = null,
		long maxDocumentCharacters = MarkupLimits.DefaultMaxDocumentCharacters,
		CancellationToken cancellationToken = default
	)
	{
		var sourceText = MarkupSourceText.Build(source);

		MarkupFindings findings = MarkupAnalysis.Measure(
			sourceText,
			options,
			skipsMembers: reprintLayout is not null,
			maxDocumentCharacters,
			cancellationToken
		);

		if (findings.Failure is { } failure)
			return new(source, [], failure);

		List<MarkupEdit> edits = [];

		AddSpacingEdits(sourceText, findings, options, edits);
		AddRemovalEdits(sourceText, findings, options, edits);

		// The comment-marker space applies whatever a file enables unless it sets ZA0105 to none.
		if (!options.DisabledRules.Contains(MarkupDiagnostics.SeparateMarkupCommentMarkerFromTextId))
		{
			foreach (MarkupCommentMarker marker in findings.CommentMarkers)
				AddMarkerEdit(sourceText, marker, edits);
		}

		if (reprintLayout is { } layout && findings.Root is { } root)
		{
			var cleanup = DocCleanupOptions.From(options.Rules);

			foreach (MarkupReprintedBody body in MarkupReprint.Collect(root, sourceText, reprintLayout: layout, cleanup, cancellationToken))
				AddReprintEdit(sourceText, body, edits);
		}

		return Apply(source, sourceText, edits);
	}

	/// <summary>Applies the edits in document order, or refuses an overlapping list as a defect.</summary>
	public static MarkupFormatResult Apply(string source, MarkupSourceText sourceText, List<MarkupEdit> edits)
	{
		if (edits.Count == 0)
			return new(source, [], null);

		// The edits are gathered one kind at a time, and a child's after its parent's, so the list goes into document order.
		// The sort is stable, so two edits at one offset keep the order they were gathered in.
		edits = [.. edits.OrderBy(edit => edit.Change.Span.Start)];

		List<MarkupChange> changes = [.. edits.Select(edit => edit.Change)];

		// A defensive guard: the applier copies the document change by change,
		// so a list that overlaps or reaches past the end would throw.
		// The document is refused as a defect instead.
		return !IsSortedWithoutOverlap(sourceText, changes)
			? new(source, [], new MarkupFailure("overlapping edits", MarkupFailure.FirstLine, MarkupFailure.FirstColumn)) { IsDefect = true }
			: new(sourceText.Apply(changes), [.. edits], null);
	}

	private static void AddSpacingEdits(MarkupSourceText sourceText, MarkupFindings findings, MarkupOptions options, List<MarkupEdit> edits)
	{
		// The tool adds the ZA0101 blank line unless the file enables ZA0102 or sets ZA0101 to none.
		if (
			options.Rules.Contains(MarkupDiagnostics.KeepMultiLineMarkupElementsTogetherId)
			|| options.DisabledRules.Contains(MarkupDiagnostics.SeparateMultiLineMarkupElementsId)
		)
		{
			return;
		}

		foreach (MarkupGap gap in findings.Gaps)
		{
			edits.Add(
				new MarkupEdit(
					Change: CreateInsertion(sourceText, gap.NextStartLineIndex),
					Line: gap.NextStartLineIndex + 1,
					Message: MarkupDiagnostics.AddBlankLineMessage,
					Id: MarkupDiagnostics.SeparateMultiLineMarkupElementsId
				)
			);
		}
	}

	private static void AddRemovalEdits(MarkupSourceText sourceText, MarkupFindings findings, MarkupOptions options, List<MarkupEdit> edits)
	{
		foreach (MarkupRemoval removal in findings.Removals)
		{
			// The cap applies whatever a file enables unless it sets ZA0104 to none; every other removal waits for its rule.
			bool isCap = removal.Id == MarkupDiagnostics.CollapseExtraBlankLinesBetweenMarkupElementsId;

			if (isCap ? options.DisabledRules.Contains(removal.Id) : !options.Rules.Contains(removal.Id))
				continue;

			MarkupGap gap = removal.Gap;

			edits.Add(
				new MarkupEdit(
					Change: CreateCollapse(
						sourceText,
						previousEndLineIndex: gap.PreviousEndLineIndex,
						nextStartLineIndex: gap.NextStartLineIndex,
						keptBlankLines: removal.KeptBlankLines
					),

					Line: gap.NextStartLineIndex + 1,
					Message: isCap ? MarkupDiagnostics.RemoveBlankLinesMessage : MarkupDiagnostics.RemoveBlankLineMessage,
					Id: removal.Id
				)
			);
		}
	}

	private static bool IsSortedWithoutOverlap(MarkupSourceText sourceText, IReadOnlyList<MarkupChange> changes)
	{
		int previousEnd = 0;

		foreach (MarkupChange change in changes)
		{
			if (change.Span.Start < previousEnd || change.Span.End > sourceText.Length)
				return false;

			previousEnd = change.Span.End;
		}

		return true;
	}

	private static void AddMarkerEdit(MarkupSourceText sourceText, MarkupCommentMarker marker, List<MarkupEdit> edits)
	{
		int insertOffset = sourceText.GetLine(marker.LineIndex).Start + marker.SpaceOffset;

		edits.Add(
			new MarkupEdit(
				Change: new MarkupChange(new MarkupSpan(insertOffset, 0), " "),
				Line: marker.LineIndex + 1,
				Message: MarkupDiagnostics.AddSpaceMessage,
				Id: MarkupDiagnostics.SeparateMarkupCommentMarkerFromTextId,
				Column: marker.MarkerOffset + 1
			)
		);
	}

	private static MarkupChange CreateInsertion(MarkupSourceText sourceText, int nextStartLineIndex)
	{
		int start = sourceText.GetLine(nextStartLineIndex).Start;
		return new(new MarkupSpan(start, 0), GetGapEnding(sourceText, start, nextStartLineIndex));
	}

	private static MarkupChange CreateCollapse(MarkupSourceText sourceText, int previousEndLineIndex, int nextStartLineIndex, int keptBlankLines)
	{
		int start = sourceText.GetLine(previousEndLineIndex).EndIncludingLineBreak;

		// The collapsed gap repeats the ending of the next line, so a document that mixes line endings keeps each line's own.
		return new(
			MarkupSpan.FromBounds(start, sourceText.GetLine(nextStartLineIndex).Start),
			Repeat(GetGapEnding(sourceText, start, nextStartLineIndex), keptBlankLines)
		);
	}

	/// <summary>
	/// After a bare carriage return the gap writes another, since a line feed would join it into one CRLF break.
	/// </summary>
	private static string GetGapEnding(MarkupSourceText sourceText, int start, int nextStartLineIndex)
	{
		string ending = sourceText.GetLineEnding(nextStartLineIndex);
		return start > 0 && sourceText.Text[start - 1] == '\r' && ending.Length > 0 && ending[0] == '\n' ? "\r" : ending;
	}

	private static string Repeat(string value, int count)
	{
		if (count <= 0)
			return string.Empty;

		if (value.Length == 1)
			return new string(value[0], count);

		var builder = new StringBuilder(value.Length * count);

		for (int index = 0; index < count; index++)
			builder.Append(value);

		return builder.ToString();
	}

	private static void AddReprintEdit(MarkupSourceText sourceText, MarkupReprintedBody body, List<MarkupEdit> edits)
	{
		if (string.Equals(sourceText.Text.Substring(body.Span.Start, body.Span.Length), body.Text, StringComparison.Ordinal))
			return;

		edits.Add(
			new MarkupEdit(
				Change: new MarkupChange(body.Span, body.Text),
				Line: body.LineIndex + 1,
				Message: MarkupDiagnostics.ReprintBodyMessage,
				Id: MarkupDiagnostics.ReprintDocumentationMemberBodyId
			)
		);
	}
}
