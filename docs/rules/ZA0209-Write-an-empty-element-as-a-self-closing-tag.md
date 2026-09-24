# ZA0209 - Write an empty element as a self-closing tag

**Suggestion, off by default.**
Writes an element of a documentation comment that holds nothing, or only whitespace,
as one tag that opens and closes at once.

```csharp
// Before
/// <summary>Reads the <c></c> options and a break <br></br> after them.</summary>
/// <remarks></remarks>

// After
/// <summary>Reads the <c/> options and a break <br/> after them.</summary>
/// <remarks/>
```

- It leaves alone an element that holds text, a child element or a comment,
  and an element whose start tag wraps across lines.
- It leaves alone a `<code>` sample or an element marked `xml:space="preserve"` that holds whitespace, because the whitespace is its content.
- [Limitations](https://github.com/Nickelony/Stanza/wiki/Limitations) lists the documentation comments this rule leaves alone,
  such as one whose markup is broken.
- [`ZA0201`](https://github.com/Nickelony/Stanza/wiki/ZA0201-Lay-out-a-documentation-comment) lays the comment out with the pair as written,
  so once this fix shortens a line, the next [pass](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#fixes-that-open-work) wraps it again.
- Inside a `<list>`, a `<table>`, a `<code>` sample or content marked `xml:space="preserve"`, an empty element is left as written.
- The tag takes the [slash shape](https://github.com/Nickelony/Stanza/wiki/Terms) that [`ZA0207`](https://github.com/Nickelony/Stanza/wiki/ZA0207-Separate-the-slash-of-a-self-closing-tag) describes.
