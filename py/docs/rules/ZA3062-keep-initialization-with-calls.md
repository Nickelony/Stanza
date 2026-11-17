# ZA3062 - keep-initialization-with-calls

**Level: opt-in.**
Removes the blank line between a standalone initialization and the single-line call on that name directly below it.

```python
# Before
def start():
    log()

    item = Item()

    item.first()
    item.second()

# After
def start():
    log()

    item = Item()
    item.first()
    item.second()

# Unchanged - the initialization is part of the preceding stanza
def start():
    log()
    item = Item()

    item.first()
    item.second()
```

- The initialization assigns a call, optionally awaited, to one plain name,
  and the statement below calls a method directly on that name, optionally awaited.
- The initialization opens a body or follows a blank line, a blank line that another rule writes in the same fix included.
- A multi-line initialization, a multi-line call, or a comment or a directive between them keeps the gap.
- A comment or a directive above the initialization keeps the gap, and so does a rule that keeps a run,
  such as [`ZA3055`](ZA3055-keep-statement-runs-together.md), when it would remove the blank line above.
- Only the first gap is removed. The invocation run rules judge later calls.
- A blank line another rule asks for inside the pair moves above it, as the [rule map](../Rule-map.md#separation-a-keeper-displaces) says.

## C# counterpart

[ZA0062 - Keep an initialization with its calls](https://github.com/Nickelony/Stanza/wiki/ZA0062-Keep-an-initialization-with-its-calls).

## JS counterpart

[`keep-initialization-with-calls`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/keep-initialization-with-calls.md).
