# separate-jump-statements

**Level: strong.**
Adds a blank line above a `return`, `throw`, `break` or `continue` when the stanza directly above it spans two or more lines.

```ts
// Before
function close(id) {
	const order = load(id);
	validate(order);
	order.close();
	return order;
}

// After
function close(id) {
	const order = load(id);
	validate(order);
	order.close();

	return order;
}
```

One line above keeps the jump tight: `const order = load(id);` directly above `return order;` stays as it is.

- The stanza is the statements directly above the jump that no blank line separates and no other enabled rule asks to separate.
  A statement that spans several lines is a stanza of several lines, and an import above the first statement ends the stanza.
- A comment line between the statements counts as a line of the stanza, and a comment directly above the jump moves with it.
- A jump that opens its block and a labeled `break` or `continue` are left alone.
- The `break` that closes a `case` is not judged, because it ends the section and not the logic above it.
- Where [`separate-guard-clauses`](separate-guard-clauses.md) also asks for the gap, the guard rule reports it.
- The rule has no tight counterpart; leave it disabled to keep jumps tight.

## C# counterpart

[ZA0009 - Separate a jump statement](https://github.com/Nickelony/Stanza/wiki/ZA0009-Separate-a-jump-statement).
