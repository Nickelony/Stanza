# ZA3012 - separate-checked-stanzas

**Level: opt-in.**
Separates a guard chain and the bindings it checks from neighboring statements.

```python
# Before
async def start(session, session_id):
    service = CartService(session)
    cart = await service.get_cart(session_id)
    if cart is None:
        return None
    publish(cart)

# After
async def start(session, session_id):
    service = CartService(session)

    cart = await service.get_cart(session_id)
    if cart is None:
        return None

    publish(cart)

# Unchanged - a narrowing assertion has no lower border
def start():
    log()

    cart = load()
    assert cart is not None
    publish(cart)
```

The examples run the rule alone. At `moderate` and above, [`ZA3001`](ZA3001-separate-multi-line-constructs.md)
also separates the multi-line guard from the binding above it, unless [`ZA3058`](ZA3058-keep-guard-with-binding.md)
is enabled to keep them together as the After shows.

- A checked stanza is a chain of guards and the bindings directly above it whose names the chain reads.
  A binding the chain does not read stays above the stanza.
- A guard is an `if` without `else` whose body is one jump, or a call that `guard-roots` lists.
  The value a guard returns does not matter.
- A run of asserts that each narrow a name, in a form [`ZA3060`](ZA3060-keep-assertion-with-binding.md) reads,
  is a chain too, and the rule adds only the blank line above it.
  A run that holds any other assert or an assertion helper is the assert phase of a test and does not count.
- No blank line goes at the start or at the end of a body.
  [`ZA3013`](ZA3013-separate-jump-statements.md) owns a jump below the stanza while it is enabled and applies there.
- A comment breaks a chain and stays with the statement it heads.
- [`ZA3011`](ZA3011-separate-guard-clauses.md) owns the lower border of a chain that opens a body,
  and [`ZA3019`](ZA3019-separate-import-section.md) the gap below an import, while they are enabled.
- The rule judges the body of a function and of a compound statement such as `if`, `for` or `try`, at module level too.
  It leaves the statements of a module and of a class body alone.
- When a keep rule such as [`ZA3061`](ZA3061-keep-dependent-bindings-together.md) keeps the bindings above the stanza
  together, the upper blank line goes above the whole kept group.
- Enable the binding keep rules to remove blank lines inside the stanza.

## C# counterpart

[ZA0012 - Separate a checked stanza](https://github.com/Nickelony/Stanza/wiki/ZA0012-Separate-a-checked-stanza).

## JS counterpart

[`separate-checked-stanzas`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-checked-stanzas.md).
