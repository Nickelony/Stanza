# ZA0203 - Remove documentation padding

**Suggestion, off by default.**
Removes the blank `///` line that separates nothing.

```csharp
// Before
/// <summary>
/// Binds the options from the configuration section.
/// </summary>
///
/// <returns>
/// The bound options.
/// </returns>

// After
/// <summary>
/// Binds the options from the configuration section.
/// </summary>
/// <returns>
/// The bound options.
/// </returns>
```

The blank line after a start tag goes too:

```csharp
// Before
/// <summary>
///
/// Binds the options from the configuration section.
/// </summary>

// After
/// <summary>
/// Binds the options from the configuration section.
/// </summary>
```

- A blank line stays only where prose stands on both sides of it.
  A blank line between two elements, after a start tag, or beside a block element at the top level separates nothing.
- An empty first line, above everything the comment holds, is left to
  [`ZA0201`](https://github.com/Nickelony/Stanza/wiki/ZA0201-Lay-out-a-documentation-comment), which writes the comment from its first line.
- A comment that holds a `<code>`, `<list>` or `<table>` element keeps every blank line.
- An inline element such as `<c>` or `<see>` always counts as prose,
  and inside an element every nested element does.
- A blank line before an end tag has no prose after it,
  so [`ZA0202`](https://github.com/Nickelony/Stanza/wiki/ZA0202-Mark-documentation-paragraphs) reads no paragraph break there and this rule removes the line.
- When the comment holds a paragraph break and [`ZA0202`](https://github.com/Nickelony/Stanza/wiki/ZA0202-Mark-documentation-paragraphs) is not [off](https://github.com/Nickelony/Stanza/wiki/Terms) at that break,
  this rule reports nothing for that comment;
  when `ZA0202` is off there, this rule removes the padding of the comment.
- When [`stanza_markup_doc_reprint`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_doc_reprint) writes a `<member>` and this rule is enabled for the file,
  a blank line that separates nothing between the elements of its body goes too.
- [Limitations](https://github.com/Nickelony/Stanza/wiki/Limitations) lists the documentation comments this rule leaves alone.
