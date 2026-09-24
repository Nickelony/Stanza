# ZA0204 - Lay out the items of a documentation list

**Suggestion, off by default.**
Lays out the items of a `<list>` or `<table>` element,
so each item starts its own line between the list's opening and closing tags.

```csharp
// Before
/// <list type="bullet">
/// <item><description>Reads the section, binds every option and validates the whole of it at startup.</description></item>
/// </list>

// After
/// <list type="bullet">
/// <item>
/// <description>
/// Reads the section, binds every option and validates the whole of it at startup.
/// </description>
/// </item>
/// </list>
```

The example keeps the default [`stanza_doc_indent_size`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_indent_size),
so its lines are not indented.

- An item whose tags and the content between them fit on one line stays on one line
  unless [`stanza_doc_expand`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_expand) asks for a block or the item holds a list.
- An item that does not fit on one line puts each of its tags on a line of its own,
  one indent level further in than the tag that holds it,
  as [`stanza_doc_indent_size`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_indent_size) sets it.
- The text an item holds wraps at [`stanza_doc_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_max_line_length);
  a width of `0` leaves no list to report.
- A blank line between two items is removed, so the items sit on consecutive lines.
- A comment with several lists gets one report for each list, at its opening tag,
  and each fix lays out that list alone.
- The rule leaves a list unchanged when the list holds a `<code>` block, a `<para>` element,
  an empty item such as `<item/>`, or text between two items.
- A list that shares a line with a sentence is left alone,
  and so is one with a blank line in an item too long for one line.
- In a `/** */` comment, the list's lines take the star column of the comment's other lines,
  or keep their own when the comment has no other line.
- The compiler drops the stars from the XML only when every line has one at the same column,
  so a `/** */` comment whose lines share no star column, a starless one included, is left alone.
- A comment that [`ZA0201`](https://github.com/Nickelony/Stanza/wiki/ZA0201-Lay-out-a-documentation-comment) or [`ZA0202`](https://github.com/Nickelony/Stanza/wiki/ZA0202-Mark-documentation-paragraphs) lays out gets its lists from that rule,
  so this rule reports only the lists of other comments.
- A pragma that silences that rule where it reports, at the comment's member or its paragraph break, hands the lists back.
