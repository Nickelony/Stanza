# ZA0212 - Order the elements of a documentation comment

**Suggestion, off by default.**
Writes the elements of a comment in the canonical order:
`summary`, `typeparam`, `param`, `returns`, `value`, `exception`, `remarks`, `example`, `seealso`.

```csharp
// Before
/// <remarks>Reads the value twice.</remarks>
/// <summary>Binds the value.</summary>
/// <param name="value">The value to bind.</param>
internal int Bind(int value)

// After
/// <summary>Binds the value.</summary>
/// <param name="value">The value to bind.</param>
/// <remarks>Reads the value twice.</remarks>
internal int Bind(int value)
```

- The `<param>` and `<typeparam>` elements follow the declaration's parameters in declaration order.
  An element for a name the signature lacks comes after them.
- Elements the order does not name, such as `<inheritdoc/>` and `<include/>`, split the run.
  Elements are reordered within each run but never moved across one.
- A run is left unchanged when an element shares a line with another element or with prose,
  or when a line of prose stands between two of them.
  A blank line between two elements stays where it stands, and the elements move past it.
- A comment that trails code on its line is left alone.
- A comment gets one report, at its first element out of place,
  and the fix orders every run of the comment.
- A `<member>` that [`stanza_markup_doc_reprint`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_doc_reprint) rewrites is ordered only when the file [enables](https://github.com/Nickelony/Stanza/wiki/Terms) this rule.
  The tool reads no signature, so its `<param>` elements keep their source order among themselves.
