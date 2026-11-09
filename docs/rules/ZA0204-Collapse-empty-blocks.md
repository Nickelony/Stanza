# ZA0204 - Collapse empty blocks

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
- A brace that follows its header on one line joins that line, and so does a block statement of its own,
  so the fix also writes the blank lines beside the unit that
  [`ZA0001`](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-multi-line-constructs) asks for, where `ZA0001` is on.
- Inside a list, the fix writes the blank lines that the list's multi-line rule asks for,
  around an accessor, a switch section or arm, an initializer member, a collection element, an argument or a declarator.
- A body that holds a statement, a comment or a directive is left unchanged.
- The rule reports nothing when `csharp_preserve_single_line_blocks` is `false`,
  since the formatter would expand the pair again ([Working with formatters](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#ide-style-rules)).
