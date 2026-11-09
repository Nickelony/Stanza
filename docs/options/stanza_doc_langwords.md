# stanza_doc_langwords

Controls which language keywords [`ZA0912`](https://github.com/Nickelony/Stanza/wiki/ZA0912-Mark-the-language-keywords) marks in documentation prose.
Default: `null`.
Read by [`ZA0912`](https://github.com/Nickelony/Stanza/wiki/ZA0912-Mark-the-language-keywords),
[`ZA0910`](https://github.com/Nickelony/Stanza/wiki/ZA0910-Capitalize-the-documentation-text)
and the stanza-markup [reprint](https://github.com/Nickelony/Stanza/wiki/Terms).

| Value                                            | Effect                                                                  |
| ------------------------------------------------ | ----------------------------------------------------------------------- |
| a comma separated list of keywords               | the rule marks exactly the listed `null`, `true`, `false` and `default` |
| empty, absent, or a list that names none of them | the default                                                             |

The same sentence settles two ways:

```csharp
// Before
/// <returns>Returns true if the section exists, or null.</returns>

// After - at the default
/// <returns>Returns true if the section exists, or <see langword="null"/>.</returns>

// After - with null and true listed
/// <returns>Returns <see langword="true"/> if the section exists, or <see langword="null"/>.</returns>
```

- The list replaces the default, so `true, false` leaves `null` unmarked; list `null, true, false` to mark each.
- A word is listed in lowercase or any case, and a word the list cannot name is skipped, which stanza-markup reports as a warning.
- `default` is marked only where the author wrote `<c>default</c>`, as a bare `default` is never matched.
- The page of [`ZA0912`](https://github.com/Nickelony/Stanza/wiki/ZA0912-Mark-the-language-keywords) says which phrases prove a `true` or `false`.
- [`ZA0910`](https://github.com/Nickelony/Stanza/wiki/ZA0910-Capitalize-the-documentation-text) leaves a listed keyword that opens a text in lowercase,
  so `ZA0912` still finds it.
