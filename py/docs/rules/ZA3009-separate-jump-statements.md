# ZA3009 - separate-jump-statements

**Level: strong.**
Adds a blank line above a `return`, `raise`, `break` or `continue` when the stanza directly above it spans two or more lines.

```python
# Before
def close(order_id):
    order = load(order_id)
    validate(order)
    order.close()
    return order

# After
def close(order_id):
    order = load(order_id)
    validate(order)
    order.close()

    return order
```

One line above keeps the jump tight: `order = load(order_id)` directly above `return order` stays as it is.

- The stanza is the statements directly above the jump that no blank line separates and no other enabled rule asks to separate.
  A statement that spans several lines is a stanza of several lines.
- A comment directly above the jump moves with it, so the blank line goes above the comment.
- A jump that opens its block is left alone.
- Where [`ZA3011`](ZA3011-separate-guard-clauses.md) also asks for the gap, the guard rule reports it.
- The rule has no tight counterpart; leave it disabled to keep jumps tight.

## C# counterpart

[ZA0009 - Separate a jump statement](https://github.com/Nickelony/Stanza/wiki/ZA0009-Separate-a-jump-statement).

## JS counterpart

[`separate-jump-statements`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-jump-statements.md).
