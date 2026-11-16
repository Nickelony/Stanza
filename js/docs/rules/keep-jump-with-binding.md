# keep-jump-with-binding

**Level: opt-in.**
Removes the blank line between a lone single-line binding and the `return` or `throw` below it that uses what it binds.

```ts
// Before
function total(order) {
	validate(order);
	order.close();

	const sum = order.lines.reduce(add, 0);

	return sum;
}

// After
function total(order) {
	validate(order);
	order.close();

	const sum = order.lines.reduce(add, 0);
	return sum;
}

// Unchanged - the binding is part of a stanza above it
function total(order) {
	validate(order);
	const sum = order.lines.reduce(add, 0);

	return sum;
}
```

- A binding that only names the value a jump returns or throws is one step with the jump.
- The binding stands alone: it opens its block, or a blank line stands above it.
- A binding at the end of a longer stanza stays apart from the jump, so the rule never moves a blank line from one gap to another.
  It agrees with [`separate-jump-statements`](separate-jump-statements.md), which sets a jump apart from a stanza of two or more lines.
- The binding is read as [`keep-guard-with-binding`](keep-guard-with-binding.md) reads one, and both statements span a single line.
- The jump is a `return` or a `throw` whose value reads a bound name. A bare `return;`, a `break` and a `continue` use no name.
  A shadowing parameter or local and a write-only target do not read the preceding binding.
  A returned closure that captures that binding counts.
- A comment between the two, a trailing comment included, keeps the blank line.
- The rule wins the gap it judges, so no rule that adds a blank line asks for it.
- The rule judges the statements of a block, a switch case, a static block, a module block and a file.
- A container of the two statements alone belongs to [`keep-two-units-together`](keep-two-units-together.md) while that rule is enabled.
- A blank line another enabled rule asks for above the binding counts as standing there,
  unless a rule that removes the blank lines of a run takes that gap.
- A separation another rule asks for inside the kept group moves above the group, as the [rule map](../Rule-map.md#rules-that-keep-a-pair-together) says.
- The rule is in no preset, because it removes blank lines an author wrote on purpose; enable it with an entry of its own.

## C# counterpart

[ZA0060 - Keep a jump with its binding](https://github.com/Nickelony/Stanza/wiki/ZA0060-Keep-a-jump-with-its-binding).

## Python counterpart

[ZA3060 - keep-jump-with-binding](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3060-keep-jump-with-binding.md).
