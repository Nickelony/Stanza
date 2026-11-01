# ZA3006 - separate-await-runs

**Level: strong.**
Separates a run of two or more neighboring await statements from the statements beside it.

```python
# Before
await queue.put(first)
await queue.put(second)
log(first)

# After
await queue.put(first)
await queue.put(second)

log(first)
```

- An await statement is `await call()` on a line of its own, whatever the call is.
- A declaration or assignment that awaits, such as `data = await load()`, belongs to its own rule.
- An awaited call of an assertion root stays an assertion.
- An `async with` or `async for` statement is a block, and [`ZA3001`](ZA3001-separate-multi-line-constructs.md) owns its gaps.
- While this rule is off, an awaited call is an invocation of its callee, so [`ZA3004`](ZA3004-separate-invocation-runs.md) groups it by name.

## C# counterpart

[ZA0006 - Separate await runs](https://github.com/Nickelony/Stanza/wiki/ZA0006-Separate-await-runs).

## JS counterpart

[`separate-await-runs`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-await-runs.md).
