# ZA0912 - Mark the language keywords

**Suggestion, off by default.**
Writes the `null` in a comment's prose as a `<see langword="null"/>` element,
which marks it as a language keyword.
[`stanza_doc_langwords`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_langwords) replaces that default with the `null`, `true`, `false` and `default` it lists.

```csharp
// Before
/// <summary>Returns null when the section is missing.</summary>

// After
/// <summary>Returns <see langword="null"/> when the section is missing.</summary>
```

- Only prose takes part. A keyword inside an inline element, a `<code>` block, a `<list>` or a `<table>` is left alone.
- A top-level element that holds a `<code>` block, an `<include>` or an `<inheritdoc>` is left alone, and the elements beside it are marked.
- [Limitations](https://github.com/Nickelony/Stanza/wiki/Limitations) lists the documentation comments this rule leaves alone.
- A word that only mentions the keyword, such as `nullify`, is not the keyword, and neither is a hyphenated word such as `null-terminated`.
  Punctuation may touch it: `(null)` and `null,` are reported, and the fix replaces the keyword alone.
- A keyword inside a backtick code span, or in a word holding `://`, `/` or `\` such as a link or a path, is left alone.
  A span or a link that runs across a line or an entity such as `&amp;` counts as a whole.
- A keyword between two quotes of one kind, such as `"null"` or `'null'`, names a string and is left alone.
- The `<see>` element takes the namespace prefix of the element that holds the keyword, as in `<x:see langword="null"/>` inside `<x:summary>`.
- The `<see>` element takes the [slash shape](https://github.com/Nickelony/Stanza/wiki/Terms) that [`ZA0907`](https://github.com/Nickelony/Stanza/wiki/ZA0907-Separate-the-slash-of-a-self-closing-tag) describes.

A lowercase `null` is the keyword wherever it stands in prose.
`true` and `false` are ordinary English words, so a listed one counts only where its phrase proves it:

```csharp
// Before
/// <returns>True if the section exists; otherwise, false.</returns>

// After - with true and false listed
/// <returns><see langword="true"/> if the section exists; otherwise, <see langword="false"/>.</returns>
```

- A `true` or `false` counts when it starts a sentence or the text of an element and `if` or `when` follows, as in `True if bound`.
- It also counts after `returns` or `otherwise`, as in `Returns true if` and `otherwise, false`, and before `otherwise`, as in `false otherwise`.
- After `returns` or `otherwise`, the keyword must end on a mark, the end of the text, or `if`, `when` or `or`.
  A noun after it, as in `Returns true positives` and `Otherwise false alarms`, leaves the word alone.
- Punctuation between the words does not break the match, so `otherwise false`, `otherwise, false` and `otherwise: false` all count.
  Punctuation that touches the keyword stays, as in `false.` and `(true)`.
- The match ignores case, and the element always writes the keyword in lowercase, so `True if` becomes `<see langword="true"/> if`.
- `This is true if` and `a true value` are left alone, as the word stands mid-sentence.
- A bare `default` is never marked, as it is an ordinary word too.
- A `<c>true</c>`, `<c>false</c>` or `<c>default</c>` becomes the element, as the author has already marked the word as code.
  A `<c>null</c>` stays as written, and so does a `<c>` that holds more than the word or carries an attribute.
