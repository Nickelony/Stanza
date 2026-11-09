# ZA0011 - Separate the guard clauses

**Suggestion, off by default.**
Adds a blank line below the guard clauses that open a block, so the checks of the arguments read apart from the work.

```csharp
// Before
ArgumentNullException.ThrowIfNull(order);
if (order.IsClosed) return;
Validate(order);
order.Close();

// After
ArgumentNullException.ThrowIfNull(order);
if (order.IsClosed) return;

Validate(order);
order.Close();
```

- A guard is an `if` with no `else` whose body is a lone jump or a throw helper, with braces or without.
  A jump is a statement of a kind [`ZA0009`](https://github.com/Nickelony/Stanza/wiki/ZA0009-Separate-a-jump-statement) judges, such as a `return`, a `goto` or a `yield break`.
- A bare call of a throw helper is a guard too, as the first line of the example shows.
- A throw helper is a call whose receiver name ends in `Exception` and whose method starts with `Throw`,
  such as `ArgumentNullException.ThrowIfNull(path)`, so the framework needs no list.
  A call is also a throw helper when its callee holds a name that [`stanza_guard_roots`](https://github.com/Nickelony/Stanza/wiki/stanza_guard_roots) lists, such as `Guard` in `Guard.NotNull(path)`.
- Only the guards that open the block count, so a guard below any other statement gets no blank line from this rule.
  A block of guards alone has no statement to set apart.
- A chain of guards stays together: the blank line goes below the last guard, and none goes between them.
  A blank line already written between two guards stays.
- A guard with a comment or a directive inside it is still a guard, and a comment directly above the statement below moves with it,
  so the blank line goes above the comment.
- A directive in the gap keeps it as written.
  A conditional directive inside a guard decides whether it holds the exit, so only the [view](https://github.com/Nickelony/Stanza/wiki/Terms) that holds it is judged.
- This rule leaves a region marker to [`ZA0401`](https://github.com/Nickelony/Stanza/wiki/ZA0401-Separate-the-region-marker-from-its-neighbors)
  and a conditional block to [`ZA0402`](https://github.com/Nickelony/Stanza/wiki/ZA0402-Separate-the-conditional-block-from-its-neighbors),
  and the gap stays as written where that rule is off.
- The rule judges the statements of a block, not those of a switch section or at the top level of a file.
- [`ZA0004`](https://github.com/Nickelony/Stanza/wiki/ZA0004-Separate-invocation-runs) sets apart a run of two or more calls of one helper, so this rule leaves the gap below that run where it is on and covers a lone helper.
- A gap that another [grouping rule](https://github.com/Nickelony/Stanza/wiki/Terms) owns around a run is left to that rule.
- [`ZA0001`](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-multi-line-constructs) owns the gap beside a multi-line guard or statement,
  and [`ZA0051`](https://github.com/Nickelony/Stanza/wiki/ZA0051-Keep-the-two-units-of-a-container-together) owns the gap of a container of two units.
  A rule that is [off](https://github.com/Nickelony/Stanza/wiki/Terms) at a gap hands it back, so this rule writes the blank line.
- [`ZA0012`](https://github.com/Nickelony/Stanza/wiki/ZA0012-Separate-a-commented-sibling) owns the gap above a comment that leads the statement below,
  so where it is on, it writes the blank line above the comment and this rule leaves the gap.
- [`ZA0009`](https://github.com/Nickelony/Stanza/wiki/ZA0009-Separate-a-jump-statement) reads a guard as a statement of the stanza above a jump,
  so `if (a) return;`, `if (b) return;` and `return c;` gets its blank line from `ZA0009`, and this rule leaves that gap.
- Where `ZA0009` is off or asks for nothing, as above a jump with one guard, this rule writes the blank line.
- A base call below the guards opens the body for [`ZA0010`](https://github.com/Nickelony/Stanza/wiki/ZA0010-Separate-the-base-call),
  which owns the blank line below that call while this rule owns the one above it.
- Where `ZA0010` is on, it owns the gap above a base call that closes the body directly below the guards, and this rule leaves that gap.
- Where this rule is on, the blank line below the guards ends the stanza `ZA0009` reads, and [`ZA0052`](https://github.com/Nickelony/Stanza/wiki/ZA0052-Gather-a-run-of-loose-statements) keeps that gap blank when it gathers a run.
