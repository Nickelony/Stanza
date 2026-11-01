# ZA3008 - separate-assignment-runs

**Level: strong.**
Separates a run of two or more neighboring assignments to attributes and items, or augmented assignments, from the statements beside it.

```python
# Before
def reset(self):
    self.count = 0
    self.items = []
    super().reset()

# After
def reset(self):
    self.count = 0
    self.items = []

    super().reset()
```

- An assignment is a statement whose target is an attribute or an item, such as `self.name = name` or `table[key] = value`,
  or an augmented assignment such as `total += 1`.
- An assignment to plain names is a declaration, and [`ZA3002`](ZA3002-separate-declaration-runs.md) owns it.
- All assignments form one run, whatever the target, so the `self.` lines of an `__init__` stay together.
- A lone assignment stays tight against the statement below it.

## C# counterpart

[ZA0008 - Separate assignment runs](https://github.com/Nickelony/Stanza/wiki/ZA0008-Separate-assignment-runs).

## JS counterpart

[`separate-assignment-runs`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-assignment-runs.md).
