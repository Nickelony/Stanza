# ZA3004 - separate-invocation-runs

**Level: moderate.**
Separates a run of two or more neighboring calls of one helper from the statements beside it.

```python
# Before
print(first)
print(second)
save(first)

# After
print(first)
print(second)

save(first)
```

- A call statement is part of a run when its callee path is the same,
  so `logger.info(...)` repeats and `logger.info(...)` beside `logger.debug(...)` does not.
- A chain such as `session.get().json()` is part of the path, so only an identical chain repeats.
- A call with no callee path, such as `(a or b)()`, joins no run.
- An awaited call is an invocation of its callee while [`ZA3006`](ZA3006-separate-await-runs.md) is off, and an await statement when it is on.
- A call that matches `assertion-roots` is an assertion, and [`ZA3005`](ZA3005-separate-assertion-runs.md) owns it.

## C# counterpart

[ZA0004 - Separate invocation runs](https://github.com/Nickelony/Stanza/wiki/ZA0004-Separate-invocation-runs).

## JS counterpart

[`separate-invocation-runs`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-invocation-runs.md).
