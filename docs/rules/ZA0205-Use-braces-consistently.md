# ZA0205 - Use braces consistently

**Suggestion, on by default.**
Adds braces to every branch of an `if`/`else if`/`else` chain when any one branch is multi-line,
and to a multi-line loop, `using`, `lock` or `fixed` body.

```csharp
// Before
if (root is null)
	return;
else if (node.HasChildren)
{
	Visit(node);
	Log(node);
}
else
	Skip();

// After
if (root is null)
{
	return;
}
else if (node.HasChildren)
{
	Visit(node);
	Log(node);
}
else
{
	Skip();
}
```

- A branch is multi-line when its header spans lines, its statement spans lines,
  its block holds two or more statements,
  or a comment or directive stands on a line of its block that holds no statement.
- A block comment that starts on the statement's line and ends on a later one counts, as its last line holds no statement.
- A comment wholly on the statement's own line does not make a branch multi-line, and neither does one above an unbraced statement.
- A chain or body that only an empty block makes multi-line waits for [`ZA0204`](https://github.com/Nickelony/Stanza/wiki/ZA0204-Collapse-empty-blocks) to join that block, where it is on,
  so the braces do not depend on which rule fixes first.
- A loop, `using`, `lock` or `fixed` body is judged on its own, without chain logic.
- A `using`, `lock` or `fixed` body that is a statement of the same kind stays unbraced, as `IDE0011` leaves it,
  so a stack of them keeps its shape and only the innermost body is braced.
- A `try`, `catch` or `finally` body is not judged; [`ZA0203`](https://github.com/Nickelony/Stanza/wiki/ZA0203-Remove-brace-padding) and [`ZA0204`](https://github.com/Nickelony/Stanza/wiki/ZA0204-Collapse-empty-blocks) still check its padding and its emptiness.
- A body that shares its line with its header is left to `csharp_prefer_braces`.
- A body that ends in an `if` is left alone when an `else` in another view follows it, as the braces would close that `if`.
- The rule reports nothing when `csharp_prefer_braces` is `false`;
  see the [IDE style rules](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#ide-style-rules).
- [`ZA0206`](https://github.com/Nickelony/Stanza/wiki/ZA0206-Remove-redundant-braces) removes braces only when every branch is single-line,
  so the two never report the same chain.
