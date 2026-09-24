using System.Collections.Generic;

namespace Nickelony.Stanza.Markup.Core.DocComments;

internal static partial class DocWalk
{
	public static bool TryAddListLines(List<string> lines, DocElement list, DocLayoutOptions layout, int depth)
	{
		if (!TryReadListItems(list, layout, depth: depth + 1, out List<string> items))
			return false;

		string indent = layout.GetIndent(depth, text: false);

		lines.Add(indent + list.OpenTag);
		lines.AddRange(items);
		lines.Add(indent + (list.CloseTag ?? string.Empty));

		return true;
	}

	private static bool TryReadListItems(DocElement list, DocLayoutOptions layout, int depth, out List<string> items)
	{
		items = [];

		foreach (DocNode node in list.Body)
		{
			if (node is DocText text)
			{
				// The whitespace and the line breaks between two items are skipped,
				// so a blank line between them does not stop the list from being written.
				if (!MarkupWhitespace.IsBlank(text.Text))
					return false;

				continue;
			}

			if (node is not DocElement { IsEmpty: false } item)
				return false;

			if (!TryAddItemLines(lines: items, item, layout, depth))
				return false;
		}

		return true;
	}

	private static bool TryAddItemLines(List<string> lines, DocElement item, DocLayoutOptions layout, int depth)
	{
		if (!IsWritableItem(item, depth))
			return false;

		string openTag = item.OpenTag;
		string endTag = item.CloseTag ?? string.Empty;
		string indent = layout.GetIndent(depth, text: false);

		// An item holding a list at any depth is never folded, so that list's opening tag owns a line.
		// The fold is also refused for an item the expansion setting expands.
		string? folded = HoldsAList(item, depth: 0) || layout.IsExpanded(item.Name) ? null : FoldItem(item, depth);

		if (folded is not null && layout.GetIndentationColumns(depth, text: false) + layout.GetColumns(folded) <= layout.ContentWidth)
		{
			lines.Add(indent + folded);
			return true;
		}

		lines.Add(indent + openTag);

		if (!TryAddItemContentLines(lines, item, layout, depth))
			return false;

		lines.Add(indent + endTag);

		return true;
	}

	private static bool TryAddItemContentLines(List<string> lines, DocElement item, DocLayoutOptions layout, int depth)
	{
		List<DocAtom> atoms = [];
		ItemReader reader = new(atoms);

		foreach (DocNode node in item.Body)
		{
			if (node is DocText text)
			{
				if (!reader.TryAddText(text.Text))
					return false;

				continue;
			}

			if (node is not DocElement nested)
				return false;

			if (IsWordOfTheText(nested))
			{
				reader.AddWord(GetWordText(nested));
				continue;
			}

			// The words before a part of its own are the text of the item and end where the part opens.
			if (atoms.Count > 0)
			{
				if (!TryAddTextLines(lines, atoms, layout, depth))
					return false;

				atoms.Clear();
			}

			if (!TryAddItemLines(lines, item: nested, layout, depth: depth + 1))
				return false;

			reader.EndRun();
		}

		return atoms.Count == 0 || TryAddTextLines(lines, atoms, layout, depth);
	}

	/// <summary>
	/// The item on one line, from the atoms its expanded form writes so a format and the later check measure one text,
	/// or <see langword="null"/> when the item cannot fold.
	/// </summary>
	private static string? FoldItem(DocElement item, int depth)
	{
		if (depth >= MaxElementDepth)
			return null;

		List<DocAtom> atoms = [];
		ItemReader reader = new(atoms);

		foreach (DocNode node in item.Body)
		{
			if (node is DocText text)
			{
				if (!reader.TryAddText(text.Text))
					return null;

				continue;
			}

			if (node is not DocElement nested)
				return null;

			if (IsWordOfTheText(nested))
			{
				reader.AddWord(GetWordText(nested));
				continue;
			}

			// A part of its own folds onto the line too; the fold is refused when a part cannot fold.
			if (FoldItem(nested, depth + 1) is not { } folded)
				return null;

			reader.AddWord(folded);
		}

		return item.OpenTag + DocFill.JoinAtoms(atoms, 0, atoms.Count) + (item.CloseTag ?? string.Empty);
	}

	/// <summary>
	/// Inline and self-closing elements are words of the item's text; a term, description or list is a part of its own.
	/// </summary>
	private static bool IsWordOfTheText(DocElement element) => element.IsEmpty || DocElementNames.InlineElements.Contains(element.Name);

	private static string GetWordText(DocElement element) => element.IsEmpty ? element.OpenTag : element.Phrase;

	private static bool IsWritableItem(DocElement item, int depth)
	{
		string name = item.Name;

		if (depth >= MaxElementDepth || item.IsEmpty || DocElementNames.IsParagraphMarker(name) || DocElementNames.IsUnreadable(name))
			return false;

		foreach (DocNode node in item.Body)
		{
			if (node is DocElement { IsEmpty: false } nested && !IsWritableItem(nested, depth + 1))
				return false;
		}

		return true;
	}

	public static bool HoldsAList(DocElement element, int depth)
	{
		// Past the cap the search stops with a yes, so an item that deep is never folded onto one line.
		if (depth >= MaxElementDepth || DocElementNames.ListElements.Contains(element.Name))
			return true;

		foreach (DocNode node in element.Body)
		{
			if (node is DocElement nested && HoldsAList(nested, depth + 1))
				return true;
		}

		return false;
	}

	/// <summary>Reads the text of an item as a paragraph is read, so glue carries across text and elements.</summary>
	private sealed class ItemReader(List<DocAtom> atoms)
	{
		private readonly DocWordReader _words = new();

		/// <summary>An item holds no blank line, so one refuses the read.</summary>
		public bool TryAddText(string text) => _words.TryRead(text, atoms: () => atoms, readBreak: _ => _words.LineBreaks < 2);

		public void AddWord(string text) => _words.AddWord(atoms, text);

		public void EndRun() => _words.EndRun();
	}
}
