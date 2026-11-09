# separate-test-blocks

**Level: moderate.**
Adds a blank line between the `describe`, `it`, `test` and hook calls of a test file.

```ts
// Before
describe('orders', () => {
	beforeEach(reset);
	it.todo('retries');
	it.todo('notifies');
});

// After
describe('orders', () => {
	beforeEach(reset);

	it.todo('retries');

	it.todo('notifies');
});
```

- A test block is a call whose first name is a root that `settings.stanza.testRoots` lists,
  such as `it.each(...)(...)`, `describe.skip(...)` or `beforeEach(...)`.
- The rule separates two blocks even when both are single-line, which [`separate-multi-line-constructs`](separate-multi-line-constructs.md) leaves alone.
  The one exception is a body of exactly two single-line units, where [`keep-two-units-together`](keep-two-units-together.md) owns the gap, as the [quirks](../Quirks.md#two-single-line-units) say.
- A test block is no invocation, so a run of `it.todo(...)` calls is a run of test blocks.
- A call to a test root is a test block whether or not this rule is enabled, so it joins no invocation, assertion or await run.
  A name that is a test root and an assertion root is a test block.

## Settings

```js
export default [{ settings: { stanza: { testRoots: ['describe', 'it', 'test', 'beforeEach'] } } }];
```

The default list is `describe`, `it`, `test`, `suite`, `context`, `specify`, `before`, `beforeAll`, `beforeEach`, `after`, `afterAll` and `afterEach`.
A list replaces the default, an empty list means no roots, and the first name of the callee path must match a root exactly.
A value that is no array of strings is an error that names the key.

## C# counterpart

None. The nearest is [ZA0001 - Separate multi-line constructs](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-multi-line-constructs),
which leaves single-line test blocks tight.
