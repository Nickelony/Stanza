# stanza_markup_doc_reprint

Controls whether the `<member>` bodies in documentation XML are rewritten.
Default: `off`.
Read by stanza-markup.

| Value         | Effect                                                                                                                                                                              |
| ------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `off`         | every member body is left as it is                                                                                                                                                  |
| `members`     | the body of every member is rewritten, except the [bodies a reprint leaves as written](https://github.com/Nickelony/Stanza/wiki/Markup-behavior#bodies-a-reprint-leaves-as-written) |
| anything else | `off`, and the tool reports a warning                                                                                                                                               |

With `stanza_max_line_length = 12`, `members` rewrites this body:

```xml
<!-- Before -->
<member name="X">
  <summary>alpha beta gamma</summary>
</member>

<!-- After -->
<member name="X">
  <summary>
  alpha beta
  gamma
  </summary>
</member>
```

- `members` lays out each body with the `stanza_doc_*` keys and the indent keys, [`stanza_tab_width`](https://github.com/Nickelony/Stanza/wiki/stanza_tab_width), `tab_width` and `indent_size`.
- [`ZA0106`](https://github.com/Nickelony/Stanza/wiki/ZA0106-Reprint-the-body-of-a-documentation-member) is the diagnostic `check --format msbuild` reports for each rewritten body.
- A rewritten body starts each list item on its own line, and writes a blank line between two paragraphs as `<para>` elements,
  whether or not [`ZA0204`](https://github.com/Nickelony/Stanza/wiki/ZA0204-Lay-out-the-items-of-a-documentation-list) and [`ZA0202`](https://github.com/Nickelony/Stanza/wiki/ZA0202-Mark-documentation-paragraphs) are [enabled](https://github.com/Nickelony/Stanza/wiki/Terms).
- The [reprint](https://github.com/Nickelony/Stanza/wiki/Terms) changes only the member bodies, and the blank-line rules never act inside a member while it is on,
  so a body it leaves as written stays whole; they still act between members.
- The `--reprint` option rewrites member bodies whatever this key says,
  and a [`stanza_doc_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_max_line_length) of `0` turns the reprint off whatever either says.
- A file the reprint rewrites belongs in `.csharpierignore`, as [Working with formatters](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#documentation-xml) says.
- [Markup behavior](https://github.com/Nickelony/Stanza/wiki/Markup-behavior#what-it-changes) says which clean-ups a reprint applies, and what the tool leaves alone.
