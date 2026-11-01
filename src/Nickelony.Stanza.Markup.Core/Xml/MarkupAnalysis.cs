using System.Collections.Generic;
using System.Threading;
using System.Xml;
using Nickelony.Stanza.Markup.Core.DocComments;
using Nickelony.Stanza.Markup.Core.Options;

namespace Nickelony.Stanza.Markup.Core.Xml;

/// <summary>Parses a markup document and measures the gaps and findings its rules judge.</summary>
internal static class MarkupAnalysis
{
	public static MarkupFindings Measure(
		MarkupSourceText sourceText,
		MarkupOptions options,
		bool skipsMembers,
		long maxDocumentCharacters,
		CancellationToken cancellationToken
	) => Measure(sourceText, parse: Parse(sourceText, maxDocumentCharacters, cancellationToken), options, skipsMembers, cancellationToken);

	/// <summary>Parses the document once, so rules with different options can measure the one parse.</summary>
	public static MarkupParse Parse(MarkupSourceText sourceText, long maxDocumentCharacters, CancellationToken cancellationToken)
	{
		try
		{
			(MarkupElement? root, IReadOnlyList<MarkupComment> comments) = MarkupParser.Parse(
				sourceText,
				maxDocumentCharacters,
				cancellationToken: cancellationToken
			);

			return new MarkupParse(root, comments, Failure: null);
		}
		catch (XmlException exception)
		{
			return new MarkupParse(Root: null, Comments: [], MarkupFailure.From(exception));
		}
	}

	public static MarkupFindings Measure(
		MarkupSourceText sourceText,
		MarkupParse parse,
		MarkupOptions options,
		bool skipsMembers,
		CancellationToken cancellationToken
	)
	{
		if (parse.Failure is { } failure)
			return new(Gaps: [], CommentedGaps: [], Removals: [], CommentMarkers: [], SlashGaps: [], EmptyElements: [], Failure: failure, Root: null);

		MarkupElement? root = parse.Root;

		// Comment markers are measured whether or not there is a root element to walk.
		List<MarkupCommentMarker> commentMarkers = [];

		CollectCommentMarkers(
			sourceText,
			comments: parse.Comments,
			skipsPropertyElements: options.Policy.SkipsPropertyElements,
			markers: commentMarkers
		);

		GapLists gapLists = new();
		List<MarkupSlashGap> slashGaps = [];
		List<MarkupEmptyElement> emptyElements = [];

		if (root is not null)
		{
			// Padding, slash gaps and empty elements are measured only for a file that enables a rule that judges them, as they cost nothing otherwise.
			bool judgesPadding = options.Rules.Contains(MarkupDiagnostics.RemoveMarkupElementPaddingId);
			bool judgesEmptyElements = options.Rules.Contains(MarkupDiagnostics.WriteEmptyMarkupElementAsSelfClosingTagId);

			bool judgesSlash =
				options.Rules.Contains(MarkupDiagnostics.SeparateSlashOfSelfClosingMarkupElementId)
				|| options.Rules.Contains(MarkupDiagnostics.KeepSlashOfSelfClosingMarkupElementTightId);

			foreach (MarkupElement element in EnumerateElements(root, policy: options.Policy, skipsMembers, cancellationToken))
			{
				if (CanLayOut(element))
					CollectContainerGaps(element, options, sourceText, lists: gapLists, cancellationToken);

				if (judgesPadding && !element.HasCharacterData && !element.IsSpacePreserved)
					CollectPadding(element, sourceText, gapLists.Removals);

				if (judgesSlash)
					CollectSlashGap(element, sourceText, slashGaps);

				if (judgesEmptyElements)
					CollectEmptyElement(element, sourceText, emptyElements);
			}
		}

		return new(
			Gaps: gapLists.Gaps,
			CommentedGaps: gapLists.CommentedGaps,
			Removals: gapLists.Removals,
			CommentMarkers: commentMarkers,
			SlashGaps: slashGaps,
			EmptyElements: emptyElements,
			Failure: null,
			Root: root
		);
	}

	/// <summary>Walks the elements the rules may change, not skipped property elements or reprinted members.</summary>
	private static IEnumerable<MarkupElement> EnumerateElements(
		MarkupElement root,
		MarkupPolicy policy,
		bool skipsMembers,
		CancellationToken cancellationToken
	)
	{
		// A property element a policy skips carries its element children with it:
		// the whitespace inside one is part of its value at any depth.
		Stack<(MarkupElement Element, bool Skipped)> pending = new();

		pending.Push((root, Skipped: false));

		while (pending.Count > 0)
		{
			cancellationToken.ThrowIfCancellationRequested();

			(MarkupElement element, bool parentSkipped) = pending.Pop();

			// While the reprint is on, a member's body is the reprint's, written or declined:
			// a gap rule writing inside a declined member would change what the next pass reads, and so what it reprints.
			bool skipped =
				parentSkipped
				|| (policy.SkipsPropertyElements && element.IsPropertyElement)
				|| (skipsMembers && DocElementNames.IsMember(element.Name));

			if (!skipped)
				yield return element;

			for (int index = element.ChildElements.Count - 1; index >= 0; index--)
				pending.Push((element.ChildElements[index], skipped));
		}
	}

	private static void CollectContainerGaps(
		MarkupElement element,
		MarkupOptions options,
		MarkupSourceText sourceText,
		GapLists lists,
		CancellationToken cancellationToken
	)
	{
		IReadOnlyList<IMarkupItem> items = element.Items;
		int previousIndex = -1;

		// ZA2003 owns the gap above a comment where it is enabled, so ZA2004 leaves it and no commented gap stands.
		bool yieldsToTightRule = options.Rules.Contains(MarkupDiagnostics.KeepSingleLineMarkupElementsTogetherId);

		for (int index = 0; index < items.Count; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (items[index] is not MarkupElement next)
				continue;

			// This element becomes the previous one of the element after it, whichever way the pass ends.
			int beforeIndex = previousIndex;
			previousIndex = index;

			if (beforeIndex < 0 || items[beforeIndex] is not MarkupElement previous)
				continue;

			int previousEndLineIndex = GetTrailingEndLineIndex(items, beforeIndex, previous.EndLineIndex);

			// Elements sharing a line are left as written: the indentation a split needs is unknown.
			if (next.StartLineIndex <= previousEndLineIndex)
				continue;

			int nextStartLineIndex = GetLeadingStartLineIndex(items, index, previousEndLineIndex);
			int blankLines = nextStartLineIndex - previousEndLineIndex - 1;

			bool hasLeadingComment = nextStartLineIndex < next.StartLineIndex;

			if (blankLines == 0)
			{
				if (IsMultiLine(previous, options) || IsMultiLine(next, options))
					lists.Gaps.Add(new MarkupGap(previousEndLineIndex, nextStartLineIndex));
				else if (hasLeadingComment && !SpansMoreThanOneLine(previous) && !SpansMoreThanOneLine(next) && !yieldsToTightRule)
					lists.CommentedGaps.Add(new MarkupGap(previousEndLineIndex, nextStartLineIndex));
			}
			else if (IsBlankLineOnly(sourceText, previousEndLineIndex, nextStartLineIndex))
			{
				MarkupGap gap = new(previousEndLineIndex, nextStartLineIndex);

				string? tightRuleId = FindTightRuleId(previous, next, options);
				bool aboveCap = options.MaxBlankLines >= 1 && blankLines > options.MaxBlankLines;

				// A tight rule the file enables owns the whole gap, so the cap never cuts what it removes anyway.
				// Without one, a gap above the cap is cut back to the cap, and any other gap goes to the tight rule.
				if (tightRuleId is not null && (!aboveCap || options.Rules.Contains(tightRuleId)))
				{
					lists.Removals.Add(new MarkupRemoval(gap, tightRuleId, 0));
				}
				else if (aboveCap)
				{
					lists.Removals.Add(
						new MarkupRemoval(gap, MarkupDiagnostics.CollapseExtraBlankLinesBetweenMarkupElementsId, options.MaxBlankLines)
					);
				}
			}
		}
	}

	/// <summary>Measures the blank lines of an element beside its start tag and its end tag.</summary>
	private static void CollectPadding(MarkupElement element, MarkupSourceText sourceText, List<MarkupRemoval> removals)
	{
		IReadOnlyList<IMarkupItem> items = element.Items;

		if (items.Count == 0 || element.CloseTagStart < 0)
			return;

		// A comment on the line of a tag belongs to that tag, so the padding is measured from its end.
		int openEndLineIndex = sourceText.GetLineFromOffset(element.OpenTagEnd - 1).LineIndex;
		int first = 0;

		while (first < items.Count && IsNote(items[first]) && items[first].StartLineIndex == openEndLineIndex)
		{
			openEndLineIndex = items[first].EndLineIndex;
			first++;
		}

		if (first < items.Count)
			AddPadding(sourceText, previousEndLineIndex: openEndLineIndex, nextStartLineIndex: items[first].StartLineIndex, removals);

		int closeLineIndex = sourceText.GetLineFromOffset(element.CloseTagStart).LineIndex;
		int last = items.Count - 1;

		while (last >= first && IsNote(items[last]) && items[last].EndLineIndex == closeLineIndex)
		{
			closeLineIndex = items[last].StartLineIndex;
			last--;
		}

		if (last >= first)
			AddPadding(sourceText, previousEndLineIndex: items[last].EndLineIndex, nextStartLineIndex: closeLineIndex, removals);
		else if (first == items.Count)
			AddPadding(sourceText, previousEndLineIndex: openEndLineIndex, nextStartLineIndex: closeLineIndex, removals);
	}

	private static void AddPadding(MarkupSourceText sourceText, int previousEndLineIndex, int nextStartLineIndex, List<MarkupRemoval> removals)
	{
		if (nextStartLineIndex - previousEndLineIndex > 1 && IsBlankLineOnly(sourceText, previousEndLineIndex, nextStartLineIndex))
		{
			removals.Add(
				new MarkupRemoval(
					Gap: new MarkupGap(previousEndLineIndex, nextStartLineIndex),
					Id: MarkupDiagnostics.RemoveMarkupElementPaddingId,
					KeptBlankLines: 0
				)
			);
		}
	}

	private static bool IsNote(IMarkupItem item) => item is MarkupComment or MarkupProcessingInstruction;

	/// <summary>Measures the whitespace before the slash of a self-closing tag that sits on one line.</summary>
	private static void CollectSlashGap(MarkupElement element, MarkupSourceText sourceText, List<MarkupSlashGap> slashGaps)
	{
		// A tag in preserved space is left as written, like a comment marker there.
		if (!element.IsEmpty || element.IsSpacePreserved)
			return;

		int slash = element.OpenTagEnd - MarkupSlashGap.SlashLength;

		if (slash <= element.OpenTagStart || sourceText[slash] != '/')
			return;

		// The name keeps a character of its own, so the scan never reaches the opening bracket.
		int gapStart = slash;

		while (gapStart - 1 > element.OpenTagStart && MarkupWhitespace.IsXmlWhitespace(sourceText[gapStart - 1]))
			gapStart--;

		MarkupLine line = sourceText.GetLineFromOffset(slash);

		// A gap that spans a line is the wrapped layout of the tag, and stays as written.
		if (gapStart < line.Start)
			return;

		slashGaps.Add(
			new MarkupSlashGap(
				LineIndex: line.LineIndex,
				SlashOffset: slash - line.Start,
				GapLength: slash - gapStart,
				IsOneSpace: slash - gapStart == 1 && sourceText[gapStart] == ' '
			)
		);
	}

	/// <summary>Measures an element whose two tags stand against each other on one line.</summary>
	private static void CollectEmptyElement(MarkupElement element, MarkupSourceText sourceText, List<MarkupEmptyElement> emptyElements)
	{
		// A comment, an instruction, a CDATA section, a reference or whitespace puts a character between the tags,
		// so the tags stand against each other only where the element holds nothing at all.
		if (element.IsEmpty || element.CloseTagStart != element.OpenTagEnd || MarkupHtmlElements.RequiresEndTag(element.Name))
			return;

		int bracket = element.OpenTagEnd - 1;
		MarkupLine line = sourceText.GetLineFromOffset(bracket);

		// An end tag that wraps cannot be replaced from one line.
		if (sourceText.GetLineFromOffset(element.CloseTagEnd - 1).LineIndex != line.LineIndex)
			return;

		// The name keeps a character of its own, so the scan never reaches the opening bracket.
		int gapStart = bracket;

		while (gapStart - 1 > element.OpenTagStart && MarkupWhitespace.IsXmlWhitespace(sourceText[gapStart - 1]))
			gapStart--;

		// A gap that spans a line is the wrapped layout of the tag, and stays as written.
		bool wrapped = gapStart < line.Start;

		emptyElements.Add(
			new MarkupEmptyElement(
				LineIndex: line.LineIndex,
				BracketOffset: bracket - line.Start,
				Length: element.CloseTagEnd - bracket,
				GapLength: wrapped ? 0 : bracket - gapStart,
				HasWrappedGap: wrapped
			)
		);
	}

	/// <summary>
	/// A break is safe only in an element with element children and no mixed content or preserved whitespace.
	/// </summary>
	private static bool CanLayOut(MarkupElement element) => element.HasElementChildren && !element.HasCharacterData && !element.IsSpacePreserved;

	/// <summary>The tight rule that owns the gap, or <see langword="null"/> when none does.</summary>
	/// <remarks>
	/// ZA2003 judges only single-line neighbors, so it never owns a gap beside a multi-line one.
	/// </remarks>
	private static string? FindTightRuleId(MarkupElement previous, MarkupElement next, MarkupOptions options)
	{
		if (IsMultiLine(previous, options) || IsMultiLine(next, options))
			return MarkupDiagnostics.KeepMultiLineMarkupElementsTogetherId;

		return !SpansMoreThanOneLine(previous) && !SpansMoreThanOneLine(next) ? MarkupDiagnostics.KeepSingleLineMarkupElementsTogetherId : null;
	}

	/// <summary>Whether the element takes the blank lines of a multi-line one: it spans lines or is listed.</summary>
	private static bool IsMultiLine(MarkupElement element, MarkupOptions options)
	{
		if (options.SeparatedElements?.Contains(element.LocalName) == true)
			return true;

		return SpansMoreThanOneLine(element) && (!options.Policy.ContainersOnly || element.HasElementChildren);
	}

	private static bool SpansMoreThanOneLine(MarkupElement element) => element.StartLineIndex != element.EndLineIndex;

	private static int GetLeadingStartLineIndex(IReadOnlyList<IMarkupItem> items, int elementIndex, int floor)
	{
		int startLineIndex = items[elementIndex].StartLineIndex;

		for (int index = elementIndex - 1; index >= 0; index--)
		{
			IMarkupItem item = items[index];

			if (item is not (MarkupComment or MarkupProcessingInstruction) || startLineIndex - item.EndLineIndex != 1 || item.StartLineIndex <= floor)
				break;

			startLineIndex = item.StartLineIndex;
		}

		return startLineIndex;
	}

	private static int GetTrailingEndLineIndex(IReadOnlyList<IMarkupItem> items, int elementIndex, int endLineIndex)
	{
		for (int index = elementIndex + 1; index < items.Count; index++)
		{
			IMarkupItem item = items[index];

			if (item is not (MarkupComment or MarkupProcessingInstruction) || item.StartLineIndex != endLineIndex)
				break;

			endLineIndex = item.EndLineIndex;
		}

		return endLineIndex;
	}

	private static bool IsBlankLineOnly(MarkupSourceText sourceText, int previousEndLineIndex, int nextStartLineIndex) =>
		sourceText.IsBlank(MarkupSpan.FromBounds(sourceText.GetLine(previousEndLineIndex).End, sourceText.GetLine(nextStartLineIndex).Start));

	private static void CollectCommentMarkers(
		MarkupSourceText sourceText,
		IReadOnlyList<MarkupComment> comments,
		bool skipsPropertyElements,
		List<MarkupCommentMarker> markers
	)
	{
		foreach (MarkupComment comment in comments)
		{
			// A preserved comment is content, so its markers are left as written,
			// and so is a comment inside a property element a policy skips:
			// the whitespace of that element is part of its value.
			if (comment.IsSpacePreserved || (skipsPropertyElements && comment.IsInPropertyElement))
				continue;

			// An empty comment has no text to stand apart from its two markers.
			if (comment.CloseStart - comment.Start <= MarkupComment.OpenerLength)
				continue;

			AddCommentMarker(
				sourceText,
				markerStart: comment.Start,
				markerLength: MarkupComment.OpenerLength,
				textOffset: comment.Start + MarkupComment.OpenerLength,
				insertOffset: comment.Start + MarkupComment.OpenerLength,
				markers
			);

			AddCommentMarker(
				sourceText,
				markerStart: comment.CloseStart,
				markerLength: MarkupComment.CloserLength,
				textOffset: comment.CloseStart - 1,
				insertOffset: comment.CloseStart,
				markers
			);
		}
	}

	private static void AddCommentMarker(
		MarkupSourceText sourceText,
		int markerStart,
		int markerLength,
		int textOffset,
		int insertOffset,
		List<MarkupCommentMarker> markers
	)
	{
		// char.IsWhiteSpace is asked on purpose rather than MarkupWhitespace:
		// a character such as U+00A0 already separates the marker from its text, and the code fix asks the same test.
		if (char.IsWhiteSpace(sourceText[textOffset]))
			return;

		MarkupLine markerLine = sourceText.GetLineFromOffset(markerStart);

		markers.Add(
			new MarkupCommentMarker(
				LineIndex: markerLine.LineIndex,
				MarkerOffset: markerStart - markerLine.Start,
				Length: markerLength,
				SpaceOffset: insertOffset - markerLine.Start
			)
		);
	}

	/// <summary>The lists the gap pass fills, held together so one instance serves a whole measure.</summary>
	private sealed class GapLists
	{
		public List<MarkupGap> Gaps { get; } = [];
		public List<MarkupGap> CommentedGaps { get; } = [];
		public List<MarkupRemoval> Removals { get; } = [];
	}
}
