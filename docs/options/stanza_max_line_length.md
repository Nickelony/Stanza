# stanza_max_line_length

Controls the width the rules measure a line against.
Default: the standard `max_line_length`, then `100`.
Read by the body-shape rules that measure a width, such as [`ZA0305`](https://github.com/Nickelony/Stanza/wiki/ZA0305-Break-the-expression-off-the-signature-line),
and by [`ZA1001`](https://github.com/Nickelony/Stanza/wiki/ZA1001-Shorten-a-long-line), which reports each line past it.
The documentation layout rules and stanza-markup also read it when [`stanza_doc_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_max_line_length) is absent or invalid.

| Value                                           | Effect                                                                                      |
| ----------------------------------------------- | ------------------------------------------------------------------------------------------- |
| `0`                                             | no width is measured, so those rules leave a member as written and `ZA1001` reports nothing |
| a number from 1 to 1000                         | at most that many columns                                                                   |
| below zero, above 1000, absent, or not a number | the next key of the chain; the tool also reports a warning for a value it cannot read       |

With [`ZA0302`](https://github.com/Nickelony/Stanza/wiki/ZA0302-Use-an-expression-body-for-a-single-line-member) [enabled](https://github.com/Nickelony/Stanza/wiki/Terms),
the same member collapses at the default width and breaks at a narrower one:

```csharp
// Before
class C
{
	string Describe(int value)
	{
		return value.ToString();
	}
}

// After at the default width, 100
class C
{
	string Describe(int value) => value.ToString();
}

// After at a width of 40
class C
{
	string Describe(int value) =>
		value.ToString();
}
```

The chain takes `max_line_length` only as a width from 1 to 1000, as the EditorConfig format defines it,
so `max_line_length = off`, `0` and any other value fall through to `100`.
The tool and the analyzers resolve it the same way.

A `0` also turns the documentation layout off, unless [`stanza_doc_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_max_line_length) holds a width above zero.

A line's indentation counts toward its width,
and a tab reaches the next multiple of the file's `tab_width`, then its `indent_size`, then `4`.
A tab after other text counts the columns left to the next stop, so at `4` a tab at column 3 ends at column 4.
Every other character counts as one column per UTF-16 code unit, so an emoji counts 2 and a CJK character 1.

A member fits on one line when its whole line fits.
The line is the indentation, the signature, the arrow with a space either side, and the expression with its semicolon.
[`stanza_multi_line_member_part`](https://github.com/Nickelony/Stanza/wiki/stanza_multi_line_member_part) says when the arrow's columns count toward the line it lands on.

Keep this key equal to the formatter's print width,
and `tab_width` equal to its tab width, so the analyzer and the formatter agree.
[Working with formatters](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#arrow-expressions) says what a narrower width does.
