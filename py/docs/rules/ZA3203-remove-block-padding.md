# ZA3203 - remove-block-padding

**Level: minimal.**
Removes the blank lines between a block header and the first line of its body.

```python
# Before
def process(order):

    log(order)
    return order

# After
def process(order):
    log(order)
    return order
```

The headers are those of `def`, `async def`, `class`, `if`, `elif`, `else`, `for`, `while`, `with`, `try`, `except`, `finally`, `match` and `case`:

```python
# Before
try:

    load()
except OSError:

    # fall back to the cache
    use_cache()

# After
try:
    load()
except OSError:
    # fall back to the cache
    use_cache()
```

- Only whitespace is removed, and the body is the unit below the header, whatever it is: a statement, a docstring or an import.
- A comment under the header stays tight under it: the blank lines above the first comment go,
  and the blank lines between comments, or between the last comment and the body, stay.
- A [tool directive](../Quirks.md#comments) in the gap, such as `# noqa` or `# fmt: off`, keeps the gap as written,
  and so does a region between `# fmt: off` and `# fmt: on`. An ordinary comment follows the rule above.
- A comment indented less than the body leaves the gap as written, because the owner of the comment is unclear.
- The blank line above a function or class docstring goes too, as pydocstyle `D201` asks. The blank line below it stays;
  no rule asks for it and `D202` forbids it.
- A body on the line of its header, as in `if ready: start()`, has no gap.
- A function or class that opens a block of another kind, such as an `if` or a `try`, keeps the blank line,
  because the formatters write it there. A `def` or `class` header leaves its first nested definition free,
  and so does a comment above the definition.
- Black keeps one blank line under every header, and `ruff format` removes it except under `def` and `async def`;
  neither writes one, so this rule never fights them.
- No rule asks for a blank line in this gap, so the rule has no pair and no owner order.
  The gaps between the statements of the body belong to the other rules.

## C# counterpart

[ZA0203 - Remove brace padding](https://github.com/Nickelony/Stanza/wiki/ZA0203-Remove-brace-padding).

## JS counterpart

None. JS Stanza leaves the padding beside braces to Prettier.
