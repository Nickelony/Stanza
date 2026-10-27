# ZA0038 - Use braces consistently

**Suggestion, on by default.**
Adds braces to every branch of an `if`/`else if`/`else` chain when any one branch is multi-line.

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
  or its block holds two or more statements.
- A loop, `using`, `lock` or `fixed` body is judged on its own, without chain logic.
- A `try`, `catch` or `finally` body is not judged; [`ZA0036`](https://github.com/Nickelony/Stanza/wiki/ZA0036-Remove-brace-padding) and [`ZA0037`](https://github.com/Nickelony/Stanza/wiki/ZA0037-Collapse-empty-blocks) still check its padding and its emptiness.
- A body that shares its line with its header is left to `csharp_prefer_braces`.
- The rule reports nothing when `csharp_prefer_braces` is `false`;
  see the [IDE style rules](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#ide-style-rules).
- [`ZA0039`](https://github.com/Nickelony/Stanza/wiki/ZA0039-Remove-redundant-braces) removes braces only when every branch is single-line,
  so the two never report the same chain.
