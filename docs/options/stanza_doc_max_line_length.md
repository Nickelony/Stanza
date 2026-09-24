# stanza_doc_max_line_length

Controls the width a documentation comment is wrapped to.
Default: the width [`stanza_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_max_line_length) resolves to.
Read by the documentation layout rules, such as [`ZA0201`](https://github.com/Nickelony/Stanza/wiki/ZA0201-Lay-out-a-documentation-comment),
and by the stanza-markup [reprint](https://github.com/Nickelony/Stanza/wiki/Terms).

| Value                                           | Effect                                                                  |
| ----------------------------------------------- | ----------------------------------------------------------------------- |
| `0`                                             | the documentation layout is off                                         |
| a number from 1 to 1000                         | at most that many columns                                               |
| below zero, above 1000, absent, or not a number | the default; the tool also reports a warning for a value it cannot read |

When [`stanza_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_max_line_length) is `0`,
the default is `0` too, so the documentation layout is off.

This key sets the documentation width alone, so a file may keep its code wide and its comments short:

```ini
# A member may run to 150 columns, a comment only to 120
stanza_max_line_length = 150
stanza_doc_max_line_length = 120
```

CSharpier does not reflow a comment,
so a documentation width below its print width does not fight the formatter.

- The indentation of the comment, its `/// ` marker
  and the indent that [`stanza_doc_indent_size`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_indent_size) gives an element's text all count against the width,
  so a content line holds fewer columns than the width.
- The lines of a `<member>` body written by [`stanza_markup_doc_reprint`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_doc_reprint) are written to this width as well.
