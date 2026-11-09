using System;
using System.Collections.Generic;

namespace Nickelony.Stanza.Markup.Core.DocComments;

/// <summary>Holds the rules of documentation text: padding, capitals, periods, keywords and slash shape.</summary>
internal static class DocTextRules
{
	private const string NullWord = "null";

	/// <summary>The keywords the rule marks where a file lists none.</summary>
	public static readonly MarkupNameSet DefaultKeywords = new(NullWord);

	/// <summary>The keywords a file may list, of which a bare <c>default</c> is never marked.</summary>
	public static readonly MarkupNameSet ListableKeywords = new(NullWord, "true", "false", "default");

	/// <summary>The characters this thread's word scans read; a test seam for a bound without a clock.</summary>
	[ThreadStatic]
	public static long KeywordScanSteps;

	/// <summary>The markup of the keyword, in the namespace the prefix names.</summary>
	public static string GetKeywordMarkup(string word, bool spaced, string prefix = "") =>
		BuildSelfClosingTag($"<{prefix}see langword=\"{word}\">", spaced);

	public static string BuildSelfClosingTag(string openTag, bool spaced) =>
		openTag.Substring(0, openTag.Length - 1).TrimEnd() + (spaced ? " />" : "/>");

	public static string ShapeSlash(string openTag, bool spaced)
	{
		int slash = openTag.Length - 2;

		if (slash < 0 || openTag[slash] != '/' || openTag[slash + 1] != '>')
			return openTag;

		int end = slash;

		while (end > 0 && MarkupWhitespace.IsXmlWhitespace(openTag[end - 1]))
			end--;

		string gap = openTag.Substring(end, slash - end);
		return ResolveSlashGap(gap, spaced) is { } wanted ? openTag.Substring(0, end) + wanted + "/>" : openTag;
	}

	public static string? ResolveSlashGap(string gap, bool spaced)
	{
		if (gap.IndexOf('\n') >= 0 || gap.IndexOf('\r') >= 0)
			return null;

		if (spaced)
			return gap == " " ? null : " ";

		return gap.Length == 0 ? null : string.Empty;
	}

	/// <summary>Finds the listed keywords that the text uses as the language does.</summary>
	/// <remarks>
	/// A <c>null</c> counts anywhere; a <c>true</c> or <c>false</c> needs its phrase; a bare <c>default</c> never.
	/// </remarks>
	public static List<DocKeyword> FindKeywords(string text, MarkupNameSet words, bool startsElement)
	{
		List<DocKeyword> found = [];

		if (!HoldsAKeyword(text, words))
			return found;

		// The spans are the ones the fill reads, so a keyword the fill treats as prose is found here too.
		bool[] withinSpan = MarkSpans(text);

		// Each keyword of one word asks the same question about it, so the word is read once.
		WordFacts word = new(End: -1, IsAddress: false);

		int offset = 0;

		while (offset < text.Length)
		{
			if (!IsKeywordCharacter(text[offset]))
			{
				offset++;
				continue;
			}

			int end = GetKeywordWordEnd(text, offset);

			// The checks run from the cheapest to the dearest, and the phrase last.
			if (
				FindBareKeyword(text, start: offset, end, words) is { } keyword
				&& !JoinsAWord(text, offset - 1, direction: -1)
				&& !JoinsAWord(text, end, direction: 1)
			)
			{
				if (
					!IsLiteral(text, offset, withinSpan, ref word)
					&& !IsQuoted(text, offset, end)
					&& (keyword == NullWord || IsProvedByItsPhrase(text, start: offset, end, startsElement))
				)
				{
					found.Add(new DocKeyword(offset, keyword));
				}
			}

			offset = end;
		}

		return found;
	}

	/// <summary>Whether the text may hold a listed keyword in any case, so a text with none skips the scan.</summary>
	private static bool HoldsAKeyword(string text, MarkupNameSet words)
	{
		return HoldsListedWord(text, words, word: NullWord, comparison: StringComparison.Ordinal)
			|| HoldsListedWord(text, words, word: "true", comparison: StringComparison.OrdinalIgnoreCase)
			|| HoldsListedWord(text, words, word: "false", comparison: StringComparison.OrdinalIgnoreCase);
	}

	private static bool HoldsListedWord(string text, MarkupNameSet words, string word, StringComparison comparison) =>
		words.Contains(word) && text.IndexOf(word, comparison) >= 0;

	private static int GetKeywordWordEnd(string text, int start)
	{
		int end = start;

		while (end < text.Length && IsKeywordCharacter(text[end]))
			end++;

		KeywordScanSteps += end - start;

		return end;
	}

	/// <summary>The listed keyword the word spells, or <see langword="null"/>; only <c>null</c> matches case.</summary>
	private static string? FindBareKeyword(string text, int start, int end, MarkupNameSet words)
	{
		return (end - start) switch
		{
			4 when words.Contains(NullWord) && Spells(text, start, NullWord) => NullWord,
			4 when words.Contains("true") && SpellsInAnyCase(text, start, "true") => "true",
			5 when words.Contains("false") && SpellsInAnyCase(text, start, "false") => "false",
			_ => null,
		};
	}

	private static bool Spells(string text, int start, string word) => SpellsAs(text, start, word, comparison: StringComparison.Ordinal);

	private static bool SpellsInAnyCase(string text, int start, string word) =>
		SpellsAs(text, start, word, comparison: StringComparison.OrdinalIgnoreCase);

	private static bool SpellsAs(string text, int start, string word, StringComparison comparison) =>
		string.Compare(strA: text, indexA: start, strB: word, indexB: 0, length: word.Length, comparisonType: comparison) == 0;

	/// <summary>Whether the phrase around the word proves it is the keyword and not the English word.</summary>
	private static bool IsProvedByItsPhrase(string text, int start, int end, bool startsElement)
	{
		// The phrase opens on the word with `if` or `when` next, follows `returns` or `otherwise`, or precedes `otherwise`.
		// Punctuation between the words does not break it, and a noun after the keyword does, as in `returns true positives`.
		(int Start, int End)? next = FindWordAfter(text, end);

		if (OpensAPhrase(text, start, startsElement) && (SpellsWord(text, next, "if") || SpellsWord(text, next, "when")))
			return true;

		if (SpellsWord(text, next, "otherwise"))
			return true;

		(int Start, int End)? previous = FindWordBefore(text, start);

		return (SpellsWord(text, previous, "returns") || SpellsWord(text, previous, "otherwise")) && ClosesTheKeyword(text, end, next);
	}

	/// <summary>Whether a mark, the text's end, <c>if</c>, <c>when</c> or <c>or</c> follows the keyword.</summary>
	private static bool ClosesTheKeyword(string text, int end, (int Start, int End)? next)
	{
		if (next is not { } word)
			return true;

		for (int offset = end; offset < word.Start; offset++)
		{
			KeywordScanSteps++;

			if (!MarkupWhitespace.IsXmlWhitespace(text[offset]))
				return true;
		}

		return SpellsWord(text, next, "if") || SpellsWord(text, next, "when") || SpellsWord(text, next, "or");
	}

	/// <summary>Whether the element's start, a blank line or a sentence's end stands before the offset.</summary>
	private static bool OpensAPhrase(string text, int offset, bool startsElement)
	{
		int lineBreaks = 0;

		while (offset > 0 && MarkupWhitespace.IsXmlWhitespace(text[offset - 1]))
		{
			KeywordScanSteps++;

			if (text[offset - 1] == '\n')
				lineBreaks++;

			offset--;
		}

		if (offset == 0)
			return startsElement;

		return lineBreaks >= 2 || text[offset - 1] is '.' or '!' or '?';
	}

	private static (int Start, int End)? FindWordBefore(string text, int offset)
	{
		int end = offset;

		while (end > 0 && !IsKeywordCharacter(text[end - 1]))
		{
			KeywordScanSteps++;
			end--;
		}

		int start = end;

		while (start > 0 && IsKeywordCharacter(text[start - 1]))
		{
			KeywordScanSteps++;
			start--;
		}

		return start == end ? null : (start, end);
	}

	private static (int Start, int End)? FindWordAfter(string text, int offset)
	{
		int start = offset;

		while (start < text.Length && !IsKeywordCharacter(text[start]))
		{
			KeywordScanSteps++;
			start++;
		}

		return start == text.Length ? null : (start, GetKeywordWordEnd(text, start));
	}

	private static bool SpellsWord(string text, (int Start, int End)? word, string spelling) =>
		word is { } found && found.End - found.Start == spelling.Length && SpellsInAnyCase(text, found.Start, spelling);

	/// <summary>Marks the span characters of the whole text, which counts as one step for each character.</summary>
	private static bool[] MarkSpans(string text)
	{
		KeywordScanSteps += text.Length;
		return DocWordReader.MarkSpanCharacters(text);
	}

	private static bool IsKeywordCharacter(char character) => char.IsLetterOrDigit(character) || character == '_';

	/// <summary>
	/// A keyword character, or a hyphen with one beyond it as in <c>null-terminated</c>, joins the keyword to a word.
	/// </summary>
	private static bool JoinsAWord(string text, int offset, int direction)
	{
		if (offset < 0 || offset >= text.Length)
			return false;

		if (IsKeywordCharacter(text[offset]))
			return true;

		// A suspended hyphen, as in `null- or empty`, has no word beyond it.
		int beyond = offset + direction;
		return text[offset] == '-' && beyond >= 0 && beyond < text.Length && IsKeywordCharacter(text[beyond]);
	}

	/// <summary>Tells a blank text from one that ends on a mark or on a word.</summary>
	public static DocTextEnd ScanTextEnd(string text, out int end)
	{
		end = text.Length;

		while (end > 0 && MarkupWhitespace.IsXmlWhitespace(text[end - 1]))
			end--;

		if (end == 0)
			return DocTextEnd.Blank;

		// A period after a trailing link or path would join it and break the link, so the address counts as a mark.
		return char.IsLetterOrDigit(text[end - 1]) && !IsAddress(text, GetWordStart(text, end), end) ? DocTextEnd.Word : DocTextEnd.Mark;
	}

	/// <summary>Where a period closes the text, or <see langword="null"/> when the text takes none.</summary>
	public static int? FindPeriodEnd(string text)
	{
		DocTextEnd kind = ScanTextEnd(text, out int end);

		if (kind == DocTextEnd.Word)
			return end;

		return kind == DocTextEnd.Mark && EndsOnKeywordElement(text, end) ? end : null;
	}

	/// <summary>Writes the period at the end <see cref="FindPeriodEnd"/> found.</summary>
	public static string WritePeriod(string text, int end) => text.Substring(0, end) + "." + text.Substring(end);

	/// <summary>Where the letter to capitalize stands, or <see langword="null"/> when the start stays.</summary>
	public static int? FindCapital(string text, MarkupNameSet keywords)
	{
		int start = 0;

		while (start < text.Length && MarkupWhitespace.IsXmlWhitespace(text[start]))
			start++;

		if (start == text.Length || !HasOneCapital(text[start]))
			return null;

		// The first word runs to a space or to an element that touches it.
		int end = start + 1;

		while (end < text.Length && !MarkupWhitespace.IsXmlWhitespace(text[end]) && text[end] != '<')
			end++;

		return KeepsItsCase(text, start, end, keywords) ? null : start;
	}

	/// <summary>Writes the capital at the offset <see cref="FindCapital"/> found.</summary>
	public static string WriteCapital(string text, int offset) => text.Substring(0, offset) + ToCapital(text[offset]) + text.Substring(offset + 1);
	public static string ToCapital(char letter) => new(char.ToUpperInvariant(letter), 1);

	/// <summary>A lowercase letter whose uppercase form is one character, which the sharp s lacks.</summary>
	private static bool HasOneCapital(char character) => char.IsLower(character) && char.ToUpperInvariant(character) != character;

	/// <summary>A capital inside the word, an address, a file name or a listed keyword keeps its case.</summary>
	private static bool KeepsItsCase(string text, int start, int end, MarkupNameSet keywords)
	{
		for (int offset = start + 1; offset < end; offset++)
		{
			if (char.IsUpper(text[offset]))
				return true;

			// A dot with a letter or a digit after it stands inside a name, as in `appsettings.json` or `e.g.`.
			if (text[offset] == '.' && offset + 1 < end && char.IsLetterOrDigit(text[offset + 1]))
				return true;
		}

		if (IsAddress(text, start, end))
			return true;

		int wordEnd = start;

		while (wordEnd < end && IsKeywordCharacter(text[wordEnd]))
			wordEnd++;

		// A hyphenated word such as `null-terminated` is not the keyword, as the keyword rule reads it.
		return keywords.Contains(text.Substring(start, wordEnd - start)) && !JoinsAWord(text, wordEnd, direction: 1);
	}

	/// <summary>
	/// A <c>&lt;see langword&gt;</c> element stands for a word of the sentence, so the sentence closes after it.
	/// </summary>
	private static bool EndsOnKeywordElement(string text, int end)
	{
		if (end < 2 || text[end - 1] != '>' || text[end - 2] != '/')
			return false;

		int open = text.LastIndexOf('<', end - 1);

		if (open < 0 || end - open < 5)
			return false;

		string tag = text.Substring(open, end - open);

		return tag.StartsWith("<see", StringComparison.Ordinal)
			&& MarkupWhitespace.IsXmlWhitespace(tag[4])
			&& tag.IndexOf("langword", StringComparison.Ordinal) >= 0;
	}

	/// <summary>A keyword between two quotes of one kind names a string, not the language keyword.</summary>
	private static bool IsQuoted(string text, int offset, int end) =>
		offset > 0 && end < text.Length && text[offset - 1] == text[end] && text[end] is '"' or '\'';

	/// <summary>A keyword inside a code span, a link or a path is the text's own, not the language keyword.</summary>
	/// <remarks>
	/// The offsets arrive in order, so a keyword before the end of the last word read stands in that word.
	/// </remarks>
	private static bool IsLiteral(string text, int offset, bool[] withinSpan, ref WordFacts word)
	{
		if (withinSpan[offset])
			return true;

		if (offset >= word.End)
		{
			int end = GetWordEnd(text, offset);
			word = new WordFacts(End: end, IsAddress: IsAddress(text, GetWordStart(text, offset), end));
		}

		return word.IsAddress;
	}

	/// <summary>Where a word ends and whether it is a link or a path.</summary>
	private readonly record struct WordFacts(int End, bool IsAddress);

	private static bool IsAddress(string text, int start, int end)
	{
		string word = text.Substring(start, end - start);
		return word.IndexOf("://", StringComparison.Ordinal) >= 0 || word.IndexOf('/') >= 0 || word.IndexOf('\\') >= 0;
	}

	private static int GetWordStart(string text, int offset)
	{
		while (offset > 0 && !MarkupWhitespace.IsXmlWhitespace(text[offset - 1]))
		{
			KeywordScanSteps++;
			offset--;
		}

		return offset;
	}

	private static int GetWordEnd(string text, int offset)
	{
		while (offset < text.Length && !MarkupWhitespace.IsXmlWhitespace(text[offset]))
		{
			KeywordScanSteps++;
			offset++;
		}

		return offset;
	}

	/// <summary>Finds the blank lines of a text that separate no prose, as spans of the text.</summary>
	public static List<MarkupSpan>? FindPadding(string text, bool precededByProse, bool followedByProse)
	{
		List<MarkupSpan>? padding = null;

		// The scan's state inside a blank line: the breaks counted so far, where the previous break ended,
		// where the blank line's span starts and ends, and whether prose stood before it.
		int breaks = 0;
		int previousBreakEnd = 0;
		int start = -1;
		int end = -1;

		bool preceded = false;
		bool words = false;

		for (int offset = 0; offset < text.Length; offset++)
		{
			char character = text[offset];

			if (character == '\n')
			{
				breaks++;

				if (breaks == 2)
				{
					start = previousBreakEnd;
					end = offset + 1;
					preceded = precededByProse || words;
				}
				else if (start >= 0)
				{
					end = offset + 1;
				}

				previousBreakEnd = offset + 1;
				continue;
			}

			// A run of nothing but whitespace is the indentation of a blank line rather than prose.
			if (MarkupWhitespace.IsXmlWhitespace(character))
				continue;

			words = true;
			breaks = 0;

			// A word closes the run the scan is inside;
			// the run separates prose only when prose stood before it too.
			if (start >= 0)
			{
				if (!preceded)
					(padding ??= []).Add(new MarkupSpan(start, end - start));

				start = -1;
			}
		}

		// A run the text ends inside separates prose only when prose stands before it and beside the next node.
		if (start >= 0 && !(preceded && followedByProse))
			(padding ??= []).Add(new MarkupSpan(start, end - start));

		return padding;
	}

	public static string NormalizeWord(string word)
	{
		int start = 0;
		int end = word.Length;

		while (start < end && !char.IsLetterOrDigit(word[start]))
			start++;

		while (end > start && !char.IsLetterOrDigit(word[end - 1]))
			end--;

		return word.Substring(start, end - start).ToLowerInvariant();
	}
}
