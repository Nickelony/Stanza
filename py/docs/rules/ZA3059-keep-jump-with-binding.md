# ZA3059 - keep-jump-with-binding

**Level: opt-in.**
Removes the blank line between a lone single-line binding and the `return` or `raise` that uses what it binds.

```python
# Before
def describe(rule, container):
    check(rule)

    bound = read_names(container)

    return any(name in bound for name in rule.names)

# After
def describe(rule, container):
    check(rule)

    bound = read_names(container)
    return any(name in bound for name in rule.names)

# Unchanged - the binding belongs to the stanza above it
def describe(rule, container):
    check(rule)
    bound = read_names(container)

    return any(name in bound for name in rule.names)
```

- A binding that stands alone computes the value the jump hands back, so the two read as one step.
- The binding stands alone when it opens the body or a blank line stands above it,
  a blank line that another rule writes in the same fix included.
- A binding inside a stanza stays with that stanza, and [`ZA3013`](ZA3013-separate-jump-statements.md) keeps the
  jump apart from a stanza of two or more lines.
  The rule never moves the blank line from above the jump to above the binding.
- A binding is an assignment, an annotated assignment with a value or an augmented assignment,
  as [`ZA3060`](ZA3060-keep-assertion-with-binding.md) reads one.
- The jump uses a name when its value reads it: the value of a `return`, or the exception or the cause of a `raise`.
  A bare `return`, a `break` and a `continue` use none.
- Lambda and comprehension locals and write-only assignment expressions do not read the binding;
  free captures, lambda defaults, outer iterables and the owners or indices of member targets can.
- Both statements span one line. A statement that spans several keeps the blank line, and
  [`ZA3001`](ZA3001-separate-multi-line-constructs.md) asks for it.
- A comment between the two, a trailing comment included, keeps the blank line.
- Where [`ZA3055`](ZA3055-keep-statement-runs-together.md)
  or [`ZA3061`](ZA3061-keep-dependent-bindings-together.md) joins the binding to the stanza above,
  the binding stands alone no more, and the jump stays apart.
- A blank line another rule asks for inside the pair moves above it, as the [rule map](../Rule-map.md#separation-a-keep-rule-displaces) says.

## C# counterpart

[ZA0059 - Keep a jump with its binding](https://github.com/Nickelony/Stanza/wiki/ZA0059-Keep-a-jump-with-its-binding).

## JS counterpart

[`keep-jump-with-binding`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/keep-jump-with-binding.md).
