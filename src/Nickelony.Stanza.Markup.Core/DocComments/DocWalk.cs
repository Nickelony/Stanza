using System.Collections.Generic;

namespace Nickelony.Stanza.Markup.Core.DocComments;

/// <summary>Writes a documentation body as the lines its layout gives it.</summary>
internal static partial class DocWalk
{
	/// <summary>A cap on element depth: the walk recurses, and a stack overflow cannot be caught.</summary>
	public const int MaxElementDepth = 32;

	/// <summary>Returns the laid-out lines, or <see langword="null"/> when the body is left as written.</summary>
	public static List<string>? Write(IReadOnlyList<DocNode> body, DocLayoutOptions layout, bool markParagraphs, bool closeParagraphs = false)
	{
		BodyWriter writer = new(layout, markParagraphs, closeParagraphs);

		foreach (DocNode node in body)
		{
			if (!writer.TryWrite(node))
				return null;
		}

		return writer.Finish();
	}

	public static DocContent? ReadParagraphs(DocElement element, int depth)
	{
		// A preserved element keeps its whitespace as content, whether it sets xml:space or inherits it,
		// so the block it stands in is left as written.
		if (element.IsSpacePreserved)
			return null;

		if (depth >= MaxElementDepth)
			return null;

		ParagraphReader reader = new(depth);

		foreach (DocNode node in element.Body)
		{
			if (!reader.TryRead(node))
				return null;
		}

		return reader.Finish();
	}

	private static bool TryAddTextLines(List<string> lines, IReadOnlyList<DocAtom> atoms, DocLayoutOptions layout, int depth)
	{
		string indent = layout.GetIndent(depth, text: true);
		DocLayoutOptions textLayout = layout.NarrowBy(layout.GetIndentationColumns(depth, text: true));

		// An element left no columns is not laid out:
		// a block cut into one word per line is worse than the one its author wrote.
		if (textLayout.ContentWidth <= 0)
			return false;

		foreach (string line in DocFill.LayOut(atoms, textLayout))
			lines.Add(indent + line);

		return true;
	}

	private static DocBlock StartProse()
	{
		DocBlock prose = new();
		prose.Paragraphs.Add(new DocParagraph());
		return prose;
	}

	private static void CloseProse(List<DocBlock> blocks, DocBlock prose)
	{
		prose.Paragraphs.RemoveAll(static paragraph => paragraph.Atoms.Count == 0);

		if (prose.Paragraphs.Count > 0)
			blocks.Add(prose);
	}

	private static MarkupSpan? FindBreak(DocText text, int index) => text.Breaks is { } breaks && index < breaks.Count ? breaks[index] : null;

	/// <summary>
	/// An element that holds nothing, a self-closing tag or an open tag and its end tag, stands as the author wrote it.
	/// </summary>
	private static bool IsContentless(DocElement element)
	{
		if (element.IsEmpty)
			return true;

		// A list owns its lines, so an empty one is laid out as a list rather than kept as a pair of tags.
		if (element.IsSpacePreserved || DocElementNames.ListElements.Contains(element.Name))
			return false;

		foreach (DocNode node in element.Body)
		{
			if (node is not DocText text || !MarkupWhitespace.IsBlank(text.Text))
				return false;
		}

		return true;
	}

	private static string GetContentlessTags(DocElement element) => element.IsEmpty ? element.OpenTag : element.OpenTag + element.CloseTag;

	private static List<DocAtom> CloseParagraph(List<DocAtom> atoms)
	{
		if (atoms.Count == 0 || DocTextRules.FindPeriodEnd(atoms[atoms.Count - 1].Text) is not { } end)
			return atoms;

		DocAtom last = atoms[atoms.Count - 1];
		List<DocAtom> closed = [.. atoms];

		closed[closed.Count - 1] = last with { Text = DocTextRules.WritePeriod(last.Text, end) };

		return closed;
	}
}
