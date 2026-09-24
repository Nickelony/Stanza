using System.Collections.Generic;
using System.Threading;
using System.Xml;
using Nickelony.Stanza.Markup.Core.DocComments;

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
			return new(Gaps: [], Removals: [], CommentMarkers: [], Failure: failure, Root: null);

		MarkupElement? root = parse.Root;

		// Comment markers are measured whether or not there is a root element to walk.
		List<MarkupCommentMarker> commentMarkers = [];

		CollectCommentMarkers(
			sourceText,
			comments: parse.Comments,
			skipsPropertyElements: options.Policy.SkipsPropertyElements,
			markers: commentMarkers
		);

		List<MarkupGap> gaps = [];
		List<MarkupRemoval> removals = [];

		if (root is not null)
		{
			foreach (MarkupElement element in EnumerateLaidOutElements(root, policy: options.Policy, skipsMembers, cancellationToken))
				CollectContainerGaps(element, options, sourceText, gaps, removals, cancellationToken);
		}

		return new(Gaps: gaps, Removals: removals, CommentMarkers: commentMarkers, Failure: null, Root: root);
	}

	private static IEnumerable<MarkupElement> EnumerateLaidOutElements(
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

			if (!skipped && CanLayOut(element))
				yield return element;

			for (int index = element.ChildElements.Count - 1; index >= 0; index--)
				pending.Push((element.ChildElements[index], skipped));
		}
	}

	private static void CollectContainerGaps(
		MarkupElement element,
		MarkupOptions options,
		MarkupSourceText sourceText,
		List<MarkupGap> gaps,
		List<MarkupRemoval> removals,
		CancellationToken cancellationToken
	)
	{
		IReadOnlyList<IMarkupItem> items = element.Items;
		int previousIndex = -1;

		for (int index = 0; index < items.Count; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (items[index] is not MarkupElement next)
				continue;

			if (previousIndex >= 0 && items[previousIndex] is MarkupElement previous)
			{
				int previousEndLineIndex = GetTrailingEndLineIndex(items, previousIndex, previous.EndLineIndex);

				// Elements sharing a line are left as written: the indentation a split needs is unknown.
				if (next.StartLineIndex > previousEndLineIndex)
				{
					int nextStartLineIndex = GetLeadingStartLineIndex(items, index, previousEndLineIndex);
					int blankLines = nextStartLineIndex - previousEndLineIndex - 1;

					if (blankLines == 0)
					{
						if (IsMultiLine(previous, options) || IsMultiLine(next, options))
							gaps.Add(new MarkupGap(previousEndLineIndex, nextStartLineIndex));
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
							removals.Add(new MarkupRemoval(gap, tightRuleId, 0));
						}
						else if (aboveCap)
						{
							removals.Add(
								new MarkupRemoval(gap, MarkupDiagnostics.CollapseExtraBlankLinesBetweenMarkupElementsId, options.MaxBlankLines)
							);
						}
					}
				}
			}

			previousIndex = index;
		}
	}

	/// <summary>
	/// A break is safe only in an element with element children and no mixed content or preserved whitespace.
	/// </summary>
	private static bool CanLayOut(MarkupElement element) => element.HasElementChildren && !element.HasCharacterData && !element.IsSpacePreserved;

	/// <summary>The tight rule that owns the gap, or <see langword="null"/> when none does.</summary>
	/// <remarks>
	/// ZA0103 judges only single-line neighbors, so it never owns a gap beside a multi-line one.
	/// </remarks>
	private static string? FindTightRuleId(MarkupElement previous, MarkupElement next, MarkupOptions options)
	{
		if (IsMultiLine(previous, options) || IsMultiLine(next, options))
			return MarkupDiagnostics.KeepMultiLineMarkupElementsTogetherId;

		return !SpansMoreThanOneLine(previous) && !SpansMoreThanOneLine(next) ? MarkupDiagnostics.KeepSingleLineMarkupElementsTogetherId : null;
	}

	private static bool IsMultiLine(MarkupElement element, MarkupOptions options) =>
		SpansMoreThanOneLine(element) && (!options.Policy.ContainersOnly || element.HasElementChildren);

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
}
