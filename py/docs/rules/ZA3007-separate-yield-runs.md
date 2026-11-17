# ZA3007 - separate-yield-runs

**Level: strong.**
Separates a run of two or more neighboring yield statements from the statements beside it.

```python
# Before
def numbers(limit):
    total = 0
    yield 1
    yield 2
    yield from range(limit)
    log(total)

# After
def numbers(limit):
    total = 0

    yield 1
    yield 2
    yield from range(limit)

    log(total)
```

- A yield statement is `yield value`, a bare `yield` or `yield from iterable` on a line of its own.
- A declaration or assignment that yields, such as `line = yield`, belongs to its own rule.
- `yield` and `yield from` are one kind, so a run may mix them. A run of `await` statements is another run, which [`ZA3006`](ZA3006-separate-await-runs.md) owns.
- A lone yield stays tight.
- While this rule is off, a yield statement belongs to no run, so no other rule groups it.

## C# counterpart

[ZA0007 - Separate yield runs](https://github.com/Nickelony/Stanza/wiki/ZA0007-Separate-yield-runs).

## JS counterpart

[`separate-yield-runs`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-yield-runs.md).
