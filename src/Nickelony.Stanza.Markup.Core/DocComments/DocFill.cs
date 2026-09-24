using System;
using System.Collections.Generic;
using System.Text;

namespace Nickelony.Stanza.Markup.Core.DocComments;

/// <summary>Fills the words of a documentation text into lines of the layout's width, at the cheapest breaks.</summary>
internal static class DocFill
{
	private static readonly char[] s_clauseMarks = [',', '.', ':', ';', '!', '?', '-', '\u2013', '\u2014', '\u2026', ')', ']'];
	private static readonly char[] s_openingMarks = ['(', '['];
	private static readonly char[] s_whitespace = [' ', '\t', '\r', '\n'];
	private static readonly char[] s_closingMarks = [',', '.', ';', ':', '!', '?', ')', ']', '}'];

	private const double TailPenalty = 10_000;
	private const double WeakTailPenalty = 10_000;
	private const double RuntPenalty = 100_000;
	private const double ToleratedPenalty = 1_000;
	private const double OverflowPenalty = 1_000_000;

	/// <summary>The candidate lines this thread's fills priced; a test seam for a bound without a clock.</summary>
	[ThreadStatic]
	public static long LayOutSteps;

	/// <summary>Adds the words of one line of text; a backtick span is one word, spaces and all.</summary>
	public static void AddWords(List<DocAtom> atoms, string text, bool attached, bool continuesSpan = false, int plainBacktick = -1)
	{
		bool glued = attached && text.Length > 0 && !MarkupWhitespace.IsXmlWhitespace(text[0]);
		bool first = true;
		bool withinSpan = continuesSpan;

		int offset = 0;

		while (offset < text.Length)
		{
			if (!withinSpan && IsWordBreak(text[offset]))
			{
				offset++;
				continue;
			}

			// The span the line before opened goes on in this line, after the one space its line break stands for.
			bool joinsSpan = withinSpan && atoms.Count > 0;

			if (joinsSpan)
			{
				while (offset < text.Length && IsWordBreak(text[offset]))
					offset++;
			}

			int start = offset;
			offset = GetWordEnd(text, start, plainBacktick, ref withinSpan);

			// A span that runs on past the line ends its part here without the whitespace before the break.
			string word = text.Substring(start, offset - start);

			if (withinSpan)
				word = word.TrimEnd(s_whitespace);

			bool firstWord = first;
			first = false;

			if (joinsSpan)
			{
				DocAtom open = atoms[atoms.Count - 1];
				atoms[atoms.Count - 1] = new DocAtom(open.Text + " " + word, open.IsGlued);

				continue;
			}

			// A word made of closing marks alone belongs to the word before it, wherever the line puts it,
			// so neither a space nor a break may come between the two.
			if (atoms.Count > 0 && IsOnlyClosingMarks(word))
			{
				DocAtom previous = atoms[atoms.Count - 1];
				atoms[atoms.Count - 1] = new DocAtom(previous.Text + word, previous.IsGlued);

				continue;
			}

			atoms.Add(new DocAtom(word, glued && firstWord));
		}
	}

	private static bool IsWordBreak(char character) => MarkupWhitespace.IsXmlWhitespace(character);

	private static int GetWordEnd(string text, int start, int plainBacktick, ref bool withinSpan)
	{
		int offset = start;

		for (; offset < text.Length; offset++)
		{
			char character = text[offset];

			if (character == '`' && offset != plainBacktick)
				withinSpan = !withinSpan;
			else if (!withinSpan && IsWordBreak(character))
				break;
		}

		return offset;
	}

	private static bool IsOnlyClosingMarks(string word)
	{
		foreach (char character in word)
		{
			if (Array.IndexOf(s_closingMarks, character) < 0)
				return false;
		}

		return word.Length > 0;
	}

	public static bool EndsTouching(string text) => text.Length > 0 && !MarkupWhitespace.IsXmlWhitespace(text[text.Length - 1]);

	public static string JoinAtoms(IReadOnlyList<DocAtom> atoms, int start, int count)
	{
		StringBuilder text = new();

		for (int index = start; index < start + count; index++)
		{
			if (index > start && !atoms[index].IsGlued)
				text.Append(' ');

			text.Append(atoms[index].Text);
		}

		return text.ToString();
	}

	public static List<string> LayOut(IReadOnlyList<DocAtom> atoms, DocLayoutOptions layout)
	{
		int count = atoms.Count;

		// A run of one atom has one arrangement, so the search is skipped for it.
		if (count <= 1)
			return count == 0 ? [] : [atoms[0].Text];

		// An atom is measured in columns, so a tab counts as more than one.
		int[] widths = new int[count];

		for (int index = 0; index < count; index++)
			widths[index] = layout.GetColumns(atoms[index].Text);

		int total = 0;

		for (int index = 0; index < count; index++)
			total += widths[index] + (index == 0 || atoms[index].IsGlued ? 0 : 1);

		// A run that fits one line is returned as that line:
		// no break can beat it, since the line a break ends pays the slack it leaves behind.
		if (total <= layout.ContentWidth)
			return [JoinAtoms(atoms, 0, count)];

		int[] next = FindBreaks(atoms, layout, new LineFacts(atoms, widths, layout));

		List<string> lines = [];

		for (int index = 0; index < count; index = next[index])
			lines.Add(JoinAtoms(atoms, index, next[index] - index));

		return lines;
	}

	/// <summary>For each atom, the atom the cheapest layout of the rest of the run starts its next line at.</summary>
	private static int[] FindBreaks(IReadOnlyList<DocAtom> atoms, DocLayoutOptions layout, LineFacts facts)
	{
		int count = atoms.Count;

		double[] best = new double[count + 1];
		int[] next = new int[count + 1];

		for (int index = 0; index <= count; index++)
			best[index] = double.PositiveInfinity;

		best[count] = 0;

		long steps = 0;

		for (int start = count - 1; start >= 0; start--)
		{
			for (int end = start; end < count; end++)
			{
				steps++;

				int slack = layout.ContentWidth - facts.GetLength(start, end);

				// A word that touches the one before it may not be moved onto the next line:
				// a break there would put a space where the author wrote none.
				if (end + 1 < count && atoms[end + 1].IsGlued)
				{
					// Past the tolerated overflow the rest of the run only adds columns, so the search goes to where the run ends.
					if (-slack > layout.MaxOverflowColumns)
						end = facts.RunEnds[end] - 1;

					continue;
				}

				double cost = best[end + 1] + facts.GetLineCost(start, end, slack);

				if (cost < best[start])
				{
					best[start] = cost;
					next[start] = end + 1;
				}

				// Past the tolerated overflow each column costs more than every other penalty of the rest of the run,
				// so a longer line never wins and the search for this start ends at its first legal break there.
				if (-slack > layout.MaxOverflowColumns)
					break;
			}
		}

		LayOutSteps += steps;

		return next;
	}

	private static bool OpensAClause(string atom) => atom.Length > 0 && Array.IndexOf(s_openingMarks, atom[0]) >= 0;

	private static string[] NormalizeAtoms(IReadOnlyList<DocAtom> atoms)
	{
		string[] normalized = new string[atoms.Count];

		for (int index = 0; index < atoms.Count; index++)
			normalized[index] = DocTextRules.NormalizeWord(atoms[index].Text);

		return normalized;
	}

	private static bool IsClauseEnd(string atom) => atom.Length > 0 && Array.IndexOf(s_clauseMarks, atom[atom.Length - 1]) >= 0;

	/// <summary>What the search reads of each candidate line, computed once per run.</summary>
	private sealed class LineFacts
	{
		private readonly int[] _columnsBefore;
		private readonly int[] _spacesBefore;
		private readonly int[] _lastMarks;
		private readonly int[] _tailStarts;
		private readonly int[] _nextClauseEnds;

		private readonly DocLayoutOptions _layout;
		private readonly DocNameSet? _weak;

		/// <summary>
		/// The atoms normalized as stanza_doc_weak_tail_words normalizes its words, once, not per candidate line.
		/// </summary>
		private readonly string[] _normalized;

		public LineFacts(IReadOnlyList<DocAtom> atoms, int[] widths, DocLayoutOptions layout)
		{
			int count = atoms.Count;

			_layout = layout;
			_weak = layout.WeakTailWords;
			_normalized = _weak is null ? [] : NormalizeAtoms(atoms);

			_columnsBefore = new int[count + 1];
			_spacesBefore = new int[count + 1];
			_lastMarks = new int[count];
			_tailStarts = new int[count];
			_nextClauseEnds = new int[count + 1];
			RunEnds = new int[count];

			for (int index = 0; index < count; index++)
			{
				_columnsBefore[index + 1] = _columnsBefore[index] + widths[index];
				_spacesBefore[index + 1] = _spacesBefore[index] + (atoms[index].IsGlued ? 0 : 1);

				// A word that ends a clause wins the tie over a word that opens one, since the run after the end mark is the tail.
				string text = atoms[index].Text;
				bool endsAClause = IsClauseEnd(text);

				if (endsAClause || OpensAClause(text))
				{
					_lastMarks[index] = index;
					_tailStarts[index] = endsAClause ? index + 1 : index;
				}
				else
				{
					_lastMarks[index] = index == 0 ? -1 : _lastMarks[index - 1];
					_tailStarts[index] = index == 0 ? -1 : _tailStarts[index - 1];
				}
			}

			_nextClauseEnds[count] = count;

			for (int index = count - 1; index >= 0; index--)
			{
				_nextClauseEnds[index] = IsClauseEnd(atoms[index].Text) ? index : _nextClauseEnds[index + 1];
				RunEnds[index] = index + 1 < count && atoms[index + 1].IsGlued ? RunEnds[index + 1] : index;
			}
		}

		/// <summary>The last atom of the glued run each atom stands in, where a line may end.</summary>
		public int[] RunEnds { get; }

		/// <summary>
		/// The space before an atom counts unless the atom opens the line or is glued to the one before it.
		/// </summary>
		public int GetLength(int start, int end) =>
			_columnsBefore[end + 1] - _columnsBefore[start] + _spacesBefore[end + 1] - _spacesBefore[start + 1];

		public int FindTailStart(int start, int end) => _lastMarks[end] >= start ? _tailStarts[end] : -1;

		/// <summary>
		/// The window is capped at the atom count, so a large minimum cannot overflow the last index to a negative one.
		/// </summary>
		public bool LeavesAFragmentBeforeAClauseEnd(int end, int windowWords)
		{
			int count = RunEnds.Length;
			int last = Math.Min(end + Math.Min(windowWords, count), count - 2);

			return end + 1 <= last && _nextClauseEnds[end + 1] <= last;
		}

		/// <summary>What a line from start to end costs, apart from the lines after it.</summary>
		public double GetLineCost(int start, int end, int slack)
		{
			int count = RunEnds.Length;
			double cost = 0;

			if (slack < 0)
			{
				int excess = -slack;

				// A line may stand a few columns over the width when that keeps a phrase whole.
				// stanza_doc_max_overflow_columns says how many; each column costs ToleratedPenalty, below TailPenalty,
				// so overflow goes only to avoid a short tail, a weak tail, a fragment before a clause end or a leftover line.
				// A line further over pays OverflowPenalty per column, a far higher cost.
				cost += excess <= _layout.MaxOverflowColumns ? ToleratedPenalty * excess : OverflowPenalty * excess;
			}

			// Every line is weighed, not only the one that ends the run:
			// a line with fewer words than the minimum reads as a leftover wherever it stands.
			int words = end - start + 1;

			if (words < _layout.MinLineWords)
				cost += RuntPenalty * (_layout.MinLineWords - words);

			if (end + 1 < count)
			{
				cost += Math.Max(slack, 0);

				int tailStart = FindTailStart(start, end);
				int tail = tailStart >= 0 ? end - tailStart + 1 : 0;

				// A tail of 0 (the line ends on the punctuation) is not penalized;
				// only a tail shorter than stanza_doc_min_tail_words is.
				// A minimum of 1 or 0 adds no penalty, so either value disables the tail penalty.
				if (tail > 0 && tail < _layout.MinTailWords)
					cost += TailPenalty;

				// The other side of the same punctuation:
				// a line opening with the words that close the clause before it reads badly,
				// at the same cost.
				if (LeavesAFragmentBeforeAClauseEnd(end, _layout.MinTailWords - 1))
					cost += TailPenalty;
			}

			if (_weak is not null && _weak.Contains(_normalized[end]))
				cost += WeakTailPenalty;

			return cost;
		}
	}
}
