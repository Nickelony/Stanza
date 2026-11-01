# ZA3053 - gather-loose-members

**Level: strong.**
Removes the blank lines between the members of a loose run that holds more than `max-loose-run-size` members of a class body.

```python
# Before
class Order:
    total: float

    count: int

    size: int

    limit: int

# After
class Order:
    total: float
    count: int
    size: int
    limit: int
```

- A member is a single-line field, attribute or other statement of a class body.
  A method and a nested class are definitions, and the formatters own the gaps beside them.
- A loose run, what ends it and its limit are those of [`ZA3052`](ZA3052-gather-loose-statements.md).
- The blank line below a class docstring stays, because [`ZA3013`](ZA3013-separate-member-groups.md) and the formatters ask for it.
- While [`ZA3013`](ZA3013-separate-member-groups.md) is enabled, the blank line it asks for between a run of fields and a run of attributes stays, so the run ends there.
- A class body of exactly two single-line members belongs to [`ZA3051`](ZA3051-keep-two-units-together.md).
- The statements of a function body belong to [`ZA3052`](ZA3052-gather-loose-statements.md).

## Options

| Key | Default | Effect |
| --- | --- | --- |
| `max-loose-run-size` | `3` | The most statements or members a loose run holds before the gather rules join it. `0` turns both rules off. |

## C# counterpart

[ZA0053 - Gather a run of loose members](https://github.com/Nickelony/Stanza/wiki/ZA0053-Gather-a-run-of-loose-members),
whose option is [`stanza_max_loose_run_size`](https://github.com/Nickelony/Stanza/wiki/stanza_max_loose_run_size).

## JS counterpart

[`gather-loose-members`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/gather-loose-members.md).
