using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Nickelony.Stanza.Markup.Files;

/// <summary>Picks a document's encoding from its byte order mark, its bytes and its XML declaration.</summary>
internal static partial class MarkupEncodings
{
	private const int DeclarationLength = 256;

	private static readonly Encoding s_utf8WithoutMark = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
	private static readonly Encoding s_utf16WithoutMark = new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true);

	private static readonly Encoding s_utf16BigEndianWithoutMark = new UnicodeEncoding(
		bigEndian: true,
		byteOrderMark: false,
		throwOnInvalidBytes: true
	);

	private static readonly Encoding s_utf8WithMark = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true);
	private static readonly Encoding s_utf16WithMark = new UnicodeEncoding(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: true);
	private static readonly Encoding s_utf16BigEndianWithMark = new UnicodeEncoding(bigEndian: true, byteOrderMark: true, throwOnInvalidBytes: true);
	private static readonly Encoding s_ascii = Encoding.GetEncoding("us-ascii", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);

	/// <summary>
	/// <see cref="Encoding.Latin1"/> made strict: its fallback writes <c>?</c> where a write must refuse.
	/// </summary>
	private static readonly Encoding s_latin1 = Encoding.GetEncoding(
		"iso-8859-1",
		EncoderFallback.ExceptionFallback,
		DecoderFallback.ExceptionFallback
	);

	private static readonly Encoding s_utf32WithMark = new UTF32Encoding(bigEndian: false, byteOrderMark: true, throwOnInvalidCharacters: true);

	private static readonly Encoding s_utf32BigEndianWithMark = new UTF32Encoding(
		bigEndian: true,
		byteOrderMark: true,
		throwOnInvalidCharacters: true
	);

	private static readonly Encoding s_utf32WithoutMark = new UTF32Encoding(bigEndian: false, byteOrderMark: false, throwOnInvalidCharacters: true);

	private static readonly Encoding s_utf32BigEndianWithoutMark = new UTF32Encoding(
		bigEndian: true,
		byteOrderMark: false,
		throwOnInvalidCharacters: true
	);

	[GeneratedRegex("^\\s*<\\?xml\\s[^>]*?encoding\\s*=\\s*[\"'](?<name>[^\"']+)[\"']", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
	private static partial Regex Declaration();

	internal static MarkupEncodingChoice Resolve(byte[] bytes)
	{
		// A mark wins over the declaration, and one that disagrees with it is refused;
		// without a mark the prolog byte pattern decides, then the declaration.
		if (TryReadPreamble(bytes, out Encoding marked, out int preambleLength))
		{
			return new MarkupEncodingChoice(
				Encoding: marked,
				PreambleLength: preambleLength,
				WriteBackRefusal: null,
				Refusal: FindDeclarationRefusal(ReadDeclaration(bytes, marked, preambleLength), marked)
			);
		}

		// A declaration in UTF-16 or UTF-32 is written in that encoding,
		// so a Latin-1 read would find a NUL after every character and no name at all.
		if (SniffMarklessUnicode(bytes) is { } sniffed)
		{
			return new MarkupEncodingChoice(
				Encoding: sniffed,
				PreambleLength: 0,
				WriteBackRefusal: null,
				Refusal: FindDeclarationRefusal(ReadDeclaration(bytes, sniffed), sniffed)
			);
		}

		string? name = ReadDeclaration(bytes, Encoding.Latin1);

		if (name is null)
			return new MarkupEncodingChoice(Encoding: s_utf8WithoutMark, PreambleLength: 0, WriteBackRefusal: null, Refusal: null);

		if (ReadDeclared(name) is { } resolved)
		{
			// The bytes held no UTF-16 or UTF-32 prolog above, so a declaration of either stands over single-byte text.
			string? refusal = IsWide(resolved) ? FindDeclarationRefusal(name, stated: s_utf8WithoutMark) : null;
			return new MarkupEncodingChoice(Encoding: resolved, PreambleLength: 0, WriteBackRefusal: null, Refusal: refusal);
		}

		// A declaration naming an encoding outside the set is read as Latin-1, a guess;
		// only a write reports the refusal.
		return new MarkupEncodingChoice(Encoding: s_latin1, PreambleLength: 0, WriteBackRefusal: DescribeRefusal(name), Refusal: null);
	}

	private static string? FindDeclarationRefusal(string? declared, Encoding stated)
	{
		return declared is null || !DeclaresADifferentEncoding(declared, stated)
			? null
			: $"could not be read: it declares the encoding '{declared}', which does not match the bytes it starts with, so it was left alone.";
	}

	private static bool DeclaresADifferentEncoding(string declared, Encoding stated)
	{
		string name = declared.Trim().ToLowerInvariant();

		if (name is "utf-16" or "utf-32")
		{
			int littleEndian = name == "utf-32" ? 12000 : 1200;
			return stated.CodePage != littleEndian && stated.CodePage != littleEndian + 1;
		}

		return ReadDeclared(name)?.CodePage != stated.CodePage;
	}

	private static bool IsWide(Encoding encoding) => encoding.CodePage is 1200 or 1201 or 12000 or 12001;

	private static string DescribeRefusal(string declared) =>
		$"it declares the encoding '{declared}', which is not one this tool writes back, so it was left alone.";

	private static Encoding? SniffMarklessUnicode(byte[] bytes)
	{
		if (bytes.StartsWith<byte>([0x3C, 0x00, 0x00, 0x00]))
			return s_utf32WithoutMark;

		if (bytes.StartsWith<byte>([0x00, 0x00, 0x00, 0x3C]))
			return s_utf32BigEndianWithoutMark;

		// The UTF-32 openings are tested first, because they begin with the two bytes of a UTF-16 one.
		if (bytes.StartsWith<byte>([0x3C, 0x00]))
			return s_utf16WithoutMark;

		if (bytes.StartsWith<byte>([0x00, 0x3C]))
			return s_utf16BigEndianWithoutMark;

		return null;
	}

	private static Encoding? ReadDeclared(string name)
	{
		return name.ToLowerInvariant() switch
		{
			"utf-8" or "utf8" => s_utf8WithoutMark,
			"us-ascii" or "ascii" => s_ascii,
			"iso-8859-1" or "latin1" or "latin-1" => s_latin1,
			"utf-16" or "utf-16le" or "unicode" => s_utf16WithoutMark,
			"utf-16be" or "unicodefffe" => s_utf16BigEndianWithoutMark,
			"utf-32" or "utf-32le" => s_utf32WithoutMark,
			"utf-32be" => s_utf32BigEndianWithoutMark,
			_ => null,
		};
	}

	private static bool TryReadPreamble(byte[] bytes, out Encoding encoding, out int preambleLength)
	{
		if (bytes.StartsWith<byte>([0xEF, 0xBB, 0xBF]))
		{
			preambleLength = 3;
			encoding = s_utf8WithMark;

			return true;
		}

		if (bytes.StartsWith<byte>([0xFF, 0xFE, 0x00, 0x00]))
		{
			preambleLength = 4;
			encoding = s_utf32WithMark;

			return true;
		}

		if (bytes.StartsWith<byte>([0x00, 0x00, 0xFE, 0xFF]))
		{
			preambleLength = 4;
			encoding = s_utf32BigEndianWithMark;

			return true;
		}

		if (bytes.StartsWith<byte>([0xFF, 0xFE]))
		{
			preambleLength = 2;
			encoding = s_utf16WithMark;

			return true;
		}

		if (bytes.StartsWith<byte>([0xFE, 0xFF]))
		{
			preambleLength = 2;
			encoding = s_utf16BigEndianWithMark;

			return true;
		}

		encoding = s_utf8WithoutMark;
		preambleLength = 0;

		return false;
	}

	private static string? ReadDeclaration(byte[] bytes, Encoding encoding, int start = 0)
	{
		int length = Math.Min(bytes.Length - start, DeclarationLength);

		if (length <= 0)
			return null;

		string head;

		try
		{
			// The decoder is not flushed, so a character the window cuts in two is left out
			// instead of failing the whole head and hiding the declaration.
			char[] characters = new char[encoding.GetMaxCharCount(length)];
			int count = encoding.GetDecoder().GetChars(bytes, byteIndex: start, byteCount: length, chars: characters, charIndex: 0, flush: false);

			head = new string(characters, 0, count);
		}
		catch (ArgumentException)
		{
			// A byte inside the window is not valid in the encoding, so no declaration is read.
			return null;
		}

		Match match = Declaration().Match(head);
		return match.Success ? match.Groups["name"].Value : null;
	}
}
