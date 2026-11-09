# ZA3202 - keep-clause-keywords-adjacent

**Level: minimal.**
Removes the blank line between the last statement of a block and the `elif`, `else`, `except` or `finally` keyword that continues its statement.

```python
# Before
try:
    options = bind()

except ConfigurationError as error:
    log.error(error)

# After
try:
    options = bind()
except ConfigurationError as error:
    log.error(error)
```

A comment in the gap sits directly under the block, and the blank line between the comment and the keyword stays:

```python
# Before
try:
    load()

# fall back to the cache

except OSError:
    use_cache()

# After
try:
    load()
# fall back to the cache

except OSError:
    use_cache()
```

- The rule covers the keywords that [`ZA3201`](ZA3201-separate-clause-keywords.md) lists.
- With a comment in the gap, the blank lines above its first comment go and the blank lines below that comment stay.
- A comment indented deeper than the keyword trails the block above, so the blank lines below it go.
- A [tool directive](../Quirks.md#comments) in the gap, such as `# noqa` or `# fmt: off`, keeps the gap as written,
  and so does a region between `# fmt: off` and `# fmt: on`. An ordinary comment follows the rule above.
- A block that ends in a function or class keeps the blank line, because the formatters write it there.
- Black and `ruff format` keep a comment directly under the block, so this rule never fights them.
- The separating rule is [`ZA3201`](ZA3201-separate-clause-keywords.md);
  [the pair table](../Rule-map.md#pairs-and-their-default-halves) names the default half and the owner when both are enabled.

## C# counterpart

[ZA0202 - Keep clause keywords adjacent](https://github.com/Nickelony/Stanza/wiki/ZA0202-Keep-clause-keywords-adjacent).

## JS counterpart

None. JS Stanza leaves clause keywords to Prettier.
