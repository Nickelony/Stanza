# separate-guard-clauses

**Level: strong.**
Adds a blank line below the guard clauses that open a block, so the checks of the arguments read apart from the work.

```ts
// Before
function close(order) {
	invariant(order);
	if (order.closed) return;
	validate(order);
	order.close();
}

// After
function close(order) {
	invariant(order);
	if (order.closed) return;

	validate(order);
	order.close();
}
```

- A guard is an `if` with no `else` whose body is a lone `return`, `throw`, `break` or `continue`, with braces or without.
  A bare call of a helper that the `guardRoots` setting lists is a guard too.
- Only the guards that open the block count, so a guard below another statement gets no blank line from this rule.
  A block of guards alone has no statement to set apart.
- A chain of guards stays together: the blank line goes below the last guard.
- A comment directly above the statement below moves with it, so the blank line goes above the comment.
- The rule judges the blocks of a file and leaves a switch case and the top level of a file alone.

## Settings

```js
export default [{ settings: { stanza: { guardRoots: ['assert', 'invariant'] } } }];
```

The sample is the default.

`guardRoots` lists the names whose bare call is a guard, such as `invariant(value)` and, for `assert`, `assert.ok(value)`.
A name matches any name along the callee path exactly, and a list replaces the default, so an empty list makes no call a guard.
A value that is no array of strings is an error that names the key.
This rule and [`separate-base-call`](separate-base-call.md) read the key, so both find the same guards whether or not the other is enabled.

## C# counterpart

[ZA0011 - Separate the guard clauses](https://github.com/Nickelony/Stanza/wiki/ZA0011-Separate-the-guard-clauses),
whose helper list is [`stanza_guard_roots`](https://github.com/Nickelony/Stanza/wiki/stanza_guard_roots).
