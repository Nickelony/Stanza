# ZA3013 - separate-member-groups

**Level: moderate.**
Separates the docstring of a class and the runs of two or more fields or attributes of one kind from their neighbors.

```python
# Before
class Order:
    """A customer order."""
    id: int
    total: float
    LIMIT = 10
    STEP = 2

# After
class Order:
    """A customer order."""

    id: int
    total: float

    LIMIT = 10
    STEP = 2
```

- A field is an annotated assignment, as in a dataclass, and an attribute is an assignment without an annotation.
- Members group by kind, so a run of fields stands apart from a run of attributes.
- The blank line below the class docstring is the one Black and `ruff format` force, as PEP 257 asks.
- The gaps beside a method or a nested class belong to the formatters, so this rule leaves them as written.
- A lone attribute stays tight against the member beside it.

## C# counterpart

[ZA0013 - Separate member groups](https://github.com/Nickelony/Stanza/wiki/ZA0013-Separate-member-groups).

## JS counterpart

[`separate-member-groups`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-member-groups.md).
