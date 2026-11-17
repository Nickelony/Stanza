# separate-invocation-runs

**Level: moderate.**
Separates a run of two or more neighboring calls of one helper from the statements beside it.

```ts
// Before
const total = sum(values);
logger.info('Summed', total);
logger.info('Done');
return total;

// After
const total = sum(values);

logger.info('Summed', total);
logger.info('Done');

return total;
```

- Calls are one helper when their callee paths match, so `console.log` and `console.error` are two helpers.
- A lone call stays tight.
- A call of an assertion root stays an assertion, and a `describe` or `it` call is a test block,
  so [`separate-assertion-runs`](separate-assertion-runs.md) and [`separate-test-blocks`](separate-test-blocks.md) judge them.
- An awaited call counts as the call it awaits, unless [`separate-await-runs`](separate-await-runs.md) is enabled.
  Then it is an await statement unless an active receiver consumer groups its selected receiver.
- An immediately invoked function has no callee path and joins no run.

## Settings

`receiverRoots` defaults to an empty list. A listed exact name matches any segment of a direct receiver path
after one leading `this`, `self` or `super` qualifier is removed. Thus `['db']` matches `this.db`, and
`['child']` selects `object.child` without selecting `object` itself. `['*']` selects every concrete receiver.
Direct `this.save()` and `self.save()` calls have no concrete receiver and remain outside.

`receiverComparison` defaults to `root` and accepts `root` or `path`:

| Value | Identity |
| --- | --- |
| `root` | The first name of the receiver path, after a leading `this`: `this.db.items.add()` and `db.flush()` share `db`. |
| `path` | The full receiver: `object.call()` uses `object`, while `object.child.call()` uses `object.child`. |

Comparison excludes the terminal method or assigned member. Selection and comparison are independent.
A real later member named `self` stays in the path, so `this.self.db` has root `self`.
Plain names, private names such as `this.#db`, listed as `'#db'`, parentheses and transparent TypeScript type and non-null wrappers qualify.
Optional, computed, indexed and call-produced receivers do not qualify. A multi-line call or comment breaks a run.

Receiver keys activate when this rule, receiver separation or receiver keeping is enabled.
Statement gathering reads no receiver keys, and global statement keeping needs this invocation rule to judge
invocation runs; neither activates receiver keys independently. Configured calls then share invocation runs
across `await`; selectors alone cannot suppress await separation when all those consumers are off.
Assertions, guards and test calls keep their semantic roles.

This setting changes invocation classification. Enable [statement keeping](keep-statement-runs-together.md)
to tighten those runs, or [receiver keeping](keep-receiver-groups-together.md) to tighten selected receivers
independently. [Receiver separation](separate-receiver-groups.md) also groups member writes with calls; ordinary
assignment run keys stay unchanged. Both receiver policies belong to no preset.

```js
export default [{ settings: { stanza: { receiverRoots: [], receiverComparison: 'root' } } }];
```

## C# counterpart

[ZA0004 - Separate invocation runs](https://github.com/Nickelony/Stanza/wiki/ZA0004-Separate-invocation-runs)
and [ZA0008 - Separate event subscription runs](https://github.com/Nickelony/Stanza/wiki/ZA0008-Separate-event-subscription-runs),
because `emitter.on(...)` calls are calls of one helper.
