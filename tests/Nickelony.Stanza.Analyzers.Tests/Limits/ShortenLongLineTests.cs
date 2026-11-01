using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Limits;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Limits;

[TestClass]
public sealed class ShortenLongLineTests
{
	private static readonly string s_crlf = new([(char)13, (char)10]);

	private static IReadOnlyDictionary<string, ReportDiagnostic> Rule { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.ShortenLongLine);

	// Each source is one line whose first column past a width of 40 lies inside a string or a URL that began before it.
	public static IEnumerable<object[]> Excused { get; } =
	[
		["a regular string", "var s = \"" + new string('a', 60) + "\";"],
		["a verbatim string", "var s = @\"" + new string('a', 60) + "\";"],
		["a raw string", "var s = \"\"\"" + new string('a', 60) + "\"\"\";"],
		["a UTF-8 string", "var s = \"" + new string('a', 60) + "\"u8;"],
		["an interpolated string", "var s = $\"{x} " + new string('a', 60) + "\";"],
		["an interpolated verbatim string", "var s = $@\"{x} " + new string('a', 60) + "\";"],
		["an interpolated raw string", "var s = $\"\"\"{x} " + new string('a', 60) + "\"\"\";"],
		["a string that closes a call", "Call(\"" + new string('a', 60) + "\");"],
		["a string that a concatenation continues", "var s = \"" + new string('a', 60) + "\" +"],

		[
			"a string that holds a URL",
			"var s = \"https://example.com/" + new string('a', 60) + "\";",
		],

		["a URL in a line comment", "// See https://example.com/" + new string('a', 60)],
		["a URL in a block comment", "/* See https://example.com/" + new string('a', 60) + " */"],
		["an http URL", "// See http://example.com/" + new string('a', 60)],
		["a URL after code", "var s = 1; // See https://example.com/" + new string('a', 60)],
	];

	// Each source is one line, whose overflow no string and no URL covers from before the width.
	public static IEnumerable<object[]> Reported { get; } =
	[
		["code", "int " + new string('a', 60) + ";"],
		["a line comment", "// " + new string('a', 60)],
		["a block comment", "/* " + new string('a', 60) + " */"],

		[
			"a comment with a URL past the width",
			"// " + new string('a', 36) + " https://example.com/" + new string('a', 40),
		],

		[
			"a comment whose scheme is in capitals",
			"// See HTTPS://example.com/" + new string('a', 60),
		],

		[
			"a comment whose URL follows an opening bracket",
			"// See <https://example.com/" + new string('a', 60) + ">",
		],

		["a directive", "#region " + new string('a', 60)],
		["a directive with a URL", "#region https://example.com/" + new string('a', 60)],

		[
			"code after a string that passes the width",
			"var s = \"" + new string('a', 60) + "\" + other;",
		],

		[
			"words after a URL",
			"// See https://example.com/" + new string('a', 60) + " for the details",
		],

		["a comment after a string", "var s = \"" + new string('a', 60) + "\"; // note"],
		["code after an interpolated string", "Call($\"{x} " + new string('a', 60) + "\", other);"],
		["code after a short string", "var s = \"ab\"; var t = " + new string('a', 60) + ";"],
		["code before a short string", "var " + new string('a', 40) + " = \"ab\";"],

		[
			"a string that starts at the first column past the width",
			"var s =" + new string(' ', 33) + "\"" + new string('a', 40) + "\";",
		],

		["an interpolation hole", "var s = $\"a {" + new string('a', 60) + "}\";"],
		["a character literal", "var c = " + new string(' ', 31) + "'a';"],
	];

	[TestMethod]
	public async Task ALineAtTheWidthPassesAndOneColumnPastItIsReportedAtThatColumn()
	{
		AnalyzerRunResult atWidth = await ReportAsync(Code(40) + "\n");
		AnalyzerRunResult pastWidth = await ReportAsync(Code(41) + "\n");

		Assert.IsEmpty(atWidth.Diagnostics);
		Assert.HasCount(1, pastWidth.Diagnostics);
		Assert.AreEqual(";", LimitFixtures.Text(pastWidth.Diagnostics[0]));

		Assert.AreEqual(
			40,
			pastWidth.Diagnostics[0].Location.GetLineSpan().StartLinePosition.Character
		);
	}

	[TestMethod]
	public async Task TheDefaultWidthIsOneHundred()
	{
		AnalyzerRunResult atWidth = await ReportAsync(Code(100) + "\n", width: null);
		AnalyzerRunResult pastWidth = await ReportAsync(Code(101) + "\n", width: null);

		Assert.IsEmpty(atWidth.Diagnostics);
		Assert.HasCount(1, pastWidth.Diagnostics);
	}

	[TestMethod]
	public async Task TheReportCoversTheLineFromTheFirstColumnPastTheWidthToItsEnd()
	{
		AnalyzerRunResult result = await ReportAsync("int " + new string('a', 40) + ";\n");

		// The line is "int " and 40 letters and a semicolon, so the width of 40 ends after the first 36 letters.
		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual(new string('a', 4) + ";", LimitFixtures.Text(result.Diagnostics[0]));
	}

	[TestMethod]
	public async Task EachLongLineIsReportedOnceAndAShortLineBetweenThemIsNot()
	{
		string source = Code(60) + "\n" + Code(10) + "\n" + Code(60) + "\n" + Code(41) + "\n";

		AnalyzerRunResult result = await ReportAsync(source);

		Assert.HasCount(3, result.Diagnostics);
		Assert.AreSequenceEqual([0, 2, 3], result.DiagnosticLines);
	}

	[TestMethod]
	[DataRow("2", 0)]
	[DataRow("4", 0)]
	[DataRow("5", 0)]
	[DataRow("6", 1)]
	[DataRow("8", 1)]
	public async Task ATabCountsTheColumnsOfTheTabWidth(string tabWidth, int reports)
	{
		// The line is 16 characters, so it is 15 columns and a tab: 20 columns at a tab width of 5.
		string source = "\tint " + new string('a', 10) + ";\n";

		AnalyzerRunResult result = await ReportAsync(source, width: "20", tabWidth);

		Assert.HasCount(reports, result.Diagnostics, $"A tab width of {tabWidth}.");
	}

	[TestMethod]
	[DataRow(null, null, 1)]
	[DataRow(null, "105", 1)]
	[DataRow(null, "120", 0)]
	[DataRow(null, "off", 1)]
	[DataRow(null, "wide", 1)]
	[DataRow("120", "90", 0)]
	[DataRow("90", "120", 1)]
	[DataRow("wide", "120", 0)]
	[DataRow("5000", "120", 0)]
	[DataRow("wide", "off", 1)]
	[DataRow("0", "50", 0)]
	public async Task TheWidthFallsBackFromItsKeyToTheStandardKeyAndThenToOneHundred(
		string? width,
		string? standardWidth,
		int reports
	)
	{
		// The line is 110 columns, past the default of 100 and inside 120.
		AnalyzerRunResult result = await ReportAsync(
			Code(110) + "\n",
			width,
			standardWidth: standardWidth
		);

		Assert.HasCount(reports, result.Diagnostics, $"{width} beside {standardWidth}.");
	}

	[TestMethod]
	public async Task ATabAfterTextReachesTheNextTabStopAndTheOverflowStartsAfterIt()
	{
		// Four columns of text, a tab to column 8, then 14 letters: the width of 20 ends after the first 12 of them.
		string source = "int " + "\t" + new string('a', 14) + ";\n";

		AnalyzerRunResult result = await ReportAsync(source, width: "20", tabWidth: "8");

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual("aa;", LimitFixtures.Text(result.Diagnostics[0]));
	}

	[TestMethod]
	public async Task TheOverflowStartsAfterTheColumnsTheTabsTake()
	{
		// At a tab width of 8 the tab takes 8 columns, so 12 characters after it fill the width of 20.
		string source = "\tint " + new string('a', 10) + ";\n";

		AnalyzerRunResult result = await ReportAsync(source, width: "20", tabWidth: "8");

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual("aa;", LimitFixtures.Text(result.Diagnostics[0]));
	}

	[TestMethod]
	[DataRow("0", 0)]
	[DataRow("119", 1)]
	[DataRow("120", 0)]
	[DataRow("1000", 0)]
	[DataRow("-1", 1)]
	[DataRow("1001", 1)]
	[DataRow("wide", 1)]
	[DataRow("", 1)]
	public async Task TheWidthComesFromTheOptionAndZeroTurnsTheRuleOff(string width, int reports)
	{
		// A missing or invalid width reads as the default of 100, which a line of 120 columns passes.
		AnalyzerRunResult result = await ReportAsync(Code(120) + "\n", width);
		Assert.HasCount(reports, result.Diagnostics, $"A width of '{width}'.");
	}

	// A build that writes no documentation file parses the comment as a plain one, and its line is left alone all the same.
	[TestMethod]
	[DataRow(DocumentationMode.Parse)]
	[DataRow(DocumentationMode.None)]
	public async Task ADocumentationCommentLineIsLeftToTheDocumentationLayout(
		DocumentationMode mode
	)
	{
		using IDisposable parse = RoslynTestHarness.AtDocumentationMode(mode);

		const string comment =
			"/// <summary>A summary that runs well past the width of forty columns.</summary>\n";

		AnalyzerRunResult documented = await ReportAsync(comment + "class C { }\n");

		AnalyzerRunResult ordinary = await ReportAsync(
			comment.Replace("///", "//") + "class C { }\n"
		);

		Assert.IsEmpty(documented.Diagnostics);
		Assert.HasCount(1, ordinary.Diagnostics);
	}

	[TestMethod]
	public async Task EveryLineOfAMultiLineDocumentationCommentIsLeftAlone()
	{
		string source =
			"/// <summary>\n"
			+ "/// A summary that runs well past the width of forty columns.\n"
			+ "/// </summary>\n"
			+ "/// <param name=\"value\">A parameter that runs well past the width of forty columns.</param>\n"
			+ "class C\n{\n\tvoid M(int value) { }\n}\n";

		AnalyzerRunResult result = await ReportAsync(source);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	[DataRow(DocumentationMode.Parse)]
	[DataRow(DocumentationMode.None)]
	public async Task EveryLineOfABlockDocumentationCommentIsLeftAlone(DocumentationMode mode)
	{
		using IDisposable parse = RoslynTestHarness.AtDocumentationMode(mode);

		string source =
			"/**\n"
			+ " * A summary that runs well past the width of forty columns.\n"
			+ " */\n"
			+ "class C { }\n";

		AnalyzerRunResult result = await ReportAsync(source);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACodeLineBesideADocumentationCommentIsStillJudged()
	{
		string source =
			"/// <summary>Text.</summary>\n" + "class " + new string('A', 60) + " { }\n";

		AnalyzerRunResult result = await ReportAsync(source);

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual(1, result.Diagnostics[0].Location.GetLineSpan().StartLinePosition.Line);
	}

	[TestMethod]
	[DynamicData(
		nameof(Excused),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ALineWhoseOverflowStartsInsideAnUnbreakableTokenIsLeftAlone(
		string name,
		string line
	)
	{
		AnalyzerRunResult result = await ReportAsync(line + "\n");
		Assert.IsEmpty(result.Diagnostics, name);
	}

	[TestMethod]
	[DynamicData(
		nameof(Reported),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task ALineWhoseOverflowIsNotInsideAnUnbreakableTokenIsReported(
		string name,
		string line
	)
	{
		AnalyzerRunResult result = await ReportAsync(line + "\n");
		Assert.HasCount(1, result.Diagnostics, name);
	}

	[TestMethod]
	public async Task EveryLineOfAMultiLineStringThatPassesTheWidthInsideItIsLeftAlone()
	{
		string source =
			"var verbatim = @\"short\n"
			+ new string('a', 60)
			+ "\";\n"
			+ "var raw = \"\"\"\n"
			+ new string('b', 60)
			+ "\n\"\"\";\n";

		AnalyzerRunResult result = await ReportAsync(source);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ALongLineAfterAStringThatEndsInsideTheWidthIsReportedAtTheCode()
	{
		string source = "var s = \"ab\"; var t = " + new string('a', 40) + ";\n";

		AnalyzerRunResult result = await ReportAsync(source);

		Assert.HasCount(1, result.Diagnostics);
		Assert.EndsWith(";", LimitFixtures.Text(result.Diagnostics[0]));
		Assert.DoesNotContain("\"", LimitFixtures.Text(result.Diagnostics[0]));
	}

	[TestMethod]
	public async Task ATextInsideADisabledConditionalBlockIsJudgedAsALine()
	{
		string source = "#if NEVER\n" + new string('a', 60) + "\n#endif\n";

		AnalyzerRunResult result = await ReportAsync(source);

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual(1, result.Diagnostics[0].Location.GetLineSpan().StartLinePosition.Line);
	}

	[TestMethod]
	[DataRow(false)]
	[DataRow(true)]
	public async Task TheReportEndsBeforeTheLineBreakWhicheverBreakTheFileUses(bool crlf)
	{
		string source = (Code(60) + "\n" + Code(60) + "\n" + Code(10) + "\n").Replace(
			"\n",
			crlf ? s_crlf : "\n"
		);

		AnalyzerRunResult result = await ReportAsync(source);

		Assert.HasCount(2, result.Diagnostics);

		foreach (Diagnostic diagnostic in result.Diagnostics)
		{
			Assert.EndsWith(";", LimitFixtures.Text(diagnostic));
			Assert.IsFalse(LimitFixtures.Text(diagnostic).Contains((char)13));

			Assert.AreEqual(
				diagnostic.Location.GetLineSpan().StartLinePosition.Line,
				diagnostic.Location.GetLineSpan().EndLinePosition.Line
			);
		}
	}

	[TestMethod]
	public async Task ALineIsMeasuredInCharactersAndNotInBytes()
	{
		// An "e" with an acute accent takes two bytes in UTF-8 and one column.
		string accented = new((char)0x00E9, 30);

		AnalyzerRunResult fits = await ReportAsync("// " + accented + "\n");

		AnalyzerRunResult past = await ReportAsync(
			"// " + accented + new string((char)0x00E9, 11) + "\n"
		);

		Assert.IsEmpty(fits.Diagnostics, "33 columns, though 63 bytes.");
		Assert.HasCount(1, past.Diagnostics);
		Assert.AreEqual(new string((char)0x00E9, 4), LimitFixtures.Text(past.Diagnostics[0]));
	}

	[TestMethod]
	public async Task ACharacterOutsideTheBasicPlaneTakesTwoColumns()
	{
		string emoji = char.ConvertFromUtf32(0x1F600);

		// Each one is two UTF-16 code units, so 19 of them take 38 columns and pass the width of 40 by one with the marker.
		AnalyzerRunResult fits = await ReportAsync(
			"// " + string.Concat(Enumerable.Repeat(emoji, 18)) + "\n"
		);

		AnalyzerRunResult past = await ReportAsync(
			"// " + string.Concat(Enumerable.Repeat(emoji, 19)) + "\n"
		);

		Assert.IsEmpty(fits.Diagnostics);
		Assert.HasCount(1, past.Diagnostics);
	}

	[TestMethod]
	public async Task APragmaSilencesTheLinesBetweenItsDisableAndItsRestore()
	{
		string source =
			"#pragma warning disable ZA1001\n"
			+ Code(60)
			+ "\n"
			+ "#pragma warning restore ZA1001\n"
			+ Code(60)
			+ "\n";

		AnalyzerRunResult result = await ReportAsync(source);

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual(3, result.Diagnostics[0].Location.GetLineSpan().StartLinePosition.Line);
	}

	[TestMethod]
	public async Task AGeneratedFileIsNotJudged()
	{
		string line = Code(60) + "\n";

		AnalyzerRunResult generated = await ReportAsync("// <auto-generated/>\n" + line);
		AnalyzerRunResult handWritten = await ReportAsync("// <hand-written/>\n" + line);

		Assert.IsEmpty(generated.Diagnostics, "a line of a file marked as generated.");
		Assert.HasCount(1, handWritten.Diagnostics);
	}

	[TestMethod]
	public void TheCostGrowsLinearlyWithTheNumberOfLongLines()
	{
		static string Many(int count)
		{
			return string.Concat(
				Enumerable
					.Range(0, count)
					.Select(i =>
						$"var v{i} = M({i}, other, another, yetAnother, andMore, stillMore, last);\n"
					)
			);
		}

		AssertLinear(Many(5_000), Many(20_000));
	}

	[TestMethod]
	public void TheCostGrowsLinearlyWithTheLengthOfALine()
	{
		static string Long(int count)
		{
			return "var v = M("
				+ string.Join(", ", Enumerable.Range(0, count).Select(i => $"p{i}"))
				+ ");\n";
		}

		AssertLinear(Long(5_000), Long(20_000));
	}

	[TestMethod]
	public async Task AVeryLongLineIsOneDiagnostic()
	{
		AnalyzerRunResult result = await ReportAsync(Code(200_000) + "\n");
		Assert.HasCount(1, result.Diagnostics);
	}

	private static void AssertLinear(string small, string large)
	{
		LinearCost.AssertLinear(
			small,
			large,

			root =>
				LongLines.CollectOverflows(
					syntaxTree: root.SyntaxTree,
					maxColumns: 40,
					tabColumns: 4,
					cancellationToken: default
				)
		);
	}

	// A declaration of the given number of columns, which parses as a statement of a file with no class.
	private static string Code(int columns) => "int " + new string('a', columns - 5) + ";";

	private static Task<AnalyzerRunResult> ReportAsync(
		string source,
		string? width = "40",
		string? tabWidth = null,
		string? standardWidth = null
	)
	{
		Dictionary<string, string> options = [];

		if (width is not null)
			options["stanza_max_line_length"] = width;

		if (standardWidth is not null)
			options["max_line_length"] = standardWidth;

		if (tabWidth is not null)
			options["tab_width"] = tabWidth;

		// A line is judged whether or not it compiles, so a row is free to use names that nothing declares.
		return RoslynTestHarness.ReportAsync(
			source,

			new RunOptions([new ShortenLongLineAnalyzer()])
			{
				DiagnosticOptions = Rule,
				AnalyzerConfigOptions = options,
				RequireCompiles = false,
			}
		);
	}
}
