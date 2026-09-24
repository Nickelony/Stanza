# ZA0038 - Remove redundant braces

**Suggestion, off by default.**
Removes braces only when every branch of a chain is single-line, the opposite of `ZA0037`.

```csharp
// Before
if (root is null)
{
	return;
}
else
{
	Skip();
}

// After
if (root is null)
	return;
else
	Skip();
```

- Loop, `using`, `lock` and `fixed` bodies are judged the same way, one body at a time.
- A `try`, `catch` or `finally` body is not judged, as for [`ZA0037`](https://github.com/Nickelony/Stanza/wiki/ZA0037-Use-braces-consistently).
- Braces stay when removing them would let an `else` attach to an inner `if`,
  but a braced loop or block around that `if` closes it first, so the braces outside the loop can come off.
- A chain keeps its braces when its `else` holds only an `if`, which would read as the next link of the chain without them.
- The rule reports nothing when `csharp_prefer_braces` is `true`.
- Braces come off only from a block whose `{` and `}` each stand alone on a line,
  around exactly one single-line statement indented deeper than its braces.
- A block keeps its braces when its one statement is a labeled statement, a local declaration,
  a local function or an empty statement, or when it contains a directive.
