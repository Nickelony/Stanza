# ZA3012 - separate-commented-siblings

**Level: strong.**
Adds a blank line above a comment that leads a single-line statement, so a comment introduces a group.

```python
# Before
open_file()
# Reads the file.
read_file()

# After
open_file()

# Reads the file.
read_file()
```

- The rule asks for the gap only when both neighbors are one line each, because [`ZA3001`](ZA3001-separate-multi-line-constructs.md) owns the others.
- A comment separated from the unit below by a blank line is not leading it.
- A comment that trails the unit above stays with it, and so does a comment indented deeper than the unit below, as at the end of an `if` body.
- A [tool directive](../Quirks.md#comments) does not lead a group:
  `# type: ignore`, `# noqa`, `# pragma: no cover`, `# pylint: disable=...`, `# fmt: off`, `# isort: skip`, `# mypy: ...`, `# pyright: ...`
  and similar comments annotate the line below and stay adjacent.
- A class body is judged too, so a commented attribute stands apart from the attribute above it.

## C# counterpart

[ZA0012 - Separate a commented sibling](https://github.com/Nickelony/Stanza/wiki/ZA0012-Separate-a-commented-sibling).

## JS counterpart

[`separate-commented-siblings`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-commented-siblings.md).
