# ZA0206 - Collapse the whitespace inside a tag

**Suggestion, off by default.**
Collapses two or more spaces or tabs between the tokens of a tag into a single space.

```csharp
// Before
/// <summary>Reads <see  langword="null"/> when it is set</summary>

// After
/// <summary>Reads <see langword="null"/> when it is set</summary>
```

- Spaces before a tag's closing bracket collapse too, so `<summary  >` becomes `<summary >`.
- The [gap](https://github.com/Nickelony/Stanza/wiki/Terms) before a self-closing tag's slash is left to the slash rules,
  [`ZA0207`](https://github.com/Nickelony/Stanza/wiki/ZA0207-Separate-the-slash-of-a-self-closing-tag) and [`ZA0208`](https://github.com/Nickelony/Stanza/wiki/ZA0208-Keep-the-slash-of-a-self-closing-tag-tight).
- A gap that crosses a line break is left alone,
  because [`ZA0201`](https://github.com/Nickelony/Stanza/wiki/ZA0201-Lay-out-a-documentation-comment) owns the layout of a tag written across lines.
- The inside of an attribute value is left as it is.
- A tag inside a `<list>`, a `<table>`, a `<code>` sample or content marked `xml:space="preserve"` is left as written.
- A `<c>` element is inline text, so a tag inside it is judged like a tag in prose.
