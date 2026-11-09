using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Nickelony.Stanza.Markup.Core.DocComments;
using Nickelony.Stanza.Markup.Core.Xml;

namespace Nickelony.Stanza.Markup.Core.Formatting;

/// <summary>Rewrites the body of each member element with the documentation layout.</summary>
internal static class MarkupReprint
{
	/// <summary>Collects the bodies in document order, as the walk visits the elements.</summary>
	public static List<MarkupReprintedBody> Collect(
		MarkupElement? root,
		MarkupSourceText sourceText,
		DocLayoutOptions reprintLayout,
		DocCleanupOptions cleanup = default,
		CancellationToken cancellationToken = default
	)
	{
		List<MarkupReprintedBody> bodies = [];

		if (root is null)
			return bodies;

		Stack<MarkupElement> pending = new();

		pending.Push(root);

		while (pending.Count > 0)
		{
			cancellationToken.ThrowIfCancellationRequested();

			MarkupElement element = pending.Pop();

			if (BuildReprintedBody(element, sourceText, reprintLayout, cleanup) is { } body)
			{
				bodies.Add(body);
				continue;
			}

			for (int index = element.ChildElements.Count - 1; index >= 0; index--)
				pending.Push(element.ChildElements[index]);
		}

		return bodies;
	}

	private static MarkupReprintedBody? BuildReprintedBody(
		MarkupElement element,
		MarkupSourceText sourceText,
		DocLayoutOptions reprintLayout,
		DocCleanupOptions cleanup
	)
	{
		// A member body that is not block-shaped, or that holds something the engine cannot read,
		// is left as written.
		if (!DocElementNames.IsMember(element.Name) || element.IsEmpty || element.CloseTagStart < element.OpenTagEnd || element.IsSpacePreserved)
			return null;

		// The parser gives an element its source text when it opens it, so an element with an end tag always has one.
		Debug.Assert(element.SourceText is not null, "An element with an end tag has no source text.");

		int start = element.OpenTagEnd;
		int end = element.CloseTagStart;

		string raw = sourceText.Text.Substring(start, end - start);

		if (!TryReadShape(raw, out string baseIndent, out string memberIndent))
			return null;

		// A body holding `&`, CDATA, a comment or a processing instruction is left as written.
		// The reader expands a character reference and resolves a CDATA section,
		// so the check reads the raw text, where the source still shows them.
		if (HoldsUnreadableText(raw))
			return null;

		DocLayoutOptions layout = reprintLayout.NarrowBy(reprintLayout.GetColumns(baseIndent));

		// The body begins on the line its start tag closes on, which a tag broken across lines leaves apart from the one it opens on.
		string lineEnding = sourceText.GetLineEnding(sourceText.GetLineFromOffset(start).LineIndex);
		MemberBodyShape shape = new(BaseIndent: baseIndent, LineEnding: lineEnding, MemberIndent: memberIndent);

		return Write(element, layout, cleanup, shape) is { } written
			? new MarkupReprintedBody(Span: new MarkupSpan(start, end - start), Text: written, LineIndex: element.StartLineIndex)
			: null;
	}

	private static bool HoldsUnreadableText(string raw)
	{
		return raw.IndexOf('&') >= 0
			|| raw.IndexOf("<![CDATA[", StringComparison.Ordinal) >= 0
			|| raw.IndexOf("<!--", StringComparison.Ordinal) >= 0
			|| raw.IndexOf("<?", StringComparison.Ordinal) >= 0;
	}

	private static bool TryReadShape(string raw, out string baseIndent, out string memberIndent)
	{
		baseIndent = string.Empty;
		memberIndent = string.Empty;

		int firstBreak = raw.IndexOf('\n');
		int lastBreak = raw.LastIndexOf('\n');

		// A bare carriage return is a line break the split below cannot see, so a body that holds one is left as written.
		if (firstBreak < 0 || lastBreak < 0 || HoldsBareCarriageReturn(raw))
			return false;

		// The member's opening line holds nothing but the layout that follows its tag,
		// and its closing line holds the indentation its end tag stands at.
		if (!MarkupWhitespace.IsBlank(raw.Substring(0, firstBreak)))
			return false;

		memberIndent = raw.Substring(lastBreak + 1);

		if (!MarkupWhitespace.IsBlank(memberIndent))
			return false;

		foreach (string line in raw.Split('\n'))
		{
			if (MarkupWhitespace.IsBlank(line))
				continue;

			// The indentation is the spaces and tabs before the text; a CRLF break leaves its carriage return at the line's end.
			int offset = 0;

			while (offset < line.Length && line[offset] is ' ' or '\t')
				offset++;

			baseIndent = line.Substring(0, offset);

			return true;
		}

		return false;
	}

	private static bool HoldsBareCarriageReturn(string raw)
	{
		for (int offset = raw.IndexOf('\r'); offset >= 0; offset = raw.IndexOf('\r', offset + 1))
		{
			if (offset + 1 >= raw.Length || raw[offset + 1] != '\n')
				return true;
		}

		return false;
	}

	private static string? Write(MarkupElement element, DocLayoutOptions layout, DocCleanupOptions cleanup, MemberBodyShape shape)
	{
		// The member itself stands at depth 0, so its children stand at depth 1.
		if (BuildMemberBody(element.Body, shape.BaseIndent) is not { } body)
			return null;

		// The clean-ups rewrite the body the walk lays out rather than the lines it writes,
		// so a sentence that gains a period is filled into the width the way any other is.
		body = DocCleanup.Apply(body, cleanup);

		if (DocWalk.Write(body, layout, markParagraphs: true, cleanup) is not { } lines)
			return null;

		StringBuilder builder = new();

		foreach (string line in lines)
		{
			builder.Append(shape.LineEnding);

			if (line.Length > 0)
				builder.Append(shape.BaseIndent);

			builder.Append(line);
		}

		builder.Append(shape.LineEnding).Append(shape.MemberIndent);

		return builder.ToString();
	}

	/// <summary>Reads the nodes of a member, keeping an element that holds a sample or a pointer as written.</summary>
	private static List<DocNode>? BuildMemberBody(IReadOnlyList<IMarkupBodyNode> nodes, string baseIndent)
	{
		List<DocNode> body = [];

		foreach (IMarkupBodyNode node in nodes)
		{
			DocNode? built =
				node is MarkupElement element && HoldsKeptElement(element, depth: 1)
					? BuildKeptElement(element, baseIndent)
					: BuildNode(node, depth: 1);

			if (built is null)
				return null;

			body.Add(built);
		}

		return body;
	}

	private static bool HoldsKeptElement(MarkupElement element, int depth)
	{
		if (DocElementNames.IsKeptAsWritten(element.LocalName))
			return true;

		if (depth >= DocWalk.MaxElementDepth)
			return false;

		foreach (MarkupElement child in element.ChildElements)
		{
			if (HoldsKeptElement(child, depth + 1))
				return true;
		}

		return false;
	}

	/// <summary>Reads an element as its lines, or <see langword="null"/> if one stands left of the body.</summary>
	private static DocElement? BuildKeptElement(MarkupElement element, string baseIndent)
	{
		if (element.SourceText is not { } sourceText)
			return null;

		string source = sourceText.Text;

		int start = element.OpenTagStart;
		int end = element.IsEmpty ? element.OpenTagEnd : element.CloseTagEnd;

		// The first line keeps the indentation before the element, unless other text stands there too.
		int lineStart = start;

		while (lineStart > 0 && source[lineStart - 1] != '\n' && MarkupWhitespace.IsXmlWhitespace(source[lineStart - 1]))
			lineStart--;

		bool isFirstOnLine = lineStart == 0 || source[lineStart - 1] == '\n';
		string before = isFirstOnLine ? source.Substring(lineStart, start - lineStart) : string.Empty;

		List<string> lines = [];
		string[] rows = source.Substring(start, end - start).Split('\n');

		for (int index = 0; index < rows.Length; index++)
		{
			string row = rows[index].TrimEnd('\r');

			if (index == 0 && !isFirstOnLine)
			{
				lines.Add(row);
				continue;
			}

			string line = index == 0 ? before + row : row;

			if (line.StartsWith(baseIndent, StringComparison.Ordinal))
				lines.Add(line.Substring(baseIndent.Length));
			else if (MarkupWhitespace.IsBlank(line))
				lines.Add(string.Empty);
			else
				return null;
		}

		string name = element.LocalName;

		return new DocElement(
			name,
			openTag: element.OpenTag,
			closeTag: element.CloseTag,
			isEmpty: element.IsEmpty,
			body: [],
			phrase: GetElementText(element, depth: 0)
		)
		{
			VerbatimLines = lines,
		};
	}

	private static List<DocNode>? BuildBody(IReadOnlyList<IMarkupBodyNode> nodes, int depth)
	{
		List<DocNode> body = [];

		foreach (IMarkupBodyNode node in nodes)
		{
			if (BuildNode(node, depth) is not { } built)
				return null;

			body.Add(built);
		}

		return body;
	}

	private static DocNode? BuildNode(IMarkupBodyNode node, int depth)
	{
		return node switch
		{
			MarkupText text => new DocText(text.Text),
			MarkupElement element => BuildElement(element, depth),
			_ => null,
		};
	}

	public static DocElement? BuildElement(MarkupElement element, int depth)
	{
		if (depth >= DocWalk.MaxElementDepth)
			return null;

		string name = element.LocalName;

		// An element the engine may not re-wrap leaves the whole body as written.
		if (
			DocElementNames.IsUnreadable(name)
			|| DocElementNames.TakesContentFromElsewhere(name)
			|| element.IsSpacePreserved
			|| BuildBody(element.Body, depth: depth + 1) is not { } body
		)
		{
			return null;
		}

		string openTag = element.OpenTag;
		string? closeTag = element.CloseTag;
		bool isEmpty = element.IsEmpty;
		string phrase = GetElementText(element, depth);

		return new DocElement(name, openTag, closeTag, isEmpty, body, phrase);
	}

	public static string GetElementText(MarkupElement element, int depth)
	{
		MarkupCollapser collapser = new();

		AppendElementText(collapser, element, depth);

		return collapser.ToString();
	}

	private static void AppendElementText(MarkupCollapser collapser, MarkupElement element, int depth)
	{
		if (depth >= DocWalk.MaxElementDepth)
			return;

		collapser.Append(element.OpenTag);

		if (element.IsEmpty)
			return;

		foreach (IMarkupBodyNode node in element.Body)
		{
			if (node is MarkupText text)
			{
				collapser.Append(text.Text);
				continue;
			}

			if (node is MarkupElement child)
				AppendElementText(collapser, child, depth + 1);
		}

		collapser.Append(element.CloseTag ?? string.Empty);
	}

	private readonly record struct MemberBodyShape(string BaseIndent, string LineEnding, string MemberIndent);
}
