using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Nickelony.Stanza.Analyzers.Tests.Braces;

/// <summary>Chains whose second branch holds text besides at most one statement.</summary>
internal static class BraceFamilyFixtures
{
	// A block is written as its lines joined by a bar, and a tab inside a line indents it.
	// Each row names the shape, then gives the block.

	/// <summary>Blocks with a comment or a directive on a line besides the statement.</summary>
	public static IEnumerable<object[]> HeldBlocks { get; } =
	[
		["a comment line above", "{|\t// Skip it.|\tDoThing();|}"],
		["two comment lines above", "{|\t// An empty edit|\t// is skipped.|\tDoThing();|}"],
		["a comment line below", "{|\tDoThing();|\t// Done.|}"],
		["a comment on the opening brace line", "{ // Skip it.|\tDoThing();|}"],
		["a block comment on its own line", "{|\t/* Skip it. */|\tDoThing();|}"],
		["a block comment that ends on the statement line", "{|\t/* Skip|\t   it. */ DoThing();|}"],

		[
			"a block comment that starts on the opening brace line",
			"{ /* Skip|   it. */ DoThing(); }",
		],

		["a block comment that ends below the statement", "{ DoThing(); /* Skip|   it. */ }"],
		["a comment on the closing brace line", "{|\tDoThing();|} // Done."],
		["a comment and nothing else", "{|\t// Nothing to do.|}"],
		["pragma directives", "{|#pragma warning disable|\tDoThing();|#pragma warning restore|}"],
		["a pragma directive above and nothing below", "{|#pragma warning disable|\tDoThing();|}"],
		["a conditional that leaves the statement", "{|#if !FLAG|\tDoThing();|#endif|}"],
		["a conditional that leaves no statement", "{|#if FLAG|\tDoThing();|#endif|}"],
	];

	/// <summary>Blocks whose comment stays on the statement's line or outside the braces.</summary>
	public static IEnumerable<object[]> PlainBlocks { get; } =
	[
		["a comment after the statement", "{|\tDoThing(); // Done.|}"],
		["a block comment before the statement", "{|\t/* Skip it. */ DoThing();|}"],
		["a comment above the opening brace", "// Skip it.|{|\tDoThing();|}"],

		[
			"a comment after the opening brace on the statement's line",
			"{ /* Skip it. */ DoThing(); }",
		],

		["a comment after the closing brace on the statement's line", "{ DoThing(); } // Done."],
	];

	/// <summary>A first branch, braced or not, then an else-if branch holding the block.</summary>
	public static string Chain(string block, bool bracedFirst)
	{
		string[] first = bracedFirst
			? ["\t\t{", "\t\t\tDoThing();", "\t\t}"]
			: ["\t\t\tDoThing();"];

		string[] second = [.. block.Split('|').Select(line => "\t\t" + line)];

		string[] lines =
		[
			"class C",
			"{",
			"\tvoid M(bool first, bool second)",
			"\t{",
			"\t\tif (first)",
			.. first,
			"\t\telse if (second)",
			.. second,
			"\t}",
			"}",
		];

		return string.Join(Environment.NewLine, lines);
	}

	private const string Label = "a label";

	private static readonly string[] s_pathStatements =
	[
		"foreach (bool v# in xs)",
		"for (;;)",
		"while (cond)",
		"using (Open())",
		"lock (xs)",
		"fixed (int* p# = ns)",
		"if (cond)",
		Label,
	];

	private static readonly string[] s_frameNames =
	[
		"",
		" in an inner else",
		" before an else if",
		" in an else if",
	];

	/// <summary>Each path of braced statements that ends in an open if, before an else.</summary>
	public static IEnumerable<object?[]> BoundShapes { get; } = CreateBoundShapes();

	// Every row is a name, the source, and the csharp_prefer_braces value, which is null for the default.
	// A path holds up to three statements: one of each kind, every pair, and every triple of three kinds.
	private static List<object?[]> CreateBoundShapes()
	{
		string[][] paths =
		[
			.. s_pathStatements.Select(first => new[] { first }),

			.. s_pathStatements.SelectMany(first =>
				s_pathStatements.Select(second => new[] { first, second })
			),

			.. new[] { s_pathStatements[0], s_pathStatements[6], Label }.SelectMany(first =>
				new[] { s_pathStatements[0], s_pathStatements[6], Label }.SelectMany(second =>
					new[] { s_pathStatements[0], s_pathStatements[6], Label }.Select(third =>
						new[] { first, second, third }
					)
				)
			),
		];

		List<object?[]> rows = [];

		string[] shortKinds =
		[
			s_pathStatements[0],
			s_pathStatements[2],
			s_pathStatements[6],
			Label,
		];

		foreach (string[] path in paths)
		{
			// The other frames put the path in an else, and in the branches of an else if, for the short paths.
			int frames =
				path.Length <= 2 && path.All(shortKinds.Contains) ? s_frameNames.Length : 1;

			for (int frame = 0; frame < frames; frame++)
			{
				// A bare else is a second form of the shortest paths.
				foreach (
					bool bracedElse in path.Length == 1 && frame == 0
						? new[] { true, false }
						: [true]
				)
				{
					AddBoundRows(rows, path, bracedElse, frame);
				}
			}
		}

		return rows;
	}

	private static void AddBoundRows(
		List<object?[]> rows,
		string[] path,
		bool bracedElse,
		int frame
	)
	{
		string source = CreateBoundShape(path, bracedElse, frame);

		foreach (string? preferBraces in new string?[] { "false", null })
		{
			string name =
				string.Join(" > ", path).Replace("#", string.Empty, StringComparison.Ordinal)
				+ s_frameNames[frame]
				+ (bracedElse ? string.Empty : " with a bare else")
				+ $" under {preferBraces ?? "the default"}";

			rows.Add([name, source, preferBraces]);
		}
	}

	private static string CreateBoundShape(string[] path, bool bracedElse, int frame)
	{
		List<string> lines = ["if (b) DoThing();"];
		int labels = 0;

		for (int index = path.Length - 1; index >= 0; index--)
		{
			if (path[index] == Label)
			{
				lines[0] = $"L{labels++}: {lines[0]}";
			}
			else
			{
				lines =
				[
					path[index]
						.Replace(
							"#",
							index.ToString(CultureInfo.InvariantCulture),
							StringComparison.Ordinal
						),

					"{",
					.. lines.Select(line => "\t" + line),
					"}",
				];
			}
		}

		string[] elseLines = bracedElse
			? ["else", "{", "\tDoThing();", "}"]
			: ["else", "\tDoThing();"];

		string[] inBlock = [.. lines.Select(line => "\t" + line)];

		string[] body = frame switch
		{
			1 =>
			[
				"if (c)",
				"{",
				"\tif (a)",
				"\t\tDoThing();",
				"\telse",
				"\t{",
				.. inBlock.Select(line => "\t" + line),
				"\t}",
				"}",
				.. elseLines,
			],

			2 =>
			[
				"if (c)",
				"{",
				"\tDoThing();",
				"}",
				"else if (a)",
				"{",
				.. inBlock,
				"}",
				.. elseLines,
			],

			3 =>
			[
				"if (c)",
				"{",
				.. inBlock,
				"}",
				"else if (a)",
				"{",
				"\tDoThing();",
				"}",
				.. elseLines,
			],

			_ => ["if (a)", "{", .. inBlock, "}", .. elseLines],
		};

		string[] all =
		[
			"class C",
			"{",
			"\tunsafe void M(bool a, bool b, bool c, bool cond, bool[] xs, int[] ns)",
			"\t{",
			.. body.Select(line => "\t\t" + line),
			"\t}",
			"}",
		];

		return string.Join(Environment.NewLine, all);
	}
}
