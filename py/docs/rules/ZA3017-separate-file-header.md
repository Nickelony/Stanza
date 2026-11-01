# ZA3017 - separate-file-header

**Level: minimal.**
Adds a blank line below the shebang, the encoding line and the module docstring.

```python
# Before
#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Close orders."""
import sys

# After
#!/usr/bin/env python
# -*- coding: utf-8 -*-

"""Close orders."""

import sys
```

- The shebang and the encoding line form one unit, the preamble. The encoding line counts on line one, or on line two under a comment.
- The module docstring is the first string statement of the file.
  A string below another statement is not a docstring and is left alone.
- A comment directly above the first statement moves with it, so the blank line goes above the comment.
- The blank line below the module docstring is the one Black and `ruff format` force, so the formatters never undo it.
- `from __future__ import` statements are imports, and [`ZA3015`](ZA3015-separate-import-section.md) owns the blank line below them.

## C# counterpart

[ZA0017 - Separate a file-scoped namespace declaration from its first member](https://github.com/Nickelony/Stanza/wiki/ZA0017-Separate-a-file-scoped-namespace-declaration-from-its-first-member),
the header of a C# file.

## JS counterpart

[`separate-directives`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-directives.md), which separates the shebang and the directive prologue.
