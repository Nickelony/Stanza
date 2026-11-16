# keep-guard-with-binding

**Level: opt-in.**
Removes the blank line between a statement that binds a name and the guard clause below it that tests that name.

```ts
// Before
async function readCustomer(service, id) {
	audit(id);
	const customer = await service.getCustomer(id);

	if (!customer) {
		throw new NotFound(id);
	}
	return customer.name;
}

// After
async function readCustomer(service, id) {
	audit(id);
	const customer = await service.getCustomer(id);
	if (!customer) {
		throw new NotFound(id);
	}
	return customer.name;
}

// Unchanged - the guard tests a name the binding does not give
async function readCustomer(service, id) {
	audit(id);
	const customer = await service.getCustomer(id);

	if (!id) {
		throw new NotFound(id);
	}
	return customer.name;
}
```

- A guard that tests the value the statement above it binds checks that value, so the two read as one step.
- The guard is an `if` that [`separate-guard-clauses`](separate-guard-clauses.md) reads as one: no `else`,
  and a body of a single `return`, `throw`, `break` or `continue`, with braces or without.
  A call of a helper that the `guardRoots` setting lists is no `if`, so it does not count.
- The binding is a `const`, `let`, `var` or `using` declaration whose declarators give a value, destructuring patterns included,
  or an assignment statement such as `x = load();`. It may span several lines.
- The guard tests a name when its condition reads it as a variable; `order.customer` reads `order` and not `customer`.
  A shadowing parameter or local and a write-only target do not read the preceding binding.
  A closure that captures that binding counts.
- A comment between the two, a trailing comment included, keeps the blank line.
- The rule wins the gap it judges: [`separate-multi-line-constructs`](separate-multi-line-constructs.md) and the other rules that add a
  blank line ask for none of them. The gap below the guard belongs to the other rules.
- With [`keep-guard-clauses-together`](keep-guard-clauses-together.md), a chain of guards below the binding joins it too.
- The rule judges the blocks of a file, as `separate-guard-clauses` does, and leaves a switch case and the top level of a file alone.
- In a block, the rule keeps the whole contiguous binding group with its checks when the chain reads every binding
  and opens with an `if` guard, whether or not [`separate-checked-stanzas`](separate-checked-stanzas.md) is enabled.
- A separation another rule asks for inside the kept group moves above the group, as the [rule map](../Rule-map.md#rules-that-keep-a-pair-together) says.
- The rule is in no preset, because it removes blank lines an author wrote on purpose; enable it with an entry of its own.

## C# counterpart

[ZA0058 - Keep a guard with its binding](https://github.com/Nickelony/Stanza/wiki/ZA0058-Keep-a-guard-with-its-binding).

## Python counterpart

[ZA3058 - keep-guard-with-binding](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3058-keep-guard-with-binding.md).
