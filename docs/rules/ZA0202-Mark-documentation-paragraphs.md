# ZA0202 - Mark documentation paragraphs

**Suggestion, off by default.**
Writes each paragraph of an element as its own `<para>` element
when a blank `///` line marks the break between two paragraphs.

```csharp
// Before
/// <remarks>
/// Binds the options from the configuration section.
///
/// Validates them and reports every problem it found.
/// </remarks>

// After
/// <remarks>
/// <para>
/// Binds the options from the configuration section.
/// </para>
/// <para>
/// Validates them and reports every problem it found.
/// </para>
/// </remarks>
```

- A blank line between paragraphs is this rule's, one after a `</para>` included, so [`ZA0201`](https://github.com/Nickelony/Stanza/wiki/ZA0201-Lay-out-a-documentation-comment) leaves its comment alone;
  one with no text before it, such as right after a start tag, belongs to [`ZA0203`](https://github.com/Nickelony/Stanza/wiki/ZA0203-Remove-documentation-padding).
  So does one with no text after it, such as right before an end tag.
- A phrase element such as `<c>` is never split into paragraphs.
- The fix wraps the comment once, so enable [`ZA0201`](https://github.com/Nickelony/Stanza/wiki/ZA0201-Lay-out-a-documentation-comment) beside this rule:
  it wraps the comment again when a later change widens a line.
- Lists and tables are laid out as [`ZA0204`](https://github.com/Nickelony/Stanza/wiki/ZA0204-Lay-out-the-items-of-a-documentation-list) describes.
- The fix re-wraps the text it writes at [`stanza_doc_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_max_line_length).
- The rule reports nothing when [`stanza_doc_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_max_line_length) is `0`, as for [`ZA0201`](https://github.com/Nickelony/Stanza/wiki/ZA0201-Lay-out-a-documentation-comment).
- [Limitations](https://github.com/Nickelony/Stanza/wiki/Limitations) lists the documentation comments this rule leaves alone.
