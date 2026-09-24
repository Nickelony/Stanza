using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Nickelony.Stanza.Markup.Paths;

/// <summary>Matches a path against a glob of the <c>.editorconfig</c> dialect, through a cached regex.</summary>
internal static class GlobMatcher
{
	/// <summary>The translation and the regex engine recurse; deeper nesting would overflow the stack.</summary>
	private const int MaxNesting = 16;

	/// <summary>The translation reads on from each brace, so it is quadratic: 80,000 braces take 4 s.</summary>
	private const int MaxLength = 1_024;

	private static readonly char[] s_classRefusals = ['[', ']', '^', '\\'];

	private static readonly char[] s_wildcards = ['*', '?', '{', '['];

	public static bool IsGlob(string value) => FindWildcard(value) >= 0;

	public static int FindWildcard(string value, int startOffset = 0) => value.IndexOfAny(s_wildcards, startOffset);

	/// <summary>The offset of the first wildcard a backslash escapes, or -1 when none is.</summary>
	public static int FindEscapedWildcard(string pattern)
	{
		for (int offset = 0; offset + 1 < pattern.Length; offset++)
		{
			if (pattern[offset] != '\\')
				continue;

			offset++;

			if (Array.IndexOf(s_wildcards, pattern[offset]) >= 0)
				return offset;
		}

		return -1;
	}

	/// <summary>A resolved path is a real path, so each wildcard in it is escaped.</summary>
	public static string Escape(string path)
	{
		StringBuilder builder = new(path.Length);

		foreach (char character in path)
		{
			if (Array.IndexOf(s_wildcards, character) >= 0)
				builder.Append('\\');

			builder.Append(character);
		}

		return builder.ToString();
	}

	public static bool IsMatch(string pattern, string path)
	{
		Compiled compiled = s_cache.GetOrAdd(pattern, Compile);

		if (compiled.Matcher is not { } matcher)
			return false;

		// A pattern that names a separator matches the whole path; one without matches the file name.
		string candidate = compiled.HasSeparator ? Normalize(path) : Path.GetFileName(path);

		// A rooted pattern is translated without its leading separator,
		// so an absolute candidate drops its own leading separator to match it.
		if (pattern.StartsWith('/') && candidate.StartsWith('/'))
			candidate = candidate[1..];

		return matcher.IsMatch(candidate);
	}

	public static bool TryValidate(string pattern, [NotNullWhen(false)] out string? refusal)
	{
		Compiled compiled = s_cache.GetOrAdd(pattern, Compile);
		refusal = compiled.Refusal;

		return compiled.Matcher is not null;
	}

	/// <summary>Writes each backslash as a slash, as a command-line glob and a path are compared.</summary>
	public static string Normalize(string value) => value.Replace('\\', '/');

	private static bool IsNumericRange(ReadOnlySpan<char> content)
	{
		int separator = content.IndexOf("..", StringComparison.Ordinal);
		return separator > 0 && separator < content.Length - 2 && IsWholeNumber(content[..separator]) && IsWholeNumber(content[(separator + 2)..]);
	}

	/// <summary>
	/// A number may carry one leading minus, so <c>{-1..4}</c> is a numeric range, which the translation refuses.
	/// </summary>
	private static bool IsWholeNumber(ReadOnlySpan<char> value)
	{
		if (value.Length > 1 && value[0] == '-')
			value = value[1..];

		foreach (char character in value)
		{
			if (!char.IsAsciiDigit(character))
				return false;
		}

		return true;
	}

	private static Compiled Compile(string pattern)
	{
		bool rooted = pattern.StartsWith('/');
		string normalized = rooted ? pattern[1..] : pattern;

		// A command-line glob is rooted with a separator it never held, so that one is not counted.
		if (normalized.Length > MaxLength)
			return new(null, $"it is longer than {MaxLength} characters", HasSeparator: false);

		// A pattern written from the working directory is the same pattern without its leading "./",
		// which no candidate carries; the separator it names still counts,
		// so it keeps matching whole paths rather than file names alone.
		bool fromWorkingDirectory = !rooted && normalized.StartsWith("./", StringComparison.Ordinal);

		if (fromWorkingDirectory)
			normalized = normalized[2..];

		Translation translation = Translate(normalized);

		// A rooted pattern names a separator of its own, and so does one written from the working directory.
		bool hasSeparator = translation.HasSeparator || rooted || fromWorkingDirectory;

		// A construct the dialect does not describe is refused, not translated into something else.
		if (translation.Refusal is { } refusal)
			return new(null, refusal, hasSeparator);

		// Matching ignores case on every platform, and a crossing wildcard crosses a name that holds a line break.
		const RegexOptions options = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline;

		try
		{
			return new(new Regex(translation.Builder.ToString(), options), null, hasSeparator);
		}
		catch (ArgumentException exception)
		{
			// A pattern the parser rejects is one this dialect cannot use.
			return new(null, exception.Message, hasSeparator);
		}
		catch (NotSupportedException)
		{
			// The translation writes no construct the engine lacks, so the engine refuses only the size of the pattern.
			return new(null, "it is longer than the matcher's limit of about 200 literal characters or wildcards", hasSeparator);
		}
	}

	private static Translation Translate(string pattern)
	{
		Translation translation = new();

		Append(pattern, start: 0, end: pattern.Length, translation, depth: 0);

		// A `$` would also match before a final line break, which a name on Linux can end with.
		translation.Builder.Append(@"\z");

		return translation;
	}

	private static void Append(string pattern, int start, int end, Translation translation, int depth)
	{
		// Too deep to translate is a refusal, the same as a character class the dialect does not know.
		if (depth > MaxNesting)
		{
			translation.Refusal ??= $"the alternation nests more than {MaxNesting} levels deep";
			return;
		}

		for (int offset = start; offset < end; offset++)
		{
			char current = pattern[offset];

			if (current == '/')
			{
				translation.HasSeparator = true;
				translation.Builder.Append('/');
				continue;
			}

			if (current == '\\')
			{
				// The dialect's escape, as the EditorConfig reference and Roslyn read it:
				// a backslash writes the character after it as itself, so `file\*.xml` names the one file whose own name holds a star.
				if (offset + 1 >= end)
				{
					translation.Refusal ??= "a backslash at the end of a pattern escapes nothing";
					continue;
				}

				offset++;
				translation.Builder.Append(Regex.Escape(pattern[offset].ToString()));
				continue;
			}

			if (current == '*')
			{
				offset = AppendStar(pattern, offset, end, builder: translation.Builder);
				continue;
			}

			if (current == '?')
			{
				translation.Builder.Append("[^/]");
				continue;
			}

			if (current == '{' && TryAppendAlternatives(pattern, start: offset, end, translation, depth, out int closing))
			{
				offset = closing;
				continue;
			}

			if (current == '[')
			{
				if (
					TryAppendClass(
						pattern,
						start: offset,
						end,
						builder: translation.Builder,
						closing: out int classEnd,
						refusal: out string? refusedClass
					)
				)
				{
					offset = classEnd;
					continue;
				}

				// A refused class refuses the whole pattern, so it matches nothing;
				// the first refusal is the one reported.
				translation.Refusal ??= refusedClass;
			}

			translation.Builder.Append(Regex.Escape(current.ToString()));
		}
	}

	private static bool TryAppendAlternatives(string pattern, int start, int end, Translation translation, int depth, out int closing)
	{
		if (!TryFindClosingBrace(pattern, start, end, out closing))
			return false;

		string[] alternatives = SplitAlternatives(pattern[(start + 1)..closing]);

		if (alternatives.Length < 2)
		{
			// A numeric range such as {1..3} is legal in .editorconfig but not translated here,
			// so the whole pattern is refused and reported rather than matching a silent subset.
			if (IsNumericRange(pattern.AsSpan(start + 1, closing - start - 1)))
				translation.Refusal ??= "the {num1..num2} range form is not supported";

			return false;
		}

		translation.Builder.Append("(?:");

		for (int index = 0; index < alternatives.Length; index++)
		{
			if (index > 0)
				translation.Builder.Append('|');

			Append(pattern: alternatives[index], start: 0, end: alternatives[index].Length, translation, depth: depth + 1);
		}

		translation.Builder.Append(')');

		return true;
	}

	private static bool TryAppendClass(
		string pattern,
		int start,
		int end,
		StringBuilder builder,
		out int closing,
		[NotNullWhen(false)] out string? refusal
	)
	{
		refusal = null;
		closing = pattern.IndexOf(']', start + 1);

		if (closing < 0 || closing >= end || closing == start + 1)
		{
			refusal = "the character class is not closed";
			return false;
		}

		string body = pattern[(start + 1)..closing];
		bool negated = body.StartsWith('!');

		if (negated)
			body = body[1..];

		if (body.Length == 0)
		{
			refusal = "the character class is empty";
			return false;
		}

		if (body.IndexOfAny(s_classRefusals) >= 0)
		{
			refusal = "the character class holds a character the dialect does not accept";
			return false;
		}

		// A class that names the separator, or holds a range that crosses it, is refused.
		if (body.Contains('/') || CrossesSeparator(body))
		{
			refusal = "the character class matches a path separator, which a class never does";
			return false;
		}

		// The body goes straight into a regex character class,
		// so only literals, ranges and a leading ! are accepted.
		builder.Append(negated ? "[^/" : "[");
		builder.Append(body);
		builder.Append(']');

		return true;
	}

	private static bool CrossesSeparator(string body)
	{
		for (int offset = 1; offset + 1 < body.Length; offset++)
		{
			if (body[offset] == '-' && body[offset - 1] <= '/' && body[offset + 1] >= '/')
				return true;
		}

		return false;
	}

	private static bool TryFindClosingBrace(string pattern, int start, int end, out int closing)
	{
		int depth = 0;

		for (int offset = start; offset < end; offset++)
		{
			if (pattern[offset] == '\\' && offset + 1 < end)
			{
				offset++;
			}
			else if (pattern[offset] == '[' && pattern.IndexOf(']', offset + 1) is int classEnd && classEnd > offset + 1 && classEnd < end)
			{
				// A brace inside a character class is a literal, as SplitAlternatives reads it.
				offset = classEnd;
			}
			else if (pattern[offset] == '{')
			{
				depth++;
			}
			else if (pattern[offset] == '}' && --depth == 0)
			{
				closing = offset;
				return true;
			}
		}

		closing = -1;
		return false;
	}

	private static string[] SplitAlternatives(string body)
	{
		List<string> alternatives = [];

		int depth = 0;
		int start = 0;

		for (int offset = 0; offset < body.Length; offset++)
		{
			switch (body[offset])
			{
				case '\\' when offset + 1 < body.Length:
					offset++;
					break;

				case '{':
				case '[':
					depth++;
					break;

				case '}':
				case ']':
					// Clamped: a closing bracket with nothing open is a literal.
					if (depth > 0)
						depth--;

					break;

				case ',' when depth == 0:
					alternatives.Add(body[start..offset]);
					start = offset + 1;
					break;
			}
		}

		alternatives.Add(body[start..]);

		return [.. alternatives];
	}

	private static int AppendStar(string pattern, int offset, int end, StringBuilder builder)
	{
		if (offset + 1 >= end || pattern[offset + 1] != '*')
		{
			builder.Append("[^/]*");
			return offset;
		}

		if (offset + 2 < end && pattern[offset + 2] == '/')
		{
			// "**/" also matches no directory at all, so "**/*.xml" finds a file next to the root.
			builder.Append("(?:.*/)?");
			return offset + 2;
		}

		builder.Append(".*");

		return offset + 1;
	}

	/// <summary>The translation of each pattern, taking a check of this repository from 1.4 s to 0.13 s.</summary>
	private static readonly ConcurrentDictionary<string, Compiled> s_cache = new(StringComparer.Ordinal);

	private sealed record Compiled(Regex? Matcher, string? Refusal, bool HasSeparator);

	/// <summary>The regex a translation writes, its first refusal, and whether it wrote a separator.</summary>
	private sealed class Translation
	{
		public StringBuilder Builder { get; } = new("^");

		public string? Refusal { get; set; }

		public bool HasSeparator { get; set; }
	}
}
