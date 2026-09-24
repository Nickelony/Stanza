# stanza_max_line_length

Controls the width the rules measure a line against.
Default: `100`.
Read by the body-shape rules that measure a width, such as [`ZA0043`](https://github.com/Nickelony/Stanza/wiki/ZA0043-Break-the-expression-off-the-signature-line).
The documentation layout rules and stanza-markup also read it when [`stanza_doc_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_max_line_length) is absent or invalid.

| Value                                           | Effect                                                                                        |
| ----------------------------------------------- | --------------------------------------------------------------------------------------------- |
| `0`                                             | no width is measured, so those rules leave a member as written                                |
| a number from 1 to 1000                         | at most that many columns                                                                     |
| below zero, above 1000, absent, or not a number | the default; the tool also reports a warning for a value it cannot read                       |

With [`ZA0040`](https://github.com/Nickelony/Stanza/wiki/ZA0040-Use-an-expression-body-for-a-single-line-member) [enabled](https://github.com/Nickelony/Stanza/wiki/Terms),
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

A `0` also turns the documentation layout off, unless [`stanza_doc_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_max_line_length) holds a width above zero.

A line's indentation counts toward its width,
and a tab reaches the next multiple of the columns [`stanza_tab_width`](https://github.com/Nickelony/Stanza/wiki/stanza_tab_width) resolves to.
Every other character counts as one column per UTF-16 code unit, so an emoji counts 2 and a CJK character 1.

A member fits on one line when its whole line fits.
The line is the indentation, the signature, the arrow with a space either side, and the expression with its semicolon.
[`stanza_multiline_body_trigger`](https://github.com/Nickelony/Stanza/wiki/stanza_multiline_body_trigger) says when the arrow's columns count toward the line it lands on.

Keep this key equal to the formatter's print width,
and [`stanza_tab_width`](https://github.com/Nickelony/Stanza/wiki/stanza_tab_width) equal to its tab width, so the analyzer and the formatter agree.
[Working with formatters](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#arrow-expressions) says what a narrower width does.
