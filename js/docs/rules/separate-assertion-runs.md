# separate-assertion-runs

**Level: moderate.**
Separates a run of two or more neighboring assertions from the statements beside it,
so the arrange, act and assert steps of a test read as three stanzas.

```ts
// Before
const result = parse(text);
expect(result.count).toBe(2);
expect(result.first).not.toBeNull();
cleanup();

// After
const result = parse(text);

expect(result.count).toBe(2);
expect(result.first).not.toBeNull();

cleanup();
```

- A call is an assertion when a name along its callee path is a root that `settings.stanza.assertionRoots` lists.
  The default roots are `assert`, `expect`, `expectTypeOf` and `t`.
- Assertions group by root and not by method, so `assert.equal(...)` and `assert.ok(...)` are one run.
- An awaited assertion, such as `await expect(task).rejects.toThrow();`, stays an assertion.
- A lone assertion stays tight.

## Settings

```js
export default [{ settings: { stanza: { assertionRoots: ['expect', 'assert', 'should'] } } }];
```

A list replaces the default, an empty list means no roots, and a name matches exactly.
A value that is no array of strings is an error that names the key.
[`separate-invocation-runs`](separate-invocation-runs.md) and [`separate-await-runs`](separate-await-runs.md) read the same list, so they leave an assertion to this rule.
A call whose first name is a test root is a test block, even when the name is also an assertion root.

## C# counterpart

[ZA0005 - Separate assertion runs](https://github.com/Nickelony/Stanza/wiki/ZA0005-Separate-assertion-runs),
whose option is [`stanza_assertion_roots`](https://github.com/Nickelony/Stanza/wiki/stanza_assertion_roots).
