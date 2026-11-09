# keep-two-units-together

**Level: moderate.**
Removes the blank line between the only two units of a container when each spans a single line.

```ts
// Before
function field(x) {
	const next = decode(x);

	return next.y;
}

// After
function field(x) {
	const next = decode(x);
	return next.y;
}
```

- With only two units, neither has a neighbor outside the pair to be separated from,
  so the rules that add blank lines ask for nothing there and the one present is padding.
- A container is a file, a block, a switch case, a class body, a static block, an interface body, a type literal or a `declare module` block.
- A comment between the two units, a trailing comment included, keeps the blank line.
- A unit that spans several lines keeps the blank line, and [`separate-multi-line-constructs`](separate-multi-line-constructs.md) asks for it.
- The rule skips a container that holds a function declaration, an import, a directive or a shebang, because other rules own those gaps.
- The rules that remove a blank line from a run skip a container of two single-line units, so this rule owns it.

## C# counterpart

[ZA0051 - Keep the two units of a container together](https://github.com/Nickelony/Stanza/wiki/ZA0051-Keep-the-two-units-of-a-container-together).
