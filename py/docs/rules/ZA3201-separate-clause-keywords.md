# ZA3201 - separate-clause-keywords

**Level: opt-in.**
Adds a blank line between the last statement of a block and the `elif`, `else`, `except` or `finally` keyword that continues its statement.

```python
# Before
if order.paid:
    ship(order)
else:
    hold(order)

# After
if order.paid:
    ship(order)

else:
    hold(order)
```

- The clause keywords are `elif`, `else` of an `if`, a loop or a `try`, `except` including `except*`, and `finally`.
- A comment directly above the keyword at its indent moves with it, so the blank line goes above the comment.
  A comment indented deeper trails the block above, and the blank line goes below it.
- A blank line that already stands in the gap, above or below a comment, satisfies the rule.
- A [tool directive](../Quirks.md#comments) in the gap, such as `# noqa` or `# fmt: off`, keeps the gap as written.
  An ordinary comment moves with the keyword.
- A block that ends in a function or class keeps the blank line the formatters write.
- Black and `ruff format` keep the blank line, so this rule never fights them.
- The tight rule is [`ZA3202`](ZA3202-keep-clause-keywords-adjacent.md), and no level enables this rule because the `minimal` level holds that one.
  [The pair table](../Rule-map.md#pairs-and-their-default-halves) says what naming this rule does to the level and who owns the gap when both are enabled.

## C# counterpart

[ZA0201 - Separate a clause keyword from the brace before it](https://github.com/Nickelony/Stanza/wiki/ZA0201-Separate-a-clause-keyword-from-the-brace-before-it).

## JS counterpart

None. JS Stanza leaves clause keywords to Prettier.
