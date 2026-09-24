# stanza_doc_min_tail_words

Controls the fewest words a [tail](https://github.com/Nickelony/Stanza/wiki/Terms) or a [fragment](https://github.com/Nickelony/Stanza/wiki/Terms) may hold before the wrapper gives it a [penalty](https://github.com/Nickelony/Stanza/wiki/Terms).
Default: `2`.
Read by the documentation layout rules, such as [`ZA0201`](https://github.com/Nickelony/Stanza/wiki/ZA0201-Lay-out-a-documentation-comment),
and by the stanza-markup [reprint](https://github.com/Nickelony/Stanza/wiki/Terms).

| Value                                           | Effect                                                                  |
| ----------------------------------------------- | ----------------------------------------------------------------------- |
| `0` or `1`                                      | neither a tail nor a fragment carries a penalty                         |
| `2`                                             | a one-word tail or fragment carries a penalty                           |
| a number from 3 to 1000                         | a tail or fragment shorter than that many words carries a penalty       |
| below zero, above 1000, absent, or not a number | the default; the tool also reports a warning for a value it cannot read |

With `stanza_max_line_length = 52` and one tab of indentation (44 content columns),
the default moves a one-word tail to the next line:

```csharp
// 1 (or 0) - a one-word tail is allowed
	/// Binds the options of the sample, validating
	/// the host fails at startup.

// 2 (default) - a one-word tail carries a penalty, so it moves to the next line
	/// Binds the options of the sample,
	/// validating the host fails at startup.
```

The wrapper can spend overflow to avoid a short tail;
see [`stanza_doc_max_overflow_columns`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_max_overflow_columns).
