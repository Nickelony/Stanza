# ZA3058 - keep-guard-with-binding

**Level: opt-in.**
Removes the blank line between a statement that binds a name and the guard clause below it that tests that name.

```python
# Before
async def read_customer(service, customer_id):
    audit(customer_id)
    customer = await service.get_customer(customer_id)

    if not customer:
        raise NotFound(customer_id)
    return customer.name

# After
async def read_customer(service, customer_id):
    audit(customer_id)
    customer = await service.get_customer(customer_id)
    if not customer:
        raise NotFound(customer_id)
    return customer.name

# Unchanged - the guard tests a name the binding does not give
async def read_customer(service, customer_id):
    audit(customer_id)
    customer = await service.get_customer(customer_id)

    if not customer_id:
        raise NotFound(customer_id)
    return customer.name
```

- A guard that tests the value bound right above it checks that value, so the two read as one step.
- A guard is an `if` that [`ZA3011`](ZA3011-separate-guard-clauses.md) reads as one, with no `elif` or `else`
  and a body of a single `return`, `raise`, `break` or `continue`, or a call of a helper that `guard-roots` lists.
  An `assert` belongs to [`ZA3060`](ZA3060-keep-assertion-with-binding.md).
- The guard tests a name when its condition, or the helper call, reads it.
- Lambda and comprehension locals and write-only assignment expressions do not read the binding above.
  Free captures, lambda defaults, outer iterables and the owners or indices of member targets can read it.
- A binding is read as [`ZA3060`](ZA3060-keep-assertion-with-binding.md) reads one, and it may span several lines.
- Unlike [`ZA3059`](ZA3059-keep-jump-with-binding.md), the binding need not stand alone:
  a guard checks the statement right above it, whatever stanza that statement belongs to.
- Above a chain of guards, the rule keeps every binding the chain reads, whether
  [`ZA3012`](ZA3012-separate-checked-stanzas.md) is on or off. A binding the chain does not read stays apart.
- A chain that a narrowing `assert` opens leaves its bindings to `ZA3060`, so the rule keeps none of them.
- A comment between the two, a trailing comment included, keeps the blank line.
- The rule judges the body of a function and of a compound statement such as `if`, `for` or `try`, at module level too,
  as `ZA3011` does. It leaves the statements of a module and of a class body alone.
- With [`ZA3057`](ZA3057-keep-guard-clauses-together.md), a chain of guards below the binding joins it too.
  The gap below the chain belongs to the other rules.
- A blank line another rule asks for inside the group moves above it, as the [rule map](../Rule-map.md#separation-a-keep-rule-displaces) says.

## C# counterpart

[ZA0058 - Keep a guard with its binding](https://github.com/Nickelony/Stanza/wiki/ZA0058-Keep-a-guard-with-its-binding).

## JS counterpart

[`keep-guard-with-binding`](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/keep-guard-with-binding.md).
