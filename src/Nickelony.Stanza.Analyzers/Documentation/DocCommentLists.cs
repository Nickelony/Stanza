using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Nickelony.Stanza.Markup.Core.DocComments;

namespace Nickelony.Stanza.Analyzers.Documentation;

/// <summary>Builds the list layout and list item order edits of a documentation comment.</summary>
internal static class DocCommentLists
{
	public static List<DocCommentEdit> CreateListLayoutEdits(
		SyntaxTrivia trivia,
		DocumentationCommentTriviaSyntax comment,
		SourceText sourceText,
		DocLayoutOptions layout,
		CancellationToken cancellationToken
	)
	{
		// A comment that shares its line with code cannot be re-indented.
		if (DocCommentLines.FindCommentPrefix(sourceText, trivia) is not { } prefix)
			return [];

		if (DocCommentSyntax.IsDamaged(comment))
			return [];

		List<XmlElementSyntax> lists = [];
		CollectLists(comment.Content, lists);

		if (lists.Count == 0)
			return [];

		// A layout with no content width lays out no list, and the fix reads the same layout back,
		// so a report here could not be fixed.
		if (layout.ContentWidth <= 0)
			return [];

		var frame = DocCommentFrame.Read(sourceText, prefix, comment);

		List<DocCommentEdit> edits = [];

		foreach (XmlElementSyntax list in lists)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (!TryReadListLayout(list, sourceText, frame, layout, commentSpan: trivia.Span, out DocCommentEdit edit))
				continue;

			edits.Add(edit);
		}

		return edits;
	}

	private static void CollectLists(SyntaxList<XmlNodeSyntax> nodes, List<XmlElementSyntax> lists)
	{
		foreach (XmlNodeSyntax node in nodes)
		{
			if (node is not XmlElementSyntax element || !DocCommentSyntax.IsWithinDepth(element))
				continue;

			string name = element.StartTag.Name.LocalName.ValueText;

			if (DocElementNames.ListElements.Contains(name))
			{
				lists.Add(element);
				continue;
			}

			if (DocElementNames.OpaqueElements.Contains(name))
				continue;

			CollectLists(element.Content, lists);
		}
	}

	private static bool TryReadListLayout(
		XmlElementSyntax list,
		SourceText sourceText,
		DocCommentFrame frame,
		DocLayoutOptions layout,
		TextSpan commentSpan,
		out DocCommentEdit edit
	)
	{
		edit = default;
		DocCommentDelimiters delimiters = frame.Delimiters;

		TextLine first = sourceText.Lines.GetLineFromPosition(list.SpanStart);
		int openingLineIndex = sourceText.Lines.GetLineFromPosition(commentSpan.Start).LineNumber;

		if (DocCommentLines.StandsOnTheOpeningLine(delimiters, first.LineNumber, openingLineIndex))
			return false;

		TextLine last = sourceText.Lines[DocCommentLines.GetLastLineIndex(sourceText, list.Span)];

		// The run takes the lines the list stands on, so they must hold nothing but the list.
		// A list that follows other content on its first line, or is followed by other content on its last,
		// shares a line with a sentence and is left as it is.
		if (
			!DocCommentLines.StandsAfterAMarker(sourceText, line: first, position: list.SpanStart, delimiters)
			|| !StanzaText.IsWhitespaceOnly(sourceText, TextSpan.FromBounds(list.Span.End, last.End))
		)
		{
			return false;
		}

		if (DocCommentSyntax.BuildElement(list, depth: 0) is not { } element)
			return false;

		var region = TextSpan.FromBounds(first.Start, last.EndIncludingLineBreak);

		if (FindLineMarker(sourceText, commentSpan, region, frame) is not { } marker)
			return false;

		string current = sourceText.ToString(region);
		List<string> lines = [];

		if (!DocWalk.TryAddListLines(lines, list: element, layout, depth: DocCommentSyntax.GetElementDepth(list)))
			return false;

		string wanted = BuildListText(lines, marker, lineEnding: frame.LineEnding, trailing: DocCommentLines.GetTrailingLineBreak(current));

		// Nothing to change is never reported: the fix makes the same comparison.
		if (string.Equals(current, wanted, StringComparison.Ordinal))
			return false;

		edit = new DocCommentEdit(new TextChange(region, wanted), list.StartTag.Span);

		return true;
	}

	private static bool IsWritableItem(XmlElementSyntax item)
	{
		string name = item.StartTag.Name.LocalName.ValueText;

		if (!DocCommentSyntax.IsWithinDepth(item) || DocElementNames.IsParagraphMarker(name) || DocElementNames.IsUnreadable(name))
			return false;

		foreach (XmlNodeSyntax node in item.Content)
		{
			if (node is XmlElementSyntax nested && !IsWritableItem(nested))
				return false;
		}

		return true;
	}

	/// <summary>Finds the indentation and marker that each line of the list starts with.</summary>
	private static string? FindLineMarker(SourceText sourceText, TextSpan commentSpan, TextSpan region, DocCommentFrame frame)
	{
		if (!frame.Delimiters.IsBlock)
			return frame.Indentation + frame.Delimiters.Marker;

		int openingLineIndex = sourceText.Lines.GetLineFromPosition(commentSpan.Start).LineNumber;
		int closingLineIndex = DocCommentLines.GetLastLineIndex(sourceText, commentSpan);

		List<TextLine> listLines = [];
		List<TextLine> otherLines = [];

		for (int index = openingLineIndex + 1; index <= closingLineIndex; index++)
		{
			TextLine line = sourceText.Lines[index];

			if (index != closingLineIndex || !HoldsOnlyTheCloser(sourceText, line, commentSpan))
				(region.Contains(line.Start) ? listLines : otherLines).Add(line);
		}

		// The compiler strips a block's stars only when every line after the opening one has its star at one column,
		// leaving out a last line that holds only the closer.
		// So the list repeats the star the other lines share, or keeps its own when it has no other line,
		// and a comment whose lines share none, a starless one included, is left alone.
		return FindSharedStar(sourceText, otherLines.Count > 0 ? otherLines : listLines);
	}

	/// <summary>Whether the lines of a block comment carry stars unevenly.</summary>
	public static bool HasUnevenStars(SourceText sourceText, TextSpan commentSpan)
	{
		int openingLineIndex = sourceText.Lines.GetLineFromPosition(commentSpan.Start).LineNumber;
		int closingLineIndex = DocCommentLines.GetLastLineIndex(sourceText, commentSpan);

		string? shared = null;

		bool atOneColumn = true;

		int starred = 0;
		int lines = 0;

		for (int index = openingLineIndex + 1; index <= closingLineIndex; index++)
		{
			TextLine line = sourceText.Lines[index];

			if (index == closingLineIndex && HoldsOnlyTheCloser(sourceText, line, commentSpan))
				continue;

			lines++;

			if (FindStarMarker(sourceText, line) is not { } star)
				continue;

			starred++;
			atOneColumn &= shared is null || string.Equals(star, shared, StringComparison.Ordinal);
			shared = star;
		}

		return starred > 0 && (starred < lines || !atOneColumn);
	}

	/// <summary>Finds the star, with the indentation before it, that every line carries.</summary>
	private static string? FindSharedStar(SourceText sourceText, List<TextLine> lines)
	{
		string? shared = null;

		foreach (TextLine line in lines)
		{
			string? star = FindStarMarker(sourceText, line);

			if (star is null || (shared is not null && !string.Equals(star, shared, StringComparison.Ordinal)))
				return null;

			shared = star;
		}

		return shared;
	}

	private static string? FindStarMarker(SourceText sourceText, TextLine line)
	{
		int position = line.Start;

		while (position < line.End && char.IsWhiteSpace(sourceText[position]))
			position++;

		return position < line.End && sourceText[position] == '*' ? sourceText.ToString(TextSpan.FromBounds(line.Start, position + 1)) : null;
	}

	private static bool HoldsOnlyTheCloser(SourceText sourceText, TextLine line, TextSpan commentSpan) =>
		string.Equals(sourceText.ToString(TextSpan.FromBounds(line.Start, commentSpan.End)).Trim(), "*/", StringComparison.Ordinal);

	private static string BuildListText(List<string> lines, string marker, string lineEnding, string trailing)
	{
		StringBuilder builder = new();

		for (int index = 0; index < lines.Count; index++)
		{
			if (index > 0)
				builder.Append(lineEnding);

			builder.Append(DocCommentDelimiters.BuildLine(marker, lines[index]));
		}

		return builder.Append(trailing).ToString();
	}

	private static bool TryReadListItems(
		XmlElementSyntax list,
		SourceText sourceText,
		DocCommentDelimiters delimiters,
		out List<XmlElementSyntax> items
	)
	{
		items = [];

		TextLine first = sourceText.Lines.GetLineFromPosition(list.SpanStart);

		// Only the comment marker may precede the list's opening tag on its line;
		// otherwise, the items stand inside a sentence and no line of them is a line of the list alone.
		if (!DocCommentLines.StandsAfterAMarker(sourceText, line: first, position: list.SpanStart, delimiters))
			return false;

		foreach (XmlNodeSyntax node in list.Content)
		{
			if (node is not XmlElementSyntax item)
				continue;

			if (!IsWritableItem(item) || !StandsOnItsOwnLines(item, sourceText, delimiters))
				return false;

			items.Add(item);
		}

		return items.Count > 0;
	}

	/// <summary>Whether only a marker comes before the item, nothing follows it, and no blank line breaks it.</summary>
	private static bool StandsOnItsOwnLines(XmlElementSyntax item, SourceText sourceText, DocCommentDelimiters delimiters)
	{
		int firstLineIndex = sourceText.Lines.GetLineFromPosition(item.SpanStart).LineNumber;
		int lastLineIndex = DocCommentLines.GetLastLineIndex(sourceText, item.Span);

		if (
			!DocCommentLines.StandsAfterAMarker(sourceText, line: sourceText.Lines[firstLineIndex], position: item.SpanStart, delimiters)
			|| !StanzaText.IsWhitespaceOnly(sourceText, TextSpan.FromBounds(item.Span.End, sourceText.Lines[lastLineIndex].End))
		)
		{
			return false;
		}

		for (int index = firstLineIndex; index <= lastLineIndex; index++)
		{
			if (IsBlankCommentLine(sourceText, sourceText.Lines[index], delimiters))
				return false;
		}

		return true;
	}

	private static bool IsBlankCommentLine(SourceText sourceText, TextLine line, DocCommentDelimiters delimiters) =>
		StanzaText.IsBlankLine(sourceText, line.LineNumber) || DocCommentLines.StandsAfterAMarker(sourceText, line, position: line.End, delimiters);

	public static List<DocCommentEdit> CreateListItemOrderEdits(
		SyntaxTrivia trivia,
		DocumentationCommentTriviaSyntax comment,
		SourceText sourceText,
		CancellationToken cancellationToken
	)
	{
		if (DocCommentLines.FindCommentPrefix(sourceText, trivia) is null)
			return [];

		if (DocCommentSyntax.IsDamaged(comment))
			return [];

		List<XmlElementSyntax> lists = [];
		CollectLists(comment.Content, lists);

		if (lists.Count == 0)
			return [];

		DocCommentDelimiters delimiters = DocCommentSyntax.GetDelimiters(comment);

		List<DocCommentEdit> edits = [];

		foreach (XmlElementSyntax list in lists)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (!TryReadListItems(list, sourceText, delimiters, out List<XmlElementSyntax> items))
				continue;

			// An element the order does not name ends the run before it,
			// so the runs around it are ordered among themselves and the element stays in place.
			int[] ranks = new int[items.Count];

			for (int index = 0; index < items.Count; index++)
				ranks[index] = DocElementOrder.FindRank(DocElementOrder.ItemOrder, items[index].StartTag.Name.LocalName.ValueText);

			foreach ((int start, int end) in DocElementOrder.FindRuns(ranks, minLength: 1))
			{
				if (
					!TryReadOrderedItemsText(
						items: items.GetRange(start, end - start),
						sourceText,
						delimiters,
						out TextSpan region,
						out string text,
						out TextSpan anchor
					)
				)
				{
					continue;
				}

				edits.Add(new DocCommentEdit(new TextChange(region, text), anchor));
			}
		}

		return edits;
	}

	private static bool TryReadOrderedItemsText(
		List<XmlElementSyntax> items,
		SourceText sourceText,
		DocCommentDelimiters delimiters,
		out TextSpan region,
		out string text,
		out TextSpan anchor
	)
	{
		region = default;
		text = string.Empty;
		anchor = default;

		if (items.Count == 0)
			return false;

		int[] ranks = new int[items.Count];

		for (int index = 0; index < items.Count; index++)
		{
			ranks[index] = DocElementOrder.FindRank(DocElementOrder.ItemOrder, items[index].StartTag.Name.LocalName.ValueText);

			// The list caller passes only ranked names, but a row passes every child element,
			// so an unranked one leaves the run as it stands.
			if (ranks[index] < 0)
				return false;
		}

		XmlElementSyntax lastItem = items[items.Count - 1];

		TextLine firstLine = sourceText.Lines.GetLineFromPosition(items[0].SpanStart);
		TextLine lastLine = sourceText.Lines[DocCommentLines.GetLastLineIndex(sourceText, lastItem.Span)];

		region = TextSpan.FromBounds(firstLine.Start, lastLine.EndIncludingLineBreak);

		// Only the comment marker may precede the first item on its line,
		// and only whitespace may follow the last.
		// Otherwise, a tag sharing either line would travel with whichever item the order moves,
		// such as the <list> start tag before the first item or the </list> end tag after the last.
		if (
			!DocCommentLines.StandsAfterAMarker(sourceText, line: firstLine, position: items[0].SpanStart, delimiters)
			|| !StanzaText.IsWhitespaceOnly(sourceText, TextSpan.FromBounds(lastItem.Span.End, lastLine.End))
		)
		{
			return false;
		}

		string[] verbatim = new string[items.Count];
		string[] ordered = new string[items.Count];

		for (int index = 0; index < items.Count; index++)
		{
			verbatim[index] = GetItemRegionText(sourceText, items[index]);
			ordered[index] = BuildOrderedItemText(sourceText, items[index], delimiters);
		}

		string current = string.Concat(verbatim);

		// The region must hold the items and nothing else:
		// a blank line between two of them is the author's own break, and a line of prose would travel with them.
		if (!StanzaText.IsExactly(sourceText, region, current))
			return false;

		int[] order = DocElementOrder.ArrangeRanks(ranks, 0, items.Count);

		StringBuilder wanted = new();

		foreach (int index in order)
			wanted.Append(ordered[index]);

		string result = wanted.ToString();

		if (string.Equals(result, current, StringComparison.Ordinal))
			return false;

		text = result;
		anchor = items[DocElementOrder.FindMisplacedIndex(order) ?? 0].StartTag.Span;

		return true;
	}

	private static string BuildOrderedItemText(SourceText sourceText, XmlElementSyntax item, DocCommentDelimiters delimiters)
	{
		TextSpan region = GetItemRegion(sourceText, item);
		string text = sourceText.ToString(region);

		if (!DocElementOrder.IsRow(item.StartTag.Name.LocalName.ValueText))
			return text;

		List<XmlElementSyntax> children = [];

		foreach (XmlNodeSyntax node in item.Content)
		{
			if (node is XmlElementSyntax child)
				children.Add(child);
		}

		if (children.Count < 2)
			return text;

		bool ordered = TryReadOrderedItemsText(
			items: children,
			sourceText,
			delimiters,
			region: out TextSpan inner,
			text: out string innerText,
			anchor: out _
		);

		if (!ordered)
			return text;

		return string.Concat(text.Substring(0, inner.Start - region.Start), innerText, text.Substring(inner.End - region.Start));
	}

	private static TextSpan GetItemRegion(SourceText sourceText, XmlElementSyntax item)
	{
		return TextSpan.FromBounds(
			sourceText.Lines.GetLineFromPosition(item.SpanStart).Start,
			sourceText.Lines[DocCommentLines.GetLastLineIndex(sourceText, item.Span)].EndIncludingLineBreak
		);
	}

	private static string GetItemRegionText(SourceText sourceText, XmlElementSyntax item) => sourceText.ToString(GetItemRegion(sourceText, item));
}
