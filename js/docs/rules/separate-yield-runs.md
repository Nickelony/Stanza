# separate-yield-runs

**Level: strong.**
Separates a run of two or more neighboring yield statements from the statements beside it.

```ts
// Before
function* pages(list: string[]) {
	const first = list[0];
	yield first;
	yield* list.slice(1);
	log('Done.');
}

// After
function* pages(list: string[]) {
	const first = list[0];

	yield first;
	yield* list.slice(1);

	log('Done.');
}
```

- A yield statement is an expression statement that yields, such as `yield value;`, `yield* values;` or a bare `yield;`.
  A `yield` and a `yield*` belong to one run, and `yield call();` is a yield statement whatever it calls.
- `const input = yield request;` is a declaration and `input = yield request;` an assignment, so this rule leaves both.
- A lone yield statement stays tight.
- A run is one statement kind, so a run of `yield` statements and a run of `await` statements are two, and [`separate-await-runs`](separate-await-runs.md) judges the second.

## C# counterpart

[ZA0020 - Separate yield runs](https://github.com/Nickelony/Stanza/wiki/ZA0020-Separate-yield-runs).
