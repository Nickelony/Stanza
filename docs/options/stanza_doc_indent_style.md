# stanza_doc_indent_style

Controls where the text of an element stands among the levels of [`stanza_doc_indent_size`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_indent_size).
Default: `nested`.
Read by the documentation layout rules, such as [`ZA0201`](https://github.com/Nickelony/Stanza/wiki/ZA0201-Lay-out-a-documentation-comment),
and by the stanza-markup [reprint](https://github.com/Nickelony/Stanza/wiki/Terms).

| Value         | Effect                                                                 |
| ------------- | ---------------------------------------------------------------------- |
| `nested`      | one level inside the tags that wrap it                                 |
| `hanging`     | at the level of those tags                                             |
| anything else | one level inside the tags, as `nested`, and the tool reports a warning |

The example shows both styles with `stanza_doc_indent_size = 2` and `stanza_doc_expand` at `sections`,
so the summary takes its block form:

```csharp
// nested - the text stands one level inside the tags that wrap it
/// <summary>
///   Binds the options of the sample.
/// </summary>
/// <remarks>
///   <para>
///     The section is read once at startup.
///   </para>
/// </remarks>

// hanging - the text stands at the level of those tags
/// <summary>
/// Binds the options of the sample.
/// </summary>
/// <remarks>
///   <para>
///   The section is read once at startup.
///   </para>
/// </remarks>
```

The style decides nothing while [`stanza_doc_indent_size`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_indent_size) is `0`.
