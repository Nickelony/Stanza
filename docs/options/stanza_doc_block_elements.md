# stanza_doc_block_elements

Controls which documentation elements take a block form even when they fit on one line.
Default: `minimal`.
Read by the documentation layout rules, such as [`ZA0901`](https://github.com/Nickelony/Stanza/wiki/ZA0901-Lay-out-a-documentation-comment),
and by the stanza-markup [reprint](https://github.com/Nickelony/Stanza/wiki/Terms).

| Value         | Effect                                                                                                             |
| ------------- | ------------------------------------------------------------------------------------------------------------------ |
| `minimal`     | `remarks`, `example` and `para` take a block form; the other elements stay on one line while they fit              |
| `sections`    | as `minimal`, and `summary`, `returns`, `value`, `param`, `typeparam`, `exception`, `seealso` and `permission` too |
| `all`         | every element with a block form, including list items                                                              |
| anything else | as `minimal`, and the tool reports a warning                                                                       |

An element too long for one line takes a block form whatever the value.
The same comment under `minimal` and under `sections`:

```csharp
// minimal (default) - summary and returns fit on one line, so neither takes a block form
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

- An element that holds a `<code>` block, an `<include>` or an `<inheritdoc>` is left as written, so this key changes nothing in it.
- The layout of list items is in [`ZA0904`](https://github.com/Nickelony/Stanza/wiki/ZA0904-Lay-out-the-items-of-a-documentation-list).
- Inline phrase elements have no block form, so this key leaves them unchanged.
