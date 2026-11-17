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
  so while this rule is enabled no other rule asks for anything there, and the one present is padding.
- While this rule is disabled, only the [keep rules](../Rule-map.md#rules-that-keep-a-pair-together),
  [`separate-checked-stanzas`](separate-checked-stanzas.md) and [`separate-receiver-groups`](separate-receiver-groups.md) judge the gap,
  and every other rule leaves it as written. The C# rules all judge it then.
- A container is a file, a block, a switch case, a class body, a static block, an interface body, a type literal or a `declare module` block.
- A comment between the two units, a trailing comment included, keeps the blank line.
- A unit that spans several lines keeps the blank line, and [`separate-multi-line-constructs`](separate-multi-line-constructs.md) asks for it.
- The rule skips a container that holds a function declaration, an import, a directive or a shebang, because other rules own those gaps.

## C# counterpart

[ZA0054 - Keep the two units of a container together](https://github.com/Nickelony/Stanza/wiki/ZA0054-Keep-the-two-units-of-a-container-together).
