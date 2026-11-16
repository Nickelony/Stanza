# ZA0058 - Keep a guard with its binding

**Suggestion, off by default.**
Removes the blank line between a statement that binds a name and the guard clause below it that tests that name.

```csharp
// Before
Audit(id);
var customer = repository.Find(id);

if (customer is null) return;
Show(customer);

// After
Audit(id);
var customer = repository.Find(id);
if (customer is null) return;
Show(customer);

// Unchanged - the guard tests a name the binding does not give
Audit(id);
var customer = repository.Find(id);

if (id < 0) return;
Show(customer);
```

- A guard that tests the value a statement binds checks that value, so the two read as one step.
- The guard is an `if` that [`ZA0011`](https://github.com/Nickelony/Stanza/wiki/ZA0011-Separate-the-guard-clauses) reads as one:
  no `else`, and a body of a lone jump or a throw helper, with braces or without.
  A bare throw helper that narrows the name, in the forms [`ZA0061`](https://github.com/Nickelony/Stanza/wiki/ZA0061-Keep-a-narrowing-assertion-with-its-binding) lists, tests it too.
- Contiguous bindings that the checks read form one group, whether or not [`ZA0018`](https://github.com/Nickelony/Stanza/wiki/ZA0018-Separate-a-checked-stanza) is on.
  A binding the checks do not read stays outside the group.
- The binding is a local declaration, a deconstruction included, or a statement that assigns a name with `=`, and it may span several lines.
- The guard tests a name when its condition reads it, on its own or as the receiver of a member, such as `customer` in `customer.IsBlocked`.
  The member a dot names and a type are not read.
- A `nameof` operand or a name used only as an assignment target or an `out` argument supplies no value read.
- Names bound inside the condition do not attach an earlier binding; a closure can still read an outer binding.
- The rule judges the statements of a block, of a switch section and at the top level of a file.
- A comment or a directive in the gap, a trailing comment included, keeps the blank line.
- The rule owns the gap it keeps tight, so the separating rules ask for no blank line there, as [A keep rule and the other owners of its gap](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-keep-rule-and-the-other-owners-of-its-gap) says.
- With [`ZA0057`](https://github.com/Nickelony/Stanza/wiki/ZA0057-Keep-a-chain-of-guard-clauses-together), a chain of guards below the binding joins it too. The gap below the chain belongs to the other rules.
- A boundary that a separating rule asks for inside the group moves above it, as [The boundary of a kept group](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#the-boundary-of-a-kept-group) says.
