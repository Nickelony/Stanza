# ZA0901 - Lay out a documentation comment

**Suggestion, off by default.**
Wraps the text of a documentation comment at the [configured width](https://github.com/Nickelony/Stanza/wiki/stanza_doc_max_line_length),
and writes an element that does not fit on one line as a block.

```csharp
// Before
/// <summary>Binds the options from the configuration section, validates them and lets the host fail at startup rather than at the first request.</summary>
/// <returns>The bound options.</returns>

// After
/// <summary>
/// Binds the options from the configuration section, validates them and lets the host fail at
/// startup rather than at the first request.
/// </summary>
/// <returns>The bound options.</returns>
```

- The rule reports nothing when [`stanza_doc_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_max_line_length) is `0`,
  which turns the documentation layout off.
- An element whose whole form - both tags included - fits on one line stays on one line,
  except the elements [`stanza_doc_block_elements`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_block_elements) says take a block form.
- A blank `///` line inside an element is not this rule's:
  [`ZA0902`](https://github.com/Nickelony/Stanza/wiki/ZA0902-Mark-documentation-paragraphs) owns a line that separates paragraphs,
  and [`ZA0903`](https://github.com/Nickelony/Stanza/wiki/ZA0903-Remove-documentation-padding) owns one that separates nothing.
  A comment that holds such a line is left as written.
- An inline element such as `<c>` is never split across lines, even when its line exceeds the width, because it has no block form.
  The spaces between its words still collapse to one.
- A backtick code span is one word of the fill: it moves whole and keeps the spaces inside it, and a line break inside it becomes one space.
- Only a space, a tab and a line break separate words, so a character such as a form feed stays in its word.
- A word made of closing marks, such as `?` or `,`, joins the word before it, so a space before it goes and no line starts with it.
  Write a mark the prose names, such as the `;` of a statement, in a backtick code span to keep it apart.
- A `<list>` or a `<table>` is laid out with the comment, as [`ZA0904`](https://github.com/Nickelony/Stanza/wiki/ZA0904-Lay-out-the-items-of-a-documentation-list) describes.
  Prose on both sides of a list gains a `<para>` pair around each run.
- [`ZA0902`](https://github.com/Nickelony/Stanza/wiki/ZA0902-Mark-documentation-paragraphs) wraps a comment only as it writes the paragraphs,
  so enable both rules together: this one wraps the comment again whenever a later change widens a line.
- A top-level element that holds a `<code>` block, an `<include>` or an `<inheritdoc>` stays exactly as written,
  and the elements beside it are laid out.
- A comment that holds an element marked `xml:space="preserve"`, and one that shares its first or last line with code, is left alone whole,
  as [Limitations](https://github.com/Nickelony/Stanza/wiki/Limitations) lists with the others.
- With this rule [enabled](https://github.com/Nickelony/Stanza/wiki/Terms), a `/** */` comment opens its markers on their own lines,
  even when its text fits on one line.
  Each text line it writes starts with ` * `, whether or not the comment had stars.
- A block whose lines carry stars unevenly, some without one or at another column, is left alone, because the compiler reads those stars as text.
