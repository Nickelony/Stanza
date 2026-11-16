# ZA0057 - Keep a chain of guard clauses together

**Suggestion, off by default.**
Removes the blank line between two neighboring guard clauses, so a chain of guards reads as one.

```csharp
// Before
ArgumentNullException.ThrowIfNull(order);

if (order.IsClosed) return;

if (order.Lines.Count == 0) return;
order.Close();

// After
ArgumentNullException.ThrowIfNull(order);
if (order.IsClosed) return;
if (order.Lines.Count == 0) return;
order.Close();

// Unchanged - a comment heads the second guard
Validate(order);
if (order.IsClosed) return;

// An empty order has nothing to close.
if (order.Lines.Count == 0) return;
order.Close();
```

- A guard is what [`ZA0011`](https://github.com/Nickelony/Stanza/wiki/ZA0011-Separate-the-guard-clauses) reads as one:
  an `if` with no `else` whose body is a lone jump or a throw helper, with braces or without, or a bare call of a throw helper.
- A returned value does not affect whether an exit is a guard. Bare throw helpers and `if` guards form one chain.
- An `if` with an `else`, or with more than one statement in its body, is no guard, so the blank line beside it stays.
- A chain is two or more guards that stand next to each other anywhere in a block, a switch section or the top level of a file, not only at its top.
- A comment or a directive in the gap, a trailing comment included, keeps the blank line, since it heads a guard of its own.
- The rule owns the gap it keeps tight, so the separating rules ask for no blank line there, as [A keep rule and the other owners of its gap](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-keep-rule-and-the-other-owners-of-its-gap) says.
- A boundary that a separating rule asks for inside the chain moves above it, as [The boundary of a kept group](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#the-boundary-of-a-kept-group) says.
- The gaps above and below the chain belong to the other rules, so `ZA0011` still sets the guards that open a block
  apart from the statements below them.
