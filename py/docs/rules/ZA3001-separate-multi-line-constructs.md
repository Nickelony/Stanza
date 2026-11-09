# ZA3001 - separate-multi-line-constructs

**Level: moderate.**
Adds a blank line between a statement that spans more than one line and its neighbors.

```python
# Before
order = load(order_id)
if order.closed:
    notify(order)
log(order)

# After
order = load(order_id)

if order.closed:
    notify(order)

log(order)
```

- A unit spans several lines when it is a block, a call or literal that breaks across lines, a string that spans lines,
  or a statement that continues with a backslash.
- An `if`, `for`, `while`, `with`, `try` or `match` statement is a block when its body stands on its own lines,
  and one written on a single line, as in `if ok: return`, is not.
- Two multi-line imports in a row stay together.
- A comment directly above the unit moves with it, so the blank line goes above the comment.
- The gaps beside a function or class belong to the formatter, so this rule leaves them as written.
- Statements that share a line, as in `a = 1; b = foo(`, stay joined.
- The `case` clauses of a `match` statement are the business of [`ZA3111`](ZA3111-separate-multi-line-cases.md),
  and this rule judges the statements around the `match`.

## C# counterpart

[ZA0001 - Separate multi-line constructs](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-multi-line-constructs).

## JS counterpart

[`separate-multi-line-constructs`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-multi-line-constructs.md).
