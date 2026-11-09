# ZA3011 - separate-guard-clauses

**Level: strong.**
Adds a blank line below the guard clauses that open a block, so the checks of the arguments read apart from the work.

```python
# Before
def close(order):
    """Closes the order."""
    if order.closed: return
    validate(order)
    order.close()

# After
def close(order):
    """Closes the order."""
    if order.closed: return

    validate(order)
    order.close()
```

- A guard is an `if` with no `else` whose body is a lone `return`, `raise`, `break` or `continue`.
  An `assert` statement is a guard too, and so is a bare call of a helper that `guard-roots` lists.
- Only the guards that open the block count, after the docstring of a function, so a guard below another statement gets no blank line from this rule.
- A chain of guards stays together: the blank line goes below the last guard.
- A comment directly above the statement below moves with it, so the blank line goes above the comment.
- The rule judges function bodies and the bodies of compound statements, and leaves a module and a class body alone.
- Black writes the body of an `if` on its own line, which makes the guard a multi-line statement,
  so [`ZA3001`](ZA3001-separate-multi-line-constructs.md) separates it from both neighbors where it is enabled.
  This rule matters for single-line guards and for the `guard-roots` helpers.
- No rule asks for a blank line below a function docstring, so the first guard stays against it.

## Options

| Key | Default | Effect |
| --- | --- | --- |
| `guard-roots` | `[]` | The patterns that make a bare call a guard, such as `check_argument`. |

A pattern is a shell-style glob that matches the dotted path of the callee or any name along it, as the [configuration page](../Configuration.md#patterns) says.
Unlike the exact names of the C# and JS roots, it can match a family of helpers.
[`ZA3010`](ZA3010-separate-base-call.md) reads the list too, to find where the guards before a base call end.

## C# counterpart

[ZA0011 - Separate the guard clauses](https://github.com/Nickelony/Stanza/wiki/ZA0011-Separate-the-guard-clauses),
whose helper list is [`stanza_guard_roots`](https://github.com/Nickelony/Stanza/wiki/stanza_guard_roots).

## JS counterpart

[`separate-guard-clauses`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-guard-clauses.md).
