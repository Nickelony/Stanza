# ZA3002 - separate-declaration-runs

**Level: moderate.**
Separates a run of two or more neighboring declarations of one kind from the statements beside it.

```python
# Before
limit = 3
retries: int = 5
log(limit)

# After
limit = 3
retries: int = 5

log(limit)
```

- A declaration binds names:
  an assignment whose targets are names or a tuple of names, an annotated assignment to a name,
  a `global` or `nonlocal` statement, or a `type` alias.
- Declarations group by kind, so a run of `global` names stands apart from a run of assignments. An annotation does not make a kind.
- An assignment to an attribute or an item is an assignment, and [`ZA3008`](ZA3008-separate-assignment-runs.md) owns it.
- A lone declaration stays tight against the statement below it, unless that statement starts a run of another rule.
- A declaration that awaits, such as `data = await load()`, is a declaration, and [`ZA3006`](ZA3006-separate-await-runs.md) leaves it alone.
- A class body is judged by [`ZA3013`](ZA3013-separate-member-groups.md), which reads fields and attributes.

## C# counterpart

[ZA0002 - Separate declaration runs](https://github.com/Nickelony/Stanza/wiki/ZA0002-Separate-declaration-runs).

## JS counterpart

[`separate-declaration-runs`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-declaration-runs.md).
