# ZA3057 - keep-guard-clauses-together

**Level: opt-in.**
Removes the blank lines between neighboring guard clauses, so a chain of guards reads as one.

```python
# Before
def start(cart):
    if cart.expired:
        raise CartError("Cart has expired")

    if not cart.items:
        raise CartError("Cart is empty")
    publish(cart)

# After
def start(cart):
    if cart.expired:
        raise CartError("Cart has expired")
    if not cart.items:
        raise CartError("Cart is empty")
    publish(cart)

# Unchanged - a comment heads the second guard
def start(cart):
    if cart.expired:
        raise CartError("Cart has expired")

    # An empty cart cannot be checked out.
    if not cart.items:
        raise CartError("Cart is empty")
    publish(cart)
```

- A guard is what [`ZA3011`](ZA3011-separate-guard-clauses.md) reads as one: an `if` without `elif` or `else`
  whose body is a single `return`, `raise`, `break` or `continue`, an `assert`,
  or a call of a helper that `guard-roots` lists.
- A multi-line `assert`, or a multi-line call that `assertion-roots` lists, keeps its blank line.
  A multi-line `if` guard, or a helper that only `guard-roots` lists, joins the chain.
- The value a guard returns does not matter, and helper guards and `if` guards form one chain.
- A chain is two or more guards that stand next to each other anywhere in a body, not only at its top.
- The rule judges the body of a function and of a compound statement such as `if`, `for` or `try`, at module level too,
  as `ZA3011` does. It leaves the statements of a module and of a class body alone.
- A comment between two guards, a trailing comment included, keeps the blank line, since it heads a guard of its own.
- The rule wins the gaps inside the chain over the rules that write a blank line, such as
  [`ZA3001`](ZA3001-separate-multi-line-constructs.md), which separates every `if` that spans several lines.
  Such a blank line moves above the chain, as the [rule map](../Rule-map.md#separation-a-keeper-displaces) says.
- The gaps above and below the chain belong to the other rules, so `ZA3011` still sets the guards that open a body
  apart from the statements below them.

With `guard-roots = ["check_order"]`, a helper and an `if` guard form one chain:

```python
# Before
def close(order):
    check_order(order)

    if order.closed:
        return False
    order.close()

# After
def close(order):
    check_order(order)
    if order.closed:
        return False
    order.close()
```

## C# counterpart

[ZA0057 - Keep guard clauses together](https://github.com/Nickelony/Stanza/wiki/ZA0057-Keep-guard-clauses-together).

## JS counterpart

[`keep-guard-clauses-together`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/keep-guard-clauses-together.md).
