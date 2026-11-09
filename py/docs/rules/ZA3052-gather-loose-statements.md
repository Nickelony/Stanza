# ZA3052 - gather-loose-statements

**Level: strong.**
Removes the blank lines between the statements of a loose run that holds more than `max-loose-run-size` statements.

```python
# Before
total = 0

count = 1

size = 2

limit = 3

# After
total = 0
count = 1
size = 2
limit = 3
```

- A loose run is a chain of single-line statements with nothing but blank lines between neighbors.
  The rule reports every blank line of a run past the limit, and the fix removes the blank lines of the whole run.
- A multi-line statement, a comment, a trailing comment or a gap with no blank line ends a run.
- A statement that sits tight against its other neighbor is not part of the run, so the blank line that sets that group apart stays.
  A statement tight against a neighbor that a rule asks to set apart, as [`ZA3001`](ZA3001-separate-multi-line-constructs.md) does beside a multi-line statement, still joins.
- The rule never removes a blank line that another rule would write back.
  A gap that an enabled separating rule asks for ends a run, and a run that then holds no more than the limit is left as written.
- A jump statement, an import, a function and a class are no part of a run.
  The gap above a jump stays blank, and the gap that [`ZA3015`](ZA3015-separate-import-section.md) asks for below the imports stays too.
- The rule judges the statements of a module, a function and a block.
  The fields and attributes of a class body belong to [`ZA3053`](ZA3053-gather-loose-members.md).
- A container of exactly two statements belongs to [`ZA3051`](ZA3051-keep-two-units-together.md).
- A region between `# fmt: off` and `# fmt: on` keeps its blank lines.

## Options

| Key | Default | Effect |
| --- | --- | --- |
| `max-loose-run-size` | `3` | The most statements or members a loose run holds before the gather rules join it. `0` turns both rules off. |

## C# counterpart

[ZA0052 - Gather a run of loose statements](https://github.com/Nickelony/Stanza/wiki/ZA0052-Gather-a-run-of-loose-statements),
whose option is [`stanza_max_loose_run_size`](https://github.com/Nickelony/Stanza/wiki/stanza_max_loose_run_size).

## JS counterpart

[`gather-loose-statements`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/gather-loose-statements.md).
