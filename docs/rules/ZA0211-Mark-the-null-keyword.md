# ZA0211 - Mark the null keyword

**Suggestion, off by default.**
Writes the `null` in a comment's prose as a `<see langword="null"/>` element,
which marks it as a language keyword.

```csharp
// Before
/// <summary>Returns null when the section is missing.</summary>

// After
/// <summary>Returns <see langword="null"/> when the section is missing.</summary>
```

- Only prose takes part. A keyword inside an inline element, a `<code>` block, a `<list>` or a `<table>` is left alone.
- A `<code>`, `<list>` or `<table>` at the top level of the comment leaves the whole comment alone.
- [Limitations](https://github.com/Nickelony/Stanza/wiki/Limitations) lists the documentation comments this rule leaves alone.
- A word that only mentions the keyword, such as `nullify`, is not the keyword, and neither is a hyphenated word such as `null-terminated`.
  Punctuation may touch it: `(null)` and `null,` are reported, and the fix replaces the keyword alone.
- A keyword inside a backtick code span, or in a word holding `://`, `/` or `\` such as a link or a path, is left alone.
  A span or a link that runs across a line or an entity such as `&amp;` counts as a whole.
- A keyword between two quotes of one kind, such as `"null"` or `'null'`, names a string and is left alone.
- The `<see>` element takes the namespace prefix of the element that holds the keyword, as in `<x:see langword="null"/>` inside `<x:summary>`.
- The `<see>` element takes the [slash shape](https://github.com/Nickelony/Stanza/wiki/Terms) that [`ZA0207`](https://github.com/Nickelony/Stanza/wiki/ZA0207-Separate-the-slash-of-a-self-closing-tag) describes.
