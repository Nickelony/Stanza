# stanza_doc_indent_size

Controls the columns one level of nesting takes inside a documentation comment,
or `tab` for one tab per level.
Default: `0`, which writes every line flush after its `///` marker.
Read by the documentation layout rules, such as [`ZA0201`](https://github.com/Nickelony/Stanza/wiki/ZA0201-Lay-out-a-documentation-comment),
and by the stanza-markup [reprint](https://github.com/Nickelony/Stanza/wiki/Terms).

| Value                                         | Effect                                                                                           |
| --------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| `0`                                           | every line is flush after the marker                                                             |
| a number from 1 to 64                         | each level moves that many columns in                                                            |
| `tab`                                         | each level moves one tab in                                                                      |
| below zero, above 64, absent, or not a number | every line is flush after the marker; the tool also reports a warning for a value it cannot read |

The example assumes [`stanza_doc_expand`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_expand) is `all`,
so each tag stands on its own line at either value of `stanza_doc_indent_size`:

```csharp
// 0 (default) - every line is flush after its marker
/// <summary>
/// Binds the options of the sample.
/// </summary>
/// <remarks>
/// <para>
/// The section is read once at startup.
/// </para>
/// </remarks>

// 2 - a level per element, and one more for the text inside it
/// <summary>
///   Binds the options of the sample.
/// </summary>
/// <remarks>
///   <para>
///     The section is read once at startup.
///   </para>
/// </remarks>
```

With `tab` and `stanza_max_line_length = 46`, the summary is too long for one line, so it breaks,
and its text sits one tab in:

```csharp
/// <summary>
/// 	Binds the options of the sample.
/// </summary>
```

- `tab` writes a tab where a number writes blank space,
  so a file indented with tabs keeps them inside the comment.
- `tab` measures a level as one tab at the tab width that [`stanza_tab_width`](https://github.com/Nickelony/Stanza/wiki/stanza_tab_width) resolves.
- [`stanza_doc_indent_style`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_indent_style) sets where the text of an element stands among those levels.
