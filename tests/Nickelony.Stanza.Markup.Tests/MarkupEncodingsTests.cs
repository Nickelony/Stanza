using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Files;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Markup.Tests;

[TestClass]
public sealed class MarkupEncodingsTests
{
	private const string Document = """
		<root>
		  <child>café</child>
		</root>

		""";

	private const string Latin1Document = """
		<?xml version="1.0" encoding="iso-8859-1"?>
		<root>
		  <child>café</child>
		</root>

		""";

	private const string AsciiDocument = """
		<?xml version="1.0" encoding="us-ascii"?>
		<root>
		  <child>cafe</child>
		</root>

		""";

	private const string Utf16Document = """
		<?xml version="1.0" encoding="utf-16"?>
		<root>
		  <child>café</child>
		</root>

		""";

	private const string Utf16BeDocument = """
		<?xml version="1.0" encoding="utf-16be"?>
		<root>
		  <child>café</child>
		</root>

		""";

	private const string Utf32Document = """
		<?xml version="1.0" encoding="utf-32"?>
		<root>
		  <child>café</child>
		</root>

		""";

	private const string Utf32BeDocument = """
		<?xml version="1.0" encoding="utf-32be"?>
		<root>
		  <child>café</child>
		</root>

		""";

	private const string DeclaredUtf8Document = """
		<?xml version="1.0" encoding="utf-8"?>
		<root>
		  <child>café</child>
		</root>

		""";

	public static IEnumerable<object[]> GetRoundTripRows()
	{
		List<object[]> rows =
		[
			["utf-8, no mark", Document, Utf8(mark: false)],
			["utf-8, mark", Document, Utf8(mark: true)],
			["utf-8 declared, no mark", DeclaredUtf8Document, Utf8(mark: false)],
			["utf-8 declared, mark", DeclaredUtf8Document, Utf8(mark: true)],
			["utf-16, no mark", Utf16Document, Utf16(bigEndian: false, mark: false)],
			["utf-16, mark", Utf16Document, Utf16(bigEndian: false, mark: true)],
			["utf-16, mark, no declaration", Document, Utf16(bigEndian: false, mark: true)],
			["utf-16, no mark, no declaration", Document, Utf16(bigEndian: false, mark: false)],
			["utf-16 big endian, no mark", Utf16BeDocument, Utf16(bigEndian: true, mark: false)],

			[
				"utf-16 big endian, no mark, no declaration",
				Document,
				Utf16(bigEndian: true, mark: false),
			],

			["utf-16 big endian, mark", Utf16BeDocument, Utf16(bigEndian: true, mark: true)],
			["utf-32, no mark", Utf32Document, Utf32(bigEndian: false, mark: false)],
			["utf-32, mark", Utf32Document, Utf32(bigEndian: false, mark: true)],
			["utf-32 big endian, no mark", Utf32BeDocument, Utf32(bigEndian: true, mark: false)],
			["utf-32 big endian, mark", Utf32BeDocument, Utf32(bigEndian: true, mark: true)],
			["iso-8859-1", Latin1Document, Encoding.Latin1],
			["us-ascii", AsciiDocument, Encoding.ASCII],
			["utf-8 declared in upper case", Declared("\"UTF-8\"", "café"), Utf8(mark: false)],
			["utf-8 declared in single quotes", Declared("'utf-8'", "café"), Utf8(mark: false)],
			["latin1 named by its alias", Declared("\"latin1\"", "café"), Encoding.Latin1],
			["us-ascii named by its alias", Declared("\"ascii\"", "cafe"), Encoding.ASCII],
			["utf-16 over a big-endian mark", Utf16Document, Utf16(bigEndian: true, mark: true)],
			["utf-32 over a big-endian mark", Utf32Document, Utf32(bigEndian: true, mark: true)],
			["utf8 named by its alias", Declared("\"utf8\"", "café"), Utf8(mark: false)],
			["latin-1 named by its alias", Declared("\"latin-1\"", "café"), Encoding.Latin1],
			["utf-16le named by its alias", Declared("\"utf-16le\"", "café"), Utf16(false, false)],
			["unicode named by its alias", Declared("\"unicode\"", "café"), Utf16(false, false)],

			[
				"unicodefffe named by its alias",
				Declared("\"unicodefffe\"", "café"),
				Utf16(true, false),
			],

			["utf-32le named by its alias", Declared("\"utf-32le\"", "café"), Utf32(false, false)],
		];

		return rows;
	}

	public static IEnumerable<object[]> GetRefusedRows()
	{
		List<object[]> rows =
		[
			[
				"utf-16 declaring utf-8",
				DeclaredUtf8Document,
				Utf16(bigEndian: false, mark: false),
				Mismatch("utf-8"),
			],

			[
				"utf-8 with a mark declaring utf-16",
				Utf16Document,
				Utf8(mark: true),
				Mismatch("utf-16"),
			],

			[
				"utf-32 declaring utf-32be",
				"<?xml version=\"1.0\" encoding=\"utf-32be\"?>\r\n<root />\r\n",
				Utf32(bigEndian: false, mark: false),
				Mismatch("utf-32be"),
			],

			[
				"utf-32be declared above a little endian mark",
				Utf32BeDocument,
				Utf32(bigEndian: false, mark: true),
				Mismatch("utf-32be"),
			],

			[
				"us-ascii without a mark declaring utf-16",
				Utf16Document,
				Encoding.ASCII,
				Mismatch("utf-16"),
			],

			[
				"us-ascii without a mark declaring utf-32",
				Utf32Document,
				Encoding.ASCII,
				Mismatch("utf-32"),
			],

			[
				"iso-8859-1 without a declaration",
				Document,
				Encoding.Latin1,
				"could not be read: the bytes could not be decoded as utf-8, so it was left alone.",
			],
		];

		return rows;
	}

	[TestMethod]
	[DynamicData(
		nameof(GetRoundTripRows),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public void EverySupportedEncodingRoundTripsByteForByte(
		string name,
		string text,
		Encoding encoding
	) => AssertRoundTrips(text, encoding, name);

	[TestMethod]
	[DynamicData(
		nameof(GetRefusedRows),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public void AnEncodingTheToolCannotCarryIsRefusedCleanly(
		string name,
		string text,
		Encoding encoding,
		string message
	)
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		File.WriteAllText(path, text, encoding);

		byte[] written = File.ReadAllBytes(path);

		_ = MarkupSource.TryRead(path, out MarkupSource? source, out string? error);

		Assert.IsNull(source, name);
		Assert.AreEqual(message, error, name);
		Assert.AreSequenceEqual(written, File.ReadAllBytes(path), name);
	}

	[TestMethod]
	public void ABytesAfterAMarkThatDoNotDecodeAreRefusedAndTheFileIsLeftAlone()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		byte[] written = [0xEF, 0xBB, 0xBF, (byte)'<', (byte)'a', 0xFF, (byte)'>'];

		File.WriteAllBytes(path, written);

		Assert.IsFalse(MarkupSource.TryRead(path, out MarkupSource? source, out string? error));
		Assert.IsNull(source);

		Assert.AreEqual(
			"could not be read: the bytes could not be decoded as utf-8, so it was left alone.",
			error
		);

		Assert.AreSequenceEqual(written, File.ReadAllBytes(path));
	}

	[TestMethod]
	public void ACharacterSplitByTheEndOfTheDeclarationWindowStillLetsTheDeclarationBeRead()
	{
		const string declaration = "<?xml version=\"1.0\" encoding=\"iso-8859-1\"?>";

		// The comment fills the window to byte 255, so the two bytes of the next character straddle byte 256.
		string padding = new('a', 255 - declaration.Length - "<!---->".Length);
		string text = declaration + "<!--" + padding + "-->" + "é<root/>";

		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

		_ = MarkupSource.TryRead(path, out MarkupSource? source, out string? error);

		Assert.IsNull(source, "a declaration that disagrees with the mark is refused.");
		Assert.Contains("iso-8859-1", error!);
	}

	private static void AssertRoundTrips(string text, Encoding encoding, string message = "")
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		File.WriteAllText(path, text, encoding);

		byte[] written = File.ReadAllBytes(path);
		_ = MarkupSource.TryRead(path, out MarkupSource? source, out string? error);

		Assert.IsNull(error, message);

		MarkupSource located = source ?? throw new AssertFailedException("The file was refused.");

		Assert.AreEqual(text, located.Text, message);
		Assert.IsTrue(located.TryWrite(path, located.Text, out string? writeError), message);
		Assert.IsNull(writeError, message);
		Assert.AreSequenceEqual(written, File.ReadAllBytes(path), message);
	}

	[TestMethod]
	public void AProcessingInstructionIsNotADeclaration()
	{
		using TempDirectory directory = new();
		string path = directory.Write("sample.xml", string.Empty);

		const string text =
			"<?xml-stylesheet type=\"text/xsl\" href=\"a.xsl\" encoding=\"utf-16\"?>\r\n"
			+ "<root>\r\n  <a>one</a>\r\n</root>\r\n";

		File.WriteAllText(path, text, Utf8(mark: false));

		_ = MarkupSource.TryRead(path, out MarkupSource? source, out string? error);

		Assert.IsNull(error);
		Assert.IsNotNull(source);
		Assert.AreEqual(text, source.Text);
	}

	private static string Declared(string quotedName, string text) =>
		$"<?xml version=\"1.0\" encoding={quotedName}?>\n<root>\n  <child>{text}</child>\n</root>\n";

	private static string Mismatch(string declared) =>
		$"could not be read: it declares the encoding '{declared}', which does not match the bytes it starts with, so it was left alone.";

	private static UTF8Encoding Utf8(bool mark) => new(encoderShouldEmitUTF8Identifier: mark);

	private static UnicodeEncoding Utf16(bool bigEndian, bool mark) =>
		new(bigEndian, byteOrderMark: mark);

	private static UTF32Encoding Utf32(bool bigEndian, bool mark) =>
		new(bigEndian, byteOrderMark: mark);
}
