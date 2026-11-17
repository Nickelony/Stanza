# ZA3063 - keep-accumulator-with-loop

**Level: opt-in.**
Removes the blank line between a binding that seeds a name and the loop below it that builds that name.

```python
# Before
def subtotal(cart):
    check(cart)
    total = Decimal("0.00")

    for item in cart.items:
        total += item.price * item.quantity
    return total

# After
def subtotal(cart):
    check(cart)
    total = Decimal("0.00")
    for item in cart.items:
        total += item.price * item.quantity
    return total

# Unchanged - the loop only uses the object, it does not build it
def send_all(messages):
    check(messages)
    client = Client()

    for message in messages:
        client.send(message)
    return client
```

- The seed and the loop that fills it are one step: the seed means nothing without the loop.
- A seed is a single-line assignment or annotated assignment to one plain name whose value reads no variable,
  such as `0`, `None`, `[]`, `{}`, `set()`, `Decimal("0.00")` or `collections.Counter()`.
- A name in the value may only be the callee of a call, as `set` in `set()`,
  or the root of a dotted callee when the file imports that root, as `collections` in `collections.Counter()`.
  Any other root reads a variable, so `items = source.copy()` and `items = self.make()` seed nothing.
- The seeds directly above the loop form one group with it when the loop builds each of them,
  so the rule removes the blank lines between them too. A seed the loop does not build ends the group.
- The loop is a `for`, an `async for` or a `while`.
  Its body builds a seed through an update it executes, inside nested control flow and loops too.
- An assignment, an annotated assignment with a value, an augmented assignment or a nested loop target
  updates the seed. An annotation without a value does not, and neither does a read of the seed as an index.
- A write into an element of the seed, as in `counts[word] = 1` or `tally[word] += 1`, does not build it.
- A call of `append`, `appendleft`, `add`, `extend`, `extendleft`, `insert`, `setdefault` or `update`
  on the seed builds it. Any other method call only uses the object, so the blank line stays.
- A nested function, lambda, class or comprehension is outside the rule, with its defaults, decorators,
  bases and annotations. A Python loop target shares the enclosing scope, so a nested loop target updates the seed.
- A comment between the two, a trailing comment included, keeps the blank line.
- The rule wins the gap above the loop over [`ZA3001`](ZA3001-separate-multi-line-constructs.md),
  which separates the loop, and that blank line moves above the seeds, as the [rule map](../Rule-map.md#separation-a-keeper-displaces) says.
  The gap below the loop belongs to the other rules.

## C# counterpart

[ZA0063 - Keep an accumulator with its loop](https://github.com/Nickelony/Stanza/wiki/ZA0063-Keep-an-accumulator-with-its-loop).

## JS counterpart

[`keep-accumulator-with-loop`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/keep-accumulator-with-loop.md).
