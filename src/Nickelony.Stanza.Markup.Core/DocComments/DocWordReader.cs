using System;
using System.Collections.Generic;
using System.Text;

namespace Nickelony.Stanza.Markup.Core.DocComments;

/// <summary>Reads prose into the atoms of a run: the one splitter and glue rule every reader of text shares.</summary>
internal sealed class DocWordReader
{
	/// <summary>
	/// Whether the last thing read ends without whitespace, so what is read straight after it joins that word.
	/// </summary>
	public bool Attached { get; set; }

	/// <summary>The line breaks read since the last word.</summary>
	public int LineBreaks { get; private set; }

	/// <summary>Reads a text into atoms, and hands each line break to the caller, which may refuse the read.</summary>
	public bool TryRead(string text, Func<List<DocAtom>> atoms, Func<int, bool> readBreak)
	{
		List<DocTextLine> lines = [.. DocTextLines.Split(text)];
		bool[] withinSpan = MarkSpanCharacters(text);

		int breakIndex = 0;
		bool continuesSpan = false;

		// The joins to the last atom build up in a builder, so a span over many lines is copied once, not once per line.
		List<DocAtom>? target = null;
		StringBuilder? open = null;

		for (int index = 0; index < lines.Count; index++)
		{
			if (lines[index].Text is not { } piece)
			{
				// A break inside a backtick span is a space of the span, which stays one atom.
				if (withinSpan[lines[index].Offset])
				{
					breakIndex++;
					continuesSpan = true;

					continue;
				}

				// The break may start another list of atoms, so the open word is written to the list it belongs to first.
				if (target is not null)
					DocFill.CloseWord(target, ref open);

				LineBreaks++;
				Attached = false;

				if (!readBreak(breakIndex++))
					return false;

				continue;
			}

			LineBreaks = 0;

			int plainBacktick = FindPlainBacktick(piece, lines[index].Offset, withinSpan);

			target = atoms();
			DocFill.AddWords(atoms: target, text: piece, attached: Attached, continuesSpan, plainBacktick, ref open);

			continuesSpan = false;
			Attached = DocFill.EndsTouching(piece);
		}

		if (target is not null)
			DocFill.CloseWord(target, ref open);

		return true;
	}

	public void AddWord(List<DocAtom> atoms, string text)
	{
		atoms.Add(new DocAtom(text, Attached));

		Attached = true;
		LineBreaks = 0;
	}

	public void EndRun()
	{
		Attached = false;
		LineBreaks = 0;
	}

	/// <summary>Marks each character a backtick span holds, as <see cref="TryRead"/> reads the spans.</summary>
	public static bool[] MarkSpanCharacters(string text)
	{
		bool[] within = new bool[text.Length];
		int open = -1;
		bool lineIsBlank = false;

		for (int offset = 0; offset < text.Length; offset++)
		{
			char character = text[offset];

			// A blank line ends the paragraph and so a span still open, whose backtick is then plain text.
			if (character == '\n')
			{
				if (lineIsBlank)
					open = -1;

				lineIsBlank = true;
				continue;
			}

			if (!MarkupWhitespace.IsXmlWhitespace(character))
				lineIsBlank = false;

			if (character != '`')
				continue;

			if (open < 0)
			{
				open = offset;
				continue;
			}

			for (int spanned = open; spanned <= offset; spanned++)
				within[spanned] = true;

			open = -1;
		}

		return within;
	}

	/// <summary>
	/// A backtick that opens a span with no closing backtick, on this line or a later one, is an ordinary character.
	/// </summary>
	private static int FindPlainBacktick(string piece, int offset, bool[] withinSpan)
	{
		int plain = -1;

		for (int index = 0; index < piece.Length; index++)
		{
			if (piece[index] == '`' && !withinSpan[offset + index])
				plain = index;
		}

		return plain;
	}
}
