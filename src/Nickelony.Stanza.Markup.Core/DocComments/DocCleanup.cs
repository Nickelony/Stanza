using System;
using System.Collections.Generic;
using System.Text;

namespace Nickelony.Stanza.Markup.Core.DocComments;

/// <summary>Applies the clean-ups to a documentation body, or finds the changes of one against the body.</summary>
internal static class DocCleanup
{
	/// <summary>Cleans the body up, as the tool writes it.</summary>
	public static List<DocNode> Apply(IReadOnlyList<DocNode> body, in DocCleanupOptions cleanup) => Apply(body, cleanup, changes: null);

	/// <summary>Finds the changes of one clean-up, each against the body as written.</summary>
	public static List<MarkupChange> FindChanges(IReadOnlyList<DocNode> body, DocCleanupKind kind, bool spacedSlash = false)
	{
		DocCleanupOptions cleanup = kind switch
		{
			DocCleanupKind.Padding => DocCleanupOptions.None with { Padding = true },
			DocCleanupKind.NullKeyword => DocCleanupOptions.None with { NullKeyword = true, SpacedSlash = spacedSlash },
			DocCleanupKind.Period => DocCleanupOptions.None with { Period = true },
			DocCleanupKind.EmptyElement => DocCleanupOptions.None with { EmptyElement = true, SpacedSlash = spacedSlash },
			_ => DocCleanupOptions.None with { SlashShape = true, SpacedSlash = spacedSlash },
		};

		List<MarkupChange> changes = [];

		// A pass rewrites a node into one without a source, so a recorded change matches the body as written only when one clean-up runs.
		_ = Apply(body, cleanup, changes);

		return changes;
	}

	private static List<DocNode> Apply(IReadOnlyList<DocNode> body, in DocCleanupOptions cleanup, List<MarkupChange>? changes)
	{
		if (cleanup.IsNone)
			return body as List<DocNode> ?? [.. body];

		List<DocNode> ordered = body as List<DocNode> ?? [.. body];

		if (cleanup.Padding)
			ordered = RemovePadding(ordered, changes);

		if (cleanup.ElementOrder)
			ordered = Reorder(ordered);

		// An opaque element at the top level, such as a code sample, leaves every keyword of the body as written.
		DocCleanupOptions transform = cleanup.NullKeyword && HoldsTopLevelOpaqueElement(ordered) ? cleanup with { NullKeyword = false } : cleanup;

		return Transform(body: ordered, cleanup: transform, changes, keywordsAreProse: true, depth: 0, prefix: string.Empty);
	}

	private static bool HoldsTopLevelOpaqueElement(List<DocNode> body) =>
		body.Exists(static node => node is DocElement element && DocElementNames.OpaqueElements.Contains(element.Name));

	/// <summary>
	/// An element whose content is not prose leaves the whole body as it is: the body yields no padding.
	/// </summary>
	private static List<DocNode> RemovePadding(List<DocNode> body, List<MarkupChange>? changes) =>
		HoldsOpaqueElement(body, depth: 0) ? body : RemovePadding(body, changes, elementsAreProse: false, depth: 0);

	private static List<DocNode> RemovePadding(List<DocNode> body, List<MarkupChange>? changes, bool elementsAreProse, int depth)
	{
		List<DocNode>? trimmed = null;

		for (int index = 0; index < body.Count; index++)
		{
			DocNode? kept = body[index] switch
			{
				DocText text => TrimPadding(
					text,
					precededByProse: StandsAsProse(body, index - 1, elementsAreProse),
					followedByProse: StandsAsProse(body, index + 1, elementsAreProse),
					changes
				),

				DocElement element => TrimElementPadding(element, changes, depth),
				_ => null,
			};

			if (kept is null)
				continue;

			trimmed ??= [.. body];
			trimmed[index] = kept;
		}

		return trimmed ?? body;
	}

	/// <summary>
	/// Inside an element every element stands as prose, so only the blank lines at its edges are padding.
	/// </summary>
	private static DocElement? TrimElementPadding(DocElement element, List<MarkupChange>? changes, int depth)
	{
		if (element.IsEmpty || element.IsSpacePreserved || depth + 1 >= DocWalk.MaxElementDepth)
			return null;

		List<DocNode> body = [.. element.Body];
		List<DocNode> trimmed = RemovePadding(body, changes, elementsAreProse: true, depth: depth + 1);

		return ReferenceEquals(trimmed, body) ? null : WithBody(element, trimmed);
	}

	private static DocText? TrimPadding(DocText text, bool precededByProse, bool followedByProse, List<MarkupChange>? changes)
	{
		if (DocTextRules.FindPadding(text.Text, precededByProse, followedByProse) is not { } padding)
			return null;

		StringBuilder trimmed = new(text.Text.Length);
		int kept = 0;

		foreach (MarkupSpan span in padding)
		{
			Record(
				changes,
				start: text.FindFileOffset(span.Start, startsSpan: false),
				end: text.FindFileOffset(span.Start + span.Length, startsSpan: false),
				newText: string.Empty
			);

			trimmed.Append(text.Text, kept, span.Start - kept);
			kept = span.Start + span.Length;
		}

		return new DocText(trimmed.Append(text.Text, kept, text.Text.Length - kept).ToString());
	}

	private static bool StandsAsProse(List<DocNode> body, int index, bool elementsAreProse) =>
		index >= 0 && index < body.Count && IsProse(body[index], elementsAreProse);

	private static bool IsProse(DocNode node, bool elementsAreProse)
	{
		return node switch
		{
			DocText text => !MarkupWhitespace.IsBlank(text.Text),
			DocElement element => elementsAreProse || DocElementNames.InlineElements.Contains(element.Name),
			_ => false,
		};
	}

	private static bool HoldsOpaqueElement(IReadOnlyList<DocNode> body, int depth)
	{
		foreach (DocNode node in body)
		{
			if (node is not DocElement element)
				continue;

			if (DocElementNames.OpaqueElements.Contains(element.Name))
				return true;

			if (depth + 1 < DocWalk.MaxElementDepth && HoldsOpaqueElement(element.Body, depth + 1))
				return true;
		}

		return false;
	}

	private static List<DocNode> Reorder(List<DocNode> body) => ReorderRuns(body, DocElementOrder.ElementOrder) ?? body;

	private static List<DocNode>? ReorderRuns(IReadOnlyList<DocNode> body, IReadOnlyList<string> order)
	{
		// Layout whitespace neither joins nor ends a run, so the runs are read over the other nodes alone.
		List<int> places = [];
		List<int> ranks = [];

		for (int index = 0; index < body.Count; index++)
		{
			if (IsLayoutWhitespace(body[index]))
				continue;

			places.Add(index);
			ranks.Add(FindRunRank(body[index], order));
		}

		List<DocNode>? reordered = null;

		foreach (DocMisorderedRun run in DocElementOrder.FindMisorderedRuns(ranks))
		{
			reordered ??= [.. body];

			for (int index = 0; index < run.Arrangement.Length; index++)
				reordered[places[run.Start + index]] = body[places[run.Start + run.Arrangement[index]]];
		}

		return reordered;
	}

	private static int FindRunRank(DocNode node, IReadOnlyList<string> order) =>
		node is DocElement element ? DocElementOrder.FindRank(order, element.Name) : -1;

	private static bool IsLayoutWhitespace(DocNode node) => node is DocText text && MarkupWhitespace.IsBlank(text.Text);

	private static DocElement OrderList(DocElement list, in DocCleanupOptions cleanup, int depth) =>
		cleanup.ListOrder ? OrderItems(list, depth) : list;

	private static DocElement OrderItems(DocElement element, int depth)
	{
		if (depth >= DocWalk.MaxElementDepth)
			return element;

		List<DocNode>? body = ReorderRuns(element.Body, DocElementOrder.ItemOrder);

		for (int index = 0; index < element.Body.Count; index++)
		{
			DocNode node = body is null ? element.Body[index] : body[index];

			if (node is not DocElement { IsEmpty: false } row || !DocElementOrder.IsRow(row.Name))
				continue;

			DocElement ordered = OrderItems(row, depth + 1);

			if (ReferenceEquals(ordered, row))
				continue;

			body ??= [.. element.Body];
			body[index] = ordered;
		}

		return body is null ? element : WithBody(element, body);
	}

	private static DocElement WithBody(DocElement element, IReadOnlyList<DocNode> body)
	{
		return new(
			name: element.Name,
			openTag: element.OpenTag,
			closeTag: element.CloseTag,
			isEmpty: element.IsEmpty,
			body,
			phrase: BuildPhrase(element.OpenTag, body, element.CloseTag),
			isSpacePreserved: element.IsSpacePreserved
		);
	}

	private static List<DocNode> Transform(
		List<DocNode> body,
		in DocCleanupOptions cleanup,
		List<MarkupChange>? changes,
		bool keywordsAreProse,
		int depth,
		string prefix
	)
	{
		List<DocNode> transformed = new(body.Count);

		foreach (DocNode node in body)
		{
			switch (node)
			{
				case DocText text:
					AddText(body: transformed, text, cleanup, changes, keywordsAreProse, prefix);
					break;

				case DocElement element:
					// Only the item order reaches inside a list, and it rewrites the items themselves.
					transformed.Add(
						DocElementNames.ListElements.Contains(element.Name)
							? OrderList(element, cleanup, depth)
							: TransformElement(element, cleanup, changes, keywordsAreProse, depth)
					);

					break;

				default:
					transformed.Add(node);
					break;
			}
		}

		return transformed;
	}

	private static void AddText(
		List<DocNode> body,
		DocText text,
		in DocCleanupOptions cleanup,
		List<MarkupChange>? changes,
		bool keywordsAreProse,
		string prefix
	)
	{
		if (!cleanup.NullKeyword || !keywordsAreProse)
		{
			body.Add(text);
			return;
		}

		List<int> offsets = DocTextRules.FindNullKeywords(text.Text);

		if (offsets.Count == 0)
		{
			body.Add(text);
			return;
		}

		string markup = DocTextRules.GetNullMarkup(cleanup.SpacedSlash, prefix);
		int copied = 0;

		foreach (int offset in offsets)
		{
			int end = offset + DocTextRules.NullKeywordText.Length;

			Record(changes, start: text.FindFileOffset(offset, startsSpan: true), end: text.FindFileOffset(end, startsSpan: false), newText: markup);

			if (offset > copied)
				body.Add(new DocText(text.Text.Substring(copied, offset - copied)));

			body.Add(BuildSelfClosingElement("see", markup));
			copied = end;
		}

		if (copied < text.Text.Length)
			body.Add(new DocText(text.Text.Substring(copied)));
	}

	private static DocElement TransformElement(
		DocElement element,
		in DocCleanupOptions cleanup,
		List<MarkupChange>? changes,
		bool keywordsAreProse,
		int depth
	)
	{
		// The recursion stops at the cap the callers share, so a nest deeper than any caller admits is left as it is.
		if (depth >= DocWalk.MaxElementDepth)
			return element;

		bool inline = DocElementNames.InlineElements.Contains(element.Name);
		bool opaque = DocElementNames.OpaqueElements.Contains(element.Name);

		if (element.IsEmpty)
		{
			if (!cleanup.SlashShape)
				return element;

			// A reader that kept the gap as written lets the clean-up change the gap alone, where the collapsed tag cannot say where it stood.
			if (element.SlashGap is { } gap && element.Source is { } source)
			{
				if (DocTextRules.ResolveSlashGap(gap, cleanup.SpacedSlash) is { } wanted)
					Record(changes, start: source.End - 2 - gap.Length, end: source.End - 2, newText: wanted);

				return element;
			}

			string shaped = DocTextRules.ShapeSlash(element.OpenTag, cleanup.SpacedSlash);
			return string.Equals(shaped, element.OpenTag, StringComparison.Ordinal) ? element : BuildSelfClosingElement(element.Name, shaped);
		}

		// Content that is not prose, or that keeps its whitespace, stays as written, and only the element's own shape may change.
		bool untouched = opaque || element.IsSpacePreserved;

		// An inline element wraps the text around it and an opaque one carries data rather than prose,
		// so neither takes the period or the null keyword clean-up.
		bool carriesProse = !inline && !opaque;

		bool keywordsInside = carriesProse && (keywordsAreProse || DocElementNames.IsParagraphMarker(element.Name));

		// The period is added before the keyword pass,
		// so a sentence ending in `null` still closes after the element that replaces it.
		List<DocNode> body = [.. element.Body];

		if (!untouched)
		{
			if (cleanup.Period && carriesProse)
				AddPeriod(body, changes);

			body = Transform(body, cleanup, changes, keywordsAreProse: keywordsInside, depth: depth + 1, prefix: GetPrefix(element.OpenTag));
		}

		// The whitespace of an element that keeps it is content, so only an element with no text at all shortens.
		if (cleanup.EmptyElement && (untouched ? HoldsNoText(body) : HoldsNothing(body)))
		{
			string tag = DocTextRules.BuildSelfClosingTag(element.OpenTag, cleanup.SpacedSlash);

			Record(changes, start: element.Source?.Start, end: element.Source?.End, newText: tag);

			return BuildSelfClosingElement(element.Name, tag);
		}

		if (untouched)
			return element;

		return new DocElement(
			name: element.Name,
			openTag: element.OpenTag,
			closeTag: element.CloseTag,
			isEmpty: false,
			body,
			phrase: BuildPhrase(element.OpenTag, body, element.CloseTag),
			isSpacePreserved: element.IsSpacePreserved
		);
	}

	/// <summary>The namespace prefix of a tag, with its colon, or nothing for a tag without one.</summary>
	private static string GetPrefix(string tag)
	{
		int end = 1;

		while (end < tag.Length && !MarkupWhitespace.IsXmlWhitespace(tag[end]) && tag[end] is not ('>' or '/'))
			end++;

		int colon = tag.LastIndexOf(':', end - 1, end - 1);

		return colon < 0 ? string.Empty : tag.Substring(1, colon);
	}

	private static DocElement BuildSelfClosingElement(string name, string tag) =>
		new(name, openTag: tag, closeTag: null, isEmpty: true, body: NoChildren, phrase: tag);

	private static bool HoldsNoText(IReadOnlyList<DocNode> body)
	{
		foreach (DocNode node in body)
		{
			if (node is not DocText { Text.Length: 0 })
				return false;
		}

		return true;
	}

	private static bool HoldsNothing(IReadOnlyList<DocNode> body)
	{
		foreach (DocNode node in body)
		{
			if (node is not DocText text || !MarkupWhitespace.IsBlank(text.Text))
				return false;
		}

		return true;
	}

	private static void AddPeriod(List<DocNode> body, List<MarkupChange>? changes)
	{
		for (int index = body.Count - 1; index >= 0; index--)
		{
			switch (body[index])
			{
				case DocText text when DocTextRules.ScanTextEnd(text.Text, out _) == DocTextEnd.Blank:
					continue;

				// A text that ends on a mark takes no period, however a word stands before it.
				case DocText text when DocTextRules.FindPeriodEnd(text.Text) is { } end:
					int? fileOffset = text.FindFileOffset(end, startsSpan: false);

					Record(changes, start: fileOffset, end: fileOffset, newText: ".");

					body[index] = new DocText(DocTextRules.WritePeriod(text.Text, end), text.Breaks);
					return;

				// A keyword element stands for the last word, so the period follows it.
				case DocElement { IsEmpty: true } element when DocTextRules.FindPeriodEnd(element.Phrase) is not null:
					Record(changes, start: element.Source?.End, end: element.Source?.End, newText: ".");

					body.Insert(index + 1, new DocText("."));
					return;

				default:
					return;
			}
		}
	}

	private static void Record(List<MarkupChange>? changes, int? start, int? end, string newText)
	{
		if (changes is not null && start is { } from && end is { } to && to >= from)
			changes.Add(new MarkupChange(new MarkupSpan(from, to - from), newText));
	}

	private static string BuildPhrase(string openTag, IReadOnlyList<DocNode> body, string? closeTag)
	{
		DocCollapser collapser = new();

		collapser.Append(openTag);

		foreach (DocNode node in body)
		{
			if (node is DocText text)
				collapser.Append(text.Text);
			else if (node is DocElement element)
				collapser.Append(element.Phrase);
		}

		collapser.Append(closeTag ?? string.Empty);

		return collapser.ToString();
	}

	private static IReadOnlyList<DocNode> NoChildren => [];
}
