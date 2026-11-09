# ZA3015 - separate-import-section

**Level: minimal.**
Separates a run of imports from the first statement below it.

```python
# Before
import os
import sys
logger = make_logger()

# After
import os
import sys

logger = make_logger()
```

- A run is the `import` and `from ... import` statements in a row, in a module or in a body, such as a function that imports lazily.
- The rule writes the one blank line that Black and `ruff format` force below imports, so the formatters never undo it.
- A comment directly above the statement below moves with it, so the blank line goes above the comment.
- Two multi-line imports in a row stay together, and the gap above a function or class belongs to the formatter.
- The blank lines between import groups belong to `isort` and Ruff `I001`.

## C# counterpart

[ZA0015 - Separate the import section](https://github.com/Nickelony/Stanza/wiki/ZA0015-Separate-the-import-section).

## JS counterpart

[`separate-import-section`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-import-section.md).
