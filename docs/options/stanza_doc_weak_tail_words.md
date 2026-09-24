# stanza_doc_weak_tail_words

Controls the words a documentation line should not end on.
Default: `none`.
Read by the documentation layout rules, such as [`ZA0201`](https://github.com/Nickelony/Stanza/wiki/ZA0201-Lay-out-a-documentation-comment),
and by the stanza-markup [reprint](https://github.com/Nickelony/Stanza/wiki/Terms).

| Value                                     | Effect                                              |
| ----------------------------------------- | --------------------------------------------------- |
| a comma separated list                    | a line that ends on a listed word carries a penalty |
| `none`, empty, absent, or only separators | nothing: the cost is off                            |

With `stanza_max_line_length = 52` and one tab of indentation, the content width is 44 columns.
The sentence below breaks two ways:

```csharp
// none, or absent - the first line ends on "the"
	/// Binds the options of the sample and read the
	/// value from the section.

// `the` listed - no line ends on `the`, so the break moves before it
	/// Binds the options of the sample and read
	/// the value from the section.
```

- A word in the text matches a listed word when both are equal after case and surrounding punctuation are removed,
  so `The,` matches `the` and `theme` does not.
- A listed word is a cost, not a ban: if no other break fits, the word stays at the line end,
  and [`stanza_doc_max_overflow_columns`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_max_overflow_columns) decides how far the line may stand over the width to move it.
- The key has no built-in word list, so add the function words that read poorly at a line end to the `.editorconfig` of the files.
