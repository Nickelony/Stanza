# separate-await-runs

**Level: strong.**
Separates a run of two or more neighboring await statements from the statements beside it.

```ts
// Before
const client = createClient();
await client.connect();
await client.send(request);
log('Sent.');

// After
const client = createClient();

await client.connect();
await client.send(request);

log('Sent.');
```

- An await statement is an expression statement that awaits, such as `await save();` or `await task;`.
- `const data = await load();` is a declaration and `data = await load();` an assignment, so this rule leaves both.
- An awaited assertion stays an assertion, and an awaited test call stays a test block.
- A lone await statement stays tight.
- Where this rule is enabled, an awaited call normally belongs to its await run,
  so [`separate-invocation-runs`](separate-invocation-runs.md) leaves it out of its runs.
- A `for await` loop and an `await using` declaration are no await statements.
- A selected [receiver](separate-invocation-runs.md#settings) belongs to its invocation run across `await`
  while a receiver consumer is enabled. Selectors alone leave this rule unchanged.

## C# counterpart

[ZA0006 - Separate await runs](https://github.com/Nickelony/Stanza/wiki/ZA0006-Separate-await-runs).
