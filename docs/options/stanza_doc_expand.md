# stanza_doc_expand

Controls which documentation elements take a block form even when they fit on one line.
Default: `none`.
Read by the documentation layout rules, such as [`ZA0201`](https://github.com/Nickelony/Stanza/wiki/ZA0201-Lay-out-a-documentation-comment),
and by the stanza-markup [reprint](https://github.com/Nickelony/Stanza/wiki/Terms).

| Value         | Effect                                                                                                          |
| ------------- | --------------------------------------------------------------------------------------------------------------- |
| `none`        | `remarks`, `example` and `para` take a block form; the other elements stay on one line while they fit           |
| `sections`    | as `none`, and `summary`, `returns`, `value`, `param`, `typeparam`, `exception`, `seealso` and `permission` too |
| `all`         | every element with a block form, including list items                                                           |
| anything else | as `none`, and the tool reports a warning                                                                       |

An element too long for one line takes a block form whatever the value.
The same comment under `none` and under `sections`:

```csharp
// none (default) - summary and returns fit on one line, so neither expands
/// <summary>Binds the options of the sample.</summary>
/// <returns>The bound options of <c>the sample</c>.</returns>

// sections - summary and returns take their block form
/// <summary>
/// Binds the options of the sample.
/// </summary>
/// <returns>
/// The bound options of <c>the sample</c>.
/// </returns>
```

- A comment that holds a `<code>` block is left as written, so this key expands nothing in it.
- The layout of list items is in [`ZA0204`](https://github.com/Nickelony/Stanza/wiki/ZA0204-Lay-out-the-items-of-a-documentation-list).
- Inline phrase elements have no block form, so this key leaves them unchanged.
