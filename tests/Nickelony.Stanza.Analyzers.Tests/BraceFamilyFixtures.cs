using System;
using System.Collections.Generic;
using System.Linq;

namespace Nickelony.Stanza.Analyzers.Tests;

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
}
