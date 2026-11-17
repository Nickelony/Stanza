# keep-accumulator-with-loop

**Level: opt-in.**
Removes the blank lines between the statements that seed names and the loop below them that builds those names.

```ts
// Before
function subtotal(cart) {
	check(cart);
	let total = 0;

	for (const item of cart.items) {
		total += item.price * item.quantity;
	}
	return total;
}

// After
function subtotal(cart) {
	check(cart);
	let total = 0;
	for (const item of cart.items) {
		total += item.price * item.quantity;
	}
	return total;
}

// Unchanged - the loop only uses the object, it does not build it
function sendAll(messages) {
	check(messages);
	const client = new Client();

	for (const message of messages) {
		client.send(message);
	}
	return client;
}
```

A run of seeds that the loop builds joins it as one group:

```ts
// Before
function average(cart) {
	check(cart);
	let total = 0;

	let count = 0;

	for (const item of cart.items) {
		total += item.price;
		count++;
	}
	return total / count;
}

// After
function average(cart) {
	check(cart);
	let total = 0;
	let count = 0;
	for (const item of cart.items) {
		total += item.price;
		count++;
	}
	return total / count;
}
```

- The seed and the loop that fills it are one step: the seed means nothing without the loop.
- A run of seeds directly above the loop, each built by that loop, joins it as one group,
  and the blank lines between the seeds go too, as in the C# rule.
- A seed the loop does not build ends the run, so the seeds above it stay apart from the loop.
- A seed is a single-line `const`, `let` or `var` declaration of one plain name, or an assignment `name = value;`,
  whose value reads no variable, such as `0`, `''`, `null`, `undefined`, `[]`, `{}`, `new Map()` or `BigInt(0)`.
- A name may appear in a seed only as the callee of a call or a constructor, so `const total = start;` and `const items = [first];` are no seeds.
- The loop is a `for`, a `for...in`, a `for...of`, a `while` or a `do...while`.
  It builds the seeded binding when its executing body assigns that binding, with `=` or a compound operator,
  increments or decrements it, or calls `push`, `unshift`, `add`, `set` or `splice` on it.
- Nested control flow and loops count. Updates to shadowing bindings do not.
- Nested function and class definitions are excluded as whole nodes,
  including parameter defaults, bases and decorators.
- Any other method call only uses the object, so the blank line stays.
- A comment between the two, a trailing comment included, keeps the blank line.
- The rule wins the gap it judges: [`separate-multi-line-constructs`](separate-multi-line-constructs.md), which separates the loop,
  and the other rules that add a blank line ask for none of them. The gap below the loop belongs to the other rules.
- The rule judges the statements of a block, a switch case, a static block, a module block and a file.
- A separation another rule asks for inside the kept group moves above the group, as the [rule map](../Rule-map.md#rules-that-keep-a-pair-together) says.
- The rule is in no preset, because it removes blank lines an author wrote on purpose; enable it with an entry of its own.

## C# counterpart

[ZA0063 - Keep an accumulator with its loop](https://github.com/Nickelony/Stanza/wiki/ZA0063-Keep-an-accumulator-with-its-loop).

## Python counterpart

[ZA3063 - keep-accumulator-with-loop](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3063-keep-accumulator-with-loop.md).
