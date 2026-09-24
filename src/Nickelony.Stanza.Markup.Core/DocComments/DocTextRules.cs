using System;
using System.Collections.Generic;

namespace Nickelony.Stanza.Markup.Core.DocComments;

/// <summary>Holds the rules of documentation text: padding, the final period, keywords and slash shape.</summary>
internal static class DocTextRules
{
	public const string NullKeywordText = "null";

	/// <summary>The characters this thread's word scans read; a test seam for a bound without a clock.</summary>
	[ThreadStatic]
	public static long KeywordScanSteps;

	/// <summary>The markup of the keyword, in the namespace the prefix names.</summary>
	public static string GetNullMarkup(bool spaced, string prefix = "") => BuildSelfClosingTag($"<{prefix}see langword=\"null\">", spaced);

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

	public static List<int> FindNullKeywords(string text)
	{
		List<int> offsets = [];
		int offset = text.IndexOf(NullKeywordText, StringComparison.Ordinal);

		if (offset < 0)
			return offsets;

		// The spans are the ones the fill reads, so a keyword the fill treats as prose is found here too.
		bool[] withinSpan = MarkSpans(text);

		// Each keyword of one word asks the same question about it, so the word is read once.
		WordFacts word = new(End: -1, IsAddress: false);

		while (offset >= 0)
		{
			int end = offset + NullKeywordText.Length;

			if (
				!JoinsAWord(text, offset - 1, direction: -1)
				&& !JoinsAWord(text, end, direction: 1)
				&& !IsLiteral(text, offset, withinSpan, ref word)
				&& !IsQuoted(text, offset, end)
			)
			{
				offsets.Add(offset);
			}

			offset = end < text.Length ? text.IndexOf(NullKeywordText, end, StringComparison.Ordinal) : -1;
		}

		return offsets;
	}

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
