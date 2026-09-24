# ZA0036 - Collapse empty blocks

**Suggestion, on by default.**
Joins a pair of braces that sit on separate lines,
with nothing but whitespace between them, into `{ }`.

```csharp
// Before
public void Reset()
{
}

private static readonly List<string> Empty = new()
{
};

// After
public void Reset()
{ }

private static readonly List<string> Empty = new()
{ };
```

- Blocks, type bodies and initializers are checked; a namespace body and a `switch` are not.
- Only the whitespace between the two braces changes. The text before the opening brace stays as written.
- A body that holds a statement, a comment or a directive is left unchanged.
- The rule reports nothing when `csharp_preserve_single_line_blocks` is `false`,
  since the formatter would expand the pair again ([Working with formatters](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#ide-style-rules)).
