using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Core;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Guards;

/// <summary>
/// Runs the pinned CSharpier on lines that the column count puts at its print width and past it.
/// </summary>
[TestClass]
[TestCategory("Repository")]
public sealed class ColumnCountGuardTests
{
	private const int PrintWidth = 100;

	private const string Declaration = "public static string S() => \"";
	private const string Suffix = "\";";

	// CSharpier 1.3.0 lists these ranges, first and last code unit of each, in CharacterSizeCalculator.CalculateWidth.
	private static readonly int[] s_wideRanges =
	[
		0x1100, 0x115F, 0x2329, 0x232A, 0x2E80, 0x303E, 0x3040, 0xA4CF, 0xAC00, 0xD7A3,
		0xF900, 0xFAFF, 0xFE10, 0xFE19, 0xFE30, 0xFE6F, 0xFF00, 0xFF60, 0xFFE0, 0xFFE6,
	];

	[TestMethod]
	public void CSharpierBreaksALineExactlyWhereTheColumnCountSaysItIsPastTheWidth()
	{
		List<(string Text, string Name, int Depth)> rows = ReadRows();
		List<string> sources = [];

		foreach ((string text, _, int depth) in rows)
		{
			sources.Add(Member(text, depth, PrintWidth));
			sources.Add(Member(text, depth, PrintWidth + 1));
		}

		using TempDirectory directory = new(editorConfig: false);

		_ = directory.Write(".csharpierrc.json", "{ \"printWidth\": 100, \"useTabs\": true, \"indentSize\": 4, \"endOfLine\": \"lf\" }\n");

		for (int index = 0; index < sources.Count; index++)
			_ = directory.Write(index + ".cs", sources[index]);

		(int exitCode, string error) = FormatterClaimGuardTests.RunCSharpier(directory.Root);

		Assert.AreEqual(0, exitCode, $"'dotnet tool run csharpier' failed. Run 'dotnet tool restore' first. {error}");

		List<string> disagreements = [];

		for (int index = 0; index < rows.Count; index++)
		{
			bool joinsAtWidth = IsOneLine(directory.Read((2 * index) + ".cs"));
			bool joinsPastWidth = IsOneLine(directory.Read((2 * index) + 1 + ".cs"));

			if (!joinsAtWidth)
				disagreements.Add($"{rows[index].Name}: CSharpier broke a line that is {PrintWidth} columns by our count.");

			if (joinsPastWidth)
				disagreements.Add($"{rows[index].Name}: CSharpier kept a line that is {PrintWidth + 1} columns by our count.");
		}

		Assert.IsEmpty(disagreements, string.Join('\n', disagreements));
	}

	// Each row is three copies of one character, so that a count off by one for it moves the line across the width.
	private static List<(string Text, string Name, int Depth)> ReadRows()
	{
		SortedSet<int> units = [];

		for (int index = 0; index < s_wideRanges.Length; index++)
		{
			// The code unit beside each edge and the edge itself.
			int edge = s_wideRanges[index];

			_ = units.Add(edge);
			_ = units.Add(index % 2 == 0 ? edge - 1 : edge + 1);
		}

		// A spread across the basic plane, so a range that the list above lacks shows too.
		for (int unit = 0x400; unit < 0x10000; unit += 0x400)
			_ = units.Add(unit);

		// The characters that the terminal and the formatter could count differently: emoji, joiners, selectors, combining marks.
		foreach (int unit in new[] { 0x2705, 0x231A, 0x2764, 0xFE0F, 0x200B, 0x200D, 0x0301, 0x3099, 0xFF61 })
			_ = units.Add(unit);

		List<(string, string, int)> rows = [];

		foreach (int unit in units.Where(IsPlainUnit))
			rows.Add((new string((char)unit, 3), $"U+{unit:X4}", 1));

		foreach (int codePoint in new[] { 0x1F600, 0x20000, 0x1D400 })
			rows.Add((string.Concat(Enumerable.Repeat(char.ConvertFromUtf32(codePoint), 3)), $"U+{codePoint:X}", 1));

		// A tab counts to its stop in the indentation, and one column anywhere else, such as in a string.
		foreach (int depth in new[] { 1, 2, 3 })
		{
			rows.Add(("\t", $"a tab in a string at depth {depth}", depth));
			rows.Add(("\t\t\t\t\t", $"five tabs in a string at depth {depth}", depth));
			rows.Add(("x\tx\tx\t", $"tabs between letters in a string at depth {depth}", depth));
		}

		return rows;
	}

	// A line break or a surrogate cannot stand inside a string literal as a code unit of its own.
	private static bool IsPlainUnit(int unit)
	{
		return char.GetUnicodeCategory((char)unit)
			is not (
				UnicodeCategory.Control
				or UnicodeCategory.Surrogate
				or UnicodeCategory.LineSeparator
				or UnicodeCategory.ParagraphSeparator
			);
	}

	// The member sits in classes nested to the depth, and CSharpier writes its indentation as tabs.
	private static string Member(string text, int depth, int columns)
	{
		string line = new string('\t', depth) + Declaration;
		int fixedColumns = TextColumns.GetColumns(line + text + Suffix, tabColumns: 4);
		string padding = new('a', columns - fixedColumns);
		int[] levels = [.. Enumerable.Range(0, depth - 1)];

		string opening = string.Concat(levels.Select(level => $"{new string('\t', level + 1)}class D{level}\n{new string('\t', level + 1)}{{\n"));
		string closing = string.Concat(Enumerable.Reverse(levels).Select(level => $"{new string('\t', level + 1)}}}\n"));

		return $"class C\n{{\n{opening}{line}{padding}{text}{Suffix}\n{closing}}}\n";
	}

	// CSharpier writes a member that is too wide with its arrow at the end of the line and the expression below it.
	private static bool IsOneLine(string formatted) =>
		!formatted.Contains("=>\n", StringComparison.Ordinal);
}
