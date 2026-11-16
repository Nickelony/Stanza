# separate-checked-stanzas

**Level: opt-in.**
Sets a guard chain and the contiguous bindings it checks apart from neighboring statements.

```ts
// Before
function ship(id) {
	audit(id);
	const order = find(id);
	if (!order) return false;
	if (order.closed) return false;
	send(order);
}

// After
function ship(id) {
	audit(id);

	const order = find(id);
	if (!order) return false;
	if (order.closed) return false;

	send(order);
}

// Unchanged - narrowing assertions stay tight with the flow below
function ship(id) {
	audit(id);

	const order = find(id);
	assert(order != null);
	send(order);
}
```

- A checked stanza contains a chain of guards anywhere in a block and the bindings directly above it whose names the checks read.
  A binding the checks do not read stays above the stanza.
- A read refers to the actual binding, closure captures included; a shadowing declaration and a write-only target do not count.
- A guard is an `if` with no `else` whose body is a lone jump, or a call listed in `guardRoots`, even one that narrows its argument.
  A narrowing assertion is a call of an `assertionRoots` name in the forms of [`keep-assertion-with-binding`](keep-assertion-with-binding.md).
  A plain result assertion does not count.
- A stanza made only of narrowing assertions gets a blank line above, never below.
  A contiguous assertion run containing any plain result assertion stays in its test phase and is not a checked stanza.
- A stanza opening or closing the block gets no blank line at that edge.
  A comment above the stanza prevents the rule from adding a blank line there.
- [`separate-guard-clauses`](separate-guard-clauses.md) owns the edge below an opening guard chain while enabled,
  except in a container of two single-line statements, which that rule never judges.
  [`separate-jump-statements`](separate-jump-statements.md) judges a jump below the chain.
- The rule judges the blocks of a file and leaves a switch case and the top level of a file alone, where the C# rule judges them too.
- The stanza borders end surrounding runs, while kept pairs win inside the stanza.
- [`keep-two-units-together`](keep-two-units-together.md) owns a container of two single-line statements while that rule is enabled.
- Enable [`keep-guard-clauses-together`](keep-guard-clauses-together.md) and [`keep-guard-with-binding`](keep-guard-with-binding.md)
  to keep multi-line checks tight inside the stanza. With the matching binding keeper enabled,
  the whole checked binding group stays tight even when each check reads a different binding.

## C# counterpart

[ZA0018 - Separate a checked stanza](https://github.com/Nickelony/Stanza/wiki/ZA0018-Separate-a-checked-stanza).

## Python counterpart

[ZA3018 - separate-checked-stanzas](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3018-separate-checked-stanzas.md).
