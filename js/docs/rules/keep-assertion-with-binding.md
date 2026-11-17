# keep-assertion-with-binding

**Level: opt-in.**
Removes the blank line between a statement that binds a name and the single-line assertion below it that narrows that name.

```ts
// Before
function ship(id) {
	audit(id);
	const order = orders.get(id);

	invariant(order !== undefined);
	order.ship();
}

// After
function ship(id) {
	audit(id);
	const order = orders.get(id);
	invariant(order !== undefined);
	order.ship();
}

// Unchanged - the assertion leads the assert phase of a test
it('loads the order', () => {
	const order = load(1);

	assert(order);
	assert.equal(order.id, 1);
});
```

- An assertion that narrows the value the statement above it bound checks that value, so the two read as one step.
- The assertion is a single-line call of a root that the `guardRoots` or the `assertionRoots` setting lists,
  made on the root itself, as in `assert(x)` and `invariant(x)`, or through `ok`, as in `assert.ok(x)`.
- Its first argument narrows a bound name: the name itself, `x != null`, `x !== null`, `x != undefined`, `x !== undefined`,
  `typeof x === 'string'`, `x instanceof Order`, or an `&&` that holds one of these.
  `assert.equal(x, 3)` and `expect(x).toBe(3)` compare a value and narrow nothing.
- A contiguous assertion phase containing an ordinary result check retains its separate phase. A narrowing-only
  assertion chain can keep the bindings it checks. Comments end the phase, and multi-line assertions stay outside.
- The binding is read as [`keep-guard-with-binding`](keep-guard-with-binding.md) reads one, and it may span several lines.
- A comment between the two, a trailing comment included, keeps the blank line.
- The rule wins the gap it judges, so no rule that adds a blank line asks for it.
- The rule judges the statements of a block, a switch case, a static block, a module block and a file.
- In a block, the rule keeps the whole contiguous binding group with its checks when the chain reads every binding
  and opens with a narrowing assertion that leads no assert phase, whether or not [`separate-checked-stanzas`](separate-checked-stanzas.md) is enabled.
- A separation another rule asks for inside the kept group moves above the group, as the [rule map](../Rule-map.md#rules-that-keep-a-pair-together) says.
- The rule is in no preset, because it removes blank lines an author wrote on purpose; enable it with an entry of its own.

## C# counterpart

[ZA0060 - Keep a narrowing assertion with its binding](https://github.com/Nickelony/Stanza/wiki/ZA0060-Keep-a-narrowing-assertion-with-its-binding).

## Python counterpart

[ZA3060 - keep-assertion-with-binding](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3060-keep-assertion-with-binding.md).
