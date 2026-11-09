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
  Then it is an await statement whatever it calls.
- An immediately invoked function has no callee path and joins no run.

## C# counterpart

[ZA0004 - Separate invocation runs](https://github.com/Nickelony/Stanza/wiki/ZA0004-Separate-invocation-runs)
and [ZA0007 - Separate event subscription runs](https://github.com/Nickelony/Stanza/wiki/ZA0007-Separate-event-subscription-runs),
because `emitter.on(...)` calls are calls of one helper.
