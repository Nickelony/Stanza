using System.Collections.Generic;

namespace Nickelony.Stanza.Markup.Core.DocComments;

internal static partial class DocWalk
{
	/// <summary>Reads the body of one element into its blocks of prose and lists.</summary>
	private sealed class ParagraphReader(int depth)
	{
		private readonly List<DocBlock> _blocks = [];
		private DocBlock _prose = StartProse();

		private readonly DocWordReader _words = new();

		private bool _sawBlankLine;
		private bool _holdsUnmarkedBlankLine;

		private MarkupSpan? _firstParagraphBreak;

		/// <summary>A blank line after prose, which is a break only once something follows it.</summary>
		private MarkupSpan? _pendingParagraphBreak;

		/// <summary>
		/// Where the last break ends in the source, or -1 until a break has a position; a blank line spans from it to
		/// the start of the break that closes it.
		/// </summary>
		private int _previousBreakEnd = -1;

		private List<DocParagraph> Paragraphs => _prose.Paragraphs;
		private DocParagraph Current => Paragraphs[Paragraphs.Count - 1];

		public bool TryRead(DocNode node)
		{
			switch (node)
			{
				case DocText text:
					bool read = TryReadText(text);

					// A text counts as what follows the blank line once it adds a word to the paragraph the line opened.
					if (Current.Atoms.Count > 0)
						RecordParagraphBreak();

					return read;

				case DocElement child:
					RecordParagraphBreak();
					return TryReadElement(child);

				default:
					return false;
			}
		}

		private void RecordParagraphBreak()
		{
			_firstParagraphBreak ??= _pendingParagraphBreak;
			_pendingParagraphBreak = null;
		}

		public DocContent? Finish()
		{
			CloseProse(_blocks, _prose);

			return _blocks.Count == 0
				? null
				: new DocContent(
					blocks: _blocks,
					firstParagraphBreak: _firstParagraphBreak,
					sawBlankLine: _sawBlankLine,
					holdsUnmarkedBlankLine: _holdsUnmarkedBlankLine
				);
		}

		private bool TryReadText(DocText text)
		{
			return _words.TryRead(
				text.Text,
				atoms: () => Current.Atoms,

				readBreak: index =>
				{
					ReadBreak(FindBreak(text, index));
					return true;
				}
			);
		}

		private void ReadBreak(MarkupSpan? current)
		{
			if (_words.LineBreaks >= 2)
			{
				_sawBlankLine = true;

				// The blank line splits a paragraph that already holds text.
				// After a paragraph marker the split is made, but the blank line is still a break the layout drops.
				bool holdsText = Current.Atoms.Count > 0;
				bool followsAMarker = !holdsText && Paragraphs.Count > 1 && Paragraphs[Paragraphs.Count - 2].IsMarked;

				// Words after the earlier blank line confirm it, even if this one is never followed by any.
				if (holdsText)
					RecordParagraphBreak();

				if ((holdsText || followsAMarker) && current is { } span && _previousBreakEnd >= 0)
					_pendingParagraphBreak ??= new MarkupSpan(_previousBreakEnd, span.Start - _previousBreakEnd);

				if (holdsText)
					Paragraphs.Add(new DocParagraph());
			}

			if (current is { } end)
				_previousBreakEnd = end.Start + end.Length;
		}

		private bool TryReadElement(DocElement child)
		{
			string name = child.Name;

			// An element that holds nothing stands for a phrase however its name reads,
			// so it joins the text around it:
			// an empty `<code/>` is one word of the prose, not a block of its own.
			if (IsContentless(child))
			{
				_words.AddWord(Current.Atoms, GetContentlessTags(child));
				return true;
			}

			if (child.IsSpacePreserved)
				return false;

			// A list is a block of its own:
			// the prose written before it ends here, and the list stands between the two runs of text.
			if (DocElementNames.ListElements.Contains(name))
			{
				CloseProse(_blocks, _prose);

				_blocks.Add(new DocBlock { List = child });

				_prose = StartProse();
				_words.EndRun();

				return true;
			}

			if (DocElementNames.IsParagraphMarker(name))
			{
				if (!TryAddParagraphMarker(child))
					return false;

				// The marker owns its paragraphs, so the text after it opens one of its own.
				Paragraphs.Add(new DocParagraph());

				_words.EndRun();

				return true;
			}

			return TryAddNestedElement(child);
		}

		private bool TryAddParagraphMarker(DocElement element)
		{
			// A marker holds prose alone: one holding a list is left as written, with the block it stands in.
			if (ReadParagraphs(element, depth + 1) is not { IsProseOnly: true } inner)
				return false;

			_sawBlankLine |= inner.SawBlankLine;
			_holdsUnmarkedBlankLine |= inner.HoldsUnmarkedBlankLine;
			_firstParagraphBreak ??= inner.FirstParagraphBreak;

			string openTag = element.OpenTag;
			string closeTag = element.CloseTag ?? string.Empty;

			bool ownTags = true;

			foreach (DocParagraph paragraph in inner.Paragraphs)
			{
				DocParagraph marked = new()
				{
					IsMarked = true,
					OpenTag = ownTags ? openTag : paragraph.OpenTag,
					CloseTag = ownTags ? closeTag : paragraph.CloseTag,
				};

				marked.Atoms.AddRange(paragraph.Atoms);
				Paragraphs.Add(marked);
				ownTags = false;
			}

			return true;
		}

		private bool TryAddNestedElement(DocElement element)
		{
			// The element is one atom of this text, so it holds prose alone:
			// one that holds a list has no atom form, so the block it stands in is left as written.
			if (ReadParagraphs(element, depth + 1) is not { IsProseOnly: true } inner)
				return false;

			// As one atom of this text, the element leaves a blank line inside it unmarked, however deep it sits.
			_holdsUnmarkedBlankLine |= inner.HoldsBlankLine;

			_words.AddWord(Current.Atoms, element.Phrase);

			return true;
		}
	}
}
