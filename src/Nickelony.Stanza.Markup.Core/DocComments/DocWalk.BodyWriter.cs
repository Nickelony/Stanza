using System.Collections.Generic;

namespace Nickelony.Stanza.Markup.Core.DocComments;

internal static partial class DocWalk
{
	/// <summary>Writes the nodes of one body as laid-out lines.</summary>
	private sealed class BodyWriter(DocLayoutOptions layout, bool markParagraphs, bool closeParagraphs)
	{
		/// <summary>An element is a child of the block, so its tags stand at the indentation's top level.</summary>
		private const int ElementDepth = 0;

		/// <summary>A <c>&lt;para&gt;</c> an element writes stands one level further in.</summary>
		private const int ParagraphDepth = ElementDepth + 1;

		private readonly List<string> _lines = [];
		private readonly List<DocAtom> _run = [];

		private readonly DocWordReader _words = new();

		public bool TryWrite(DocNode node)
		{
			if (node is DocText text)
			{
				WriteText(text.Text);
				return true;
			}

			if (node is not DocElement child)
				return false;

			if (IsContentless(child))
			{
				WriteEmptyElement(child);
				return true;
			}

			if (!TryWriteElement(child))
				return false;

			// A run that still holds atoms is the one the element joined;
			// an element written as its own lines leaves no atom for the text after it to touch.
			_words.EndRun();
			_words.Attached = _run.Count > 0;

			return true;
		}

		public List<string>? Finish()
		{
			Flush();
			return _lines.Count == 0 ? null : _lines;
		}

		/// <summary>A run of line breaks writes one blank line however many breaks it holds.</summary>
		private void WriteText(string body)
		{
			_ = _words.TryRead(
				body,
				atoms: () => _run,

				readBreak: _ =>
				{
					if (_words.LineBreaks == 2)
						WriteBlankLine();

					return true;
				}
			);
		}

		private void WriteBlankLine()
		{
			Flush();
			_lines.Add(string.Empty);
		}

		private void WriteEmptyElement(DocElement child)
		{
			string tags = GetContentlessTags(child);

			if (DocElementNames.InlineElements.Contains(child.Name))
			{
				_words.AddWord(_run, tags);
				return;
			}

			Flush();
			_lines.Add(tags);
			_words.EndRun();
		}

		private bool TryWriteElement(DocElement element)
		{
			if (element.IsSpacePreserved)
				return false;

			string name = element.Name;

			// A list owns its own lines: the walk reads it as a shape rather than as prose,
			// so the run of text before it ends where the list opens.
			if (DocElementNames.ListElements.Contains(name))
			{
				Flush();
				return TryAddListLines(lines: _lines, list: element, layout, depth: ElementDepth);
			}

			DocContent? content = ReadParagraphs(element, depth: 0);

			if (content is null)
				return false;

			// A blank /// line inside an element is the author's own break rather than a line to wrap.
			// The element is left as written when its blank line cannot be marked as a paragraph break:
			// markParagraphs is off, or the blank line sits inside a nested element.
			if (content.HoldsUnmarkedBlankLine || (!markParagraphs && content.SawBlankLine))
				return false;

			// An element that wraps a phrase is one atom of the run around it, so it has no block form.
			bool wrapsAPhrase = DocElementNames.InlineElements.Contains(name);
			bool plainText = IsPlainText(content);

			if (content.IsProseOnly && plainText)
				return TryWritePlainText(element, name, atoms: content.Paragraphs[0].Atoms, wrapsAPhrase);

			// A phrase element with a list, more than one paragraph or a marked paragraph has no block form:
			// the block is left as written rather than gaining a `<para>` inside an element that cannot hold one.
			if (wrapsAPhrase)
				return false;

			// A paragraph marker holds prose alone, so a list inside one is left as written:
			// the marker keeps the one pair of tags the author wrote and has no form a list could stand in.
			if (!content.IsProseOnly && DocElementNames.IsParagraphMarker(name))
				return false;

			return TryWriteBlocks(element, name, content, plainText);
		}

		/// <summary>
		/// Whether the prose, lists aside, is exactly one paragraph that no <c>&lt;para&gt;</c> of the author's wraps.
		/// </summary>
		private static bool IsPlainText(DocContent content)
		{
			int paragraphs = 0;
			bool marked = false;

			foreach (DocBlock block in content.Blocks)
			{
				if (block.IsProse)
				{
					paragraphs += block.Paragraphs.Count;
					marked |= block.Paragraphs.Exists(static paragraph => paragraph.IsMarked);
				}
			}

			return paragraphs == 1 && !marked;
		}

		/// <summary>
		/// One line when the whole of it fits, both tags included, and a block when not or an expansion setting asks;
		/// an element that wraps a phrase joins the run around it however long the line turns out.
		/// </summary>
		private bool TryWritePlainText(DocElement element, string name, List<DocAtom> atoms, bool wrapsAPhrase)
		{
			string openTag = element.OpenTag;
			string closeTag = element.CloseTag ?? string.Empty;
			string text = DocFill.JoinAtoms(atoms, 0, atoms.Count);

			if (wrapsAPhrase)
			{
				_words.AddWord(_run, openTag + text + closeTag);
				return true;
			}

			Flush();

			string openIndent = layout.GetIndent(ElementDepth, text: false);

			if (
				!layout.IsExpanded(name)
				&& layout.GetIndentationColumns(ElementDepth, text: false)
					+ layout.GetColumns(openTag)
					+ layout.GetColumns(text)
					+ layout.GetColumns(closeTag)
					<= layout.ContentWidth
			)
			{
				_lines.Add(openIndent + openTag + text + closeTag);
				return true;
			}

			_lines.Add(openIndent + openTag);

			if (!TryAddTextLines(lines: _lines, atoms, layout, depth: ElementDepth))
				return false;

			_lines.Add(openIndent + closeTag);

			return true;
		}

		private bool TryWriteBlocks(DocElement element, string name, DocContent content, bool plainText)
		{
			Flush();

			string openTag = element.OpenTag;
			string closeTag = element.CloseTag ?? string.Empty;
			string openIndent = layout.GetIndent(ElementDepth, text: false);

			// A paragraph marker keeps the tag the author wrote (attributes and namespace prefix included),
			// as does the first paragraph of a nested marker.
			// Paragraphs a blank line splits off take a bare pair, since the author wrote no tag for those.
			bool wrap = !DocElementNames.IsParagraphMarker(name);
			int textDepth = wrap ? ParagraphDepth : ElementDepth;

			if (wrap)
				_lines.Add(openIndent + openTag);

			string paragraphIndent = layout.GetIndent(textDepth, text: false);
			bool ownTags = !wrap;

			foreach (DocBlock block in content.Blocks)
			{
				// A list owns its own lines:
				// its tags and every item stand at the depth the element's paragraphs use.
				if (block.List is { } list)
				{
					if (!TryAddListLines(lines: _lines, list, layout, depth: textDepth))
						return false;

					ownTags = false;
					continue;
				}

				foreach (DocParagraph paragraph in block.Paragraphs)
				{
					if (plainText)
					{
						if (!TryAddTextLines(lines: _lines, atoms: paragraph.Atoms, layout, depth: ElementDepth))
							return false;

						continue;
					}

					_lines.Add(paragraphIndent + (ownTags ? openTag : paragraph.OpenTag ?? "<para>"));

					// The next pass judges each paragraph on its own, so the period rule closes every one this pass writes.
					List<DocAtom> atoms = closeParagraphs ? CloseParagraph(paragraph.Atoms) : paragraph.Atoms;

					if (!TryAddTextLines(lines: _lines, atoms, layout, depth: textDepth))
						return false;

					_lines.Add(paragraphIndent + (ownTags ? closeTag : paragraph.CloseTag ?? "</para>"));
					ownTags = false;
				}
			}

			if (wrap)
				_lines.Add(openIndent + closeTag);

			return true;
		}

		private void Flush()
		{
			if (_run.Count == 0)
				return;

			_lines.AddRange(DocFill.LayOut(_run, layout));
			_run.Clear();
		}
	}
}
