# stanza_doc_min_line_words

Controls the fewest words a prose line may hold; the wrapper gives a shorter line a [penalty](https://github.com/Nickelony/Stanza/wiki/Terms).
Default: `2`.
Read by the documentation layout rules, such as [`ZA0201`](https://github.com/Nickelony/Stanza/wiki/ZA0201-Lay-out-a-documentation-comment),
and by the stanza-markup [reprint](https://github.com/Nickelony/Stanza/wiki/Terms).

| Value                                           | Effect                                                                  |
| ----------------------------------------------- | ----------------------------------------------------------------------- |
| `0` or `1`                                      | no line carries a penalty, so a line may hold a single word             |
| a number from 2 to 1000                         | a line with fewer words carries a penalty                               |
| below zero, above 1000, absent, or not a number | the default; the tool also reports a warning for a value it cannot read |

At the default width of [`stanza_doc_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_max_line_length) and one tab of indentation,
the content width is 92 columns, so the same text breaks two ways:

```csharp
// 2 (default) - the last line may not hold a single word, so one more word is moved onto it
	/// <returns>
	/// Binds the options from the configuration section and validates them before the host
	/// is built.
	/// </returns>

// 1 (or 0) - a line of one word is allowed, and the fill runs to the width instead
	/// <returns>
	/// Binds the options from the configuration section and validates them before the host is
	/// built.
	/// </returns>
```

A leftover word lands on the final line, so raising this minimum can lengthen that line.
Overflow can pull that word back onto the previous line instead;
see [`stanza_doc_max_overflow_columns`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_max_overflow_columns).
