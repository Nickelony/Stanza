# stanza_doc_max_overflow_columns

Controls how many columns a documentation line may stand over its width at a low cost,
which [`stanza_doc_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_max_line_length) sets.
Default: `4`.
Read by the documentation layout rules, such as [`ZA0201`](https://github.com/Nickelony/Stanza/wiki/ZA0201-Lay-out-a-documentation-comment),
and by the stanza-markup [reprint](https://github.com/Nickelony/Stanza/wiki/Terms).

| Value                                           | Effect                                                                  |
| ----------------------------------------------- | ----------------------------------------------------------------------- |
| `0`                                             | a line stands over the width only when every break costs more           |
| a number from 1 to 1000                         | a line may stand over it by that many columns at the low cost           |
| below zero, above 1000, absent, or not a number | the default; the tool also reports a warning for a value it cannot read |

The example uses `stanza_max_line_length = 32` and `stanza_doc_min_tail_words = 3`,
with one tab of indentation,
which leaves 24 content columns as [`stanza_doc_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_max_line_length) measures them:

```csharp
// 4 (default) - the second line stands 2 columns over the width, which keeps the clause whole
	/// <returns>
	/// Binds the options from
	/// the configuration section,
	/// which the host reads
	/// at startup.
	/// </returns>

// 0 - no line may stand over the width, so the clause is split where it fits
	/// <returns>
	/// Binds the options
	/// from the configuration
	/// section, which the host
	/// reads at startup.
	/// </returns>
```

- The limit applies to each line on its own.
- The wrapper spends overflow only to avoid a [penalty](https://github.com/Nickelony/Stanza/wiki/Terms):
  a line shorter than [`stanza_doc_min_line_words`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_min_line_words),
  a tail or fragment shorter than [`stanza_doc_min_tail_words`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_min_tail_words),
  or a [weak tail word](https://github.com/Nickelony/Stanza/wiki/stanza_doc_weak_tail_words).
- An unbreakable token may exceed the width whatever this key says.
