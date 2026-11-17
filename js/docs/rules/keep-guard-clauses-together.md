# keep-guard-clauses-together

**Level: opt-in.**
Removes the blank line between two neighboring guard clauses, so a chain of guards reads as one.

```ts
// Before
function ship(order) {
	load(order);
	if (!order) {
		return;
	}

	if (order.shipped) {
		throw new Error('Shipped twice.');
	}
	send(order);
}

// After
function ship(order) {
	load(order);
	if (!order) {
		return;
	}
	if (order.shipped) {
		throw new Error('Shipped twice.');
	}
	send(order);
}

// Unchanged - a comment between the two guards
function ship(order) {
	load(order);
	if (!order) {
		return;
	}

	// A shipped order is a bug of the caller.
	if (order.shipped) {
		throw new Error('Shipped twice.');
	}
	send(order);
}

// Unchanged - an if with an else is no guard
function ship(order) {
	load(order);
	if (!order) {
		return;
	}

	if (order.shipped) {
		throw new Error('Shipped twice.');
	} else {
		mark(order);
	}
}
```

- A guard is what [`separate-guard-clauses`](separate-guard-clauses.md) reads as one: an `if` with no `else` whose body is a lone
  `return`, `throw`, `break` or `continue`, with braces or without, or a bare call of a helper that the `guardRoots` setting lists.
  An `if` whose body holds two statements is no guard.
- A helper call must fit on one line, while a multi-line `if` guard still joins the chain.
- The returned value does not matter: a bare `return`, `return false` and a returned object are all jumps.
- Two guards anywhere in a block are a chain, not only the guards that open it.
- A comment between the two, a trailing comment included, keeps the blank line.
- The rule wins the gaps it judges: [`separate-multi-line-constructs`](separate-multi-line-constructs.md) and the other rules that add a
  blank line ask for none of them, so a braced guard stays tight against the guard above it. The gap below the chain belongs to the other rules.
- The rule judges the blocks of a file, as `separate-guard-clauses` does, and leaves a switch case and the top level of a file alone.
- A block of two single-line guards belongs to [`keep-two-units-together`](keep-two-units-together.md) while that
  rule is enabled; otherwise this rule judges it.
- The rule is in no preset, because it removes blank lines an author wrote on purpose; enable it with an entry of its own.

A call of a `guardRoots` helper, such as the default `invariant`, and an `if` guard form one chain:

```ts
// Before
function ship(order) {
	invariant(order);

	if (order.closed) return false;
	send(order);
}

// After
function ship(order) {
	invariant(order);
	if (order.closed) return false;
	send(order);
}
```

## C# counterpart

[ZA0057 - Keep guard clauses together](https://github.com/Nickelony/Stanza/wiki/ZA0057-Keep-guard-clauses-together).

## Python counterpart

[ZA3057 - keep-guard-clauses-together](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3057-keep-guard-clauses-together.md).
