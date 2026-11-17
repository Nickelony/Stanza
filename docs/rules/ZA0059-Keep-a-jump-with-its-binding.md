# ZA0059 - Keep a jump with its binding

**Suggestion, off by default.**
Removes the blank line between a lone single-line binding and the `return` or `throw` below it that uses what it binds.

```csharp
// Before
Validate(order);

decimal total = order.Lines.Sum(line => line.Price);

return total;

// After
Validate(order);

decimal total = order.Lines.Sum(line => line.Price);
return total;

// Unchanged - the binding belongs to the stanza above it
Validate(order);
decimal total = order.Lines.Sum(line => line.Price);

return total;
```

- A binding that only names the value a jump hands on is one step with the jump.
- The binding is lone when it opens its block, a blank line stands above it, or a separating rule asks for one there.
- A blank line above the binding that another keep rule removes, as inside a run that [`ZA0055`](https://github.com/Nickelony/Stanza/wiki/ZA0055-Keep-a-run-of-statements-together) joins, does not count,
  so the rule never moves a blank line from one gap to another.
- A binding at the end of a longer stanza stays apart from the jump, and it agrees with
  [`ZA0013`](https://github.com/Nickelony/Stanza/wiki/ZA0013-Separate-a-jump-statement), which sets a jump apart from a stanza of two or more lines.
- The binding is a local declaration, a deconstruction included, or a statement that assigns a name with `=`.
- The jump is a `return` or a `throw` whose value reads a bound name.
  A bare `return;`, a `break`, a `continue` and a `yield break` use no name.
- A `nameof` operand or a name used only as an assignment target or an `out` argument supplies no value read.
- Names bound inside the returned expression do not consume an earlier binding; a closure can still read an outer binding.
- Both statements span one line. A statement that spans several keeps the blank line, and [`ZA0001`](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-multi-line-constructs) asks for it.
- A comment or a directive in the gap, a trailing comment included, keeps the blank line.
- The rule judges the statements of a block, of a switch section and at the top level of a file.
- The rule owns the gap it keeps tight, so the separating rules ask for no blank line there, as [A keep rule and the other owners of its gap](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#a-keep-rule-and-the-other-owners-of-its-gap) says.
- A boundary that a separating rule asks for inside the kept group moves above it, as [The boundary of a kept group](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap#the-boundary-of-a-kept-group) says.
